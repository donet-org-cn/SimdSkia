using SimdSkia;
using SimdSkia.Gpu;
namespace SkiaSharp;
public partial class SKImage
{
    internal GlImage? TextureSource {get{ThrowIfDisposed();return gpuImage as GlImage;}}
    private SKImage(SKImageInfo info,IGpuImage texture)
    {
        this.info=SKColorSpace.RetainInfo(info);gpuImage=texture;
        var lease=new ResourceLease(texture);
        if(texture is GlImage gl)gl.Lease=lease;
        generated=new(()=>{var pixels=ReadTexture(texture,info);try{pixels.RetainResource(lease);return pixels;}catch{pixels.Dispose();throw;}},lease);
    }
    private static PixelBuffer ReadTexture(IGpuImage texture,SKImageInfo info)
    {
        var rgba=texture.ReadPixels();var format=info.ColorType==SKColorType.Rgba8888?PixelFormat.Rgba8888:PixelFormat.Bgra8888;if(rgba.Format==format)return rgba;
        try{var result=PixelBuffer.Rent(info.Width,info.Height,format,rgba.Alpha);try{for(int y=0;y<info.Height;y++)SimdSkia.Cpu.PixelTransferKernel.SwapRedBlue(rgba.GetRowSpan(y),result.GetRowSpan(y),SimdSkia.Cpu.CpuOptions.EnableSimd);return result;}catch{result.Dispose();throw;}}
        finally{rgba.Dispose();}
    }
    internal static SKImage FromGlSnapshot(SKImageInfo info,IGpuImage texture)
    {try{return new(info,texture);}catch{texture.Dispose();throw;}}
    private static SKImage ImportTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace? colorspace,bool adopted,SKImageTextureReleaseDelegate? releaseProc,object? releaseContext)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(texture);context.Direct.ValidateContext();
        if(!Enum.IsDefined(origin))throw new ArgumentOutOfRangeException(nameof(origin));
        if(colorType is not (SKColorType.Rgba8888 or SKColorType.Bgra8888)||alpha is not (SKAlphaType.Premul or SKAlphaType.Unpremul or SKAlphaType.Opaque))throw new NotSupportedException("GL texture images require RGBA/BGRA 8888 and a defined alpha representation.");
        if(colorspace is not null&&!colorspace.IsSrgb)throw new NotSupportedException("Non-sRGB imported textures require a destination color pipeline.");
        if(context.Backend!=texture.Backend)throw new ArgumentException("Texture and context backends differ.",nameof(texture));
        if(context.Backend==GRBackend.Vulkan)return ImportVulkanTexture(context,texture,origin,colorType,alpha,colorspace,adopted,releaseProc,releaseContext);
        if(context.Backend==GRBackend.Direct3D){if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();return ImportD3DTexture(context,texture,origin,colorType,alpha,colorspace,adopted,releaseProc,releaseContext);}
        if(context.Backend==GRBackend.Metal)return ImportMetalTexture(context,texture,origin,colorType,alpha,colorspace,adopted,releaseProc,releaseContext);
        var device=context.Direct.GlDevice;var gl=texture.GetGlTextureInfo();
        if(colorType==SKColorType.Bgra8888&&gl.Format is 0x1908 or 0x8058||colorType==SKColorType.Rgba8888&&gl.Format==0x80E1)return null!;
        if(gl.Protected||gl.Target!=0x0DE1||gl.Format is not (0 or 0x1908 or 0x8058 or 0x80E1))throw new NotSupportedException("Only unprotected renderable GL_TEXTURE_2D RGBA/BGRA8888 textures can be imported.");
        var resource=GlImage.Import(device,gl.Id,texture.Width,texture.Height,origin==GRSurfaceOrigin.BottomLeft,alpha==SKAlphaType.Opaque?AlphaFormat.Opaque:alpha==SKAlphaType.Unpremul?AlphaFormat.Straight:AlphaFormat.Premultiplied,adopted,texture.HasMipMaps,releaseProc is null?null:()=>releaseProc(releaseContext!));
        return FromGlSnapshot(new(texture.Width,texture.Height,colorType,alpha,colorspace),resource);
    }
    private SKImage UploadTexture(GRContext context,bool mipmapped,bool budgeted)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(context);context.ValidateContext();
        if(info.ColorSpace is {} sourceSpace&&!sourceSpace.IsSrgb)throw new NotSupportedException("Convert non-sRGB pixels before uploading a GPU texture.");
        if(info.ColorType is not (SKColorType.Rgba8888 or SKColorType.Bgra8888))throw new NotSupportedException("A8 GPU image upload requires a separate sampling shader.");
        if(context.Backend==GRBackend.Direct3D){if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();if(mipmapped)throw new NotSupportedException("D3D12 mipmapped uploads require a complete mip-chain generator.");return UploadD3DTexture(context);}
        if(context.Backend==GRBackend.Metal){if(mipmapped)throw new NotSupportedException("Metal mipmapped uploads require a complete mip-chain generator.");return UploadMetalTexture(context);}
        if(context.Backend==GRBackend.Vulkan){if(mipmapped)throw new NotSupportedException("Vulkan mipmapped uploads require a complete mip-chain generator.");if(gpuImage is VulkanImage current&&ReferenceEquals(current.Device,context.VulkanTransfer))return this;return FromGlSnapshot(info,context.VulkanTransfer.Create(Buffer));}
        var device=context.GlDevice;
        if(gpuImage is GlImage old&&ReferenceEquals(old.Device,device)&&(!mipmapped||old.Mipmapped))return this;
        if(info.ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Convert non-sRGB pixels before uploading a GL texture.");
        if(info.ColorType is not (SKColorType.Rgba8888 or SKColorType.Bgra8888))throw new NotSupportedException("A8 GPU image upload requires a separate sampling shader.");
        uint texture=device.Upload(Buffer,mipmapped);var resource=new GlImage(device,texture,Width,Height,false,Buffer.Alpha==AlphaFormat.Opaque?AlphaFormat.Opaque:AlphaFormat.Premultiplied,true,mipmapped);
        return FromGlSnapshot(info.WithAlphaType(Buffer.Alpha==AlphaFormat.Opaque?SKAlphaType.Opaque:SKAlphaType.Premul),resource);
    }
    internal static SKImage ContextSnapshot(SKImageInfo info,PixelBuffer pixels,GRContext context)
    {using var raster=new SKImage(info,pixels.Share());return raster.UploadTexture(context,false,true);}
}
