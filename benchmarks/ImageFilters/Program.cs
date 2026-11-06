using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
double warmup=args.Length>1?double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture):.4;
#if ORIGINAL
if(args.Length>2)SKGraphics.SetResourceCacheTotalByteLimit(long.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture)*1024*1024);
#endif
string output=args[0];Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"protocol.json"),JsonSerializer.Serialize(new{warmupSeconds=warmup,measurementSeconds=.8}));var rows=new List<object>();
using var surface=SKSurface.Create(new SKImageInfo(384,192,SKColorType.Rgba8888,SKAlphaType.Premul))??throw new Exception("surface");using var paint=new SKPaint{Color=new SKColor(30,110,220,200)};
var factories=new (string Name,Func<SKImageFilter> Create)[]{("offset",()=>SKImageFilter.CreateOffset(3,-2)),("color",()=>{using var cf=SKColorFilter.CreateLighting(new SKColor(180,255,140),new SKColor(8,20,40));return SKImageFilter.CreateColorFilter(cf);}), ("blur-2",()=>SKImageFilter.CreateBlur(2,2)),("blur-12",()=>SKImageFilter.CreateBlur(12,12)),("shadow",()=>SKImageFilter.CreateDropShadow(4,5,3,3,new SKColor(30,40,90,180))), ("dilate",()=>SKImageFilter.CreateDilate(3,2))};
foreach(var (name,create) in factories)
{
    using var filter=create();paint.ImageFilter=filter;void Work(){surface.Canvas.Clear(SKColors.White);surface.Canvas.DrawRect(new SKRect(64,56,304,136),paint);}
    var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<warmup)Work();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long allocation=GC.GetTotalAllocatedBytes(true);int frames=0;watch.Restart();while(watch.Elapsed.TotalSeconds<.8){Work();frames++;}watch.Stop();double cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;long bytes=GC.GetTotalAllocatedBytes(true)-allocation;process.Refresh();rows.Add(new{scene=name,msPerFrame=watch.Elapsed.TotalMilliseconds/frames,cpuMsPerFrame=cpuMs/frames,cpuPercentOfMachine=cpuMs/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=(double)bytes/frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,frames});
    using var image=surface.Snapshot();using var bitmap=SKBitmap.FromImage(image);File.WriteAllBytes(Path.Combine(output,name+".rgba"),bitmap.GetPixelSpan().ToArray());paint.ImageFilter=null;
#if ORIGINAL
    File.WriteAllText(Path.Combine(output,name+"-cache.json"),JsonSerializer.Serialize(new{totalByteLimit=SKGraphics.GetResourceCacheTotalByteLimit(),totalBytesUsed=SKGraphics.GetResourceCacheTotalBytesUsed(),singleAllocationLimit=SKGraphics.GetResourceCacheSingleAllocationByteLimit()}));
#endif
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
