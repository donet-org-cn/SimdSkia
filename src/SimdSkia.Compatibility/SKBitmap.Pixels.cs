using SimdSkia;

namespace SkiaSharp;

public partial class SKBitmap
{
    private bool immutable;
    public bool IsImmutable {get{ThrowIfDisposed();return immutable;}}
    public bool IsNull {get{ThrowIfDisposed();return buffer is null;}}
    public bool DrawsNothing=>IsEmpty||IsNull;
    public bool ReadyToDraw=>!DrawsNothing;
    // Like Skia this is an ownership promise, not write protection for acquired pointers/Spans.
    public void SetImmutable(){ThrowIfDisposed();immutable=true;}
    public void Reset(){ThrowIfDisposed();pixelPin?.Dispose();pixelPin=null;buffer?.Dispose();buffer=null;info=default;immutable=false;}
    public IntPtr GetAddress(int x,int y)
    {
        ThrowIfDisposed();if((uint)x>=(uint)info.Width)throw new ArgumentOutOfRangeException(nameof(x));if((uint)y>=(uint)info.Height)throw new ArgumentOutOfRangeException(nameof(y));
        var ptr=GetPixels();return ptr==IntPtr.Zero?ptr:ptr+checked(y*RowBytes+x*BytesPerPixel);
    }
    public SKPixmap PeekPixels()
    {var pixmap=new SKPixmap();try{if(PeekPixels(pixmap))return pixmap;pixmap.Dispose();return null!;}catch{pixmap.Dispose();throw;}}
    public bool PeekPixels(SKPixmap pixmap)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(pixmap);pixmap.ThrowIfDisposed();if(buffer is null)return false;pixmap.SetManaged(info,buffer);return true;}
    public SKColor[] Pixels
    {
        get
        {
            ThrowIfDisposed();var result=new SKColor[checked(info.Width*info.Height)];if(buffer is null)return result;
            var bytes=buffer.GetPixelSpan();for(int y=0;y<info.Height;y++)for(int x=0;x<info.Width;x++)result[y*info.Width+x]=PixelOperations.ReadColor(bytes,y*buffer.RowBytes+x*BytesPerPixel,info);return result;
        }
        set
        {
            ThrowIfDisposed();ArgumentNullException.ThrowIfNull(value);if(value.Length!=(long)info.Width*info.Height)throw new ArgumentException("Pixel count must equal Width x Height.",nameof(value));
            if(buffer is null)return;var bytes=buffer.GetPixelSpan();for(int y=0;y<info.Height;y++)for(int x=0;x<info.Width;x++)PixelOperations.WriteTaggedColor(bytes,y*buffer.RowBytes+x*BytesPerPixel,info,value[y*info.Width+x]);
        }
    }
    public bool CanCopyTo(SKColorType colorType)
    {ThrowIfDisposed();if(colorType==SKColorType.Unknown)return false;if(colorType is not (SKColorType.Alpha8 or SKColorType.Rgba8888 or SKColorType.Bgra8888))throw new NotSupportedException($"Pixel format {colorType} is not implemented.");return true;}
    public SKBitmap Copy(SKColorType colorType)
    {var result=new SKBitmap();try{if(CopyTo(result,colorType))return result;result.Dispose();return null!;}catch{result.Dispose();throw;}}
    public bool CopyTo(SKBitmap destination)=>CopyTo(destination,ColorType);
    public bool CopyTo(SKBitmap destination,SKColorType colorType)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(destination);destination.ThrowIfDisposed();if(!CanCopyTo(colorType)||buffer is null)return false;
        var next=info.WithColorType(colorType);if(colorType==SKColorType.Alpha8&&next.AlphaType==SKAlphaType.Unpremul)next=next.WithAlphaType(SKAlphaType.Premul);var copy=new PixelBuffer(next.Width,next.Height,colorType==SKColorType.Alpha8?PixelFormat.Alpha8:colorType==SKColorType.Rgba8888?PixelFormat.Rgba8888:PixelFormat.Bgra8888,next.AlphaType==SKAlphaType.Premul?AlphaFormat.Premultiplied:buffer.Alpha);
        try
        {
            PixelOperations.Read(buffer.GetPixelSpan(),info,buffer.RowBytes,copy.GetPixelSpan(),next,copy.RowBytes,0,0);
            destination.pixelPin?.Dispose();destination.pixelPin=null;destination.buffer?.Dispose();destination.buffer=copy;destination.info=SKColorSpace.RetainInfo(next);destination.immutable=false;
        }
        catch{copy.Dispose();throw;}
        return true;
    }
    public bool ScalePixels(SKBitmap destination,SKSamplingOptions sampling)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(destination);destination.ThrowIfDisposed();
        PixelOperations.ValidateSampling(sampling);if(buffer is null||destination.buffer is null)return false;
        return PixelOperations.Scale(buffer.GetPixelSpan(),info,buffer.RowBytes,destination.buffer.GetPixelSpan(),destination.info,destination.buffer.RowBytes,sampling);
    }
    public bool ScalePixels(SKPixmap destination,SKSamplingOptions sampling)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(destination);PixelOperations.ValidateSampling(sampling);if(buffer is null)return false;return PixelOperations.Scale(buffer.GetPixelSpan(),info,buffer.RowBytes,destination.Storage,destination.Info,destination.RowBytes,sampling);}
    [Obsolete("Use ScalePixels(SKBitmap destination, SKSamplingOptions sampling) instead.")]
    public bool ScalePixels(SKBitmap destination,SKFilterQuality quality)=>ScalePixels(destination,SKPixmap.LegacySampling(quality));
    [Obsolete("Use ScalePixels(SKPixmap destination, SKSamplingOptions sampling) instead.")]
    public bool ScalePixels(SKPixmap destination,SKFilterQuality quality)=>ScalePixels(destination,SKPixmap.LegacySampling(quality));
    public SKBitmap Resize(SKImageInfo info,SKSamplingOptions sampling)
    {
        ThrowIfDisposed();PixelOperations.ValidateSampling(sampling);if(info.IsEmpty)return null!;var dst=new SKBitmap(info);
        try{if(ScalePixels(dst,sampling))return dst;dst.Dispose();return null!;}catch{dst.Dispose();throw;}
    }
    public SKBitmap Resize(SKSizeI size,SKSamplingOptions sampling)=>Resize(Info.WithSize(size),sampling);
    [Obsolete("Use Resize(SKImageInfo info, SKSamplingOptions sampling) instead.")]
    public SKBitmap Resize(SKImageInfo info,SKFilterQuality quality)=>Resize(info,SKPixmap.LegacySampling(quality));
    [Obsolete("Use Resize(SKSizeI size, SKSamplingOptions sampling) instead.")]
    public SKBitmap Resize(SKSizeI size,SKFilterQuality quality)=>Resize(size,SKPixmap.LegacySampling(quality));
    public bool Encode(Stream dst,SKEncodedImageFormat format,int quality)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(dst);using var encoded=Encode(format,quality);encoded.SaveTo(dst);return true;}
    public static SKBitmap Decode(SKData data){ArgumentNullException.ThrowIfNull(data);return Decode(data.AsSpan());}
}
