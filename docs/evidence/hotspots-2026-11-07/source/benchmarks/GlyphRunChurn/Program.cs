using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SimdSkia;
using SimdSkia.Cpu;

// Synthetic immutable-wrapper churn, not an Avalonia page or an original-Skia
// comparison. Preparation is excluded; every timed command still rasterizes ink.
if(args.Length!=1)throw new ArgumentException("Expected output.json");
var outline=new PathGeometry([new(PathVerb.Move,new(.25f,.125f)),new(PathVerb.Line,new(9.25f,1.25f)),new(PathVerb.Quadratic,new(12.5f,10.5f),new(1.125f,11.75f)),new(PathVerb.Close,default)]);
var templates=Enumerable.Range(0,24).Select(row=>Enumerable.Range(0,32).Select(i=>new PositionedGlyph(outline,new(i*15,row%4))).ToArray()).ToArray();
var runs=Enumerable.Range(0,2048).Select(i=>new GlyphRunGeometry(templates[i%templates.Length],Matrix3.Identity,GlyphPositioning.QuarterPixel)).ToArray();
using var pixels=new PixelBuffer(512,40,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
using var canvas=new RasterCanvas(pixels);int sequence=0;
void Frame()
{
    canvas.Execute(DrawingCommand.Clear(new(255,255,255,255)));
    canvas.Execute(new(CommandKind.DrawGlyphRun,default,Matrix3.Identity with{M02=sequence%7},new(new(19,51,107,255),CompositeMode.SourceOver,true))
    {GlyphRun=runs[sequence++%runs.Length],TextMaskPreblend=true});
}
var warm=Stopwatch.StartNew();do{Frame();}while(warm.Elapsed.TotalSeconds<2);
using var process=Process.GetCurrentProcess();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
int frames=0;long bytes=GC.GetTotalAllocatedBytes(true);double cpu=process.TotalProcessorTime.TotalMilliseconds;
var timer=Stopwatch.StartNew();do{for(int i=0;i<16;i++)Frame();frames+=16;}while(timer.Elapsed.TotalSeconds<3);timer.Stop();
cpu=process.TotalProcessorTime.TotalMilliseconds-cpu;bytes=GC.GetTotalAllocatedBytes(true)-bytes;process.Refresh();
sequence=0;Frame();string hash=Convert.ToHexString(SHA256.HashData(pixels.GetPixelSpan()));
var result=new{scope="JIT synthetic immutable glyph-run wrapper churn; CPU render only",frames,msPerFrame=timer.Elapsed.TotalMilliseconds/frames,cpuMsPerFrame=cpu/frames,allocatedBytesPerFrame=bytes/(double)frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,pixelsSha256=hash};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
File.WriteAllText(args[0],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(result));
