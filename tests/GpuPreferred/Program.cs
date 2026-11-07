using System.Runtime.InteropServices;
using Avalonia;
using Colors=Avalonia.Media.Colors;
using Avalonia.Platform;
using SimdSkia;
using SimdSkia.Avalonia;
using SimdSkia.Cpu;
using SimdSkia.Gpu;
using PixelFormat=SimdSkia.PixelFormat;
using AlphaFormat=SimdSkia.AlphaFormat;

int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
void Throws<T>(Action action)where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
GpuPreferredTarget.Candidate Candidate(string name,Func<IRenderTarget> create,SimdSkiaGpuBackend backend=SimdSkiaGpuBackend.Direct2D)=>new(name,backend,create);
void Paint(IRenderTarget target){using var context=target.CreateDrawingContext(default,out _);context.Clear(Colors.CornflowerBlue);}

Throws<ArgumentNullException>(()=>SimdSkiaGpuOptions.UseGpuPreferred(null!));
var options=new SimdSkiaOptions();options.UseGpuPreferred();Check(options.PreferredRenderTargetFactory is not null,"GPU preference is configured.");
options.UseCpu();Check(options.PreferredRenderTargetFactory is null&&options.CanvasBackendFactory is null&&options.NativeRenderTargetFactory is null,"Explicit CPU removes automatic/explicit GPU factories.");
using(var notReady=new GpuPreferredTarget([],()=>throw new Exception("Not ready"),new(),()=>false))
    Check(!notReady.PlatformRenderTargetState.IsReady,"A not-yet-ready native surface must not start GPU creation or fallback.");
