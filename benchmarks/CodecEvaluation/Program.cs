using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text.Json;
using SimdSkia;
using SimdSkia.Imaging;
using Sdcb.SimdCodecs.Jpeg;
using Sdcb.SimdCodecs.Png;

var jsonOptions=new JsonSerializerOptions{WriteIndented=true};
string command=args[0],root=Path.GetFullPath(args[1]);
Directory.CreateDirectory(root);
var jpegOptions=new JpegEncodeOptions{Quality=90,Subsampling=JpegSubsampling.Chroma444,OptimizeHuffman=false,Progressive=false};
var pngOptions=new PngWriteOptions{CompressionLevel=CompressionLevel.Optimal,ParallelFragments=1};
void Write(string file,object value)=>File.WriteAllText(file,JsonSerializer.Serialize(value,jsonOptions));
byte[] SdEncode(PixelBuffer pixels,bool png)=>png?PngWriter.Encode(pixels.GetPixelSpan(),pixels.Width,pixels.Height,pngOptions):JpegEncoder.Encode(pixels.GetPixelSpan(),pixels.Width,pixels.Height,4,jpegOptions);
byte[] ExistingEncode(PixelBuffer pixels,bool png)=>png?PngCodec.Encode(pixels):JpegCodec.Encode(pixels,90);
PixelBuffer Decode(string engine,byte[] encoded,int width,int height)
{
    if(engine=="existing")return ImageCodec.Decode(encoded);
    // The adapter includes real owned output storage and its clear/dispose cost.
    // A kernel-only DecodeTo benchmark with a permanently reused buffer is not comparable.
    var output=PixelBuffer.Rent(width,height,PixelFormat.Rgba8888,AlphaFormat.Straight);
    try
    {
        if(encoded[0]==255)
        {
            var size=JpegDecoder.DecodeTo(encoded,output.GetPixelSpan());
            if(size.Width!=width||size.Height!=height)throw new InvalidDataException("JPEG dimensions differ");
        }
        else if(!PngImageDecoder.TryDecodeIntoRgba8(encoded,output.GetPixelSpan()))throw new InvalidDataException("PNG decode rejected");
        return output;
    }
    catch{output.Dispose();throw;}
}
if(command=="prepare")
{
    var files=new List<Fixture>();
    const int width=1920,height=1080;
    foreach(string name in new[]{"ui","texture"})
    {
        using var pixels=new PixelBuffer(width,height,PixelFormat.Rgba8888,AlphaFormat.Straight);
        uint state=71863;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            state^=state<<13;state^=state>>17;state^=state<<5;
            int n=(int)(state&31),tile=(x/96+y/72)%7;
            byte r,g,b;
            if(name=="ui"){r=(byte)(22+tile*27);g=(byte)(35+tile*21);b=(byte)(61+tile*18);if(x%96<3||y%72<3)r=g=b=220;}
            else{r=(byte)((x*173/width+y*31/height+n)%256);g=(byte)((x*29/width+y*181/height+n)%256);b=(byte)((x*93/width+y*91/height+n)%256);}
            pixels.WritePixel(x,y,new Rgba32(r,g,b,255));
        }
        File.WriteAllBytes(Path.Combine(root,name+".rgba"),pixels.GetPixelSpan().ToArray());
        var png=SdEncode(pixels,true);File.WriteAllBytes(Path.Combine(root,name+".png"),png);
        var jpeg444=ExistingEncode(pixels,false);File.WriteAllBytes(Path.Combine(root,name+"-444.jpg"),jpeg444);
        var jpeg420=JpegEncoder.Encode(pixels.GetPixelSpan(),width,height,4,jpegOptions with{Subsampling=JpegSubsampling.Chroma420});File.WriteAllBytes(Path.Combine(root,name+"-420.jpg"),jpeg420);
        files.Add(new(name,width,height,png.Length,jpeg444.Length,jpeg420.Length));
    }
    Write(Path.Combine(root,"fixtures.json"),files);
    Console.WriteLine("Prepared deterministic opaque UI/texture fixtures; encoded bytes identical between compared engines.");
    return;
}
if(command=="validate")
{
    var rows=new List<object>();
    using(var fixtures=JsonDocument.Parse(File.ReadAllText(args[2])))
    foreach(var f in fixtures.RootElement.GetProperty("cases").EnumerateArray())
    {
        string name=f.GetProperty("name").GetString()!;
        byte[] encoded=Convert.FromBase64String(f.GetProperty("encoded").GetString()!);
        byte[] expected=Convert.FromBase64String(f.GetProperty("rgba").GetString()!);
        int width=f.GetProperty("width").GetInt32(),height=f.GetProperty("height").GetInt32();
        foreach(string engine in new[]{"existing","sdcb"})
        {
            try
            {
                using var decoded=Decode(engine,encoded,width,height);
                var error=Compare(expected,decoded.GetPixelSpan());
                rows.Add(new{name,engine,error.Max,error.Rmse,passed=error.Max<=f.GetProperty("max").GetInt32()&&error.Rmse<=f.GetProperty("rmse").GetDouble(),sha256=Convert.ToHexString(SHA256.HashData(decoded.GetPixelSpan()))});
            }
            catch(Exception ex){rows.Add(new{name,engine,passed=false,error=ex.GetType().Name,message=ex.Message});}
        }
    }
    Write(args[3],new{runtime=RuntimeInformation.FrameworkDescription,nativeAot=!RuntimeFeature.IsDynamicCodeSupported,avx2=Avx2.IsSupported,rows});
    Console.WriteLine($"Recorded {rows.Count} independent fixture results, including explicit rejections and colour-transform differences.");
    return;
}
if(command=="quality")
{
    var rows=new List<object>();
    foreach(var f in ReadFixtures())
    {
        byte[] raw=File.ReadAllBytes(Path.Combine(root,f.Name+".rgba"));
        using var pixels=new PixelBuffer(f.Width,f.Height,PixelFormat.Rgba8888,AlphaFormat.Straight);raw.CopyTo(pixels.GetPixelSpan());
        foreach(string format in new[]{"png","jpeg444","jpeg420"})
        {
            string file=f.Name+(format=="png"?".png":format=="jpeg444"?"-444.jpg":"-420.jpg");
            byte[] encoded=File.ReadAllBytes(Path.Combine(root,file));
            using var current=Decode("existing",encoded,f.Width,f.Height);using var candidate=Decode("sdcb",encoded,f.Width,f.Height);
            var error=Compare(current.GetPixelSpan(),candidate.GetPixelSpan());
            if(format=="png"&&(error.Max!=0||!raw.AsSpan().SequenceEqual(candidate.GetPixelSpan())))throw new Exception("Lossless PNG mismatch");
            if(!candidate.GetPixelSpan().ToArray().Where((v,i)=>i%4==3).All(v=>v==255))throw new Exception("JPEG alpha mismatch");
            rows.Add(new{kind="decode",name=f.Name,format,error.Max,error.Rmse,inputBytes=encoded.Length,inputSha256=Convert.ToHexString(SHA256.HashData(encoded))});
        }
        foreach(bool png in new[]{false,true})foreach(string engine in new[]{"existing","sdcb"})
        {
            byte[] encoded=engine=="existing"?ExistingEncode(pixels,png):SdEncode(pixels,png);
            using var decoded=Decode("existing",encoded,f.Width,f.Height);
            var error=Compare(raw,decoded.GetPixelSpan());
            if(png&&error.Max!=0)throw new Exception("PNG encoder changed pixels");
            rows.Add(new{kind="encode",name=f.Name,format=png?"png":"jpeg444",engine,error.Max,error.Rmse,psnrRgb=error.Rmse==0?(double?)null:20*Math.Log10(255/(error.Rmse*Math.Sqrt(4d/3))),outputBytes=encoded.Length});
        }
    }
    Write(args[2],rows);Console.WriteLine("PASS generated images: dimensions, alpha and cross-decoded PNG pixels; JPEG error/size reported separately.");return;
}
if(command!="run")throw new ArgumentException("Expected prepare, validate, quality or run");
string backend=args[2],scene=args[3],operation=args[4],result=args[5];
double seconds=double.Parse(args[6],System.Globalization.CultureInfo.InvariantCulture);
var fixture=ReadFixtures().Single(f=>f.Name==scene);
bool decode=operation.StartsWith("decode-"),isPng=operation.EndsWith("png");
string extension=isPng?".png":operation.EndsWith("420")?"-420.jpg":"-444.jpg";
byte[] input=decode?File.ReadAllBytes(Path.Combine(root,scene+extension)):File.ReadAllBytes(Path.Combine(root,scene+".rgba"));
using var source=decode?null:new PixelBuffer(fixture.Width,fixture.Height,PixelFormat.Rgba8888,AlphaFormat.Straight);
if(source is not null)input.CopyTo(source.GetPixelSpan());
int sink=0,lastLength=0;
void Execute()
{
    if(decode){using var output=Decode(backend,input,fixture.Width,fixture.Height);sink^=output.GetPixelSpan()[output.ByteCount/2];lastLength=output.ByteCount;}
    else{byte[] output=backend=="existing"?ExistingEncode(source!,isPng):SdEncode(source!,isPng);sink^=output[output.Length/2];lastLength=output.Length;}
}
Execute();
var warmup=Stopwatch.StartNew();do{Execute();}while(warmup.Elapsed.TotalSeconds<0.5);
GC.Collect(2,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();
using var process=Process.GetCurrentProcess();
TimeSpan startCpu=process.TotalProcessorTime;long allocated=GC.GetAllocatedBytesForCurrentThread();
int frames=0;var timer=Stopwatch.StartNew();do{Execute();frames++;}while(timer.Elapsed.TotalSeconds<seconds);timer.Stop();
double cpuMs=(process.TotalProcessorTime-startCpu).TotalMilliseconds;allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
process.Refresh();
Write(result,new{backend,scene,operation,frames,millisecondsPerImage=timer.Elapsed.TotalMilliseconds/frames,cpuMillisecondsPerImage=cpuMs/frames,allocatedBytesPerImage=allocated/(double)frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,peakWorkingSetMiB=process.PeakWorkingSet64/1048576d,lastLength,sink,runtime=RuntimeInformation.FrameworkDescription,nativeAot=!RuntimeFeature.IsDynamicCodeSupported,avx2=Avx2.IsSupported,cpu=Environment.ProcessorCount,os=RuntimeInformation.OSDescription,nativeModules=process.Modules.Cast<ProcessModule>().Select(m=>m.ModuleName).Where(n=>n is not null&&(n.Contains("compress",StringComparison.OrdinalIgnoreCase)||n.Contains("zlib",StringComparison.OrdinalIgnoreCase)||n.Contains("skia",StringComparison.OrdinalIgnoreCase))).ToArray()});
Console.WriteLine($"{backend} {scene} {operation}: {timer.Elapsed.TotalMilliseconds/frames:F3} ms, {allocated/(double)frames:F0} B/image");
Fixture[] ReadFixtures()=>JsonSerializer.Deserialize<Fixture[]>(File.ReadAllText(Path.Combine(root,"fixtures.json")))!;
static (int Max,double Rmse) Compare(ReadOnlySpan<byte> expected,ReadOnlySpan<byte> actual)
{
    if(expected.Length!=actual.Length)throw new Exception("Pixel length mismatch");
    int max=0;double squared=0;for(int i=0;i<expected.Length;i++){int d=Math.Abs(expected[i]-actual[i]);max=Math.Max(max,d);squared+=d*d;}return(max,Math.Sqrt(squared/expected.Length));
}
record Fixture(string Name,int Width,int Height,int PngBytes,int Jpeg444Bytes,int Jpeg420Bytes);
