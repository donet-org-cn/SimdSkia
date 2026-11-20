using Avalonia;
using Avalonia.Metal;
using Avalonia.Platform;
using SimdSkia.Gpu;

namespace SimdSkia.Avalonia;

// macOS window presentation through the host's CAMetalLayer (Avalonia.Native
// Metal mode). The scene is drawn into a retained unified-memory RGBA buffer
// (GPU draws, managed CPU fallback, device layers and effects all share it),
// then a render pass copies it into the frame's drawable. The CPU never waits
// for the GPU unless a CPU fallback must touch pixels the GPU is drawing.
internal sealed class MetalWindowTarget : IRenderTarget, IDeviceFaultSource
{
    private readonly IMetalDevice graphics;
    private readonly bool fallback;
    private readonly SimdSkiaGpuTextMode textMode;
    private IMetalPlatformSurfaceRenderTarget? surface;
    private MetalDevice? device;
    private PixelBuffer? pixels;
    private WindowGpuCanvas? canvas;
    private bool disposed,active,retained,faulted;
    private int frames;
    private static int nextId;
    private readonly int id=Interlocked.Increment(ref nextId);
    private static readonly string? FailureInjection=Environment.GetEnvironmentVariable("SIMDSKIA_METAL_FAIL");
    public bool IsDeviceFaulted=>faulted||canvas?.IsFaulted==true||device?.Abandoned==true;

    // One device per host graphics context: retried, recreated and additional
    // window targets share its programs and caches. The host serializes all use.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IMetalDevice,MetalDevice> devices=new();
    private static MetalDevice Shared(IMetalDevice graphics)
    {
        lock(devices)
        {
            if(devices.TryGetValue(graphics,out var existing)&&!existing.Abandoned)return existing;
            var created=new MetalDevice(graphics.Device,graphics.CommandQueue,hostSerialized:true);
            devices.AddOrUpdate(graphics,created);return created;
        }
    }
    // Created where the compositor creates render targets: the platform
    // surface target must exist before the first frame is drawn.
    internal MetalWindowTarget(IMetalDevice graphics,IMetalPlatformSurface surface,bool fallback,SimdSkiaGpuTextMode textMode=SimdSkiaGpuTextMode.ManagedGlyphAtlas)
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Metal presentation requires macOS.");
        // Diagnostics: SIMDSKIA_METAL_FAIL=init|draw exercises the CPU fallback.
        if(FailureInjection=="init")throw new NotSupportedException("Injected Metal initialization failure (SIMDSKIA_METAL_FAIL=init).");
        this.graphics=graphics;this.fallback=fallback;this.textMode=textMode;
        // Programs compile before the window binds its Metal surface: a failure
        // here leaves the window untouched for the CPU framebuffer fallback.
        device=Shared(graphics);
        if(MetalDevice.Trace)Console.Error.WriteLine($"[metal] target {id} created at {Environment.TickCount64%100000} thread {Environment.CurrentManagedThreadId}");
        this.surface=surface.CreateMetalRenderTarget(graphics);
    }
    public RenderTargetProperties Properties=>new(){RetainsPreviousFrameContents=true,IsSuitableForDirectRendering=true};
    public PlatformRenderTargetState PlatformRenderTargetState=>disposed||surface is null?PlatformRenderTargetState.Disposed:IsDeviceFaulted?new(){IsCorrupted=true}:surface.State;
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo scene,out RenderTargetDrawingContextProperties properties)
    {
        ObjectDisposedException.ThrowIf(disposed||surface is null,this);if(active)throw new InvalidOperationException("The Metal window target is already drawing.");
        long started=SimdSkiaDiagnostics.FrameStarted();long begun=System.Diagnostics.Stopwatch.GetTimestamp();
        if(FailureInjection=="draw"&&pixels is null)throw new InvalidOperationException("Injected Metal drawing failure (SIMDSKIA_METAL_FAIL=draw).");
        var session=surface.BeginRendering();
        try
        {
            var size=session.Size;
            if(size.Width<=0||size.Height<=0)throw new InvalidOperationException("The Metal layer has no drawable area.");
            // The device wraps the host's MTLDevice/queue: the host presents the
            // drawable on the same queue after this frame's command buffer.
            device??=Shared(graphics);
            if(pixels is null||pixels.Width!=size.Width||pixels.Height!=size.Height)
            {
                canvas?.Dispose();canvas=null;pixels?.Dispose();pixels=null;retained=false;
                device.TrimShared();
                pixels=device.RentShared(size.Width,size.Height,true);
                canvas=new WindowGpuCanvas(pixels,SimdSkiaGpuBackend.Metal,fallback,textMode:textMode,metal:device);
            }
            properties=new(){PreviousFrameIsRetained=retained};active=true;
            var frame=pixels;var target=canvas!;var drawable=session.Texture;bool flip=session.IsYFlipped;
            return new DrawingContext(frame,new Vector(session.Scaling*96,session.Scaling*96),false,success=>
            {
                bool presented=false;
                try
                {
                    if(success)
                    {
                        target.Present(drawable,flip);retained=true;presented=true;
                        // A captured frame is a CPU copy: wait for the device first.
                        if(SimdSkiaDiagnostics.CapturePresentedFrames)target.Flush();
                        SimdSkiaDiagnostics.Presented(frame);
                    }
                    else retained=false;
                }
                catch(Exception error){faulted=true;retained=false;System.Diagnostics.Trace.TraceError("SimdSkia Metal present failed: {0}",error);if(Environment.GetEnvironmentVariable("SIMDSKIA_METAL_TRACE")=='1'.ToString())Console.Error.WriteLine("[metal] present failed: "+error);throw;}
                finally
                {
                    active=false;session.Dispose();
                    if(presented)SimdSkiaDiagnostics.FrameCompleted(started);
                    var took=System.Diagnostics.Stopwatch.GetElapsedTime(begun).TotalMilliseconds;frames++;
                    if(MetalDevice.Trace&&(frames<=3||took>20))Console.Error.WriteLine($"[metal] target {id} frame {frames}: {took:F1} ms at {Environment.TickCount64%100000} on thread {Environment.CurrentManagedThreadId}");
                }
            },target,flushBackend:false);
        }
        catch(Exception error){active=false;session.Dispose();if(Environment.GetEnvironmentVariable("SIMDSKIA_METAL_TRACE")=='1'.ToString())Console.Error.WriteLine("[metal] begin failed: "+error);throw;}
    }
    public void Dispose()
    {
        if(disposed)return;if(active)throw new InvalidOperationException("Dispose the active Metal drawing context first.");
        disposed=true;if(MetalDevice.Trace)Console.Error.WriteLine($"[metal] target {id} disposed after {frames} frames at {Environment.TickCount64%100000}");
        try{canvas?.Dispose();}
        finally
        {
            // The device belongs to the graphics context and outlives this window:
            // only this target's pixels go, and recycled buffers are released.
            try{pixels?.Dispose();if(device is {Abandoned:false})device.TrimShared();}
            finally{surface?.Dispose();surface=null;canvas=null;pixels=null;device=null;}
        }
    }
}
