using Sdcb.SimdCodecs.Webp;
using Sdcb.SimdCodecs.Abstractions;
using CodecPixelFormat=Sdcb.SimdCodecs.Abstractions.PixelFormat;
namespace SimdSkia.Imaging;
public static class WebpCodec
{
    public static byte[] Encode(PixelBuffer pixels,bool lossless,float quality)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if(!float.IsFinite(quality)||quality<0||quality>100)throw new ArgumentOutOfRangeException(nameof(quality));
        EncodingPixels.Validate(pixels);
        if (pixels.Format == PixelFormat.Alpha8)
        {
            using var source = new EncodingPixels(pixels, true);
            return Encode(source.Span, pixels.Width, pixels.Height, CodecPixelFormat.Rgba32, 0, lossless, quality);
        }

        bool premultiplied = pixels.Alpha == AlphaFormat.Premultiplied;
        if (premultiplied && pixels.RowBytes == pixels.Width * 4 &&
            PixelBuffer.HasOnlyOpaqueAlpha(pixels.GetPixelSpan()))
            premultiplied = false;
        var format = (pixels.Format, premultiplied) switch
        {
            (PixelFormat.Rgba8888, false) => CodecPixelFormat.Rgba32,
            (PixelFormat.Rgba8888, true) => CodecPixelFormat.Rgba32Premultiplied,
            (_, false) => CodecPixelFormat.Bgra32,
            _ => CodecPixelFormat.Bgra32Premultiplied,
        };
        return Encode(pixels.GetPixelSpan(), pixels.Width, pixels.Height, format, pixels.RowBytes, lossless, quality);
    }

    private static byte[] Encode(ReadOnlySpan<byte> pixels, int width, int height, CodecPixelFormat format,
        int stride, bool lossless, float quality)
    {
        // WebP quality scales are encoder-specific. Lossless quality maps to
        // compression effort; it never quantizes the visible source pixels.
        return lossless
            ? WebpEncoder.EncodeLossless(pixels, width, height, format,
                new WebpLosslessEncodeOptions { Effort = (int)MathF.Round(quality * .06f), Exact = false }, stride)
            : WebpEncoder.EncodeLossy(pixels, width, height, format,
                new WebpLossyEncodeOptions { Quality = (int)MathF.Round(quality), AlphaQuality = 100 }, stride);
    }
}
