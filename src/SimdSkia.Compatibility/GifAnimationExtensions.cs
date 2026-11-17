using SkiaSharp;

namespace SimdSkia.Imaging;

/// <summary>Allows SimdSkia bitmaps to be written directly as GIF animation frames.</summary>
public static class GifAnimationExtensions
{
    public static void AddFrame(this GifAnimationWriter writer, SKBitmap bitmap, TimeSpan delay,
        GifFrameDisposal disposal = GifFrameDisposal.RestoreBackground)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.ColorSpace is { } space && !space.IsSrgb)
            throw new NotSupportedException("Convert the bitmap to sRGB before encoding a GIF frame.");
        writer.AddFrame(bitmap.Buffer, delay, disposal);
    }
}
