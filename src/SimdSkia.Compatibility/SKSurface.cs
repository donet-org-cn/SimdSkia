namespace SkiaSharp;

public class SKSurface : SKObject
{
    private readonly SKBitmap bitmap;
    private readonly SKCanvas canvas;
    private SKSurface(SKBitmap bitmap) { this.bitmap=bitmap; canvas=new SKCanvas(bitmap); }
    public static SKSurface? Create(SKImageInfo info) => Create(info,0);
    public static SKSurface? Create(SKImageInfo info,int rowBytes)
    {
        var bitmap=new SKBitmap();
        try
        {
            if (!bitmap.TryAllocPixels(info,rowBytes)) { bitmap.Dispose(); return null; }
            return new SKSurface(bitmap);
        }
        catch { bitmap.Dispose(); throw; }
    }
    public static SKSurface Create(SKImageInfo info,IntPtr pixels) => throw new NotSupportedException("Raster direct surfaces using external pointers are not implemented.");
    public static SKSurface Create(SKImageInfo info,IntPtr pixels,int rowBytes) => throw new NotSupportedException("Raster direct surfaces using external pointers are not implemented.");
    public SKCanvas Canvas { get { ThrowIfDisposed(); return canvas; } }
    public SKImage Snapshot() { ThrowIfDisposed(); return SKImage.FromBitmap(bitmap); }
    public void Flush() { ThrowIfDisposed(); canvas.Flush(); }
    protected override void Dispose(bool disposing) { canvas.Dispose(); bitmap.Dispose(); }
}
