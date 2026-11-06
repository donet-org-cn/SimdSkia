namespace SkiaSharp;

public partial class SKImage
{
    public SKImageInfo Info {get{ThrowIfDisposed();return info;}}
    public bool IsAlphaOnly {get{ThrowIfDisposed();return info.ColorType==SKColorType.Alpha8;}}
    public SKPixmap PeekPixels()
    {ThrowIfDisposed();var map=new SKPixmap();try{if(PeekPixels(map))return map;map.Dispose();return null!;}catch{map.Dispose();throw;}}
    public bool PeekPixels(SKPixmap pixmap)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(pixmap);if(generated is not null)return false;pixmap.SetManaged(info,Buffer);return true;}
    private static void ValidateHint(SKImageCachingHint hint)
    {if(!Enum.IsDefined(hint))throw new ArgumentOutOfRangeException(nameof(hint));} // No read cache is used for raster images.
    public bool ReadPixels(SKImageInfo dstInfo,IntPtr dstPixels)=>ReadPixels(dstInfo,dstPixels,dstInfo.RowBytes,0,0);
    public bool ReadPixels(SKImageInfo dstInfo,IntPtr dstPixels,int dstRowBytes)=>ReadPixels(dstInfo,dstPixels,dstRowBytes,0,0);
    public bool ReadPixels(SKImageInfo dstInfo,IntPtr dstPixels,int dstRowBytes,int srcX,int srcY)=>ReadPixels(dstInfo,dstPixels,dstRowBytes,srcX,srcY,SKImageCachingHint.Allow);
    public bool ReadPixels(SKImageInfo dstInfo,IntPtr dstPixels,int dstRowBytes,int srcX,int srcY,SKImageCachingHint cachingHint)
    {ThrowIfDisposed();ValidateHint(cachingHint);return PixelOperations.Read(Buffer.GetPixelSpan(),info,Buffer.RowBytes,PixelOperations.Wrap(dstInfo,dstPixels,dstRowBytes),dstInfo,dstRowBytes,srcX,srcY);}
    public bool ReadPixels(SKPixmap pixmap)=>ReadPixels(pixmap,0,0);
    public bool ReadPixels(SKPixmap pixmap,int srcX,int srcY)=>ReadPixels(pixmap,srcX,srcY,SKImageCachingHint.Allow);
    public bool ReadPixels(SKPixmap pixmap,int srcX,int srcY,SKImageCachingHint cachingHint)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(pixmap);ValidateHint(cachingHint);return PixelOperations.Read(Buffer.GetPixelSpan(),info,Buffer.RowBytes,pixmap.Storage,pixmap.Info,pixmap.RowBytes,srcX,srcY);}
    public bool ScalePixels(SKPixmap dst,SKSamplingOptions sampling)=>ScalePixels(dst,sampling,SKImageCachingHint.Allow);
    public bool ScalePixels(SKPixmap dst,SKSamplingOptions sampling,SKImageCachingHint cachingHint)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(dst);ValidateHint(cachingHint);return PixelOperations.Scale(Buffer.GetPixelSpan(),info,Buffer.RowBytes,dst.Storage,dst.Info,dst.RowBytes,sampling);}
    [Obsolete("Use ScalePixels(SKPixmap dst, SKSamplingOptions sampling) instead.")]
    public bool ScalePixels(SKPixmap dst,SKFilterQuality quality)=>ScalePixels(dst,SKPixmap.LegacySampling(quality));
    [Obsolete("Use ScalePixels(SKPixmap dst, SKSamplingOptions sampling, SKImageCachingHint cachingHint) instead.")]
    public bool ScalePixels(SKPixmap dst,SKFilterQuality quality,SKImageCachingHint cachingHint)=>ScalePixels(dst,SKPixmap.LegacySampling(quality),cachingHint);
}
