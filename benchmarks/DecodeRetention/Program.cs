using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;

if(args.Length is not (5 or 8))throw new ArgumentException("<input> <width> <height> <count> <output> [<warm-input> <warm-width> <warm-height>]");
byte[] encoded=File.ReadAllBytes(args[0]);
int width=int.Parse(args[1]),height=int.Parse(args[2]),count=int.Parse(args[3]);
if(count is <1 or >64)throw new ArgumentOutOfRangeException(nameof(count));
var info=new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul);
SKBitmap Decode()=>SKBitmap.Decode(encoded,info)??throw new InvalidDataException("Decode failed.");
byte[]? warmInput=args.Length==8?File.ReadAllBytes(args[5]):null;
var warmInfo=args.Length==8?new SKImageInfo(int.Parse(args[6]),int.Parse(args[7]),SKColorType.Rgba8888,SKAlphaType.Unpremul):default;
void WarmLargerImage()
{
    if(warmInput is null)return;
    using var image=SKBitmap.Decode(warmInput,warmInfo)??throw new InvalidDataException("Warm decode failed.");
    if(image.Info!=warmInfo)throw new InvalidDataException("Warm image layout changed.");
}
void Collect(){GC.Collect(2,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();GC.Collect(2,GCCollectionMode.Forced,true,true);}
object Snapshot()
{
    using var process=Process.GetCurrentProcess();process.Refresh();
    return new{managedLiveBytes=GC.GetTotalMemory(false),workingSetBytes=process.WorkingSet64,privateBytes=process.PrivateMemorySize64};
}
for(int i=0;i<3;i++){WarmLargerImage();using(Decode()){} }
Collect();var before=Snapshot();
var images=new List<SKBitmap>(count);
long allocated=GC.GetTotalAllocatedBytes(true);
var timer=Stopwatch.StartNew();
for(int i=0;i<count;i++){WarmLargerImage();images.Add(Decode());}
timer.Stop();allocated=GC.GetTotalAllocatedBytes(true)-allocated;
Collect();var held=Snapshot();
string pixels=Convert.ToHexString(SHA256.HashData(images[0].GetPixelSpan()));
foreach(var image in images)
{
    if(image.Info!=info||image.GetPixel(0,0)!=images[0].GetPixel(0,0))throw new InvalidDataException("Decoded image changed.");
}
GC.KeepAlive(images);
foreach(var image in images)image.Dispose();
images.Clear();Collect();var released=Snapshot();
var result=new{width,height,count,mixedSize=warmInput is not null,pixelBytes=(long)width*height*4*count,allocationBytes=allocated,decodeMilliseconds=timer.Elapsed.TotalMilliseconds,
    before,held,released,pixelSha256=pixels,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription};
File.WriteAllText(args[4],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
