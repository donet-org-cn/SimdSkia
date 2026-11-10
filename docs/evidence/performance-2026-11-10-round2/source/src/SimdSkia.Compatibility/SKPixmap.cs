using System.Runtime.InteropServices;
using SimdSkia;
#pragma warning disable CS0618 // Exact legacy API; Medium fails explicitly instead of silently dropping mipmaps.

namespace SkiaSharp;

/// <summary>A non-owning external pixel descriptor, or a retained view over managed pixels.</summary>
public partial class SKPixmap : SKObject
{
    private SKImageInfo info;
    private int rowBytes,offset;
    private IntPtr address;
    private PixelBuffer? lease;
    private PixelBufferPin? pin;
    public SKPixmap() { }
    public SKPixmap(SKImageInfo info,IntPtr addr):this(info,addr,info.RowBytes) { }
    public SKPixmap(SKImageInfo info,IntPtr addr,int rowBytes)=>Reset(info,addr,rowBytes);
    public SKImageInfo Info {get{ThrowIfDisposed();return info;}}
    public int Width=>Info.Width;
    public int Height=>Info.Height;
    public SKSizeI Size=>Info.Size;
    public SKRectI Rect=>Info.Rect;
    public SKColorType ColorType=>Info.ColorType;
    public SKAlphaType AlphaType=>Info.AlphaType;
    public SKColorSpace? ColorSpace=>Info.ColorSpace;
    public int BytesPerPixel=>Info.BytesPerPixel;
    public int BitShiftPerPixel=>Info.BitShiftPerPixel;
    public int RowBytes {get{ThrowIfDisposed();return rowBytes;}}
    public int BytesSize=>Info.BytesSize;
    public long BytesSize64=>Info.BytesSize64;
    public void Reset(){ThrowIfDisposed();Release();info=default;rowBytes=0;}
    public void Reset(SKImageInfo info,IntPtr addr,int rowBytes)
    {ThrowIfDisposed();PixelOperations.Validate(info,rowBytes);Release();this.info=SKColorSpace.RetainInfo(info);this.rowBytes=rowBytes;address=addr;}
    internal void SetManaged(SKImageInfo info,PixelBuffer pixels,int offset=0)
    {
        ThrowIfDisposed();PixelOperations.Validate(info,pixels.RowBytes);
        if(offset<0||(long)offset+PixelOperations.Length(info,pixels.RowBytes)>pixels.ByteCount)throw new ArgumentOutOfRangeException(nameof(offset));
        var retained=pixels.Share();Release();lease=retained;this.info=SKColorSpace.RetainInfo(info);rowBytes=pixels.RowBytes;this.offset=offset;
    }
    internal PixelBuffer? RetainPixels(Action? release=null)
    {
        ThrowIfDisposed();if(Storage.IsEmpty)return null;if(lease is not null&&offset==0&&info.Width==lease.Width&&info.Height==lease.Height&&release is null)return lease.Share();
        PixelBufferPin? retained=lease?.Pin();var ptr=retained is null?address:retained.Address+offset;
        try{return PixelOperations.External(info,ptr,rowBytes,()=>{try{release?.Invoke();}finally{retained?.Dispose();}});}catch{retained?.Dispose();throw;}
    }
    private void Release(){pin?.Dispose();pin=null;lease?.Dispose();lease=null;address=IntPtr.Zero;offset=0;}
    public IntPtr GetPixels()
    {ThrowIfDisposed();return lease is null?address:(pin??=lease.Pin()).Address+offset;}
    public IntPtr GetPixels(int x,int y){CheckCoordinates(x,y);var pointer=GetPixels();return pointer==IntPtr.Zero?pointer:pointer+checked(y*rowBytes+x*BytesPerPixel);}
    internal unsafe Span<byte> Storage
    {
        get
        {
            ThrowIfDisposed();int length=PixelOperations.Length(info,rowBytes);
            if(lease is not null)return lease.GetPixelSpan().Slice(offset,length);
            return address==IntPtr.Zero?Span<byte>.Empty:new Span<byte>((void*)address,length);
        }
    }
    // Unlike the baseline's compact-size calculation, these spans include row padding
    // through the last visible pixel. This prevents truncating padded images.
    public Span<byte> GetPixelSpan()=>Storage;
    public Span<byte> GetPixelSpan(int x,int y){CheckCoordinates(x,y);var bytes=Storage;return bytes.IsEmpty?bytes:bytes[checked(y*rowBytes+x*BytesPerPixel)..];}
    public Span<T> GetPixelSpan<T>() where T:unmanaged=>GetPixelSpan<T>(0,0);
    public unsafe Span<T> GetPixelSpan<T>(int x,int y) where T:unmanaged
    {
        ThrowIfDisposed();if(info.IsEmpty||info.ColorType==SKColorType.Unknown)return Span<T>.Empty;
        if(sizeof(T)!=1&&sizeof(T)!=BytesPerPixel)throw new ArgumentException("T must be byte or the size of a pixel.",nameof(T));
        return MemoryMarshal.Cast<byte,T>(GetPixelSpan(x,y));
    }
    private void CheckCoordinates(int x,int y)
    {ThrowIfDisposed();if((uint)x>=(uint)info.Width)throw new ArgumentOutOfRangeException(nameof(x));if((uint)y>=(uint)info.Height)throw new ArgumentOutOfRangeException(nameof(y));}
    public SKColor GetPixelColor(int x,int y){CheckCoordinates(x,y);return PixelOperations.ReadColor(Storage,checked(y*rowBytes+x*BytesPerPixel),info);}
    public SKColorF GetPixelColorF(int x,int y)=>GetPixelColor(x,y);
    public float GetPixelAlpha(int x,int y){CheckCoordinates(x,y);return Storage[checked(y*rowBytes+x*BytesPerPixel+BytesPerPixel-1)]*(1f/255);}
    public bool ComputeIsOpaque()
    {
        var bytes=Storage;if(bytes.IsEmpty)return false;
        for(int y=0;y<info.Height;y++)for(int x=0;x<info.Width;x++)if(bytes[y*rowBytes+x*BytesPerPixel+BytesPerPixel-1]!=255)return false;return true;
    }
    public bool ReadPixels(SKImageInfo dstInfo,IntPtr dstPixels,int dstRowBytes)=>ReadPixels(dstInfo,dstPixels,dstRowBytes,0,0);
    public bool ReadPixels(SKImageInfo dstInfo,IntPtr dstPixels,int dstRowBytes,int srcX,int srcY)
    {
        ThrowIfDisposed();return PixelOperations.Read(Storage,info,rowBytes,PixelOperations.Wrap(dstInfo,dstPixels,dstRowBytes),dstInfo,dstRowBytes,srcX,srcY);
    }
    public bool ReadPixels(SKPixmap pixmap)=>ReadPixels(pixmap,0,0);
    public bool ReadPixels(SKPixmap pixmap,int srcX,int srcY)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(pixmap);return PixelOperations.Read(Storage,info,rowBytes,pixmap.Storage,pixmap.Info,pixmap.RowBytes,srcX,srcY);}
    public bool ScalePixels(SKPixmap destination)=>ScalePixels(destination,SKSamplingOptions.Default);
    public bool ScalePixels(SKPixmap destination,SKSamplingOptions sampling)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(destination);return PixelOperations.Scale(Storage,info,rowBytes,destination.Storage,destination.Info,destination.RowBytes,sampling);}
    [Obsolete("Use ScalePixels(SKPixmap destination, SKSamplingOptions sampling) instead.")]
    public bool ScalePixels(SKPixmap destination,SKFilterQuality quality)=>ScalePixels(destination,LegacySampling(quality));
    internal static SKSamplingOptions LegacySampling(SKFilterQuality quality)=>quality switch
    {
        SKFilterQuality.None=>SKSamplingOptions.Default,SKFilterQuality.Low=>new(SKFilterMode.Linear),
        SKFilterQuality.Medium=>throw new NotSupportedException("Legacy Medium requires mipmap sampling."),
        SKFilterQuality.High=>new(SKCubicResampler.Mitchell),_=>throw new ArgumentOutOfRangeException(nameof(quality))
    };
    public bool ExtractSubset(SKPixmap result,SKRectI subset)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(result);result.ThrowIfDisposed();
        int l=Math.Max(0,subset.Left),t=Math.Max(0,subset.Top),r=Math.Min(info.Width,subset.Right),b=Math.Min(info.Height,subset.Bottom);
        if(l>=r||t>=b||Storage.IsEmpty)return false;
        var next=info.WithSize(r-l,b-t);int delta=checked(t*rowBytes+l*BytesPerPixel);
        if(lease is not null)result.SetManaged(next,lease,checked(offset+delta));
        else result.Reset(next,address+delta,rowBytes);
        return true;
    }
    public SKPixmap ExtractSubset(SKRectI subset)
    {var result=new SKPixmap();try{if(ExtractSubset(result,subset))return result;result.Dispose();return null!;}catch{result.Dispose();throw;}}
    private SKPixmap WithInfo(SKImageInfo next)
    {ThrowIfDisposed();var result=new SKPixmap();try{if(lease is not null)result.SetManaged(next,lease,offset);else result.Reset(next,address,rowBytes);return result;}catch{result.Dispose();throw;}}
    public SKPixmap WithColorType(SKColorType newColorType)=>WithInfo(Info.WithColorType(newColorType));
    public SKPixmap WithAlphaType(SKAlphaType newAlphaType)=>WithInfo(Info.WithAlphaType(newAlphaType));
    public SKPixmap WithColorSpace(SKColorSpace newColorSpace)=>WithInfo(Info.WithColorSpace(newColorSpace));
    public bool Erase(SKColor color)=>Erase(color,Rect);
    public bool Erase(SKColor color,SKRectI subset)=>PixelOperations.Erase(Storage,Info,RowBytes,color,subset);
    public bool Erase(SKColorF color)=>Erase(color,Rect);
    public bool Erase(SKColorF color,SKRectI subset)=>Erase((SKColor)color,subset);
    public SKData Encode(SKEncodedImageFormat encoder,int quality)
    {
        ThrowIfDisposed();if(Storage.IsEmpty)return null!;
        using var bitmap=new SKBitmap(info);using var dst=bitmap.PeekPixels();ReadPixels(dst);return bitmap.Encode(encoder,quality);
    }
    public bool Encode(Stream dst,SKEncodedImageFormat encoder,int quality)
    {ArgumentNullException.ThrowIfNull(dst);using var data=Encode(encoder,quality);if(data is null)return false;data.SaveTo(dst);return true;}
    protected override void Dispose(bool disposing)=>Release();
}
