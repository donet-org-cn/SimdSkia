using Sdcb.SimdCodecs.Webp;
using Sdcb.SimdCodecs.Abstractions;
namespace SimdSkia.Imaging;
public static class WebpCodec
{
    public static byte[] Encode(PixelBuffer pixels,bool lossless,float quality)
    {
        ArgumentNullException.ThrowIfNull(pixels);if(!float.IsFinite(quality)||quality<0||quality>100)throw new ArgumentOutOfRangeException(nameof(quality));
        using var source=new EncodingPixels(pixels,true);
        // WebP quality scales are encoder-specific. Lossless quality maps to
        // compression effort; it never quantizes the visible source pixels.
        return lossless?WebpEncoder.EncodeLossless(source.Span,pixels.Width,pixels.Height,ImagePixelFormat.Rgba32,new WebpLosslessEncodeOptions{Effort=(int)MathF.Round(quality*.06f),Exact=false}):WebpEncoder.EncodeLossy(source.Span,pixels.Width,pixels.Height,ImagePixelFormat.Rgba32,new WebpLossyEncodeOptions{Quality=(int)MathF.Round(quality),AlphaQuality=100});
    }
}
