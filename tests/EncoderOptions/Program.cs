using SkiaSharp;
using System.Text.Json;
using System.Buffers.Binary;
using System.IO.Compression;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var raw=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool condition){checks++;if(!condition)throw new Exception("Encoder assertion "+checks);}
values["jpeg-default"]=new object[]{SKJpegEncoderOptions.Default.Quality,(int)SKJpegEncoderOptions.Default.Downsample,(int)SKJpegEncoderOptions.Default.AlphaOption};values["png-default"]=new object[]{(int)SKPngEncoderOptions.Default.FilterFlags,SKPngEncoderOptions.Default.ZLibLevel};values["webp-default"]=new object[]{(int)SKWebpEncoderOptions.Default.Compression,SKWebpEncoderOptions.Default.Quality};
foreach(int q in new[]{0,1,50,100,110})foreach(var d in Enum.GetValues<SKJpegEncoderDownsample>())foreach(var a in Enum.GetValues<SKJpegEncoderAlphaOption>())
{var x=new SKJpegEncoderOptions(q,d,a);var y=new SKJpegEncoderOptions(q,d,a);values[$"jpeg-value-{q}-{d}-{a}"]=new object[]{x.Quality,(int)x.Downsample,(int)x.AlphaOption,x==y,x.Equals((object)y),x!=new SKJpegEncoderOptions(q+1,d,a),x.GetHashCode()==y.GetHashCode()};}
foreach(int q in new[]{-1,0,50,100})foreach(var c in Enum.GetValues<SKWebpEncoderCompression>())
{var x=new SKWebpEncoderOptions(c,q);var y=new SKWebpEncoderOptions(c,q);values[$"webp-value-{q}-{c}"]=new object[]{x.Quality,(int)x.Compression,x==y,x.Equals((object)y),x.GetHashCode()==y.GetHashCode()};}
foreach(var alpha in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul})
{
    using var bitmap=new SKBitmap(new SKImageInfo(37,23,SKColorType.Rgba8888,alpha),160);var bytes=bitmap.GetPixelSpan();
    for(int y=0;y<23;y++)for(int x=0;x<37;x++){int p=y*bitmap.RowBytes+x*4;byte a=(byte)(x*7);bytes[p]=(byte)(x*5);bytes[p+1]=(byte)(y*11);bytes[p+2]=(byte)((x+y)*3);bytes[p+3]=a;if(alpha==SKAlphaType.Premul)for(int c=0;c<3;c++)bytes[p+c]=(byte)((bytes[p+c]*a+127)/255);}
    using var map=bitmap.PeekPixels();
    foreach(var flags in new[]{SKPngEncoderFilterFlags.NoFilters,SKPngEncoderFilterFlags.None,SKPngEncoderFilterFlags.Sub,SKPngEncoderFilterFlags.Up,SKPngEncoderFilterFlags.Avg,SKPngEncoderFilterFlags.Paeth,SKPngEncoderFilterFlags.AllFilters,SKPngEncoderFilterFlags.Sub|SKPngEncoderFilterFlags.Up})foreach(int level in new[]{0,1,6,9})
    {
        var options=new SKPngEncoderOptions(flags,level);using var data=map.Encode(options);Check(data is not null);byte[] encoded=data!.ToArray();File.WriteAllBytes(Path.Combine(output,$"png-{alpha}-{(int)flags}-{level}.png"),encoded);
        var filters=PngFilters(encoded);Check(filters.All(f=>flags==0||((int)flags&(8<<f))!=0));using var decoded=SKBitmap.Decode(encoded);Check(decoded.Width==37&&decoded.Height==23);
        int offset=(int)raw.Position;using var normalized=new SKBitmap(new SKImageInfo(37,23,SKColorType.Rgba8888,SKAlphaType.Unpremul));using var image=SKImage.FromBitmap(decoded);Check(image.ReadPixels(normalized.Info,normalized.GetPixels(),normalized.RowBytes,0,0));raw.Write(normalized.GetPixelSpan());cases.Add(new{name="png",alpha=(int)alpha,flags=(int)flags,level,offset,length=normalized.ByteCount});
        using var stream=new MemoryStream();Check(map.Encode(stream,options)&&stream.ToArray().SequenceEqual(encoded));using var writer=new SKDynamicMemoryWStream();Check(map.Encode(writer,options));using var same=writer.DetachAsData();Check(same.ToArray().SequenceEqual(encoded));
    }
    foreach(var downsample in new[]{SKJpegEncoderDownsample.Downsample420,SKJpegEncoderDownsample.Downsample444})foreach(var policy in Enum.GetValues<SKJpegEncoderAlphaOption>())foreach(int quality in new[]{30,90,100})
    {
        var options=new SKJpegEncoderOptions(quality,downsample,policy);using var data=map.Encode(options);Check(data is not null);byte[] encoded=data!.ToArray();File.WriteAllBytes(Path.Combine(output,$"jpeg-{alpha}-{downsample}-{policy}-{quality}.jpg"),encoded);values[$"jpeg-sampling-{alpha}-{downsample}-{policy}-{quality}"]=JpegSampling(encoded);
        using var decoded=SKBitmap.Decode(encoded);using var normalized=new SKBitmap(new SKImageInfo(37,23,SKColorType.Rgba8888,SKAlphaType.Unpremul));using var image=SKImage.FromBitmap(decoded);Check(image.ReadPixels(normalized.Info,normalized.GetPixels(),normalized.RowBytes,0,0));int offset=(int)raw.Position;raw.Write(normalized.GetPixelSpan());cases.Add(new{name="jpeg",alpha=(int)alpha,downsample=(int)downsample,policy=(int)policy,quality,offset,length=normalized.ByteCount});
        using var stream=new MemoryStream();Check(map.Encode(stream,options)&&stream.ToArray().SequenceEqual(encoded));using var writer=new SKDynamicMemoryWStream();Check(map.Encode(writer,options));using var same=writer.DetachAsData();Check(same.ToArray().SequenceEqual(encoded));
    }
    using(var writer=new SKDynamicMemoryWStream()){Check(map.Encode(writer,SKEncodedImageFormat.Png,100));using var bytes2=writer.DetachAsData();Check(bytes2.Size>0);}
}
File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} encoded/decoded images, {values.Count} metadata entries; {checks} assertions");
static int[] PngFilters(byte[] png)
{
    int width=BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)),height=BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)),channels=png[25] switch{0 or 3=>1,2=>3,4=>2,6=>4,_=>throw new Exception()};int stride=(width*channels*png[24]+7)/8;using var compressed=new MemoryStream();
    for(int p=8;p<png.Length;){int n=BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p));if(png.AsSpan(p+4,4).SequenceEqual("IDAT"u8))compressed.Write(png.AsSpan(p+8,n));p+=n+12;}compressed.Position=0;using var decoder=new ZLibStream(compressed,CompressionMode.Decompress);var row=new byte[stride];var result=new int[height];for(int y=0;y<height;y++){result[y]=decoder.ReadByte();decoder.ReadExactly(row);}return result.Distinct().Order().ToArray();
}
static int[] JpegSampling(byte[] jpeg)
{
    for(int p=2;p<jpeg.Length;){if(jpeg[p++]!=255)continue;int marker=jpeg[p++];if(marker is 0xd8 or 0xd9)continue;int n=BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(p));if(marker is 0xc0 or 0xc1 or 0xc2){int count=jpeg[p+7];return Enumerable.Range(0,count).Select(c=>(int)jpeg[p+9+c*3]).ToArray();}p+=n;}
    throw new Exception("JPEG SOF is missing.");
}
