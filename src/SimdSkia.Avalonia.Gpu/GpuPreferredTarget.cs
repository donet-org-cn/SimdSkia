using System.Runtime.InteropServices;
using Avalonia.Metal;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;

namespace SimdSkia.Avalonia;

// Shared by windows belonging to one configured renderer. A device reported as
// lost is not recreated every render-loop tick. Surface restrictions stay local.
internal sealed class GpuPreference
{
    private readonly object gate=new();
    private readonly HashSet<SimdSkiaGpuBackend> unavailable=[];
    internal bool Allows(SimdSkiaGpuBackend backend){lock(gate)return !unavailable.Contains(backend);}
    internal void DeviceLost(SimdSkiaGpuBackend backend){lock(gate)unavailable.Add(backend);}
}

internal sealed class GpuPreferredTarget : IRenderTarget
{
    internal sealed record Candidate(string Name,SimdSkiaGpuBackend Backend,Func<IRenderTarget> Create);
    private readonly Candidate[] candidates;
    private readonly Func<IRenderTarget> createCpu;
    private readonly GpuPreference preference;
    private readonly Func<bool>? isReady;
    private IRenderTarget? selected;
    private Candidate? hardware;
    private bool disposed;

    private IDisposable? pending;
    internal GpuPreferredTarget(Candidate[] candidates,Func<IRenderTarget> createCpu,GpuPreference preference,Func<bool>? isReady=null,IDisposable? pending=null)
    {this.candidates=candidates;this.createCpu=createCpu;this.preference=preference;this.isReady=isReady;this.pending=pending;}

    internal static IRenderTarget Create(IReadOnlyList<IPlatformRenderSurface> surfaces,GpuPreference preference)=>Create(null,surfaces,preference);

    // macOS: the native Metal layer, created eagerly because the host binds a
    // surface render target where render targets are created; CPU framebuffer
    // presentation remains the fallback.
    internal static IRenderTarget CreateMetal(IPlatformGraphicsContext? graphics,IReadOnlyList<IPlatformRenderSurface> surfaces,GpuPreference preference,bool allowCpuFallback,SimdSkiaGpuTextMode textMode=SimdSkiaGpuTextMode.ManagedGlyphAtlas)
    {
        var framebuffer=surfaces.OfType<IFramebufferPlatformSurface>().FirstOrDefault();
        MetalWindowTarget? metal=null;
        if(OperatingSystem.IsMacOS()&&graphics is IMetalDevice device&&surfaces.OfType<IMetalPlatformSurface>().FirstOrDefault() is {} layer&&preference.Allows(SimdSkiaGpuBackend.Metal))
        {
            try{metal=new MetalWindowTarget(device,layer,allowCpuFallback,textMode);}
            catch(Exception error) when(error is NotSupportedException or PlatformNotSupportedException or InvalidCastException or ExternalException or ArgumentException or (InvalidOperationException and not ObjectDisposedException))
            {SimdSkiaGpuDiagnostics.InitializationFailed("Metal native layer",error);}
        }
        if(metal is null)
        {
            if(!allowCpuFallback)throw new NotSupportedException("The Metal layer is unavailable and CPU fallback was not allowed: "+SimdSkiaGpuDiagnostics.LastInitializationFailure);
            if(framebuffer is null)throw new NotSupportedException("No Metal layer or OS framebuffer surface is available.");
            SimdSkiaGpuDiagnostics.Selected(graphics is IMetalDevice?"CPU/SIMD (Metal layer unavailable)":"CPU/SIMD (host did not create a Metal device)");
            return new FramebufferTarget(framebuffer.CreateFramebufferRenderTarget());
        }
        var created=metal;
        Candidate[] choices=[new("Metal native layer",SimdSkiaGpuBackend.Metal,()=>Interlocked.Exchange(ref created,null)??throw new InvalidOperationException("The Metal layer target was already consumed."))];
        IRenderTarget Cpu()=>new FramebufferTarget((framebuffer??throw new NotSupportedException("Metal fallback requires an OS framebuffer surface.")).CreateFramebufferRenderTarget());
        return new GpuPreferredTarget(choices,Cpu,preference,pending:metal);
    }

