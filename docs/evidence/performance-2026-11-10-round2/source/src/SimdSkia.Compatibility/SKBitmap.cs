using SimdSkia;
using SimdSkia.Imaging;
using SimdSkia.Cpu;

namespace SkiaSharp;

public partial class SKBitmap : SKObject
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
    internal SKBitmap(SKImageInfo info,PixelBuffer buffer) { this.info=SKColorSpace.RetainInfo(info); this.buffer=buffer; }
    public SKBitmap Copy() => Copy(ColorType);
    public static SKBitmap Decode(byte[] buffer){ArgumentNullException.ThrowIfNull(buffer);return Decode(buffer.AsSpan());}
    public static SKBitmap Decode(ReadOnlySpan<byte> buffer)
    {var raster=DecodedRaster.Read(buffer);return new(raster.Info,raster.Pixels);}
    public static SKBitmap Decode(string filename){ArgumentNullException.ThrowIfNull(filename);return Decode(File.ReadAllBytes(filename));}
    public static SKBitmap Decode(Stream stream){ArgumentNullException.ThrowIfNull(stream);using var copy=new MemoryStream();stream.CopyTo(copy);return Decode(copy.ToArray());}
    public SKData Encode(SKEncodedImageFormat format,int quality)
    {ThrowIfDisposed();if(quality is <0 or >100)throw new ArgumentOutOfRangeException(nameof(quality));if(info.ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Tagged image encoding requires ICC serialization; convert to sRGB first.");return new(ImageCodec.Encode(Buffer,CodecFormats.ToEngine(format),quality));}
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
        return pixels.GetPixelSpan()[(y*pixels.RowBytes+x*BytesPerPixel)..];
    }
    // The address remains stable until reallocation or disposal. The caller must
    // synchronize native writers with drawing and keep this bitmap alive.
    public IntPtr GetPixels() { ThrowIfDisposed(); return buffer is null ? IntPtr.Zero : (pixelPin ??= buffer.Pin()).Address; }
    public IntPtr GetPixels(out IntPtr length) { var result=GetPixels(); length=(IntPtr)ByteCount; return result; }
    public void SetPixels(IntPtr pixels) { ThrowIfDisposed();InstallPixels(info,pixels,RowBytes); }
    public SKColor GetPixel(int x,int y) => SKColor.FromCore(Buffer.ReadPixel(x,y));
    // Baseline SetPixel uses DrawPoint(color), whose color overload selects Src.
    // Keep the direct store, including canonical zero RGB for a transparent color.
    public void SetPixel(int x,int y,SKColor color)
    {
        var target=Buffer;
        if(info.ColorSpace is null||info.ColorSpace.IsSrgb){target.WritePixel(x,y,color.Alpha==0?default:color.ToCore());return;}
        if((uint)x>=(uint)target.Width||(uint)y>=(uint)target.Height)throw new ArgumentOutOfRangeException(nameof(x));
        PixelOperations.WriteTaggedColor(target.GetPixelSpan(),y*target.RowBytes+x*BytesPerPixel,info,color);
    }
    public void Erase(SKColor color)
    {
        ThrowIfDisposed();if(buffer is null)return;
        if(info.ColorSpace is {} space&&!space.IsSrgb){PixelOperations.Erase(buffer.GetPixelSpan(),info,buffer.RowBytes,color,info.Rect);return;}
        if(color.Alpha==0)color=SKColor.Empty;
        ClearKernel.Fill(buffer,color.ToCore(),CpuOptions.EnableSimd);
    }
    public void Erase(SKColor color,SKRectI rect)
    {
        ThrowIfDisposed();if(buffer is null)return;
        if(info.ColorSpace is {} space&&!space.IsSrgb){PixelOperations.Erase(buffer.GetPixelSpan(),info,buffer.RowBytes,color,rect);return;}
        if(color.Alpha==0)color=SKColor.Empty;
        ClearKernel.FillRectangle(buffer,color.ToCore(),rect.Left,rect.Top,rect.Right,rect.Bottom,CpuOptions.EnableSimd);
    }
    public bool TryAllocPixels(SKImageInfo info) => TryAllocPixels(info,info.RowBytes);
    public bool TryAllocPixels(SKImageInfo info,int rowBytes)
    {
        ThrowIfDisposed();
        if(info.ColorType==SKColorType.Alpha8&&info.AlphaType==SKAlphaType.Unpremul)info.AlphaType=SKAlphaType.Premul;
        if (info.ColorSpace is not null) _=info.ColorSpace.IsSrgb;
        PixelFormat format=info.ColorType switch
        { SKColorType.Alpha8=>PixelFormat.Alpha8,SKColorType.Rgba8888 => PixelFormat.Rgba8888, SKColorType.Bgra8888 => PixelFormat.Bgra8888, _ => throw new NotSupportedException($"Pixel format {info.ColorType} is not implemented.") };
        AlphaFormat alpha=info.AlphaType switch
        { SKAlphaType.Opaque => AlphaFormat.Opaque, SKAlphaType.Premul => AlphaFormat.Premultiplied, SKAlphaType.Unpremul => AlphaFormat.Straight, _ => throw new NotSupportedException($"Alpha type {info.AlphaType} is not implemented.") };
        if (info.IsEmpty || rowBytes<0) return false;
        try
        {
            var allocated=new PixelBuffer(info.Width,info.Height,format,alpha,rowBytes);
            pixelPin?.Dispose(); pixelPin=null; buffer?.Dispose(); buffer=allocated; this.info=SKColorSpace.RetainInfo(info); immutable=false;
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
        catch (OverflowException) { return false; }
    }
    internal PixelBuffer Buffer { get { ThrowIfDisposed(); return buffer ?? throw new InvalidOperationException("The bitmap has no pixels."); } }
    protected override void Dispose(bool disposing) { pixelPin?.Dispose(); pixelPin=null; buffer?.Dispose(); buffer=null; }
}
