using SimdSkia.Gpu;
using System.Runtime.Versioning;
namespace SimdSkia.Gpu.Direct2D;

// Correctness-first external target bridge. The renderer is actual Direct2D
// through D3D11On12, and every completed frame is copied to the caller's D3D12
// resource with its declared state restored and a completed queue fence.
[SupportedOSPlatform("windows")]
internal sealed class D3D12ImportedCanvas:IGpuCanvas
{
    private readonly PixelBuffer pixels;
    private readonly D3D12Image image;
    private readonly Direct2DCanvas canvas;
    private bool dirty,disposed,abandoned;
    internal D3D12ImportedCanvas(PixelBuffer pixels,D3D12Image image)
    {
        this.pixels=pixels.Share();this.image=image;
        try{using var initial=image.ReadPixels();for(int y=0;y<pixels.Height;y++)initial.GetRowSpan(y).CopyTo(pixels.GetRowSpan(y));canvas=Direct2DCanvas.FromBorrowedDevice(pixels,image.Device.DrawingDevice);}
        catch{this.pixels.Dispose();image.Dispose();throw;}
    }
    public bool ContextAbandoned=>abandoned||canvas.IsFaulted;
    public bool ResourceDisposed=>disposed;
    public int MaximumImageSize=>16384;
    public (int Count,long Bytes,long Purgeable) ResourceUsage=>disposed||abandoned?default:canvas.ResourceUsage;
    public void Execute(in DrawingCommand command){if(abandoned)throw new InvalidOperationException("The D3D12 target is abandoned.");canvas.Execute(command);dirty=true;}
    public void Flush(){if(abandoned)throw new InvalidOperationException("The D3D12 target is abandoned.");canvas.Flush();if(dirty){image.Write(pixels);dirty=false;}}
    public void ConfigureResourceCache(long limit,bool pathMasks)=>canvas.ConfigureCache(limit,pathMasks);
    public void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)=>canvas.PurgeCaches(bytesToPurge,minimumAgeMilliseconds,scratchOnly);
    public void Abandon(bool releaseResources){if(disposed||abandoned)return;if(releaseResources){Flush();canvas.Dispose();}else canvas.DropLostDeviceResources();abandoned=true;}
    public void Dispose(){if(disposed)return;try{if(!abandoned&&!canvas.IsFaulted)Flush();}finally{canvas.Dispose();image.Dispose();pixels.Dispose();disposed=true;}}
}
