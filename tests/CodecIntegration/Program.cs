using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Sdcb.SimdCodecs.Bmp;
using Sdcb.SimdCodecs.Tiff;
using Sdcb.SimdCodecs.Webp;
using SimdSkia;
using SimdSkia.Imaging;
using SkiaSharp;
using CodecPixelFormat = Sdcb.SimdCodecs.Abstractions.PixelFormat;
using Facade = Sdcb.SimdCodecs.ImageCodecs;

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
    using var webp=ImageCodec.Decode(ImageCodec.Encode(source,ImageFileFormat.Webp));Check(webp.Width==source.Width&&webp.Height==source.Height,"WebP encoding produces a decodable image");
}
using (var source = new PixelBuffer(317, 215, PixelFormat.Bgra8888, AlphaFormat.Premultiplied, 1285))
{
    for (int y = 0; y < source.Height; y++)
    {
        source.GetRowSpan(y).Fill(0xad);
        for (int x = 0; x < source.Width; x++)
            source.WritePixel(x, y, new((byte)(x * 11), (byte)(y * 17), (byte)(x * 3 + y), (byte)(64 + (x + y) % 192)));
    }
    var tiff = ImageCodec.Encode(source, ImageFileFormat.Tiff);
    var layout = TiffReader.ReadLayout(tiff);
    Check(layout.RowsPerStrip > 0 && layout.RowsPerStrip < source.Height && source.Height % layout.RowsPerStrip != 0,
        "TIFF stores bounded strips and a shorter final strip");
    using var image = ImageCodec.Decode(tiff);
    using var reader = ImageCodec.OpenFrames(tiff);
    using var frame = reader.DecodeFrame(0);
    Check(image.GetPixelSpan().SequenceEqual(frame.GetPixelSpan()), "TIFF strip and frame decode agree");
    for (int y = 0; y < source.Height; y++)
    for (int x = 0; x < source.Width; x++)
        Check(image.ReadPixel(x, y) == source.ReadPixel(x, y), "TIFF strips preserve alpha, stride, and color");
}
foreach (var format in new[] { PixelFormat.Bgra8888, PixelFormat.Rgba8888 })
{
    using var source = new PixelBuffer(39, 7, format, AlphaFormat.Premultiplied, 163);
    for (int y = 0; y < source.Height; y++)
    {
        source.GetRowSpan(y).Fill(0x87);
        for (int x = 0; x < source.Width; x++)
            source.WritePixel(x, y, new((byte)(x * 7), (byte)(y * 29), 175, (byte)(1 + (x * 17 + y * 7) % 255)));
    }
    using var decoded = ImageCodec.Decode(WebpCodec.Encode(source, lossless: true, quality: 50));
    for (int y = 0; y < source.Height; y++)
    for (int x = 0; x < source.Width; x++)
        Check(decoded.ReadPixel(x, y) == source.ReadPixel(x, y), "WebP uses only visible input bytes and preserves straight color");
}
foreach(var format in new[]{SKEncodedImageFormat.Png,SKEncodedImageFormat.Jpeg,SKEncodedImageFormat.Gif,SKEncodedImageFormat.Bmp,SKEncodedImageFormat.Ico})
{
    using var bitmap=new SKBitmap(11,7);using(var canvas=new SKCanvas(bitmap))canvas.Clear(SKColors.Red);
    using var encoded=bitmap.Encode(format,90);using var codec=SKCodec.Create(encoded)??throw new Exception("Codec unavailable: "+format);
    Check(codec.EncodedFormat==format,"SKCodec must report the actual encoded format");
    using var decoded=SKBitmap.Decode(encoded.AsSpan());Check(decoded.Width==11&&decoded.Height==7,"SK bitmap encoded roundtrip");
    using var image=SKImage.FromBitmap(bitmap);using var bytes=image.Encode(format,90);Check(bytes.Size>0,"SKImage encoding");
}
GifAnimationValidation.Run(output, Check);
// Include a translucent pixel just beyond a vector boundary on a padded
// premultiplied row. The opaque shortcut must never change its RGB or alpha.
foreach (var format in new[] { PixelFormat.Rgba8888, PixelFormat.Bgra8888, PixelFormat.Alpha8 })
{
    int rowBytes = format == PixelFormat.Alpha8 ? 23 : 76;
    using var source = new PixelBuffer(17, 3, format, AlphaFormat.Premultiplied, rowBytes);
    for (int y = 0; y < source.Height; y++)
    {
        source.GetRowSpan(y).Fill(0x91);
        for (int x = 0; x < source.Width; x++)
        {
            byte alpha = y == 1 && x == 16 ? (byte)67 : y == 2 && x == 3 ? (byte)0 : (byte)255;
            source.WritePixel(x, y, new Rgba32((byte)(x * 13), (byte)(y * 85), 138, alpha));
        }
    }

    using var decoded = PngCodec.Decode(PngCodec.Encode(source));
    for (int y = 0; y < source.Height; y++)
    for (int x = 0; x < source.Width; x++)
        Check(decoded.ReadPixel(x, y) == source.ReadPixel(x, y), "PNG input uses visible pixels and exact straight alpha");
}
// Temporary codecs may transfer their RGBA storage. Persistent codec decodes
// must remain independent, and returned bitmaps must outlive all input owners.
foreach(var format in new[]{SKEncodedImageFormat.Png,SKEncodedImageFormat.Jpeg,SKEncodedImageFormat.Bmp})
foreach(int width in new[]{1,7,8,9,31,32,33})
{
    const int decodeHeight=4;
    using var original=new SKBitmap(new SKImageInfo(width,decodeHeight,SKColorType.Rgba8888,SKAlphaType.Unpremul));
    for(int y=0;y<decodeHeight;y++)for(int x=0;x<width;x++)original.SetPixel(x,y,new((byte)(x*23),(byte)(y*41),173,(byte)(x*17+y*29)));
    byte[] bytes;using(var encoded=original.Encode(format,90))bytes=encoded.ToArray();
    using var data=SKData.CreateCopy(bytes);
    using var codec=SKCodec.Create(data)??throw new Exception("Missing codec");
    var info=new SKImageInfo(width,decodeHeight,SKColorType.Rgba8888,SKAlphaType.Unpremul);
    using var expected=SKBitmap.Decode(codec,info);
    using var spanResult=SKBitmap.Decode(bytes.AsSpan(),info);
    using var dataResult=SKBitmap.Decode(data,info);
    Check(expected.GetPixelSpan().SequenceEqual(spanResult.GetPixelSpan()),"Transferred decode preserves RGBA");
    Check(expected.GetPixelSpan().SequenceEqual(dataResult.GetPixelSpan()),"SKData decode matches persistent codec");
    data.Dispose();
    spanResult.SetPixel(0,0,SKColors.Magenta);
    using var repeated=SKBitmap.Decode(codec,info);
    Check(expected.GetPixelSpan().SequenceEqual(repeated.GetPixelSpan()),"Decoded bitmap mutation cannot alter codec storage");
    Check(expected.GetPixelSpan().SequenceEqual(dataResult.GetPixelSpan()),"Transferred outputs own independent leases");
    codec.Dispose();expected.Dispose();spanResult.Dispose();
    using var later=SKBitmap.Decode(bytes,info);
    Check(dataResult.GetPixelSpan().SequenceEqual(later.GetPixelSpan()),"Pool reuse cannot overwrite a live transferred image");
    foreach(bool simd in new[]{false,true})
    {
        SimdSkia.Cpu.CpuOptions.EnableSimd=simd;
        using var decoded=SKBitmap.Decode(bytes);
        for(int y=0;y<decodeHeight;y++)for(int x=0;x<width;x++)
        {
            int offset=(y*width+x)*4;var rgba=later.GetPixelSpan();var stored=decoded.GetPixelSpan();byte a=rgba[offset+3];
            Check(stored[offset]==PixelBuffer.Multiply(rgba[offset+2],a)&&stored[offset+1]==PixelBuffer.Multiply(rgba[offset+1],a)&&stored[offset+2]==PixelBuffer.Multiply(rgba[offset],a)&&stored[offset+3]==a,"Default BGRA decode preserves exact premultiplication");
        }
    }
}
SimdSkia.Cpu.CpuOptions.EnableSimd=true;
using(var exhaustive=new SKBitmap(new SKImageInfo(256,256,SKColorType.Rgba8888,SKAlphaType.Unpremul)))
{
    for(int a=0;a<256;a++)for(int c=0;c<256;c++)exhaustive.SetPixel(c,a,new((byte)c,(byte)(255-c),(byte)(c*11+a),(byte)a));
    using var encoded=exhaustive.Encode(SKEncodedImageFormat.Png,100);
    foreach(bool simd in new[]{false,true})
    {
        SimdSkia.Cpu.CpuOptions.EnableSimd=simd;using var decoded=SKBitmap.Decode(encoded);
        for(int a=0;a<256;a++)for(int c=0;c<256;c++)
        {
            int at=(a*256+c)*4;var p=decoded.GetPixelSpan();
            Check(p[at]==PixelBuffer.Multiply((byte)(c*11+a),(byte)a)&&p[at+1]==PixelBuffer.Multiply((byte)(255-c),(byte)a)&&p[at+2]==PixelBuffer.Multiply((byte)c,(byte)a)&&p[at+3]==a,"Exhaustive premultiplication and channel order");
        }
    }
}
SimdSkia.Cpu.CpuOptions.EnableSimd=true;
using (var source = new PixelBuffer(3, 2, PixelFormat.Rgba8888, AlphaFormat.Straight))
{
    byte[] linearQoi = ImageCodec.Encode(source, ImageFileFormat.Qoi);
    linearQoi[13] = 1; // QOI's linear RGB marker
    Throws<NotSupportedException>(() => ImageCodec.Decode(linearQoi).Dispose());
    using var reader = ImageCodec.OpenFrames(linearQoi);
    Throws<NotSupportedException>(() => reader.DecodeFrame(0).Dispose());
}
byte[] ppm16 = [.. "P6\n3 1\n1023\n"u8, 0, 0, 0, 2, 0, 4, 1, 255, 2, 0, 3, 255, 0, 64, 0, 127, 3, 191];
using (var still = ImageCodec.Decode(ppm16))
using (var reader = ImageCodec.OpenFrames(ppm16))
using (var frame = reader.DecodeFrame(0))
{
    ReadOnlySpan<byte> expected = [0, 0, 1, 255, 127, 128, 255, 255, 16, 31, 239, 255];
    Check(still.GetPixelSpan().SequenceEqual(expected) && frame.GetPixelSpan().SequenceEqual(expected),
        "Direct Netpbm decode preserves the 16-bit sample scaling");
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
    byte[] png=Convert.FromBase64String(f.GetProperty("encoded").GetString()!);
    using var image=ImageCodec.Decode(png);byte[] expected=Convert.FromBase64String(f.GetProperty("rgba").GetString()!);
    int max=0;double sum=0;var actual=image.GetPixelSpan();for(int i=0;i<actual.Length;i++){int d=Math.Abs(actual[i]-expected[i]);max=Math.Max(max,d);sum+=d*d;}
    Check(max<=f.GetProperty("max").GetInt32()&&Math.Sqrt(sum/actual.Length)<=f.GetProperty("rmse").GetDouble(),"ICC P3 conversion preserves original gate");

    var raw = new byte[image.ByteCount];
    if (!Facade.TryReadMetadata(png, out var metadata) || metadata.Icc.IsEmpty ||
        !Facade.TryDecodeInto(png, raw, image.RowBytes, CodecPixelFormat.Rgba32))
        throw new Exception("The P3 fixture must expose its original samples and ICC profile.");

    var icc = metadata.Icc.ToArray();
    using var tiff = new MemoryStream();
    var writer = TiffWriter.Create(tiff);
    try
    {
        writer.AddPageAsync(raw, image.Width, image.Height, new TiffPageOptions
        {
            SamplesPerPixel = 4,
            ExtraSamples = TiffExtraSamples.UnassociatedAlpha,
            IccProfile = icc,
            RowsPerStrip = 1,
        }).GetAwaiter().GetResult();
        writer.FlushAsync().GetAwaiter().GetResult();
    }
    finally { writer.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    byte[][] profiled = [
        BmpEncoder.Encode(raw, image.Width, image.Height, CodecPixelFormat.Rgba32,
            new BmpEncodeOptions { Format = BmpEncodeFormat.Argb32, IccProfile = icc }),
        WebpEncoder.EncodeLossless(raw, image.Width, image.Height, CodecPixelFormat.Rgba32,
            new WebpLosslessEncodeOptions { Exact = true, IccProfile = icc }),
        tiff.ToArray(),
    ];
    foreach (var encoded in profiled)
    {
        using var converted = ImageCodec.Decode(encoded);
        using var reader = ImageCodec.OpenFrames(encoded);
        using var frame = reader.DecodeFrame(0);
        Check(converted.GetPixelSpan().SequenceEqual(actual) && frame.GetPixelSpan().SequenceEqual(actual),
            "BMP/WebP/TIFF still and frame APIs apply embedded ICC profiles");
    }
}
// No allocations from a malicious header before the decoded image budget check.
byte[] giantBmp=new byte[54];giantBmp[0]=(byte)'B';giantBmp[1]=(byte)'M';BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(2),54);BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(10),54);BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(14),40);BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(18),100000);BinaryPrimitives.WriteInt32LittleEndian(giantBmp.AsSpan(22),100000);giantBmp[26]=1;giantBmp[28]=24;
bool refused=false;try{using var invalid=ImageCodec.Decode(giantBmp);}catch(Exception ex)when(ex is InvalidDataException or NotSupportedException){refused=true;}Check(refused,"Giant BMP rejected before pixel allocation");
Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="JpegLibrary"),"Old JPEG dependency is absent");
if(OperatingSystem.IsWindows())using(var process=Process.GetCurrentProcess())Check(!process.Modules.Cast<ProcessModule>().Any(m=>m.ModuleName?.Contains("libSkiaSharp",StringComparison.OrdinalIgnoreCase)==true),"No Google Skia codec loaded");
bool aot=!RuntimeFeature.IsDynamicCodeSupported;
Check(PngStreamValidation.Run(),"Fragmented PNG IDAT, sliced input, empty chunks and concurrent GC");
#if NATIVE_AOT
Check(aot,"Actual Native AOT execution");
#endif
File.WriteAllText(Path.Combine(output,"result.json"),$"{{\"checks\":{checks},\"nativeAot\":{aot.ToString().ToLowerInvariant()},\"codec\":\"Sdcb.SimdCodecs 2.0.0\"}}");
Console.WriteLine($"PASS {checks} full codec integration assertions; NativeAot={aot}");
static SKColor ToSkia(Rgba32 c)=>new(c.R,c.G,c.B,c.A);
