using SkiaSharp;
using System.Text.Json;
using System.Buffers.Binary;
var output=args[0];Directory.CreateDirectory(output);var fixtures=args.Length>1?args[1]:Path.Combine(output,"fixtures");
#if ORIGINAL
Directory.CreateDirectory(fixtures);
using(var bitmap=new SKBitmap(31,17)){for(int y=0;y<17;y++)for(int x=0;x<31;x++)bitmap.SetPixel(x,y,new SKColor((byte)(x*8),(byte)(y*15),(byte)(x*3+y*2)));foreach(var format in new[]{SKEncodedImageFormat.Png,SKEncodedImageFormat.Jpeg}){using var data=bitmap.Encode(format,90);File.WriteAllBytes(Path.Combine(fixtures,format.ToString().ToLowerInvariant()+".bin"),data.ToArray());}}
File.WriteAllBytes(Path.Combine(fixtures,"bmp.bin"),Bmp(false));File.WriteAllBytes(Path.Combine(fixtures,"bmp-top.bin"),Bmp(true));File.Copy("tests/CompatibilityExpansion/Fixtures/disposal.gif",Path.Combine(fixtures,"gif.bin"));File.Copy("tests/CodecIntegration/Fixtures/still.webp",Path.Combine(fixtures,"webp.bin"));
for(int origin=1;origin<=8;origin++)
{
    var tiff=Exif(origin);var jpeg=File.ReadAllBytes(Path.Combine(fixtures,"jpeg.bin"));var tagged=new byte[jpeg.Length+tiff.Length+10];jpeg.AsSpan(0,2).CopyTo(tagged);tagged[2]=255;tagged[3]=225;BinaryPrimitives.WriteUInt16BigEndian(tagged.AsSpan(4,2),(ushort)(tiff.Length+8));"Exif\0\0"u8.CopyTo(tagged.AsSpan(6));tiff.CopyTo(tagged,12);jpeg.AsSpan(2).CopyTo(tagged.AsSpan(12+tiff.Length));File.WriteAllBytes(Path.Combine(fixtures,$"jpeg-origin-{origin}.bin"),tagged);
    var png=File.ReadAllBytes(Path.Combine(fixtures,"png.bin"));using var outputPng=new MemoryStream();outputPng.Write(png.AsSpan(0,33));WriteChunk(outputPng,"eXIf"u8,tiff);outputPng.Write(png.AsSpan(33));File.WriteAllBytes(Path.Combine(fixtures,$"png-origin-{origin}.bin"),outputPng.ToArray());
}
#endif
var values=new Dictionary<string,object?>();var details=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));values["minimum-buffer"]=SKCodec.MinBufferedBytesNeeded;
foreach(var path in Directory.GetFiles(fixtures).Order())
{
    string name=Path.GetFileNameWithoutExtension(path);using var data=SKData.CreateCopy(File.ReadAllBytes(path));using var codec=SKCodec.Create(data)!;values[name+"-base"]=new object[]{(int)codec.EncodedOrigin,(int)codec.ScanlineOrder,codec.NextScanline,codec.GetOutputScanline(0),codec.GetOutputScanline(codec.Info.Height-1)};
    foreach(float scale in new[]{.01f,.1f,.13f,.24f,.25f,.3f,.5f,.75f,.99f,1f,2f}){var size=codec.GetScaledDimensions(scale);values[$"{name}-scale-{scale}"]=new[]{size.Width,size.Height};}
    foreach(var rect in new[]{new SKRectI(1,1,codec.Info.Width-1,codec.Info.Height-1),codec.Info.Rect,new SKRectI(-1,-1,3,3)}){var valid=rect;bool success=codec.GetValidSubset(ref valid);values[$"{name}-subset-{rect}"]=new object[]{success,new[]{valid.Left,valid.Top,valid.Right,valid.Bottom}};}
    var info=codec.Info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Premul);using var bitmap=new SKBitmap(info,info.RowBytes+12);bitmap.GetPixelSpan().Fill(0x66);var start=codec.StartScanlineDecode(info);values[name+"-scan-start"]=(int)start;
    if(start==SKCodecResult.Success)
    {
        var reads=new List<object>();int at=0;
        foreach(int count in new[]{1,3,2}){int n=codec.GetScanlines(bitmap.GetPixels()+at*bitmap.RowBytes,count,bitmap.RowBytes);reads.Add(new[]{count,n,codec.NextScanline});at+=n;}
        bool skipped=codec.SkipScanlines(2);reads.Add(new object[]{skipped,codec.NextScanline});int last=codec.GetScanlines(bitmap.GetPixels()+at*bitmap.RowBytes,Math.Max(0,info.Height-at-2),bitmap.RowBytes);reads.Add(new[]{last,codec.NextScanline});values[name+"-scan-reads"]=reads;
        int offset=(int)pixels.Position;pixels.Write(bitmap.GetPixelSpan());cases.Add(new{name,operation="scan",offset,length=bitmap.ByteCount});
    }
    using var second=SKCodec.Create(data)!;bitmap.GetPixelSpan().Fill(0x66);start=second.StartIncrementalDecode(info,bitmap.GetPixels(),bitmap.RowBytes,SKCodecOptions.Default);values[name+"-incremental-start"]=(int)start;
    if(start==SKCodecResult.Success){var result=second.IncrementalDecode(out int rows);values[name+"-incremental-step"]=new[]{(int)result,(int)second.IncrementalDecode()};details[name+"-rows-on-success-not-defined-by-native"]=rows;int offset=(int)pixels.Position;pixels.Write(bitmap.GetPixelSpan());cases.Add(new{name,operation="incremental",offset,length=bitmap.ByteCount});}
    using var stream=new SKMemoryStream(File.ReadAllBytes(path));using var third=SKCodec.Create(stream,out var createResult);values[name+"-stream"]=new object[]{(int)createResult,third?.Info.Width??0};
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"details.json"),JsonSerializer.Serialize(details,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));Console.WriteLine($"Captured {values.Count} codec metadata entries and {cases.Count} scan/decode outputs");
#if ORIGINAL
static byte[] Bmp(bool top)
{
    const int w=31,h=17,stride=(w*3+3)&~3;var bytes=new byte[54+stride*h];bytes[0]=66;bytes[1]=77;void I32(int p,int n)=>BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(p,4),n);I32(2,bytes.Length);I32(10,54);I32(14,40);I32(18,w);I32(22,top?-h:h);bytes[26]=1;bytes[28]=24;I32(34,stride*h);
    for(int y=0;y<h;y++)for(int x=0;x<w;x++){int at=54+(top?y:h-y-1)*stride+x*3;bytes[at]=(byte)(x*3+y*2);bytes[at+1]=(byte)(y*15);bytes[at+2]=(byte)(x*8);}return bytes;
}
static byte[] Exif(int origin)
{
    var bytes=new byte[26];bytes[0]=bytes[1]=73;bytes[2]=42;bytes[4]=8;bytes[8]=1;bytes[10]=0x12;bytes[11]=1;bytes[12]=3;bytes[14]=1;bytes[18]=(byte)origin;return bytes;
}
static void WriteChunk(Stream stream,ReadOnlySpan<byte> tag,ReadOnlySpan<byte> data)
{
    byte[] chunk=new byte[data.Length+12];BinaryPrimitives.WriteInt32BigEndian(chunk,data.Length);tag.CopyTo(chunk.AsSpan(4));data.CopyTo(chunk.AsSpan(8));uint c=0xffffffff;foreach(byte b in chunk.AsSpan(4,data.Length+4)){c^=b;for(int i=0;i<8;i++)c=(c&1)!=0?(c>>1)^0xedb88320:c>>1;}BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(data.Length+8),~c);stream.Write(chunk);
}
#endif
