using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;

string output=args.Length>0?args[0]:"artifacts/pixel-bench";Directory.CreateDirectory(output);
var results=new List<object>();
using var source=new SKBitmap(new SKImageInfo(1920,1080,SKColorType.Bgra8888,SKAlphaType.Premul));
var initial=source.GetPixelSpan();for(int i=0;i<initial.Length;i+=4){initial[i]=(byte)(i%231);initial[i+1]=(byte)(i%183);initial[i+2]=(byte)(i%127);initial[i+3]=255;}
using var map=source.PeekPixels();
foreach(string scene in new[]{"read-copy-1080p","read-swap-1080p","nearest-512","linear-512"})
{
    bool scale=scene.Contains("512");using var destination=new SKBitmap(new SKImageInfo(scale?512:1920,scale?512:1080,scene.Contains("swap")?SKColorType.Rgba8888:SKColorType.Bgra8888,SKAlphaType.Premul));
    using var target=destination.PeekPixels();var sampling=new SKSamplingOptions(scene.StartsWith("linear")?SKFilterMode.Linear:SKFilterMode.Nearest);
    bool Work()=>scale?map.ScalePixels(target,sampling):map.ReadPixels(target);
    var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<.4)if(!Work())throw new Exception(scene);
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();
    long allocation=GC.GetAllocatedBytesForCurrentThread();var cpu=process.TotalProcessorTime;timer.Restart();long count=0;
    do{if(!Work())throw new Exception(scene);count++;}while(timer.Elapsed.TotalSeconds<.8);
    timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-allocation;var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;process.Refresh();
    results.Add(new{scene,iterations=count,msPerFrame=timer.Elapsed.TotalMilliseconds/count,cpuMsPerFrame=cpuMs/count,cpuPercentOfMachine=cpuMs/timer.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=(double)allocated/count,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d});
    File.WriteAllBytes(Path.Combine(output,scene+".bin"),destination.Bytes);
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(new{runtime=Environment.Version.ToString(),processors=Environment.ProcessorCount,results}));
