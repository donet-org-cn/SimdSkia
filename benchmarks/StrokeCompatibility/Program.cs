using SkiaSharp;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
string output=args[0];Directory.CreateDirectory(output);var rows=new List<object>();
using var b=new SKBitmap(128,128,SKColorType.Bgra8888,SKAlphaType.Premul);using var canvas=new SKCanvas(b);
var paths=Enumerable.Range(0,8).Select(n=>{var p=new SKPath();p.MoveTo(n*.25f,0);for(int i=1;i<128;i++)p.LineTo(i+n*.25f,(i%2)*10+i*.5f);return p;}).ToArray();
using var dst=new SKPath();using var paint=new SKPaint{Style=SKPaintStyle.Stroke,StrokeWidth=3,IsAntialias=true,Color=new SKColor(40,110,200,160)};
long sink=0;
foreach(string scene in new[]{"outline-hot","outline-changing","round-lines"})
{
    int index=0;
    void Frame()
    {
        if(scene.StartsWith("outline"))
        {
            paint.StrokeCap=SKStrokeCap.Butt;paint.StrokeJoin=SKStrokeJoin.Miter;
            for(int i=0;i<32;i++){if(!paint.GetFillPath(paths[scene=="outline-hot"?0:index++%8],dst))throw new Exception("Outline failed");sink+=dst.PointCount;}
        }
        else
        {
            paint.StrokeCap=SKStrokeCap.Round;paint.StrokeJoin=SKStrokeJoin.Round;canvas.Clear(SKColors.Transparent);
            for(int i=0;i<32;i++)canvas.DrawLine(8,i*3+8,118,i*3+8,paint);
        }
    }
    var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<.4)Frame();
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();double cpu=process.TotalProcessorTime.TotalMilliseconds;long allocated=GC.GetAllocatedBytesForCurrentThread();int frames=0;
    watch.Restart();do{Frame();frames++;}while(watch.Elapsed.TotalSeconds<.8);watch.Stop();cpu=process.TotalProcessorTime.TotalMilliseconds-cpu;allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;process.Refresh();
    byte[] pixels=b.Bytes;File.WriteAllBytes(Path.Combine(output,scene+".bgra"),pixels);
    rows.Add(new{scene,frames,msPerFrame=watch.Elapsed.TotalMilliseconds/frames,cpuMsPerFrame=cpu/frames,allocatedBytesPerFrame=(double)allocated/frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,pixelSha256=Convert.ToHexString(SHA256.HashData(pixels))});
}
foreach(var p in paths)p.Dispose();File.WriteAllText(Path.Combine(output,"metrics.json"),JsonSerializer.Serialize(new{runtime=Environment.Version.ToString(),rows,sink},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(output);
