namespace SimdSkia.Imaging;

public static class ImageCodec
{
    public static ImageFrames OpenFrames(ReadOnlySpan<byte> data)=>new(data);
    public static ImageFileFormat DetectFormat(ReadOnlySpan<byte> data)=>Sdcb.SimdCodecs.SimdCodecs.DetectFormat(data) switch
    {
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Png=>ImageFileFormat.Png,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Jpeg=>ImageFileFormat.Jpeg,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Gif=>ImageFileFormat.Gif,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Bmp=>ImageFileFormat.Bmp,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Ico or Sdcb.SimdCodecs.Abstractions.ImageFormat.Cur=>ImageFileFormat.Ico,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Webp=>ImageFileFormat.Webp,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Tiff=>ImageFileFormat.Tiff,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Qoi=>ImageFileFormat.Qoi,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Pnm=>ImageFileFormat.Netpbm,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Jpeg2000=>ImageFileFormat.Jpeg2000,
        Sdcb.SimdCodecs.Abstractions.ImageFormat.Jbig2=>ImageFileFormat.Jbig2,
        _=>ImageFileFormat.Unknown
    };
    public static byte[] Encode(PixelBuffer pixels,ImageFileFormat format,int quality=90)
    {
        ArgumentNullException.ThrowIfNull(pixels);if(quality is <0 or >100)throw new ArgumentOutOfRangeException(nameof(quality));
        return format switch{ImageFileFormat.Png=>PngCodec.Encode(pixels),ImageFileFormat.Jpeg=>JpegCodec.Encode(pixels,quality),ImageFileFormat.Webp=>WebpCodec.Encode(pixels,false,quality),_=>CodecBackend.EncodeOther(pixels,format)};
    }
    public static PixelBuffer Decode(ReadOnlySpan<byte> data)
    {
        CodecBackend.ValidateInput(data);
        ReadOnlySpan<byte> png=[137,80,78,71,13,10,26,10];
        if(data.Length>=8&&data[..8].SequenceEqual(png))return PngCodec.Decode(data);
        if(data.Length>=2&&data[0]==255&&data[1]==216)return JpegCodec.Decode(data);
        if(data.StartsWith("GIF87a"u8)||data.StartsWith("GIF89a"u8)){using var gif=new GifImage(data);return gif.DecodeFrame(0);}
        if(DetectFormat(data)!=ImageFileFormat.Unknown)return CodecBackend.DecodeOther(data);
        throw new NotSupportedException("Unsupported image signature or a format with no registered decoder.");
    }
}