    internal static IRenderTarget Create(IPlatformGraphicsContext? graphics,IReadOnlyList<IPlatformRenderSurface> surfaces,GpuPreference preference)
    {
        if(OperatingSystem.IsMacOS())return CreateMetal(graphics,surfaces,preference,true);
        var framebuffer=surfaces.OfType<IFramebufferPlatformSurface>().FirstOrDefault();
        var native=surfaces.OfType<INativePlatformHandleSurface>().FirstOrDefault();
        IRenderTarget Cpu()=>new FramebufferTarget((framebuffer??throw new NotSupportedException("GPU fallback requires an OS framebuffer surface.")).CreateFramebufferRenderTarget());
        IRenderTarget Readback(SimdSkiaGpuBackend backend)=>new FramebufferTarget(framebuffer!.CreateFramebufferRenderTarget(),
            pixels=>new WindowGpuCanvas(pixels,backend,true));
        var choices=new List<Candidate>();
        if(OperatingSystem.IsWindows())
        {
            if(native is not null)choices.Add(new("Direct2D native swapchain",SimdSkiaGpuBackend.Direct2D,()=>new NativeWindowTarget(native,true,SimdSkiaGpuTextMode.ManagedGlyphAtlas)));
            // Layered/transparent windows and hosts without an HWND can still use hardware drawing.
            if(framebuffer is not null)choices.Add(new("Direct2D framebuffer readback",SimdSkiaGpuBackend.Direct2D,()=>Readback(SimdSkiaGpuBackend.Direct2D)));
        }
        if((OperatingSystem.IsWindows()||OperatingSystem.IsLinux())&&framebuffer is not null)
            choices.Add(new("Vulkan framebuffer readback",SimdSkiaGpuBackend.Vulkan,()=>Readback(SimdSkiaGpuBackend.Vulkan)));
        if(choices.Count==0){SimdSkiaGpuDiagnostics.Selected("CPU/SIMD (no verified window GPU backend)");return Cpu();}
        return new GpuPreferredTarget(choices.ToArray(),Cpu,preference,()=>native is null||native.IsReady);
    }

    public RenderTargetProperties Properties=>new(){RetainsPreviousFrameContents=true,IsSuitableForDirectRendering=true};
    public PlatformRenderTargetState PlatformRenderTargetState
    {
        get
        {
            if(disposed)return PlatformRenderTargetState.Disposed;
            if(selected is null&&isReady?.Invoke()==false)return new(){IsReady=false};
            var state=selected?.PlatformRenderTargetState??PlatformRenderTargetState.Ready;
            if(state.IsCorrupted&&selected is IDeviceFaultSource{IsDeviceFaulted:true}&&hardware is {} gpu)
            {
                preference.DeviceLost(gpu.Backend);
                SimdSkiaGpuDiagnostics.InitializationFailed(gpu.Name,new InvalidOperationException("Device/target was lost; the next target skips this backend."));
                hardware=null;
            }
            return state;
        }
    }
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo scene,out RenderTargetDrawingContextProperties properties)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(selected is not null)return selected.CreateDrawingContext(scene,out properties);
        foreach(var candidate in candidates)
        {
            if(!preference.Allows(candidate.Backend))continue;
            IRenderTarget? attempt=null;
            try
            {
                attempt=candidate.Create();
                // Actual device/pipeline creation is lazy in the OS targets. Test
                // the first drawing context before declaring GPU selection success.
                var context=attempt.CreateDrawingContext(scene,out properties);
                selected=attempt;hardware=candidate;if(ReferenceEquals(attempt,pending))pending=null;SimdSkiaGpuDiagnostics.Selected(candidate.Name);return context;
            }
            catch(Exception error) when(IsUnavailable(error))
            {
                attempt?.Dispose();if(ReferenceEquals(attempt,pending))pending=null;
                SimdSkiaGpuDiagnostics.InitializationFailed(candidate.Name,error);
                // A missing loader affects all windows. A layered window or target
                // size restriction does not establish that the device is unavailable.
                if(error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
                    preference.DeviceLost(candidate.Backend);
            }
            catch{attempt?.Dispose();throw;}
        }
        selected=createCpu();SimdSkiaGpuDiagnostics.Selected("CPU/SIMD (GPU preferred fallback)");
        return selected.CreateDrawingContext(scene,out properties);
    }
    private static bool IsUnavailable(Exception error)=>error is NotSupportedException or DllNotFoundException
        or EntryPointNotFoundException or BadImageFormatException or ExternalException or PlatformNotSupportedException or global::SimdSkia.Gpu.VulkanDeviceLostException
        ||error is InvalidOperationException and not ObjectDisposedException;
    public void Dispose(){if(disposed)return;selected?.Dispose();selected=null;Interlocked.Exchange(ref pending,null)?.Dispose();disposed=true;}
}
