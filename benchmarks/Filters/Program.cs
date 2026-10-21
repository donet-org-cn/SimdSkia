using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SimdSkia;
using SimdSkia.Cpu;

if(args.Length!=2||args[0] is not ("scalar" or "simd"))throw new ArgumentException("Filters <scalar|simd> <output.json>");
CpuOptions.EnableSimd=args[0]=="simd";var results=new List<object>();
foreach(var (width,height) in new[]{(640,360),(1024,768)})foreach(float sigma in new[]{2f,8f})
{
    using var source=new PixelBuffer(width,height,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
    for(int y=0;y<height;y++)for(int x=0;x<width;x++)source.WritePixel(x,y,new((byte)x,(byte)y,(byte)(x+y),(byte)(128+(x+y)%128)));
    int checksum=0;void Render(){using var result=ManagedFilters.Blur(source,sigma,sigma);checksum+=result.GetPixelSpan()[0];}
    var warm=Stopwatch.StartNew();do{Render();}while(warm.Elapsed.TotalMilliseconds<750);
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();var p=Process.GetCurrentProcess();p.Refresh();double cpu=p.TotalProcessorTime.TotalMilliseconds;long allocated=GC.GetTotalAllocatedBytes(true);int gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2),frames=0;
    var watch=Stopwatch.StartNew();do{Render();frames++;}while(watch.Elapsed.TotalMilliseconds<1500);watch.Stop();
    cpu=p.TotalProcessorTime.TotalMilliseconds-cpu;allocated=GC.GetTotalAllocatedBytes(true)-allocated;p.Refresh();
    results.Add(new{width,height,sigma,frames,ms=watch.Elapsed.TotalMilliseconds/frames,cpuMs=cpu/frames,allocatedBytes=(double)allocated/frames,workingSetMiB=p.WorkingSet64/1048576d,privateMiB=p.PrivateMemorySize64/1048576d,retainedMiB=GC.GetTotalMemory(true)/1048576d,gc0=GC.CollectionCount(0)-gen0,gc1=GC.CollectionCount(1)-gen1,gc2=GC.CollectionCount(2)-gen2,checksum});
}
var report=new{mode=args[0],avx2=System.Runtime.Intrinsics.X86.Avx2.IsSupported,runtime=Environment.Version.ToString(),os=RuntimeInformation.OSDescription,results};
File.WriteAllText(args[1],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(args[1]);
