using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SimdSkia;
using SimdSkia.Imaging;
using SkiaSharp;

string output=args.Length>0?args[0]:"artifacts/simdcodecs-integration-2026-11-01/tests";Directory.CreateDirectory(output);
int checks=0;void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
void Throws<T>(Action action)where T:Exception{bool threw=false;try{action();}catch(T){threw=true;}Check(threw,typeof(T).Name);}
using(var source=new PixelBuffer(13,9,PixelFormat.Bgra8888,AlphaFormat.Straight,72))
{
    for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)source.WritePixel(x,y,new Rgba32((byte)(x%3*90),(byte)(y%3*90),61,(byte)((x+y)%2==0?255:0)));
    foreach(var format in new[]{ImageFileFormat.Png,ImageFileFormat.Gif,ImageFileFormat.Bmp,ImageFileFormat.Ico,ImageFileFormat.Tiff,ImageFileFormat.Qoi,ImageFileFormat.Netpbm})
    {
        byte[] bytes=ImageCodec.Encode(source,format);File.WriteAllBytes(Path.Combine(output,format+".bin"),bytes);
        Check(ImageCodec.DetectFormat(bytes)==format,format+" signature");using var decoded=ImageCodec.Decode(bytes);
        Check(decoded.Width==source.Width&&decoded.Height==source.Height,format+" dimensions");
        for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)
        {
            var actual=decoded.ReadPixel(x,y);var expected=source.ReadPixel(x,y);
            // GIF's transparent palette entry has no meaningful hidden RGB.
            Check(actual.A==expected.A&&(expected.A==0&&format==ImageFileFormat.Gif||actual.R==expected.R&&actual.G==expected.G&&actual.B==expected.B),format+" stride/channel/alpha pixels");
        }
        using var sequence=ImageCodec.OpenFrames(bytes);Check(sequence.FrameCount==1,format+" still frame count");using var first=sequence.DecodeFrame(0);
        Check(first.GetPixelSpan().SequenceEqual(decoded.GetPixelSpan()),format+" frame view matches decode");
        sequence.Dispose();Throws<ObjectDisposedException>(()=>sequence.DecodeFrame(0));
        Check(first.ReadPixel(0,0)==decoded.ReadPixel(0,0),format+" returned frame pixels outlive reader");
    }
    Throws<NotSupportedException>(()=>ImageCodec.Encode(source,ImageFileFormat.Webp));
}
foreach(var format in new[]{SKEncodedImageFormat.Png,SKEncodedImageFormat.Jpeg,SKEncodedImageFormat.Gif,SKEncodedImageFormat.Bmp,SKEncodedImageFormat.Ico})
{
    using var bitmap=new SKBitmap(11,7);using(var canvas=new SKCanvas(bitmap))canvas.Clear(SKColors.Red);
    using var encoded=bitmap.Encode(format,90);using var codec=SKCodec.Create(encoded)??throw new Exception("Codec unavailable: "+format);
    Check(codec.EncodedFormat==format,"SKCodec must report the actual encoded format");
    using var decoded=SKBitmap.Decode(encoded.AsSpan());Check(decoded.Width==11&&decoded.Height==7,"SK bitmap encoded roundtrip");
    using var image=SKImage.FromBitmap(bitmap);using var bytes=image.Encode(format,90);Check(bytes.Size>0,"SKImage encoding");
}
foreach(string name in new[]{"disposal.gif","interlaced.gif"})
{
    byte[] bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures",name));
    using var sequence=ImageCodec.OpenFrames(bytes);using var gif=new GifImage(bytes);Check(sequence.FrameCount==gif.Frames.Count,"GIF frame adapter count");
    foreach(int index in Enumerable.Range(0,sequence.FrameCount).Concat(new[]{0,sequence.FrameCount-1,0}))
    {
        using var frame=sequence.DecodeFrame(index);using var expected=gif.DecodeFrame(index);
        Check(frame.GetPixelSpan().SequenceEqual(expected.GetPixelSpan()),"GIF random access/compositing");
        Check(sequence.GetFrameInfo(index).Duration?.TotalMilliseconds==gif.Frames[index].Duration||sequence.FrameCount==1,"GIF duration");
    }
}
using(var oracles=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/oracle.json"))))
foreach(var f in oracles.RootElement.GetProperty("fixtures").EnumerateArray())
{
    string name=f.GetProperty("file").GetString()!;byte[] bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures",name));
    using var image=ImageCodec.Decode(bytes);var expectedFrames=f.GetProperty("frames");
    Check(image.GetPixelSpan().SequenceEqual(Convert.FromHexString(expectedFrames[0].GetProperty("rgba").GetString()!)),name+" independent still pixels");
    using var sequence=ImageCodec.OpenFrames(bytes);Check(sequence.FrameCount==expectedFrames.GetArrayLength(),name+" independent frame count");
    foreach(int index in Enumerable.Range(0,sequence.FrameCount).Concat(new[]{0,sequence.FrameCount-1,0}))
    {
        using var frame=sequence.DecodeFrame(index);Check(frame.GetPixelSpan().SequenceEqual(Convert.FromHexString(expectedFrames[index].GetProperty("rgba").GetString()!)),name+" independent frame pixels");
    }
    if(name=="animation.webp")
    {
        Check(sequence.LoopCount==2&&sequence.GetFrameInfo(1).Duration?.TotalMilliseconds==90,"WebP duration/loop metadata");
        using var data=SKData.CreateCopy(bytes);using var codec=SKCodec.Create(data)??throw new Exception("Animated WebP codec missing");
        Check(codec.FrameCount==3&&codec.RepetitionCount==1&&codec.FrameInfo[1].Duration==90,"Animated SKCodec real frame metadata");
        using var bitmap=new SKBitmap(codec.Info);
        foreach(int index in new[]{0,2,1,0})
        {
            Check(codec.GetPixels(bitmap.Info,bitmap.GetPixels(),new SKCodecOptions(index))==SKCodecResult.Success,"WebP SKCodec random frame");
            using var nativeOrder=sequence.DecodeFrame(index);for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)Check(bitmap.GetPixel(x,y)==ToSkia(nativeOrder.ReadPixel(x,y)),"WebP compatibility channels");
        }
    }
    if(name=="still.webp"){using var data=SKData.CreateCopy(bytes);using var codec=SKCodec.Create(data);Check(codec?.EncodedFormat==SKEncodedImageFormat.Webp,"Actual WebP SKCodec format");}
}
using(var blank=ImageCodec.Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures/blank.jb2"))))
{Check(blank.Width==13&&blank.Height==9,"JBIG2 standalone page dimensions");Check(blank.ReadPixel(7,3)==new Rgba32(255,255,255,255),"JBIG2 blank page pixels");}
// Independent existing P3 fixture must pass the old max/RMSE gate after adaptation.
using(var fixtures=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures.json"))))
foreach(var f in fixtures.RootElement.GetProperty("cases").EnumerateArray().Where(f=>f.GetProperty("name").GetString()=="png-display-p3-to-srgb-icc"))
{
    using var image=ImageCodec.Decode(Convert.FromBase64String(f.GetProperty("encoded").GetString()!));byte[] expected=Convert.FromBase64String(f.GetProperty("rgba").GetString()!);
    int max=0;double sum=0;var actual=image.GetPixelSpan();for(int i=0;i<actual.Length;i++){int d=Math.Abs(actual[i]-expected[i]);max=Math.Max(max,d);sum+=d*d;}
    Check(max<=f.GetProperty("max").GetInt32()&&Math.Sqrt(sum/actual.Length)<=f.GetProperty("rmse").GetDouble(),"ICC P3 conversion preserves original gate");
}
// No allocations from a malicious header before the decoded image budget check.
byte[] giantBmp=new byte[54];giantBmp[0]=(byte)'B';giantBmp[1]=(byte)'M';BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(2),54);BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(10),54);BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(14),40);BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(18),100000);BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(22),100000);giantBmp[26]=1;giantBmp[28]=24;
bool refused=false;try{using var invalid=ImageCodec.Decode(giantBmp);}catch(Exception ex)when(ex is InvalidDataException or NotSupportedException){refused=true;}Check(refused,"Giant BMP rejected before pixel allocation");
Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="JpegLibrary"),"Old JPEG dependency is absent");
if(OperatingSystem.IsWindows())using(var process=Process.GetCurrentProcess())Check(!process.Modules.Cast<ProcessModule>().Any(m=>m.ModuleName?.Contains("libSkiaSharp",StringComparison.OrdinalIgnoreCase)==true),"No Google Skia codec loaded");
bool aot=!RuntimeFeature.IsDynamicCodeSupported;
#if NATIVE_AOT
Check(aot,"Actual Native AOT execution");
#endif
File.WriteAllText(Path.Combine(output,"result.json"),$"{{\"checks\":{checks},\"nativeAot\":{aot.ToString().ToLowerInvariant()},\"codec\":\"Sdcb.SimdCodecs 1.2.0\"}}");
Console.WriteLine($"PASS {checks} full codec integration assertions; NativeAot={aot}");
static SKColor ToSkia(Rgba32 c)=>new(c.R,c.G,c.B,c.A);
