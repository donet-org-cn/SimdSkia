using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using Qcxt.OCR;
using Qcxt.OCR.Sample;
using Qcxt.OCR.Models.ChineseV6Tiny;
using Qcxt.OCR.SkiaSharp;

string Get(string key,string fallback){int i=Array.IndexOf(args,key);return i<0?fallback:args[i+1];}
string output=Get("--out","artifacts/ocr/worker"),fixtures=Get("--fixtures","artifacts/ocr/fixtures"),mode=Get("--mode","decode");
int size=int.Parse(Get("--size","500")),fork=int.Parse(Get("--fork","0")),samples=int.Parse(Get("--samples","5")),sampleMs=int.Parse(Get("--sample-ms","100"));
string ppmPath=Get("--ppm","F:/基础/qcxt.framework/tests/Qcxt.OCR.SkiaSharp.Tests/Fixtures/sample.ppm");
Directory.CreateDirectory(output);Directory.CreateDirectory(fixtures);
var ppm=PpmImage.Load(ppmPath);
#if ORIGINAL
const string engine="SkiaSharp 4.153.1";
#else
const string engine="SimdSkia (PNG adaptation, exact RunBitmap)";
#endif
if(args.Contains("--make-fixtures"))
{
#if ORIGINAL
    foreach(int n in new[]{500,1024,2048})
    {
        using var bitmap=new SKBitmap(n,n,SKColorType.Rgba8888,SKAlphaType.Opaque);
        var destination=bitmap.GetPixelSpan();
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {int s=((y*ppm.Height/n)*ppm.Width+x*ppm.Width/n)*3,d=y*bitmap.RowBytes+x*4;destination[d]=ppm.Pixels[s];destination[d+1]=ppm.Pixels[s+1];destination[d+2]=ppm.Pixels[s+2];destination[d+3]=255;}
        using var pixmap=bitmap.PeekPixels()??throw new Exception("No pixels.");
        using var data=pixmap.Encode(SKEncodedImageFormat.Png,100)??throw new InvalidDataException("PNG encode failed.");File.WriteAllBytes(Path.Combine(fixtures,$"sample-{n}.png"),data.ToArray());
        if(n==500){using var jpeg=pixmap.Encode(SKEncodedImageFormat.Jpeg,90)??throw new InvalidDataException("JPEG encode failed.");File.WriteAllBytes(Path.Combine(fixtures,"sample-500.jpg"),jpeg.ToArray());}
    }
    using(var alpha=new SKBitmap(2,1,SKColorType.Rgba8888,SKAlphaType.Unpremul))
    {
        new byte[]{200,100,40,128,255,255,255,0}.AsSpan().CopyTo(alpha.GetPixelSpan());
        using var pixels=alpha.PeekPixels()??throw new Exception("No alpha pixels.");
        using var data=pixels.Encode(SKEncodedImageFormat.Png,100)??throw new Exception("No alpha PNG.");
        File.WriteAllBytes(Path.Combine(fixtures,"transparent.png"),data.ToArray());
    }
    return;
#else
    throw new InvalidOperationException("Fixtures must be encoded by the isolated original.");
#endif
}
byte[] png=File.ReadAllBytes(Path.Combine(fixtures,$"sample-{size}.png"));
SKBitmap Decode(byte[] data)
{
#if ORIGINAL
    // Match the actual module's private RunStream(SKStream) decode overload.
    using var stream=new SKMemoryStream(data);
    return SKBitmap.Decode(stream)??throw new InvalidDataException("Skia decode failed.");
#else
    return SKBitmap.Decode(data);
#endif
}
if(mode=="formats")
{
    var formats=new List<object>();
    foreach(string name in new[]{"sample-500.png","sample-500.jpg"})
    {try{using var b=Decode(File.ReadAllBytes(Path.Combine(fixtures,name)));formats.Add(new{Name=name,Supported=true,Error=(string?)null});}catch(NotSupportedException e){formats.Add(new{Name=name,Supported=false,Error=e.Message});}catch(InvalidDataException e){formats.Add(new{Name=name,Supported=false,Error=e.Message});}}
    using var transparent=Decode(File.ReadAllBytes(Path.Combine(fixtures,"transparent.png")));
    var alphaBytes=transparent.GetPixelSpan().ToArray();
    File.WriteAllText(Path.Combine(output,"formats.json"),JsonSerializer.Serialize(new{Engine=engine,Formats=formats,TransparentColorType=transparent.ColorType.ToString(),TransparentAlphaType=transparent.AlphaType.ToString(),TransparentPhysicalBytes=alphaBytes},new JsonSerializerOptions{WriteIndented=true}));return;
}
if(mode is not ("decode" or "raw-ocr" or "bitmap-ocr" or "bytes-ocr"))throw new ArgumentException("Unknown mode.");
if(mode!="decode"&&size!=500)throw new ArgumentException("Full OCR uses the real unchanged 500x500 fixture.");
PaddleOcrAll? ocr=null;SKBitmap? retained=null;PaddleOcrResult? last=null;
long setupStart=Stopwatch.GetTimestamp();
if(mode!="decode")ocr=await PaddleOcrAll.LoadAsync(ChineseV6TinyModels.Default); // actual defaults: CPU, auto thread budgets.
if(mode=="bitmap-ocr")retained=Decode(png);
double setupMs=Stopwatch.GetElapsedTime(setupStart).TotalMilliseconds;
void Run()
{
    switch(mode)
    {
        case "decode":using(var b=Decode(png)){if(b.Width!=size||b.Height!=size||b.ByteCount==0)throw new Exception("Decode layout mismatch.");}break;
        case "raw-ocr":last=ocr!.Run(ppm.Pixels,ppm.Width,ppm.Height,format:ImagePixelFormat.Rgb24);break;
        case "bitmap-ocr":last=ocr!.RunBitmap(retained!);break;
        case "bytes-ocr":
#if ORIGINAL
            last=ocr!.RunBytes(png); // unmodified target module, including its stream lifecycle.
#else
            using(var b=Decode(png))last=ocr!.RunBitmap(b);
#endif
            break;
    }
}
for(int i=0;i<3;i++)Run();
int calibration=1;double elapsed;
do{long t=Stopwatch.GetTimestamp();for(int i=0;i<calibration;i++)Run();elapsed=Stopwatch.GetElapsedTime(t).TotalMilliseconds;if(elapsed>=30||calibration>=4096)break;calibration*=2;}while(true);
int iterations=Math.Clamp((int)Math.Ceiling(sampleMs*calibration/Math.Max(.001,elapsed)),1,4096);
GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
using var process=Process.GetCurrentProcess();
object Memory(){process.Refresh();return new{PrivateBytes=process.PrivateMemorySize64,WorkingSetBytes=process.WorkingSet64,PeakWorkingSetBytes=process.PeakWorkingSet64,ManagedLiveBytes=GC.GetTotalMemory(false)};}
var resident=Memory();var measurements=new List<object>();var latencies=new List<double>(checked(iterations*samples));
for(int batch=0;batch<samples;batch++)
{
    process.Refresh();var cpu=process.TotalProcessorTime;
    int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
    long threadAlloc=GC.GetAllocatedBytesForCurrentThread(),totalAlloc=GC.GetTotalAllocatedBytes(true),begin=Stopwatch.GetTimestamp();
    for(int i=0;i<iterations;i++){long frame=Stopwatch.GetTimestamp();Run();latencies.Add(Stopwatch.GetElapsedTime(frame).TotalMilliseconds);}
    double wall=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
    long threadBytes=GC.GetAllocatedBytesForCurrentThread()-threadAlloc,totalBytes=GC.GetTotalAllocatedBytes(true)-totalAlloc;
    int end0=GC.CollectionCount(0),end1=GC.CollectionCount(1),end2=GC.CollectionCount(2);
    process.Refresh();double cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;
    measurements.Add(new{Operations=iterations,TotalMilliseconds=wall,CpuMilliseconds=cpuMs,ThreadAllocatedBytes=threadBytes,TotalManagedAllocatedBytes=totalBytes,Gen0=end0-g0,Gen1=end1-g1,Gen2=end2-g2,Memory=Memory()});
}
GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();var after=Memory();
// Outside timing: validate canonical decoder bytes, not only geometry/text counts.
using(var decoded=Decode(png))
{
    var raw=decoded.GetPixelSpan();var canonical=new byte[checked(size*size*4)];int ri=decoded.ColorType==SKColorType.Rgba8888?0:decoded.ColorType==SKColorType.Bgra8888?2:throw new Exception("Unsupported decoded format.");
    for(int y=0;y<size;y++)for(int x=0;x<size;x++)
    {int s=y*decoded.RowBytes+x*4,d=(y*size+x)*4;canonical[d]=raw[s+ri];canonical[d+1]=raw[s+1];canonical[d+2]=raw[s+2-ri];canonical[d+3]=raw[s+3];}
    File.WriteAllBytes(Path.Combine(output,"decoded.rgba"),canonical);
}
Run();if(mode!="decode")
{
    if(last is null||last.Lines.Length<10||!last.Text.Contains("220ml",StringComparison.OrdinalIgnoreCase))throw new Exception("Actual OCR validation failed.");
    File.WriteAllText(Path.Combine(output,"recognition.json"),JsonSerializer.Serialize(last,new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
}
var info=new{Engine=engine,Mode=mode,Size=size,Fork=fork,Samples=measurements,Resident=resident,AfterGc=after,SetupMilliseconds=setupMs,InputSha256=Convert.ToHexString(SHA256.HashData(png)),OcrAssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PaddleOcrAll).Assembly.Location))),Framework=RuntimeInformation.FrameworkDescription,OS=RuntimeInformation.OSDescription,LogicalProcessors=Environment.ProcessorCount,SkiaAssembly=typeof(SKBitmap).Assembly.FullName,SkiaAssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(SKBitmap).Assembly.Location))),OcrBackend="CPU",OcrOptions="defaults (auto thread budgets)",InputWidth=size,InputHeight=size,Latencies=latencies,Lines=last?.Lines.Length};
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(info,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"{engine} {mode} {size} fork{fork}: median {latencies.Order().ElementAt(latencies.Count/2):F3} ms; {last?.Lines.Length} OCR lines");
retained?.Dispose();ocr?.Dispose();
