using SimdSkia.Gpu;
using SkiaSharp;

namespace SimdSkia;

/// <summary>SimdSkia Vulkan factory, separate from unsupported Skia GR native-handle APIs.</summary>
public sealed class VulkanSurface : IDisposable
{
    private readonly PixelBuffer pixels;
    private readonly VulkanCanvas backend;
    private bool disposed;
    public SKCanvas Canvas {get;}
    public string DeviceName=>backend.DeviceName;
    public bool IsSoftwareDevice=>backend.IsSoftwareDevice;
    public bool IsFaulted=>backend.IsFaulted;
    public int CompletedBatches=>backend.CompletedBatches;
    public int CachedTextures=>backend.CachedTextures;
    public double LastSubmitMilliseconds=>backend.LastSubmitMilliseconds;
    public double LastCompletionMilliseconds=>backend.LastCompletionMilliseconds;
    private VulkanSurface(int width,int height)
    {
        pixels=new(width,height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);
        try{backend=new(pixels);Canvas=new(pixels,backend);}catch{pixels.Dispose();throw;}
    }
    public static VulkanSurface Create(int width,int height)=>new(width,height);
    public SKImage Snapshot(){ObjectDisposedException.ThrowIf(disposed,this);Canvas.Flush();return new(new(pixels.Width,pixels.Height,SKColorType.Rgba8888,SKAlphaType.Premul),pixels.Clone());}
    /// <summary>Flushes pending GPU work and returns a CPU copy of the target pixels.</summary>
    public SKBitmap CopyBitmap(){ObjectDisposedException.ThrowIf(disposed,this);Canvas.Flush();return new(new(pixels.Width,pixels.Height,SKColorType.Rgba8888,SKAlphaType.Premul),pixels.Clone());}
    public void Flush(){ObjectDisposedException.ThrowIf(disposed,this);Canvas.Flush();}
    public void Dispose(){if(disposed)return;Canvas.Dispose();pixels.Dispose();disposed=true;}
}
