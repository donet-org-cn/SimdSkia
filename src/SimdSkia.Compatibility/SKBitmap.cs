using SimdSkia;
using SimdSkia.Imaging;

namespace SkiaSharp;

public class SKBitmap : SKObject
{
    private PixelBuffer? buffer;
    private PixelBufferPin? pixelPin;
    private SKImageInfo info;
    public SKBitmap() { }
    public SKBitmap(int width,int height,bool isOpaque=false) : this(new SKImageInfo(width,height,SKImageInfo.PlatformColorType,isOpaque ? SKAlphaType.Opaque : SKAlphaType.Premul)) { }
    public SKBitmap(int width,int height,SKColorType colorType,SKAlphaType alphaType) : this(new SKImageInfo(width,height,colorType,alphaType)) { }
    public SKBitmap(int width,int height,SKColorType colorType,SKAlphaType alphaType,SKColorSpace? colorspace) : this(new SKImageInfo(width,height,colorType,alphaType,colorspace)) { }
    public SKBitmap(SKImageInfo info) : this(info,info.RowBytes) { }
    public SKBitmap(SKImageInfo info,int rowBytes)
    {
        if (!TryAllocPixels(info,rowBytes)) throw new Exception("Unable to allocate pixels for the bitmap.");
    }
    internal SKBitmap(SKImageInfo info,PixelBuffer buffer) { this.info=info; this.buffer=buffer; }
    public SKBitmap Copy() => new(Info,Buffer.Clone());
    public static SKBitmap Decode(byte[] buffer){ArgumentNullException.ThrowIfNull(buffer);return Decode(buffer.AsSpan());}
    public static SKBitmap Decode(ReadOnlySpan<byte> buffer)
    {var pixels=ImageCodec.Decode(buffer);return new(new SKImageInfo(pixels.Width,pixels.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul),pixels);}
    public static SKBitmap Decode(string filename){ArgumentNullException.ThrowIfNull(filename);return Decode(File.ReadAllBytes(filename));}
    public static SKBitmap Decode(Stream stream){ArgumentNullException.ThrowIfNull(stream);using var copy=new MemoryStream();stream.CopyTo(copy);return Decode(copy.ToArray());}
    public SKData Encode(SKEncodedImageFormat format,int quality)
    {ThrowIfDisposed();if(quality is <0 or >100)throw new ArgumentOutOfRangeException(nameof(quality));return new(format switch{SKEncodedImageFormat.Png=>PngCodec.Encode(Buffer),SKEncodedImageFormat.Jpeg=>JpegCodec.Encode(Buffer,quality),_=>throw new NotSupportedException("Managed encoding supports PNG and JPEG.")});}
    public static SKBitmap FromImage(SKImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return image.CopyBitmap();
    }
    public SKImageInfo Info { get { ThrowIfDisposed(); return info; } }
    public int Width => Info.Width;
    public int Height => Info.Height;
    public SKColorType ColorType => Info.ColorType;
    public SKAlphaType AlphaType => Info.AlphaType;
    public SKColorSpace? ColorSpace => Info.ColorSpace;
    public int BytesPerPixel => Info.BytesPerPixel;
    public int RowBytes { get { ThrowIfDisposed(); return buffer?.RowBytes ?? 0; } }
    public int ByteCount { get { ThrowIfDisposed(); return buffer?.ByteCount ?? 0; } }
    public bool IsEmpty => Info.IsEmpty;
    public byte[] Bytes => GetPixelSpan().ToArray();
    public Span<byte> GetPixelSpan() { ThrowIfDisposed(); return buffer is null ? Span<byte>.Empty : buffer.GetPixelSpan(); }
    public Span<byte> GetPixelSpan(int x,int y)
    {
        var pixels=Buffer;
        if ((uint)x>=(uint)pixels.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y>=(uint)pixels.Height) throw new ArgumentOutOfRangeException(nameof(y));
        return pixels.GetPixelSpan()[(y*pixels.RowBytes+x*4)..];
    }
    // The address remains stable until reallocation or disposal. The caller must
    // synchronize native writers with drawing and keep this bitmap alive.
    public IntPtr GetPixels() { ThrowIfDisposed(); return buffer is null ? IntPtr.Zero : (pixelPin ??= buffer.Pin()).Address; }
    public IntPtr GetPixels(out IntPtr length) { var result=GetPixels(); length=(IntPtr)ByteCount; return result; }
    public void SetPixels(IntPtr pixels) { ThrowIfDisposed(); throw new NotSupportedException("External native pixel storage is not implemented."); }
    public SKColor GetPixel(int x,int y) => SKColor.FromCore(Buffer.ReadPixel(x,y));
    public void SetPixel(int x,int y,SKColor color) => Buffer.WritePixel(x,y,color.ToCore());
    public void Erase(SKColor color)
    {
        if(color.Alpha==0)color=SKColor.Empty;
        var pixels=Buffer;
        for (int y=0;y<pixels.Height;y++) for (int x=0;x<pixels.Width;x++) pixels.WritePixel(x,y,color.ToCore());
    }
    public bool TryAllocPixels(SKImageInfo info) => TryAllocPixels(info,info.RowBytes);
    public bool TryAllocPixels(SKImageInfo info,int rowBytes)
    {
        ThrowIfDisposed();
        if (info.ColorSpace is not null) _=info.ColorSpace.IsSrgb;
        PixelFormat format=info.ColorType switch
        { SKColorType.Rgba8888 => PixelFormat.Rgba8888, SKColorType.Bgra8888 => PixelFormat.Bgra8888, _ => throw new NotSupportedException($"Pixel format {info.ColorType} is not implemented.") };
        AlphaFormat alpha=info.AlphaType switch
        { SKAlphaType.Opaque => AlphaFormat.Opaque, SKAlphaType.Premul => AlphaFormat.Premultiplied, SKAlphaType.Unpremul => AlphaFormat.Straight, _ => throw new NotSupportedException($"Alpha type {info.AlphaType} is not implemented.") };
        if (info.IsEmpty || rowBytes<0) return false;
        try
        {
            var allocated=new PixelBuffer(info.Width,info.Height,format,alpha,rowBytes);
            pixelPin?.Dispose(); pixelPin=null; buffer?.Dispose(); buffer=allocated; this.info=info;
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
        catch (OverflowException) { return false; }
    }
    internal PixelBuffer Buffer { get { ThrowIfDisposed(); return buffer ?? throw new InvalidOperationException("The bitmap has no pixels."); } }
    protected override void Dispose(bool disposing) { pixelPin?.Dispose(); pixelPin=null; buffer?.Dispose(); buffer=null; }
}
