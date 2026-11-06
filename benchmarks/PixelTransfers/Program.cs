using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);double warmup=args.Length>1?double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture):2;
File.WriteAllText(Path.Combine(output,"protocol.json"),JsonSerializer.Serialize(new{warmupSeconds=warmup,measurementSeconds=.8}));var rows=new List<object>();
foreach(var (name,width,height,sourceFormat,destinationFormat,padding) in new[]{("pma-rgba-512",512,256,SKColorType.Rgba8888,SKColorType.Rgba8888,0),("pma-swap-padded-512",512,256,SKColorType.Bgra8888,SKColorType.Rgba8888,32),("pma-rgba-720p",1280,720,SKColorType.Rgba8888,SKColorType.Rgba8888,0)})
{
    using var source=new SKBitmap(new SKImageInfo(width,height,sourceFormat,SKAlphaType.Premul),width*4+padding);var data=source.GetPixelSpan();
    for(int y=0;y<height;y++)for(int x=0;x<width;x++){int p=y*source.RowBytes+x*4;byte a=(byte)(x+y);data[p]=(byte)((x*17)%((int)a+1));data[p+1]=(byte)((y*13)%((int)a+1));data[p+2]=(byte)((x+y*3)%((int)a+1));data[p+3]=a;}
    source.SetImmutable();using var image=SKImage.FromBitmap(source);var info=new SKImageInfo(width,height,destinationFormat,SKAlphaType.Unpremul);using var target=new SKBitmap(info,width*4+padding);var address=target.GetPixels();
    void Work(){if(!image.ReadPixels(info,address,target.RowBytes,0,0))throw new Exception("ReadPixels");}
    var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<warmup)Work();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long allocated=GC.GetTotalAllocatedBytes(true);int frames=0;
    watch.Restart();while(watch.Elapsed.TotalSeconds<.8){Work();frames++;}watch.Stop();double cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;long bytes=GC.GetTotalAllocatedBytes(true)-allocated;process.Refresh();rows.Add(new{scene=name,msPerFrame=watch.Elapsed.TotalMilliseconds/frames,cpuMsPerFrame=cpuMs/frames,cpuPercentOfMachine=cpuMs/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=(double)bytes/frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,gcHeapMiB=GC.GetTotalMemory(false)/1048576d,frames});
    using var stream=File.Create(Path.Combine(output,name+".rgba"));for(int y=0;y<height;y++)stream.Write(target.GetPixelSpan().Slice(y*target.RowBytes,width*4));
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
