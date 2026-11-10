using System.Runtime.Versioning;
using SimdSkia;
using SimdSkia.Gpu;
using SimdSkia.Gpu.Direct2D;
namespace SkiaSharp;
public partial class GRContext
{
    [SupportedOSPlatform("windows")]
    internal D3D12Device D3DDevice {get{ValidateContext();return deviceOwner as D3D12Device??throw new InvalidOperationException("This is not a D3D12 context.");}}
    public static GRContext CreateDirect3D(GRD3DBackendContext backendContext)=>CreateDirect3D(backendContext,null!);
    public static GRContext CreateDirect3D(GRD3DBackendContext backendContext,GRContextOptions? options)
    {
        ArgumentNullException.ThrowIfNull(backendContext);if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("D3D12 contexts require Windows.");
        if(backendContext.ProtectedContext)throw new NotSupportedException("Protected D3D12 contexts are not supported.");
        if(options is not null&&(options.DoManualMipmapping||options.RuntimeProgramCacheSize!=256||options.GlyphCacheTextureMaximumBytes!=8*1024*1024||options.BufferMapThreshold!=-1))throw new NotSupportedException("Custom D3D12 program/glyph caches and mapping options are not implemented.");
        if(backendContext.Adapter==IntPtr.Zero)return null!;
        var device=new D3D12Device(backendContext.Device,backendContext.Queue,backendContext.Adapter);
        try{return new(GRBackend.Direct3D,pixels=>{if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();return new D3DCanvas(Direct2DCanvas.FromBorrowedDevice(pixels,device.DrawingDevice));},device,options);}catch{device.Dispose();throw;}
    }
    [SupportedOSPlatform("windows")]
    private sealed class D3DCanvas(Direct2DCanvas canvas):IGpuCanvas
    {
        private bool disposed,abandoned;
        public bool ResourceDisposed=>disposed;
        public bool ContextAbandoned=>abandoned||canvas.IsFaulted;
        public int MaximumImageSize=>16384;
        public (int Count,long Bytes,long Purgeable) ResourceUsage=>disposed||abandoned?default:canvas.ResourceUsage;
        public void Execute(in DrawingCommand command){if(abandoned)throw new InvalidOperationException("The D3D12 context has been abandoned.");canvas.Execute(command);}
        public void Flush(){if(abandoned)throw new InvalidOperationException("The D3D12 context has been abandoned.");canvas.Flush();}
        public void ConfigureResourceCache(long limit,bool pathMasks)=>canvas.ConfigureCache(limit,pathMasks);
        public void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)=>canvas.PurgeCaches(bytesToPurge,minimumAgeMilliseconds,scratchOnly);
        public void Abandon(bool releaseResources){if(abandoned||disposed)return;if(releaseResources)canvas.Dispose();else canvas.DropLostDeviceResources();abandoned=true;}
        public void Dispose(){if(disposed)return;canvas.Dispose();disposed=true;}
    }
}
