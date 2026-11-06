using SimdSkia;
namespace SkiaSharp;
public delegate void SKBitmapReleaseDelegate(IntPtr address,object context);
public delegate void SKImageRasterReleaseDelegate(IntPtr pixels,object context);
public delegate void SKSurfaceReleaseDelegate(IntPtr address,object context);
public partial class SKBitmap
{
    public bool InstallPixels(SKImageInfo info,IntPtr pixels)=>InstallPixels(info,pixels,info.RowBytes,null,null!);
    public bool InstallPixels(SKImageInfo info,IntPtr pixels,int rowBytes)=>InstallPixels(info,pixels,rowBytes,null,null!);
    public bool InstallPixels(SKImageInfo info,IntPtr pixels,int rowBytes,SKBitmapReleaseDelegate? releaseProc)=>InstallPixels(info,pixels,rowBytes,releaseProc,null!);
    public bool InstallPixels(SKImageInfo info,IntPtr pixels,int rowBytes,SKBitmapReleaseDelegate? releaseProc,object context)
    {
        ThrowIfDisposed();PixelOperations.Validate(info,rowBytes);if(info.IsEmpty||pixels==IntPtr.Zero)return false;
        var next=PixelOperations.External(info,pixels,rowBytes,releaseProc is null?null:()=>releaseProc(pixels,context));
        ReplacePixels(info,next);return true;
    }
    public bool InstallPixels(SKPixmap pixmap)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(pixmap);var next=pixmap.RetainPixels();if(next is null)return false;ReplacePixels(pixmap.Info,next);return true;}
    private void ReplacePixels(SKImageInfo next,PixelBuffer pixels)
    {var old=buffer;pixelPin?.Dispose();pixelPin=null;buffer=pixels;info=SKColorSpace.RetainInfo(next);immutable=false;old?.Dispose();}
    public void NotifyPixelsChanged(){ThrowIfDisposed();/* All raster reads and GPU uploads already observe the live pixel buffer. */}
}
internal static partial class ExternalPixelOperations
{
    internal static PixelBuffer Create(SKImageInfo info,IntPtr address,int rowBytes,Action? release)
    {
        PixelOperations.Validate(info,rowBytes);return PixelBuffer.Wrap(info.Width,info.Height,info.ColorType==SKColorType.Alpha8?PixelFormat.Alpha8:info.ColorType==SKColorType.Rgba8888?PixelFormat.Rgba8888:PixelFormat.Bgra8888,info.AlphaType switch{SKAlphaType.Opaque=>AlphaFormat.Opaque,SKAlphaType.Premul=>AlphaFormat.Premultiplied,_=>AlphaFormat.Straight},rowBytes,address,release);
    }
}
