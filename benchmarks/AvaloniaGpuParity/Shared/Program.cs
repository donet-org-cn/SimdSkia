using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SimdSkia.ControlValidation;
#if MANAGED
using SimdSkia;
using SimdSkia.Avalonia;
#else
using SkiaSharp;
#endif

#if MANAGED
return Bench.Run(args);
#else
return args.Contains("--cpu")||args.Contains("--cpu-default")?Bench.Run(args,null,"CPU raster"):args.Contains("--angle")?AngleReferenceContext.Run((context,device)=>Bench.Run(args,context,device)):
    VulkanReferenceContext.Run((context,device)=>Bench.Run(args,context,device));
#endif

internal static class Bench
{
    internal static int Run(string[] args
#if !MANAGED
        ,GRContext? gpu,string device
#endif
    )
    {
        string Value(string key,string fallback){int i=Array.IndexOf(args,key);return i<0?fallback:i+1<args.Length?args[i+1]:throw new ArgumentException(key);}
#if MANAGED
        // JIT-only profiling knobs; no production cache policy is changed by
        // the harness unless an explicit command-line override is supplied.
        void Budget(Type anchor,string type,string field,string option)
        {if(args.Contains(option))anchor.Assembly.GetType(type,true)!.GetField(field,BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,long.Parse(Value(option,"0")));}
        Budget(typeof(SimdSkia.Cpu.RasterCanvas),"SimdSkia.Cpu.GlyphCoverageCache","RunByteLimit","--run-budget");
        Budget(typeof(SimdSkia.Cpu.RasterCanvas),"SimdSkia.Cpu.GlyphCoverageCache","GlyphByteLimit","--glyph-budget");
        Budget(typeof(SimdSkia.Cpu.RasterCanvas),"SimdSkia.Cpu.TextCoverageCache","ByteLimit","--text-budget");
        Budget(typeof(SimdSkia.Text.TrueTypeFont),"SimdSkia.Text.FontCacheRegistry","ByteLimit","--outline-budget");
        object CacheStats(string name)
        {var value=typeof(SimdSkia.Cpu.RasterCanvas).Assembly.GetType("SimdSkia.Cpu."+name,true)!.GetProperty("Statistics",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;return value.GetType().GetFields().ToDictionary(x=>x.Name,x=>x.GetValue(value));}
#endif
        string sceneName=Value("--scene","controls");
        double scale=double.Parse(Value("--scale","1"),System.Globalization.CultureInfo.InvariantCulture);
        double seconds=double.Parse(Value("--seconds","5"),System.Globalization.CultureInfo.InvariantCulture);
        double warmup=double.Parse(Value("--warmup","2"),System.Globalization.CultureInfo.InvariantCulture);
        if(sceneName is not ("controls" or "scroll")||scale is not (1 or 1.5)||seconds is < .1 or > 60||warmup is < 0 or > 30)throw new ArgumentException("Invalid workload.");
        string output=Path.GetFullPath(Value("--output","artifacts/gpu-parity/manual"));Directory.CreateDirectory(output);
        string font=Path.GetFullPath(Value("--font","baseline/fonts/ZCOOLXiaoWei-Regular.ttf"));
        var builder=AppBuilder.Configure<TestApp>().UseStandardRuntimePlatformSubsystem().UseManagedHeadless();
#if MANAGED
        builder.UseSimdSkia(o=>o.FontFiles.Add(font));
#else
        builder.UseSkia().UseHarfBuzz();
#endif
        builder.SetupWithoutStarting();
        var fonts=new EmbeddedFontCollection(new Uri("fonts:GpuParity"),new Uri(font));
        if(fonts.Count==0)throw new InvalidOperationException("Shared font not registered.");
        FontManager.Current.AddFontCollection(fonts);
        var scene=sceneName=="controls"?ControlScene.Create(fullOpacity:args.Contains("--full-opacity")):ControlScene.CreateList();
        TextElement.SetFontFamily(scene,fonts[0]);
        var host=new Window{Content=scene,Width=1000,Height=720};host.Show();Dispatcher.UIThread.RunJobs();ControlScene.Prepare(scene);
        int width=(int)(1000*scale),height=(int)(720*scale);var dpi=new Vector(96*scale,96*scale);
        var transform=new TranslateTransform();if(sceneName=="scroll")scene.RenderTransform=transform;
        Func<IDrawingContextImpl> create;
        Action complete;
        Func<byte[]> capture;
        string backend;
#if MANAGED
        backend=Value("--backend","vulkan");
        if(backend is not ("cpu" or "vulkan" or "direct2d"))throw new ArgumentException("Unknown backend.");
        using var pixels=new PixelBuffer(width,height,SimdSkia.PixelFormat.Bgra8888,SimdSkia.AlphaFormat.Premultiplied);
        var options=new SimdSkiaOptions();if(backend!="cpu")options.UseGpu(backend=="vulkan"?SimdSkiaGpuBackend.Vulkan:SimdSkiaGpuBackend.Direct2D,true,SimdSkiaGpuPresentation.FramebufferReadback);
        using var canvas=backend=="cpu"?new SimdSkia.Cpu.RasterCanvas(pixels):options.CanvasBackendFactory!(pixels);
        var contextType=typeof(SimdSkiaOptions).Assembly.GetType("SimdSkia.Avalonia.DrawingContext",true)!;
        var ctor=contextType.GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance).Single();
        create=Expression.Lambda<Func<IDrawingContextImpl>>(Expression.New(ctor,
            Expression.Constant(pixels),Expression.Constant(dpi),Expression.Constant(true),Expression.Constant(null,typeof(Action<bool>)),
            Expression.Constant(canvas,typeof(ICanvasBackend)),Expression.Constant(false))).Compile();
        complete=canvas.Flush;
        capture=()=>pixels.CopyBytes();
        string device=backend=="cpu"?"CPU raster":SimdSkiaGpuDiagnostics.GetStatistics().Device;
#else
        backend=gpu is null?(args.Contains("--cpu-default")?"original-cpu-default":"original-cpu"):args.Contains("--angle")?"original-angle-d3d11":"original-vulkan";
        var info=new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Premul);
        byte[] pixels=new byte[width*height*4];var pinned=GCHandle.Alloc(pixels,GCHandleType.Pinned);
        using var surface=(gpu is null?SKSurface.Create(info,pinned.AddrOfPinnedObject(),width*4):SKSurface.Create(gpu,true,info))??throw new InvalidOperationException("Native surface missing.");
        var contextType=Assembly.Load("Avalonia.Skia").GetType("Avalonia.Skia.DrawingContextImpl",true)!;
        var infoType=contextType.GetNestedType("CreateInfo",BindingFlags.Public)!;
        object creation=Activator.CreateInstance(infoType)!;
        void Set(string name,object value)=>infoType.GetField(name)!.SetValue(creation,value);
        Set("Surface",surface);Set("Canvas",surface.Canvas);Set("GrContext",gpu!);Set("Dpi",dpi);Set("ScaleDrawingToDpi",true);Set("DisableSubpixelTextRendering",!args.Contains("--cpu-default"));
        create=Expression.Lambda<Func<IDrawingContextImpl>>(Expression.New(contextType.GetConstructors().Single(),Expression.Constant(creation,infoType),Expression.Constant(null,typeof(IDisposable[])))).Compile();
        complete=()=>{surface.Flush();if(gpu is not null){gpu.Flush();if(!surface.ReadPixels(info,pinned.AddrOfPinnedObject(),width*4,0,0))throw new InvalidOperationException("Completed GPU readback failed.");}};
        capture=()=>pixels.ToArray();
#endif
        // The same internal Avalonia wrappers are compiled once, outside timing.
        // This diagnostic JIT harness deliberately pins the 12.1.5 API; it is
        // neither production reflection nor a Native AOT measurement.
        var asm=typeof(DrawingContext).Assembly;
        var wrapper=asm.GetType("Avalonia.Media.PlatformDrawingContext",true)!;
        var input=Expression.Parameter(typeof(IDrawingContextImpl));
        var wrap=Expression.Lambda<Func<IDrawingContextImpl,DrawingContext>>(Expression.New(wrapper.GetConstructors().Single(),input,Expression.Constant(true)),input).Compile();
        var immediate=asm.GetType("Avalonia.Rendering.ImmediateRenderer",true)!.GetMethod("Render",[typeof(DrawingContext),typeof(Visual)])!;
        var render=immediate.CreateDelegate<Action<DrawingContext,Visual>>();
        int sequence=0;
        (long Recording,long Completion) Frame(bool fixedPose=false)
        {
            if(sceneName=="scroll")transform.Y=fixedPose?-8:-(sequence++%17);
            long begin=Stopwatch.GetTimestamp();
            var impl=create();impl.Clear(Colors.Transparent);
            using(var drawing=wrap(impl))render(drawing,scene);
            long submitted=Stopwatch.GetTimestamp();complete();long finished=Stopwatch.GetTimestamp();
            return(submitted-begin,finished-submitted);
        }
        try
        {
            var warm=Stopwatch.StartNew();do{Frame();}while(warm.Elapsed.TotalSeconds<warmup);
            Console.WriteLine("READY");Console.Out.Flush();
            var times=new long[200000];var recording=new long[times.Length];var completion=new long[times.Length];
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            using var process=Process.GetCurrentProcess();process.Refresh();
            long allocation=GC.GetTotalAllocatedBytes(true);double cpu=process.TotalProcessorTime.TotalMilliseconds;
#if MANAGED
            var countersBefore=SimdSkiaGpuDiagnostics.GetStatistics();
#endif
            Console.WriteLine("MEASURE_BEGIN");Console.Out.Flush();
            var watch=Stopwatch.StartNew();int frames=0;
            while(watch.Elapsed.TotalSeconds<seconds)
            {
                if(frames==times.Length)throw new InvalidOperationException("Timing capacity exhausted.");
                var t=Frame();recording[frames]=t.Recording;completion[frames]=t.Completion;times[frames++]=t.Recording+t.Completion;
            }
            watch.Stop();double cpuMs=process.TotalProcessorTime.TotalMilliseconds-cpu;
            long bytes=GC.GetTotalAllocatedBytes(true)-allocation;
            Console.WriteLine("MEASURE_END");Console.Out.Flush();process.Refresh();
            Array.Sort(times,0,frames);
            double Ms(long ticks)=>ticks*1000d/Stopwatch.Frequency;
            double Mean(long[] ticks){long total=0;for(int i=0;i<frames;i++)total+=ticks[i];return Ms(total)/frames;}
            var modules=process.Modules.Cast<ProcessModule>().Select(m=>m.ModuleName).ToArray();
#if MANAGED
            if(modules.Any(n=>n.Equals("libSkiaSharp.dll",StringComparison.OrdinalIgnoreCase)||n.Equals("libHarfBuzzSharp.dll",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Native reference leaked into managed worker.");
            var counters=SimdSkiaGpuDiagnostics.GetStatistics();
            object diagnostics=new{gpuCommands=counters.GpuCommands-countersBefore.GpuCommands,cpuFallbackCommands=counters.CpuFallbackCommands-countersBefore.CpuFallbackCommands,
                completedBatches=counters.CompletedBatches-countersBefore.CompletedBatches,fallbackReasons=counters.FallbackReasons,glyphCache=CacheStats("GlyphCoverageCache"),textCache=CacheStats("TextCoverageCache")};
#else
            if(!modules.Any(n=>n.Equals("libSkiaSharp.dll",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Original native renderer not loaded.");
            object diagnostics=new{nativeReadbacks=gpu is null?0:frames};
#endif
            var result=new{backend,device,scene=sceneName,scale,width,height,frames,seconds=watch.Elapsed.TotalSeconds,
                runtime=Environment.Version.ToString(),mode=backend=="cpu"||backend.StartsWith("original-cpu",StringComparison.Ordinal)?"JIT CPU complete-frame":"JIT GPU complete-frame readback",nativeAot=false,wallMs=watch.Elapsed.TotalMilliseconds,fullOpacity=args.Contains("--full-opacity"),
                meanMs=Mean(times),recordingMs=Mean(recording),completionMs=Mean(completion),p95Ms=Ms(times[(int)Math.Ceiling(frames*.95)-1]),
                cpuMsPerFrame=cpuMs/frames,cpuPercentMachine=cpuMs/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,
                workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,allocatedBytesPerFrame=(double)bytes/frames,
                diagnostics,loadedModules=modules,fontSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(font)))};
            File.WriteAllText(Path.Combine(output,"metrics.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            Frame(true);var raw=capture();for(int i=0;i<raw.Length;i+=4)(raw[i],raw[i+2])=(raw[i+2],raw[i]);
            File.WriteAllBytes(Path.Combine(output,"scene.rgba"),raw);
            Console.WriteLine($"DONE {backend} {sceneName}/{scale}: {result.meanMs:F4}ms completed; record={result.recordingMs:F4}, completion={result.completionMs:F4}; {device}");
            return 0;
        }
        finally
        {
            host.Close();
#if !MANAGED
            pinned.Free();
#endif
        }
    }
}
internal sealed class TestApp:Application {public override void Initialize()=>Styles.Add(new FluentTheme());}
