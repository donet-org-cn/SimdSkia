using System.Runtime.InteropServices;
using Avalonia;
using Colors=Avalonia.Media.Colors;
using Avalonia.Platform;
using SimdSkia;
using SimdSkia.Avalonia;
using SimdSkia.Cpu;
using SimdSkia.Gpu;
using SimdSkia.Gpu.Direct2D;
using PixelFormat=SimdSkia.PixelFormat;
using AlphaFormat=SimdSkia.AlphaFormat;

if(args.Length==4&&args[0]=="--native-throughput")
{
    if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Native Direct2D throughput requires Windows hardware.");
    NativeThroughput.Run(args[1],int.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture),args[3]);return;
}

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
    using(var originalPixels=new PixelBuffer(64,48,PixelFormat.Bgra8888,AlphaFormat.Premultiplied))
    using(var cachedPixels=new PixelBuffer(64,48,PixelFormat.Bgra8888,AlphaFormat.Premultiplied))
    {
        AppContext.SetSwitch("SimdSkia.Direct2D.DisableStateCache",true);
        using var original=new Direct2DCanvas(originalPixels);
        AppContext.SetSwitch("SimdSkia.Direct2D.DisableStateCache",false);
        using var cached=new Direct2DCanvas(cachedPixels);
        void Both(DrawingCommand command){original.Execute(command);cached.Execute(command);}
        Both(DrawingCommand.Clear(new(22,44,66,255)));
        var mask=new CoverageClip(3,4,20,18,Enumerable.Range(0,360).Select(i=>(byte)(i%4*85)).ToArray());
        for(int i=0;i<48;i++)
        {
            var mode=(CompositeMode)(i%3);
            var matrix=i%2==0?Matrix3.Identity:new Matrix3(1,.2f,2,-.1f,1,3,0,0,1);
            var command=DrawingCommand.FillRectangle(new(i%12,2,32+i%8,35),matrix,new(new((byte)(i*17),110,190,160),mode,i%2==0)) with{Clip=i%7==0?null:new(2.25f,3.75f,54.5f,42.5f),CoverageClip=mode==CompositeMode.SourceOver&&i%2==0?mask:null};
            Both(command);
            if(i%11==0){original.Flush();cached.Flush();}
        }
        original.Flush();cached.Flush();
        Check(originalPixels.GetPixelSpan().SequenceEqual(cachedPixels.GetPixelSpan()),"Cached native state preserves exact transformed clips/layers/blends across EndDraw.");
        long a=original.NativeStateChanges,b=cached.NativeStateChanges;
        for(int i=0;i<20;i++)Both(DrawingCommand.FillRectangle(new(1,1,10,10),Matrix3.Identity,new(new(120,140,160,255),CompositeMode.SourceOver,true)) with{Clip=new(0,0,64,48)});
        original.Flush();cached.Flush();
        Check(cached.NativeStateChanges-b<(original.NativeStateChanges-a)/2&&originalPixels.GetPixelSpan().SequenceEqual(cachedPixels.GetPixelSpan()),"Repeated native state calls fall by over half without changing pixels.");
    }
    nint hwnd=NativeWindow.CreateWindowEx(0,"STATIC","SimdSkia GPU completion regression",0,0,0,64,48,0,0,0,0);
    if(hwnd==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    try
    {
        AppContext.SetSwitch("SimdSkia.Direct2D.ValidateCompletionEvent",true);
        foreach(bool legacy in new[]{false,true})
        foreach(var format in new[]{PixelFormat.Rgba8888,PixelFormat.Bgra8888})
        {
            AppContext.SetSwitch("SimdSkia.Direct2D.LegacySwapchain",legacy);
            AppContext.SetSwitch("SimdSkia.Direct2D.ForceFlush1",legacy);
            using var actual=new PixelBuffer(64,48,format,AlphaFormat.Premultiplied,64*4+12);
            using var expected=new PixelBuffer(64,48,format,AlphaFormat.Premultiplied,64*4+12);
            for(int y=0;y<48;y++){actual.GetRowSpan(y)[256..].Fill(0xa5);expected.GetRowSpan(y)[256..].Fill(0xa5);}
            using var gpu=new WindowGpuCanvas(actual,SimdSkiaGpuBackend.Direct2D,true,hwnd);
            using var cpu=new RasterCanvas(expected);
            void Both(DrawingCommand command){gpu.Execute(command);cpu.Execute(command);}
            Both(DrawingCommand.Clear(new(22,44,66,255)));
            Both(DrawingCommand.FillRectangle(new(2,3,15,19),Matrix3.Identity,new(new(235,56,78,255),CompositeMode.SourceOver)));
            gpu.Present();
            // A second partial frame must retain the first, despite flip rotation.
            Both(DrawingCommand.FillRectangle(new(31,21,44,36),Matrix3.Identity,new(new(56,210,78,255),CompositeMode.SourceOver)));
            gpu.Present();
            // The first command of the next frame falls back to CPU. It must
            // read GPU pixels even though the preceding Present cleared gpuDirty.
            Both(DrawingCommand.FillRectangle(new(0,0,25,24),Matrix3.Identity,new(new(125,180,210,128),CompositeMode.Multiply)));
            gpu.Present();gpu.Flush();
            Check(actual.GetPixelSpan().SequenceEqual(expected.GetPixelSpan()),"Native partial frames + cross-frame CPU fallback preserve exact pixels and stride: "+format+" legacy="+legacy);
            Check(SimdSkiaGpuDiagnostics.NativePendingFrames==0,"Flush drains all submitted native GPU work.");
        }
        AppContext.SetSwitch("SimdSkia.Direct2D.LegacySwapchain",false);
        AppContext.SetSwitch("SimdSkia.Direct2D.ForceFlush1",false);
        using var queuedPixels=new PixelBuffer(64,48,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        using var queued=new Direct2DCanvas(queuedPixels,hwnd);
        Check((queued.NativeDeviceCreationFlags&1)==0,"The device keeps the thread-safe flag policy after the single-threaded experiment.");
        queued.ConfigureCache(0,false); // Every image is a transient GPU resource.
        queued.Execute(DrawingCommand.Clear(new(22,44,66,255)));
        for(int i=0;i<24;i++)
        {
            using var image=new PixelBuffer(2,2,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
            for(int y=0;y<2;y++)for(int x=0;x<2;x++)image.WritePixel(x,y,new((byte)(i*10),80,120,255));
            queued.Execute(new DrawingCommand(CommandKind.DrawImage,new(i*2,1,i*2+2,40),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver)){Image=image,ImageIsImmutable=true,Sampling=ImageSampling.Nearest});
            // Dispose the CPU source immediately; its uploaded GPU bitmap must
            // stay alive until this frame's completion event actually signals.
            queued.QueuePresent();Check(queued.PendingFrames<=2,"At most two frames/resources are in flight.");
        }
        queued.WaitForIdle();
        Check(queued.CompletedFrames==24&&queued.SubmittedFrames==24&&queued.PendingFrames==0,"Idle waits for actual completion of every queued frame.");
        Check(queued.CompletionChecks==24&&queued.CompletionPolls==0,"Every queued completion event is verified by an independent GPU query without polling.");
        Check(!queued.UsesTimelineFences||queued.TimelineSignals==24,"Supported timeline fences signal every queued GPU frame.");
        Console.WriteLine($"Native completion: timeline={queued.UsesTimelineFences}, signals={queued.TimelineSignals}, explicitFlushes={queued.ExplicitCompletionFlushes}");
        queued.Flush();
        Check(queuedPixels.ReadPixel(1,2)==new Rgba32(0,80,120,255)&&queuedPixels.ReadPixel(47,2)==new Rgba32(230,80,120,255),"GPU texture retains all 24 partial frames.");
        Throws<InvalidOperationException>(()=>Task.Run(()=>{if(OperatingSystem.IsWindows())queued.WaitForIdle();}).GetAwaiter().GetResult());
    }
    finally{AppContext.SetSwitch("SimdSkia.Direct2D.ValidateCompletionEvent",false);AppContext.SetSwitch("SimdSkia.Direct2D.LegacySwapchain",false);AppContext.SetSwitch("SimdSkia.Direct2D.ForceFlush1",false);NativeWindow.DestroyWindow(hwnd);}
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

internal static partial class NativeWindow
{
    [LibraryImport("user32.dll",EntryPoint="CreateWindowExW",StringMarshalling=StringMarshalling.Utf16,SetLastError=true)]
    internal static partial nint CreateWindowEx(uint extendedStyle,string className,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint parameter);
    [LibraryImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint hwnd);
}
