namespace SkiaSharp;

public partial class SKSurface : SKObject
{
    private readonly SKBitmap bitmap;
    private readonly SKCanvas canvas;
    private GRRecordingContext? gpuContext;
    private SimdSkia.Gpu.GlCanvas? glCanvas;
    private SimdSkia.Gpu.VulkanCanvas? vulkanCanvas;
    private SimdSkia.Gpu.VulkanImportedCanvas? importedVulkanCanvas;
    private bool nullSurface;
    private SKSurface(int width,int height){bitmap=new();canvas=new SKNoDrawCanvas(width,height){OwnerSurface=this};nullSurface=true;}
    public static SKSurface CreateNull(int width,int height)=>width<=0||height<=0?null!:new SKSurface(width,height);
    private SKSurface(SKBitmap bitmap) { this.bitmap=bitmap; canvas=new SKCanvas(bitmap){OwnerSurface=this}; }
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
    public static SKSurface Create(SKImageInfo info,IntPtr pixels) => Create(info,pixels,info.RowBytes);
    public static SKSurface Create(SKImageInfo info,IntPtr pixels,int rowBytes) => Create(info,pixels,rowBytes,null,null!);
    public static SKSurface Create(SKImageInfo info,IntPtr pixels,int rowBytes,SKSurfaceReleaseDelegate? releaseProc,object context)
    {var bitmap=new SKBitmap();try{if(!bitmap.InstallPixels(info,pixels,rowBytes,releaseProc is null?null:(address,_)=>releaseProc(address,context))){bitmap.Dispose();return null!;}return new(bitmap);}catch{bitmap.Dispose();throw;}}
    public SKCanvas Canvas { get { ThrowIfDisposed(); return canvas; } }
    public SKImage Snapshot() { ThrowIfDisposed();if(nullSurface)return null!;if(glCanvas is not null)return SKImage.FromGlSnapshot(bitmap.Info,glCanvas.Snapshot());if(gpuContext is not null){canvas.Flush();return SKImage.ContextSnapshot(bitmap.Info,bitmap.Buffer,gpuContext.Direct);}return SKImage.FromBitmap(bitmap); }
    public void Flush() { ThrowIfDisposed(); canvas.Flush(); }
    protected override void Dispose(bool disposing) { canvas.Dispose(); bitmap.Dispose(); }
}
