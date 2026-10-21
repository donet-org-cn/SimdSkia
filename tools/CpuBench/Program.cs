using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using SimdSkia.Cpu;

const int width=1024,height=768,iterations=80;
using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);
var color=new SKColor(40,120,200,128);var results=new List<object>();
foreach(bool simd in new[]{false,true})
{
    CpuOptions.EnableSimd=simd;for(int i=0;i<20;i++)canvas.Clear(color);var watch=new Stopwatch();
    long start=GC.GetAllocatedBytesForCurrentThread();watch.Start();for(int i=0;i<iterations;i++)canvas.Clear(color);watch.Stop();long allocation=GC.GetAllocatedBytesForCurrentThread()-start;
    var result=new {Backend="CPU",Kernel=CpuOptions.ClearImplementation,Width=width,Height=height,Iterations=iterations,TotalMilliseconds=watch.Elapsed.TotalMilliseconds,MillisecondsPerFrame=watch.Elapsed.TotalMilliseconds/iterations,AllocatedBytes=allocation,WorkingSet=Environment.WorkingSet,LastPixel=bitmap.GetPixel(0,0).ToString()};
    results.Add(result);Console.WriteLine(JsonSerializer.Serialize(result));
}
CpuOptions.EnableSimd=true;Directory.CreateDirectory("artifacts");File.WriteAllText(args.FirstOrDefault()??"artifacts/cpu-after.json",JsonSerializer.Serialize(results,new JsonSerializerOptions {WriteIndented=true}));
