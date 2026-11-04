using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;

// Each assembly references exactly one implementation. Input IO and source
// bitmap construction are outside timing; owned outputs and Dispose are inside.
#if MANAGED
const string engine="SimdSkia + Sdcb.SimdCodecs 1.4.0";
#else
const string engine="SkiaSharp 3.119.4";
#endif
if(args.Length==3&&args[0]=="decode-file")
{
    using var image=Decode(File.ReadAllBytes(args[1]));
    File.WriteAllBytes(args[2],image.GetPixelSpan().ToArray());return;
}
if(args.Length!=6)throw new ArgumentException("<run|capture> <fixtures> <scene> <operation> <output> <seconds>");
string command=args[0],root=Path.GetFullPath(args[1]),scene=args[2],operation=args[3],output=Path.GetFullPath(args[4]);
double seconds=double.Parse(args[5],System.Globalization.CultureInfo.InvariantCulture);
if(command is not ("run" or "capture")||seconds<.1||seconds>60)throw new ArgumentException("Invalid command/duration");
bool decoding=operation.StartsWith("decode-"),png=operation.EndsWith("png");
if(operation is not ("decode-png" or "decode-jpeg444" or "decode-jpeg420" or "encode-png" or "encode-jpeg444"))throw new ArgumentException(operation);
var metadata=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"fixtures.json")));
var fixture=metadata.RootElement.EnumerateArray().Single(f=>f.GetProperty("Name").GetString()==scene);
int width=fixture.GetProperty("Width").GetInt32(),height=fixture.GetProperty("Height").GetInt32();
string extension=png?".png":operation.EndsWith("420")?"-420.jpg":"-444.jpg";
byte[] input=File.ReadAllBytes(Path.Combine(root,scene+(decoding?extension:".rgba")));
using var source=decoding?null:new SKBitmap(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul));
if(source is not null)input.CopyTo(source.GetPixelSpan());
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
if(command=="capture")
{
    if(decoding){using var decoded=Decode(input,width,height);File.WriteAllBytes(output,decoded.GetPixelSpan().ToArray());}
    else File.WriteAllBytes(output,Encode(source!,png));
    return;
}
int sink=0,length=0;
void Execute()
{
    if(decoding){using var decoded=Decode(input,width,height);var span=decoded.GetPixelSpan();sink^=span[span.Length/2];length=span.Length;}
    else{byte[] data=Encode(source!,png);sink^=data[data.Length/2];length=data.Length;}
}
Execute();var warm=Stopwatch.StartNew();do{Execute();}while(warm.Elapsed.TotalSeconds<1);
GC.Collect(2,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();
using var process=Process.GetCurrentProcess();process.Refresh();
long allocation=GC.GetTotalAllocatedBytes(true);double cpu=process.TotalProcessorTime.TotalMilliseconds;
int frames=0;var watch=Stopwatch.StartNew();do{Execute();frames++;}while(watch.Elapsed.TotalSeconds<seconds);watch.Stop();
cpu=process.TotalProcessorTime.TotalMilliseconds-cpu;allocation=GC.GetTotalAllocatedBytes(true)-allocation;process.Refresh();
var modules=process.Modules.Cast<ProcessModule>().Select(m=>m.ModuleName).ToArray();
#if MANAGED
if(modules.Any(m=>m.Equals("libSkiaSharp.dll",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Original native code leaked into managed worker");
#else
if(!modules.Any(m=>m.Equals("libSkiaSharp.dll",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Original native renderer was not loaded");
#endif
var row=new{engine,scene,operation,width,height,frames,wallMs=watch.Elapsed.TotalMilliseconds,
    millisecondsPerImage=watch.Elapsed.TotalMilliseconds/frames,cpuMillisecondsPerImage=cpu/frames,
    cpuPercentMachine=100*cpu/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount,
    allocatedBytesPerImage=(double)allocation/frames,workingSetMiB=process.WorkingSet64/1048576d,
    privateMiB=process.PrivateMemorySize64/1048576d,peakWorkingSetMiB=process.PeakWorkingSet64/1048576d,
    outputBytes=length,sink,runtime=RuntimeInformation.FrameworkDescription,nativeAot=!RuntimeFeature.IsDynamicCodeSupported,
    processors=Environment.ProcessorCount,inputSha256=Convert.ToHexString(SHA256.HashData(input)),loadedModules=modules};
File.WriteAllText(output,JsonSerializer.Serialize(row,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"{engine} {scene} {operation}: {row.millisecondsPerImage:F3}ms, CPU {row.cpuMillisecondsPerImage:F3}ms/image, WS {row.workingSetMiB:F1}MiB");

static SKBitmap Decode(byte[] input,int width=0,int height=0)
{
#if MANAGED
    return SKBitmap.Decode(input);
#else
    if(width==0){var bounds=SKBitmap.DecodeBounds(input);width=bounds.Width;height=bounds.Height;}
    return SKBitmap.Decode(input,new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul))
        ??throw new InvalidDataException("Original decode failed");
#endif
}
static byte[] Encode(SKBitmap bitmap,bool png)
{
#if MANAGED
    using var data=bitmap.Encode(png?SKEncodedImageFormat.Png:SKEncodedImageFormat.Jpeg,png?100:90);
#else
    using var pixels=bitmap.PeekPixels();
    using var data=png?pixels.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters,6)):
        pixels.Encode(new SKJpegEncoderOptions(90,SKJpegEncoderDownsample.Downsample444,SKJpegEncoderAlphaOption.Ignore));
#endif
    return (data??throw new InvalidDataException("Encoder failed")).ToArray();
}