if(OperatingSystem.IsWindows())
{
    options.UseGpuPreferred();options.UseGpu(SimdSkiaGpuBackend.Direct2D);
    Check(options.PreferredRenderTargetFactory is null&&options.NativeRenderTargetFactory is not null,"Explicit strict GPU overrides automatic fallback.");
}
using(var hardware=new TestTarget())
{
    int cpuCreations=0;
    using var target=new GpuPreferredTarget([Candidate("test hardware",()=>hardware)],()=>{cpuCreations++;return new TestTarget();},new());
    Paint(target);Paint(target);
    Check(hardware.Pixels.ReadPixel(0,0)==new Rgba32(100,149,237,255),"Selected target renders actual pixels.");
    Check(hardware.Contexts==2&&cpuCreations==0,"Persistent hardware target wins and is reused.");
    Check(SimdSkiaGpuDiagnostics.SelectedRenderer=="test hardware","Selected target is observable.");
}
using(var failed=new TestTarget(new COMException("Injected device creation failure")))
using(var recovered=new TestTarget())
{
    long before=SimdSkiaGpuDiagnostics.InitializationFailures;
    using var target=new GpuPreferredTarget([Candidate("failed native",()=>failed),Candidate("readback",()=>recovered)],()=>throw new Exception("Must keep a viable GPU."),new());
    Paint(target);Check(failed.Disposals==1&&recovered.Contexts==1,"Lazy initialization failure disposes partial target and tries another GPU path.");
    Check(SimdSkiaGpuDiagnostics.InitializationFailures==before+1&&SimdSkiaGpuDiagnostics.LastInitializationFailure.Contains("Injected"),"Fallback keeps the actual failure reason.");
}
using(var cpu=new TestTarget())
{
    int attempts=0;
    using var target=new GpuPreferredTarget([Candidate("missing driver",()=>{attempts++;throw new DllNotFoundException("Injected absent GPU loader");})],()=>cpu,new());
    Paint(target);Paint(target);
    Check(attempts==1&&cpu.Contexts==2&&cpu.Pixels.ReadPixel(1,1).B==237,"CPU fallback draws real pixels and does not retry each frame.");
    Check(SimdSkiaGpuDiagnostics.SelectedRenderer.Contains("CPU"),"CPU fallback is not reported as GPU acceleration.");
}
foreach(Exception error in new Exception[]{new ArgumentException("bad scene"),new ObjectDisposedException("test"),new OutOfMemoryException("test")})
{
    using var invalid=new TestTarget(error);int fallbacks=0;
    using var target=new GpuPreferredTarget([Candidate("invalid",()=>invalid)],()=>{fallbacks++;return new TestTarget();},new());
    try{Paint(target);throw new Exception("Unexpected swallowed error.");}
    catch(Exception actual)when(ReferenceEquals(actual,error)){checks++;}
    Check(fallbacks==0&&invalid.Disposals==1,"Argument/disposal/OOM errors propagate with cleanup.");
}
{
    var preference=new GpuPreference();using var lost=new TestTarget();
    using(var first=new GpuPreferredTarget([Candidate("lost device",()=>lost)],()=>new TestTarget(),preference))
    {
        Paint(first);lost.Lost=true;Check(first.PlatformRenderTargetState.IsCorrupted,"Device loss requests target recreation.");
    }
    int retried=0;using var cpu=new TestTarget();
    using var recreated=new GpuPreferredTarget([Candidate("lost device",()=>{retried++;return new TestTarget();})],()=>cpu,preference);
    Paint(recreated);Check(retried==0&&cpu.Contexts==1,"Recreation skips the lost backend rather than failing indefinitely.");
}
{
    var preference=new GpuPreference();using var changedSurface=new TestTarget();
    using(var first=new GpuPreferredTarget([Candidate("resized surface",()=>changedSurface)],()=>new TestTarget(),preference))
    {Paint(first);changedSurface.SurfaceCorrupted=true;Check(first.PlatformRenderTargetState.IsCorrupted,"An OS surface change still recreates the target.");}
    Check(preference.Allows(SimdSkiaGpuBackend.Direct2D),"A surface/resize event must not blacklist a healthy GPU device.");
}
if(args.Contains("--hardware"))
{
    if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This hardware test uses the current Windows Direct2D/Vulkan devices.");
    var glyph=new PositionedGlyph(PathGeometry.Rectangle(new(0,0,5,8)),new(0,0));
    var run=new GlyphRunGeometry([glyph],Matrix3.Identity);
    foreach(var backend in Enum.GetValues<SimdSkiaGpuBackend>())
    {
        using var actual=new PixelBuffer(32,20,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);using var expected=new PixelBuffer(32,20,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        using var gpu=new WindowGpuCanvas(actual,backend,true,atlasEntryLimit:3,atlasByteLimit:4096);
        using var reference=new WindowGpuCanvas(expected,backend,true);
        // Same hardware path, large cache and completion after each draw isolate
        // eviction/lifetime from the existing CPU-vs-GPU rounding differences.
        void Both(DrawingCommand command){gpu.Execute(command);reference.Execute(command);reference.Flush();}
        // A/B/C/A/D/A: frequently reused A must survive D. The former wholesale
        // eviction rebuilds A, and freeing queued Vulkan atlases used to invalidate draws.
        Both(DrawingCommand.Clear(new(255,255,255,255)));
        foreach(int color in new[]{1,2,3,1,4,1})
        {
            var command=new DrawingCommand(CommandKind.DrawGlyphRun,default,Matrix3.Identity with{M02=3,M12=4},new(new((byte)(color*40),0,0,200),CompositeMode.SourceOver)){GlyphRun=run};
            Both(command);Check(gpu.CachedGlyphAtlases<=3&&gpu.CachedGlyphAtlasBytes<=4096,"Glyph cache obeys both budgets.");
        }
        gpu.Flush();
        Check(gpu.BuiltGlyphAtlases==4&&gpu.GlyphAtlasHits==2,"LRU retains the hot glyph atlas.");
        Check(actual.GetPixelSpan().SequenceEqual(expected.GetPixelSpan()),"Eviction preserves in-flight command order and exact pixels.");
    }
    using var pixels=new PixelBuffer(32,20,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);using var vk=new VulkanCanvas(pixels);
    int count=vk.PreparedPipelineCount;Check(count==7,"All seven Vulkan pipelines are ready before drawing.");
    vk.Execute(DrawingCommand.Clear(new(1,2,3,255)));
    for(int i=0;i<4;i++)vk.Execute(DrawingCommand.FillRectangle(new(i*8,2,i*8+3,8),Matrix3.Identity,new(new(255,0,0,255),CompositeMode.SourceOver)));
    vk.Flush();Check(vk.RectangleBatchDispatches>0&&vk.PreparedPipelineCount==count,"First rectangle batch creates no pipeline during drawing.");
}
Console.WriteLine($"PASS {checks} GPU preference, failure recovery, cache lifetime and upfront pipeline checks.");

sealed class TestTarget(Exception? error=null):IRenderTarget,IDeviceFaultSource
{
    internal PixelBuffer Pixels{get;}=new(8,8,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
    internal int Contexts,Disposals;
    internal bool Lost,SurfaceCorrupted;
    public bool IsDeviceFaulted=>Lost;
    public RenderTargetProperties Properties=>new(){RetainsPreviousFrameContents=true,IsSuitableForDirectRendering=true};
    public PlatformRenderTargetState PlatformRenderTargetState=>Lost||SurfaceCorrupted?new(){IsCorrupted=true}:PlatformRenderTargetState.Ready;
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo scene,out RenderTargetDrawingContextProperties properties)
    {
        if(error is not null)throw error;
        Contexts++;properties=new(){PreviousFrameIsRetained=Contexts>1};return new DrawingContext(Pixels,new Vector(96,96));
    }
    public void Dispose(){if(Disposals!=0)return;Disposals++;Pixels.Dispose();}
}
