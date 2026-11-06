using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
double warmup=args.Length>1?double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture):.4;
var output=args[0];Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"protocol.json"),JsonSerializer.Serialize(new{warmupSeconds=warmup,measurementSeconds=.8}));var rows=new List<object>();double checksum=0;
void Bench(string scene,Action work,int perFrame=1)
{
    var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<warmup)work();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long allocation=GC.GetTotalAllocatedBytes(true);int frames=0;watch.Restart();while(watch.Elapsed.TotalSeconds<.8){work();frames++;}watch.Stop();var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;long bytes=GC.GetTotalAllocatedBytes(true)-allocation;process.Refresh();double n=(double)frames*perFrame;rows.Add(new{scene,msPerFrame=watch.Elapsed.TotalMilliseconds/n,cpuMsPerFrame=cpuMs/n,cpuPercentOfMachine=cpuMs/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=bytes/n,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,frames,perFrame});
}

using var face=SKTypeface.FromFile("baseline/fonts/ZCOOLXiaoWei-Regular.ttf");using var font=new SKFont(face,18){Hinting=SKFontHinting.None,LinearMetrics=true,Subpixel=true,BaselineSnap=false};var ids=font.GetGlyphs("Hello 中文文字 1234");var positions=font.GetGlyphPositions(ids.AsSpan());var transforms=positions.Select((p,i)=>SKRotationScaleMatrix.CreateDegrees(1,(i%3-1)*8,p.X,p.Y,0,0)).ToArray();
using var surface=SKSurface.Create(new SKImageInfo(400,80))??throw new Exception("surface");using var paint=new SKPaint{Color=SKColors.Black};
foreach(int kind in new[]{0,1,2})Bench("build-and-draw-"+kind,()=>{using var builder=new SKTextBlobBuilder();if(kind==0)builder.AddRun(ids,font);else if(kind==1)builder.AddPositionedRun(ids,font,positions);else builder.AddRotationScaleRun(ids,font,transforms);using var blob=builder.Build()??throw new Exception("blob");surface.Canvas.Clear(SKColors.White);surface.Canvas.DrawText(blob,5,45,paint);checksum+=blob.Bounds.Right;});
using(var builder=new SKTextBlobBuilder()){builder.AddPositionedRun(ids,font,positions);using var blob=builder.Build()??throw new Exception("blob");Bench("reuse-positioned-blob",()=>{surface.Canvas.Clear(SKColors.White);surface.Canvas.DrawText(blob,5,45,paint);checksum+=blob.Bounds.Right;});}
Bench("reuse-ordinary-text",()=>{surface.Canvas.Clear(SKColors.White);surface.Canvas.DrawText("Hello 中文文字 1234",5,45,font,paint);checksum+=font.Size;});
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(checksum);
