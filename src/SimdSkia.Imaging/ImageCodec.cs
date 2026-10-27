namespace SimdSkia.Imaging;

public static class ImageCodec
{
    public static PixelBuffer Decode(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> png=[137,80,78,71,13,10,26,10];
        if(data.Length>=8&&data[..8].SequenceEqual(png))return PngCodec.Decode(data);
        if(data.Length>=2&&data[0]==255&&data[1]==216)return JpegCodec.Decode(data);
        if(data.StartsWith("GIF87a"u8)||data.StartsWith("GIF89a"u8)){using var gif=new GifImage(data);return gif.DecodeFrame(0);}
        throw new NotSupportedException("Unsupported image signature. Managed PNG, JPEG and GIF decoders are available.");
    }
}
