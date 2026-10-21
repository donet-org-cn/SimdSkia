using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using SkiaSharp;

namespace SimdSkia.Benchmarks;

public static class Worker
{
    public static int Run(string[] args)
    {
        string Get(string key,string fallback){int index=Array.IndexOf(args,key);return index<0?fallback:index+1<args.Length?args[index+1]:throw new ArgumentException("Missing value: "+key);}
        var settings=new BenchmarkSettings(Get("--engine","original"),Path.GetFullPath(Get("--out","artifacts/performance/worker")),Path.GetFullPath(Get("--fixtures","artifacts/performance/fixtures")),Path.GetFullPath(Get("--font","baseline/fonts/ZenOldMincho-Regular.ttf")),int.Parse(Get("--fork","0")),int.Parse(Get("--samples","7")),int.Parse(Get("--sample-ms","60")),int.Parse(Get("--warmup-ms","100")),int.Parse(Get("--seed","20261012")),Get("--sizes","256x256,1024x768"),Get("--scenes",string.Join(',',Workload.Names)));
        if(settings.Samples<3||settings.Samples>100||settings.SampleMilliseconds<10||settings.SampleMilliseconds>5000||settings.WarmupMilliseconds<10)throw new ArgumentOutOfRangeException(nameof(args));
        var sizes=settings.Sizes.Split(',').Select(value=>{var parts=value.Split('x');if(parts.Length!=2)throw new ArgumentException("Invalid size.");int width=int.Parse(parts[0]),height=int.Parse(parts[1]);if(width<64||height<64||width>4096||height>4096)throw new ArgumentException("Size outside benchmark limits.");return(width,height);}).ToArray();
        string[] names=settings.Scenes.Split(',');foreach(string name in names)if(!Workload.Names.Contains(name)&&!Workload.ChangingNames.Contains(name))throw new ArgumentException("Unknown workload: "+name);
        Directory.CreateDirectory(settings.Output);Directory.CreateDirectory(settings.FixtureDirectory);
        if(args.Contains("--make-fixtures"))
        {
#if ORIGINAL
            foreach(var (width,height) in sizes){using var pixels=Workload.CreatePixels(width,height);using var encoded=pixels.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(Path.Combine(settings.FixtureDirectory,$"input-{width}x{height}.png"));encoded.SaveTo(stream);}return 0;
#else
            throw new InvalidOperationException("Common compressed fixtures must be created by the reference process.");
#endif
        }
#if ORIGINAL
        if(settings.Engine!="original")throw new ArgumentException("Reference worker needs --engine original.");string simd="Native Skia CPU dispatch";
#else
        if(settings.Engine is not ("scalar" or "simd"))throw new ArgumentException("Managed worker needs --engine scalar|simd.");
        SimdSkia.Cpu.CpuOptions.EnableSimd=settings.Engine=="simd";string simd=SimdSkia.Cpu.CpuOptions.ClearImplementation;
#endif
        using(var initializer=new SKBitmap(1,1))using(var canvas=new SKCanvas(initializer))canvas.Clear();
        var assembly=typeof(SKBitmap).Assembly;string cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")??"unknown";
        if(OperatingSystem.IsWindows()){using var key=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");cpu=key?.GetValue("ProcessorNameString")?.ToString()?.Trim()??cpu;}
        var runtime=new RuntimeDetails(RuntimeInformation.FrameworkDescription,RuntimeInformation.OSDescription,RuntimeInformation.ProcessArchitecture.ToString(),cpu,Environment.ProcessorCount,System.Runtime.GCSettings.IsServerGC,assembly.FullName!,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(settings.FontPath))),simd,Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"),Stopwatch.Frequency);
        var order=(from size in sizes from name in names select (name,size.width,size.height)).ToArray();var random=new Random(settings.Seed+settings.Fork*17);
        random.Shuffle(order);var results=new List<WorkloadResult>();
        foreach(var (name,width,height) in order)
        {
            Collect();var before=Memory();long setupStart=Stopwatch.GetTimestamp();
            using(var workload=new Workload(name,width,height,settings.FixtureDirectory,settings.FontPath))
            {
                double setup=Stopwatch.GetElapsedTime(setupStart).TotalMilliseconds;int frame=0;long warmStart=Stopwatch.GetTimestamp();
                do{workload.Render(frame++);}while(frame<3||Stopwatch.GetElapsedTime(warmStart).TotalMilliseconds<settings.WarmupMilliseconds);
                int calibration=1;double elapsed;
                do{long begin=Stopwatch.GetTimestamp();for(int i=0;i<calibration;i++)workload.Render(frame++);elapsed=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;if(elapsed>=20||calibration>=65536)break;calibration*=2;}while(true);
                int iterations=Math.Clamp((int)Math.Ceiling(settings.SampleMilliseconds*calibration/Math.Max(.001,elapsed)),1,65536);
                Collect();var resident=Memory();var samples=new SampleResult[settings.Samples];var latencies=new long[checked(iterations*settings.Samples)];
                using var sampler=Process.GetCurrentProcess();
                for(int sample=0;sample<samples.Length;sample++)
                {
                    sampler.Refresh();TimeSpan processorStart=sampler.TotalProcessorTime;
                    int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);long allocation=GC.GetAllocatedBytesForCurrentThread();
                    long begin=Stopwatch.GetTimestamp();
                    for(int i=0;i<iterations;i++){long tick=Stopwatch.GetTimestamp();workload.Render(frame++);latencies[sample*iterations+i]=Stopwatch.GetTimestamp()-tick;}
                    double duration=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;long bytes=GC.GetAllocatedBytesForCurrentThread()-allocation;
                    int end0=GC.CollectionCount(0),end1=GC.CollectionCount(1),end2=GC.CollectionCount(2);
                    sampler.Refresh();double cpuMilliseconds=(sampler.TotalProcessorTime-processorStart).TotalMilliseconds;
                    samples[sample]=new(iterations,duration,bytes,end0-g0,end1-g1,end2-g2,Memory(),100*cpuMilliseconds/Math.Max(.001,duration),cpuMilliseconds);
                }
                Collect();var after=Memory();string stem=$"{name}-{width}x{height}";string latencyFile=stem+".ticks";File.WriteAllBytes(Path.Combine(settings.Output,latencyFile),MemoryMarshal.AsBytes(latencies.AsSpan()).ToArray());
                using var bitmap=workload.ValidationBitmap();var rgba=new byte[checked(width*height*4)];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++){var color=bitmap.GetPixel(x,y);int i=(y*width+x)*4;rgba[i]=color.Red;rgba[i+1]=color.Green;rgba[i+2]=color.Blue;rgba[i+3]=color.Alpha;}
                string pixelsFile=stem+".rgba";File.WriteAllBytes(Path.Combine(settings.Output,pixelsFile),rgba);
                string hash=workload.InputEncodedBytes==0?"":Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(settings.FixtureDirectory,$"input-{width}x{height}.png"))));
                results.Add(new(name,width,height,"Rgba8888",name.StartsWith("png-",StringComparison.Ordinal)?"Unpremul codec output":"Premul",workload.OperationsPerFrame,workload.AllowedPixelError,name!="png-encode",iterations,setup,before,resident,after,samples,latencyFile,pixelsFile,workload.EncodedBytes,workload.InputEncodedBytes,hash));
                Console.WriteLine($"{settings.Engine}/fork{settings.Fork} {stem}: {samples.Sum(s=>s.TotalMilliseconds)/samples.Sum(s=>s.Operations):F4} ms/op; {samples.Sum(s=>s.AllocatedBytes)/(double)samples.Sum(s=>s.Operations):F1} B/op");
            }
        }
        File.WriteAllText(Path.Combine(settings.Output,"results.json"),JsonSerializer.Serialize(new WorkerResult(settings,runtime,results.ToArray()),new JsonSerializerOptions{WriteIndented=true}));return 0;
    }
    private static void Collect(){GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,true,true);}
    private static MemorySnapshot Memory(){using var process=Process.GetCurrentProcess();process.Refresh();return new(GC.GetTotalMemory(false),GC.GetGCMemoryInfo().HeapSizeBytes,process.PrivateMemorySize64,process.WorkingSet64,process.PeakWorkingSet64);}
}
