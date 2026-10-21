using SimdSkia;

namespace SkiaSharp;

/// <summary>Immutable managed pixel snapshot. Bitmap conversions allocate independent storage.</summary>
public class SKImage : SKObject
{
    private readonly PixelBuffer pixels;
    private readonly SKImageInfo info;
    internal SKImage(SKImageInfo info,PixelBuffer pixels) { this.info=info; this.pixels=pixels; }
    public int Width { get { ThrowIfDisposed(); return info.Width; } }
    public int Height { get { ThrowIfDisposed(); return info.Height; } }
    public SKColorType ColorType { get { ThrowIfDisposed(); return info.ColorType; } }
    public SKAlphaType AlphaType { get { ThrowIfDisposed(); return info.AlphaType; } }
    public SKColorSpace? ColorSpace { get { ThrowIfDisposed(); return info.ColorSpace; } }
    public static SKImage FromBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return new(bitmap.Info,bitmap.Buffer.Clone());
    }
    internal SKBitmap CopyBitmap() { ThrowIfDisposed(); return new(info,pixels.Clone()); }
    public SKData Encode(SKEncodedImageFormat format,int quality)
    {ThrowIfDisposed();if(quality is <0 or >100)throw new ArgumentOutOfRangeException(nameof(quality));return new(format switch{SKEncodedImageFormat.Png=>SimdSkia.Imaging.PngCodec.Encode(pixels),SKEncodedImageFormat.Jpeg=>SimdSkia.Imaging.JpegCodec.Encode(pixels,quality),_=>throw new NotSupportedException("Managed encoding supports PNG and JPEG.")});}
    protected override void Dispose(bool disposing) => pixels.Dispose();
}
