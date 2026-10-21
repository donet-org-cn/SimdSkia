using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;

namespace Scenes;

internal static class ClearBenchmark
{
    public static void Run()
    {
        const int width=1024,height=768,iterations=80;using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);var color=new SKColor(40,120,200,128);
        for(int i=0;i<20;i++)canvas.Clear(color);var timer=new Stopwatch();long start=GC.GetAllocatedBytesForCurrentThread();timer.Start();for(int i=0;i<iterations;i++)canvas.Clear(color);timer.Stop();
        var report=new {Backend="Original SkiaSharp CPU",Width=width,Height=height,Iterations=iterations,MillisecondsPerFrame=timer.Elapsed.TotalMilliseconds/iterations,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-start,WorkingSet=Environment.WorkingSet,LastPixel=bitmap.GetPixel(0,0).ToString()};
        Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/cpu-reference.json",JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));
    }
}
