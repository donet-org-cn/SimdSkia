using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;

string output=args[0];Directory.CreateDirectory(output);
using var bitmap=new SKBitmap(128,128);using var canvas=new SKCanvas(bitmap);
using var paint=new SKPaint{Color=new SKColor(32,76,128,160),StrokeWidth=3,IsAntialias=true};
using var path=new SKPath();path.MoveTo(0,0);for(int i=0;i<512;i++)path.LineTo(i%128,(i*17)%128);
float sink=0;
var results=new List<object>();
foreach(string scene in new[]{"bounds","lines","circles","mixed"})
{
    void Frame()
    {
        if(scene=="bounds"){for(int i=0;i<32;i++)sink+=path.Bounds.Right;return;}
        canvas.Clear(SKColors.Transparent);
        for(int i=0;i<32;i++)
        {
            if(scene is "lines" or "mixed")canvas.DrawLine(10,i*3+8,116,i*3+8,paint);
            if(scene is "circles" or "mixed")canvas.DrawCircle(i%8*15+10,i/8*29+14,6,paint);
        }
    }
    var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<.3)Frame();
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    var proc=Process.GetCurrentProcess();double cpu=proc.TotalProcessorTime.TotalMilliseconds;long allocated=GC.GetAllocatedBytesForCurrentThread();int count=0;
    watch.Restart();do{Frame();count++;}while(watch.Elapsed.TotalSeconds<.6);
    watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;cpu=proc.TotalProcessorTime.TotalMilliseconds-cpu;
    proc.Refresh();
    byte[] pixels=bitmap.Bytes;File.WriteAllBytes(Path.Combine(output,scene+".rgba"),pixels);
    results.Add(new{scene,frames=count,msPerFrame=watch.Elapsed.TotalMilliseconds/count,cpuMsPerFrame=cpu/count,allocatedBytesPerFrame=(double)allocated/count,workingSetMiB=proc.WorkingSet64/1048576d,privateMiB=proc.PrivateMemorySize64/1048576d,pixelSha256=Convert.ToHexString(SHA256.HashData(pixels))});
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{engine=typeof(SKBitmap).Assembly.FullName,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,results,sink},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(output);
