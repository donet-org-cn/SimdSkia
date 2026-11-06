namespace SkiaSharp;
public partial class SKSurface
{
    private SKSurfacePropsFlags propertyFlags;
    private SKPixelGeometry pixelGeometry;
    private SKSurface SetProperties(SKSurfaceProperties? properties)
    {try{if(properties is not null){properties.ThrowIfDisposed();propertyFlags=properties.Flags;pixelGeometry=properties.PixelGeometry;}canvas.TextPixelGeometry=pixelGeometry;return this;}catch{Dispose();throw;}}
    public SKSurfaceProperties SurfaceProperties{get{ThrowIfDisposed();return new(propertyFlags,pixelGeometry);}}
    public static SKSurface Create(SKImageInfo info,SKSurfaceProperties props)=>Create(info,0,props);
    public static SKSurface Create(SKImageInfo info,int rowBytes,SKSurfaceProperties props)=>Create(info,rowBytes)?.SetProperties(props)!;
    public static SKSurface Create(SKPixmap pixmap)=>Create(pixmap,null!);
    public static SKSurface Create(SKPixmap pixmap,SKSurfaceProperties props)
    {ArgumentNullException.ThrowIfNull(pixmap);var pixels=pixmap.RetainPixels();if(pixels is null)return null!;var bitmap=new SKBitmap(pixmap.Info,pixels);try{return new SKSurface(bitmap).SetProperties(props);}catch{bitmap.Dispose();throw;}}
    public static SKSurface Create(SKImageInfo info,IntPtr pixels,SKSurfaceProperties props)=>Create(info,pixels,info.RowBytes,props);
    public static SKSurface Create(SKImageInfo info,IntPtr pixels,int rowBytes,SKSurfaceProperties props)=>Create(info,pixels,rowBytes)?.SetProperties(props)!;
    public static SKSurface Create(SKImageInfo info,IntPtr pixels,int rowBytes,SKSurfaceReleaseDelegate releaseProc,object context,SKSurfaceProperties props)=>Create(info,pixels,rowBytes,releaseProc,context)?.SetProperties(props)!;
    public SKImage Snapshot(SKRectI bounds){ThrowIfDisposed();using var image=Snapshot();return image?.Subset(bounds)!;}
    public void Draw(SKCanvas canvas,float x,float y,SKPaint paint)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(canvas);if(nullSurface)return;if(glCanvas is not null){using var image=Snapshot();canvas.DrawImage(image,x,y,paint);return;}if(gpuContext is not null)this.canvas.Flush();canvas.DrawBitmap(bitmap,x,y,paint);}
}
