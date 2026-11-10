using SimdSkia.Gpu;
namespace SkiaSharp;
public partial class SKSurface
{
    private static SKSurface ImportSurface(GRRecordingContext context,GRBackendTexture? texture,GRBackendRenderTarget? renderTarget,GRSurfaceOrigin origin,int samples,SKColorType colorType,SKColorSpace? colorspace,SKSurfaceProperties? props)
    {
        ArgumentNullException.ThrowIfNull(context);if(texture is null&&renderTarget is null)throw new ArgumentNullException(nameof(renderTarget));
        var direct=context.Direct;direct.ValidateContext();if(!Enum.IsDefined(origin))throw new ArgumentOutOfRangeException(nameof(origin));
        if(colorType is not (SKColorType.Rgba8888 or SKColorType.Bgra8888))throw new NotSupportedException("Imported GPU surfaces require RGBA/BGRA8888 color.");
        if(colorspace is not null&&!colorspace.IsSrgb)throw new NotSupportedException("Non-sRGB GPU targets require a destination color pipeline.");
        if(samples is <0 or >1||(renderTarget?.SampleCount??1)>1)throw new NotSupportedException("Imported multisample attachments require a resolve pipeline.");
        if((texture?.Backend??renderTarget!.Backend)!=context.Backend)throw new ArgumentException("Resource and context backends differ.");
        int width=texture?.Width??renderTarget!.Width,height=texture?.Height??renderTarget!.Height;if(width<=0||height<=0)return null!;
        if(context.Backend==GRBackend.Vulkan)return ImportVulkanSurface(direct,texture,renderTarget,origin,colorType,colorspace,props,width,height);
        if(context.Backend==GRBackend.Direct3D){if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();return ImportD3DSurface(direct,texture,renderTarget,origin,colorType,colorspace,props,width,height);}
        if(context.Backend==GRBackend.Metal)return ImportMetalSurface(direct,texture,renderTarget,origin,colorType,colorspace,props,width,height);
        var device=direct.GlDevice;uint id=0;uint? framebuffer=null;
        if(texture is not null){var gl=texture.GetGlTextureInfo();if(colorType==SKColorType.Bgra8888&&gl.Format is 0x1908 or 0x8058||colorType==SKColorType.Rgba8888&&gl.Format==0x80E1)return null!;if(gl.Protected||gl.Target!=0x0DE1||gl.Id==0||gl.Format is not (0 or 0x1908 or 0x8058 or 0x80E1))throw new NotSupportedException("Imported surfaces require an unprotected renderable GL_TEXTURE_2D RGBA/BGRA8888 texture.");id=gl.Id;}
        else{var gl=renderTarget!.GetGlFramebufferInfo();if(gl.Protected||gl.Format is not (0 or 0x1908 or 0x8058 or 0x80E1))throw new NotSupportedException("Imported framebuffer format is not unprotected RGBA/BGRA8888.");framebuffer=gl.FramebufferObjectId;}
        var bitmap=new SKBitmap(new SKImageInfo(width,height,colorType,SKAlphaType.Premul,colorspace));GlCanvas? canvas=null;
        try{canvas=new(device,bitmap.Buffer,id,framebuffer,origin==GRSurfaceOrigin.BottomLeft);canvas.ConfigureResourceCache(direct.GetResourceCacheLimit(),true);direct.Register(canvas);return new SKSurface(bitmap,direct,canvas).SetProperties(props);}
        catch{canvas?.Dispose();bitmap.Dispose();throw;}
    }
    private static SKSurface ImportVulkanSurface(GRContext context,GRBackendTexture? texture,GRBackendRenderTarget? target,GRSurfaceOrigin origin,SKColorType colorType,SKColorSpace? colorspace,SKSurfaceProperties? props,int width,int height)
    {
        var native=texture?.VulkanInfo??target!.VulkanInfo;var description=SKImage.VulkanDescription(native,false);if((native.Format==44)!=(colorType==SKColorType.Bgra8888))return null!;
        if((native.ImageUsageFlags&3)!=3||native.ImageLayout==0)throw new NotSupportedException("An imported Vulkan surface requires defined pixels and TRANSFER_SRC/DST usage.");
        var image=context.VulkanTransfer.Import(description,width,height,origin==GRSurfaceOrigin.BottomLeft,SimdSkia.AlphaFormat.Premultiplied,false);var bitmap=new SKBitmap(new SKImageInfo(width,height,colorType,SKAlphaType.Premul,colorspace));VulkanImportedCanvas? canvas=null;
        try{canvas=new(bitmap.Buffer,image);canvas.ConfigureResourceCache(context.GetResourceCacheLimit(),true);context.Register(canvas);return new SKSurface(bitmap,context,canvas).SetProperties(props);}catch{canvas?.Dispose();image.Dispose();bitmap.Dispose();throw;}
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static SKSurface ImportD3DSurface(GRContext context,GRBackendTexture? texture,GRBackendRenderTarget? target,GRSurfaceOrigin origin,SKColorType colorType,SKColorSpace? colorspace,SKSurfaceProperties? props,int width,int height)
    {
        var description=texture?.D3DInfo??target!.D3DInfo;if(description.Protected||description.SampleCount!=1||description.LevelCount!=1)throw new NotSupportedException("D3D12 targets require an unprotected single-sample, single-level resource.");if((description.Format==87)!=(colorType==SKColorType.Bgra8888))return null!;
        var image=new SimdSkia.Gpu.Direct2D.D3D12Image(context.D3DDevice,description.Resource,description.ResourceState,description.Format,width,height,origin==GRSurfaceOrigin.BottomLeft,SimdSkia.AlphaFormat.Premultiplied,false,true);var bitmap=new SKBitmap(new SKImageInfo(width,height,colorType,SKAlphaType.Premul,colorspace));SimdSkia.Gpu.Direct2D.D3D12ImportedCanvas? canvas=null;
        try{canvas=new(bitmap.Buffer,image);canvas.ConfigureResourceCache(context.GetResourceCacheLimit(),true);context.Register(canvas);return new SKSurface(bitmap,context,canvas).SetProperties(props);}catch{canvas?.Dispose();image.Dispose();bitmap.Dispose();throw;}
    }
    private static SKSurface ImportMetalSurface(GRContext context,GRBackendTexture? texture,GRBackendRenderTarget? target,GRSurfaceOrigin origin,SKColorType colorType,SKColorSpace? colorspace,SKSurfaceProperties? props,int width,int height)
    {
        var native=texture?.MetalInfo??target!.MetalInfo;var image=new MetalImage(context.MetalDevice,native.TextureHandle,width,height,colorType==SKColorType.Bgra8888,origin==GRSurfaceOrigin.BottomLeft,SimdSkia.AlphaFormat.Premultiplied,false);var bitmap=new SKBitmap(new SKImageInfo(width,height,colorType,SKAlphaType.Premul,colorspace));MetalImportedCanvas? canvas=null;
        try{canvas=new(bitmap.Buffer,image);canvas.ConfigureResourceCache(context.GetResourceCacheLimit(),true);context.Register(canvas);return new SKSurface(bitmap,context,canvas).SetProperties(props);}catch{canvas?.Dispose();image.Dispose();bitmap.Dispose();throw;}
    }
}
