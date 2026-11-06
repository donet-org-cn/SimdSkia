using SkiaSharp;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

string output=args[0];Directory.CreateDirectory(output);
var results=new List<object>();
foreach(string scene in new[]{"erase-tight","erase-stride","linear-horizontal","linear-diagonal"})
{
    var info=new SKImageInfo(512,512,SKColorType.Bgra8888,SKAlphaType.Premul);
    using var bitmap=new SKBitmap(info,scene=="erase-stride"?2112:2048);
    using var canvas=new SKCanvas(bitmap);
    using var shader=SKShader.CreateLinearGradient(new(0,0),scene=="linear-horizontal"?new(512,0):new(512,512),new[]{SKColors.Red,SKColors.Blue},SKShaderTileMode.Clamp);
    using var paint=new SKPaint{Shader=shader};var color=new SKColor(32,76,128,160);
    void Frame(){if(scene.StartsWith("erase"))bitmap.Erase(color);else canvas.DrawRect(new(0,0,512,512),paint);}
    var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<.3)Frame();
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    using var proc=Process.GetCurrentProcess();double cpu=proc.TotalProcessorTime.TotalMilliseconds;long allocated=GC.GetAllocatedBytesForCurrentThread();int count=0;
    watch.Restart();do{Frame();count++;}while(watch.Elapsed.TotalSeconds<.7);
    watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;cpu=proc.TotalProcessorTime.TotalMilliseconds-cpu;proc.Refresh();
    byte[] pixels=bitmap.Bytes;File.WriteAllBytes(Path.Combine(output,scene+".bgra"),pixels);
    results.Add(new{scene,frames=count,msPerFrame=watch.Elapsed.TotalMilliseconds/count,cpuMsPerFrame=cpu/count,cpuOneCorePercent=100*cpu/watch.Elapsed.TotalMilliseconds,cpuMachinePercent=100*cpu/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount,allocatedBytesPerFrame=(double)allocated/count,workingSetMiB=proc.WorkingSet64/1048576d,privateMiB=proc.PrivateMemorySize64/1048576d,pixelSha256=Convert.ToHexString(SHA256.HashData(pixels))});
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{engine=typeof(SKBitmap).Assembly.FullName,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,logicalProcessors=Environment.ProcessorCount,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(output);
