using SimdSkia.Gpu;
namespace SkiaSharp;
public partial class GRContext
{
    internal MetalDevice MetalDevice {get{ValidateContext();return deviceOwner as MetalDevice??throw new InvalidOperationException("This is not a Metal context.");}}
    public static GRContext CreateMetal(GRMtlBackendContext backendContext)=>CreateMetal(backendContext,null!);
    public static GRContext CreateMetal(GRMtlBackendContext backendContext,GRContextOptions? options)
    {
        ArgumentNullException.ThrowIfNull(backendContext);if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Metal contexts require macOS.");
        if(options is not null&&(options.DoManualMipmapping||options.RuntimeProgramCacheSize!=256||options.GlyphCacheTextureMaximumBytes!=8*1024*1024||options.BufferMapThreshold!=-1))throw new NotSupportedException("Custom Metal program/glyph caches and mapping options are not implemented.");
        var device=new MetalDevice(backendContext.DeviceHandle,backendContext.QueueHandle);
        try{return new(GRBackend.Metal,pixels=>new MetalCanvas(device,pixels),device,options);}catch{device.Dispose();throw;}
    }
}
