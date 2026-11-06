using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var rows=new List<object>();double checksum=0;
void Bench(string scene,Action work,int perFrame=1)
{
    var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<.4)work();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long allocation=GC.GetTotalAllocatedBytes(true);int frames=0;watch.Restart();while(watch.Elapsed.TotalSeconds<.8){work();frames++;}watch.Stop();var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;long bytes=GC.GetTotalAllocatedBytes(true)-allocation;process.Refresh();double n=(double)frames*perFrame;rows.Add(new{scene,msPerFrame=watch.Elapsed.TotalMilliseconds/n,cpuMsPerFrame=cpuMs/n,cpuPercentOfMachine=cpuMs/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=bytes/n,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,frames,perFrame});
}
using var a=new SKRegion();for(int y=0;y<12;y++)a.Op(new SKRectI(0,y*8,100+y,5+y*8),SKRegionOperation.Union);using var b=new SKRegion(new SKRectI(10,1,80,3));var query=new SKRectI(5,1,90,3);
Bench("region-contains-rectangle",()=>{for(int i=0;i<100;i++)checksum+=a.Contains(query)?1:0;},100);
Bench("region-contains-region",()=>{for(int i=0;i<100;i++)checksum+=a.Contains(b)?1:0;},100);
Bench("region-intersects-region",()=>{for(int i=0;i<100;i++)checksum+=a.Intersects(b)?1:0;},100);
Bench("region-rectangle-iterator",()=>{using var iterator=a.CreateRectIterator();while(iterator.Next(out var rect))checksum+=rect.Left+rect.Top+rect.Width;});
Bench("region-boolean-operation",()=>{using var result=new SKRegion(a);checksum+=result.Op(b,SKRegionOperation.Difference)?1:0;});
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"checksum.txt"),checksum.ToString("R",System.Globalization.CultureInfo.InvariantCulture));Console.WriteLine($"{rows.Count} CPU benchmarks");
