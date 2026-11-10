using SimdSkia.Gpu;
namespace SkiaSharp;
public partial class GRContext
{
    internal GlDevice GlDevice {get{ValidateContext();return deviceOwner as GlDevice??throw new NotSupportedException("This resource import currently requires an OpenGL context; Vulkan/D3D/Metal texture imports remain behavior gaps.");}}
    public static GRContext CreateGl()=>CreateGl((GRContextOptions)null!);
    public static GRContext CreateGl(GRContextOptions options)
    {using var api=GRGlInterface.Create();return api is null?null!:CreateGl(api,options);}
    public static GRContext CreateGl(GRGlInterface backendContext)=>CreateGl(backendContext,null!);
    public static GRContext CreateGl(GRGlInterface backendContext,GRContextOptions? options)
    {
        ArgumentNullException.ThrowIfNull(backendContext);if(!backendContext.Validate())return null!;
        if(options is not null&&(options.DoManualMipmapping||options.RuntimeProgramCacheSize!=256||options.GlyphCacheTextureMaximumBytes!=8*1024*1024||options.BufferMapThreshold!=-1))throw new NotSupportedException("Custom GL program/glyph caches, manual mipmapping and map thresholds are not implemented.");
        var access=backendContext.RetainProcedures();GlDevice? device=null;
        try{device=new(access.Lookup,access.Owner);return new(GRBackend.OpenGL,pixels=>new GlCanvas(device,pixels),device,options);}
        catch{if(device is not null)device.Dispose();else access.Owner?.Dispose();throw;}
    }
}
