using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var rows=new List<object>();double checksum=0;
void Bench(string scene,Action work,int perFrame=1)
{
    var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<.4)work();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long allocation=GC.GetTotalAllocatedBytes(true);int frames=0;watch.Restart();while(watch.Elapsed.TotalSeconds<.8){work();frames++;}watch.Stop();var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;long bytes=GC.GetTotalAllocatedBytes(true)-allocation;process.Refresh();double n=(double)frames*perFrame;rows.Add(new{scene,msPerFrame=watch.Elapsed.TotalMilliseconds/n,cpuMsPerFrame=cpuMs/n,cpuPercentOfMachine=cpuMs/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=bytes/n,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,frames,perFrame});
}
using var path=new SKPath();for(int i=0;i<32;i++){path.MoveTo(i,1);for(int j=0;j<16;j++)path.QuadTo(j*5,i*2,j*6,i*2+5);if(i%2==0)path.Close();}
Bench("regular-iterator-512-curves",()=>{using var it=path.CreateIterator(true);Span<SKPoint> points=stackalloc SKPoint[4];for(SKPathVerb verb;(verb=it.Next(points))!=SKPathVerb.Done;)checksum+=points[0].X+(int)verb;});
Bench("raw-iterator-512-curves",()=>{using var it=path.CreateRawIterator();Span<SKPoint> points=stackalloc SKPoint[4];for(SKPathVerb verb;(verb=it.Next(points))!=SKPathVerb.Done;)checksum+=points[0].X+(int)verb;});
using var face=SKTypeface.FromFile("baseline/fonts/ZCOOLXiaoWei-Regular.ttf");using var font=new SKFont(face,16){Hinting=SKFontHinting.None,LinearMetrics=true,Subpixel=true,BaselineSnap=false};string text="Hello 中文文字测量 12345";var positions=new float[font.CountGlyphs(text)];
Bench("font-measure-and-offsets",()=>{for(int i=0;i<100;i++){checksum+=font.MeasureText(text);font.GetGlyphOffsets(text,positions.AsSpan());checksum+=positions[^1];}},100);
using var measure=new SKPathMeasure(path);var length=measure.Length;
Bench("path-distance-query",()=>{for(int i=0;i<100;i++){measure.GetPositionAndTangent(length*i/100,out var p,out var t);checksum+=p.X+t.Y;}},100);
using var data=SKData.CreateCopy(new byte[8192]);var dest=new byte[1024];
Bench("data-subset-stream",()=>{using var sub=data.Subset(200,4096);using var stream=sub.AsStream();checksum+=stream.Read(dest);});
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"checksum.txt"),checksum.ToString("R",System.Globalization.CultureInfo.InvariantCulture));Console.WriteLine($"{rows.Count} CPU benchmarks");
