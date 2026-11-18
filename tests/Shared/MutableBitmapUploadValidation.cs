using SkiaSharp;

internal static class MutableBitmapUploadValidation
{
    internal static int Run(GRContext context)
    {
        var info = new SKImageInfo(8, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(context, true, info)!;
        using var read = new SKBitmap(info);
        int checks = 0;
        foreach (var format in new[] { SKColorType.Rgba8888, SKColorType.Bgra8888 })
        foreach (var alpha in new[] { SKAlphaType.Premul, SKAlphaType.Unpremul, SKAlphaType.Opaque })
        {
            using var source = new SKBitmap(new SKImageInfo(257, 160, format, alpha), 257 * 4 + 8);
            byte a = alpha == SKAlphaType.Opaque ? (byte)255 : (byte)128;
            surface.Canvas.Clear(SKColors.Transparent);
            // Eight draws share one source and command batch, each with different pixels.
            for (int x = 0; x < 8; x++)
            {
                byte red = (byte)(30 + x * 20);
                var pixel = source.GetPixelSpan().Slice(source.RowBytes + 256 * 4, 4);
                pixel[format == SKColorType.Rgba8888 ? 0 : 2] =
                    alpha == SKAlphaType.Premul ? (byte)((red * a + 127) / 255) : red;
                pixel[3] = alpha == SKAlphaType.Opaque ? (byte)7 : a;
                surface.Canvas.DrawBitmap(source, new SKRect(256, 1, 257, 2), new SKRect(x, 0, x + 1, 1));
            }
            if (!surface.ReadPixels(read.Info, read.GetPixels(), read.RowBytes, 0, 0))
                throw new InvalidOperationException("Mutable texture readback failed.");
            checks++;
            for (int x = 0; x < 8; x++)
            {
                var pixel = read.GetPixelSpan().Slice(x * 4, 4);
                if (pixel[0] != ((30 + x * 20) * a + 127) / 255 || pixel[1] != 0 || pixel[2] != 0 || pixel[3] != a)
                    throw new InvalidOperationException($"Incorrect queued texture version {x} with {format}/{alpha}.");
                checks++;
            }
        }
        return checks;
    }
}
