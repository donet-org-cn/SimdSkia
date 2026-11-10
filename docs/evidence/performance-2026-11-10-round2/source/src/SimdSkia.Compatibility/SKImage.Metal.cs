using SimdSkia;
using SimdSkia.Gpu;
namespace SkiaSharp;
public partial class SKImage
{
    private static SKImage ImportMetalTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace? colorspace,bool adopted,SKImageTextureReleaseDelegate? releaseProc,object? releaseContext)
    {
        var image=new MetalImage(context.Direct.MetalDevice,texture.MetalInfo.TextureHandle,texture.Width,texture.Height,colorType==SKColorType.Bgra8888,origin==GRSurfaceOrigin.BottomLeft,alpha==SKAlphaType.Opaque?AlphaFormat.Opaque:alpha==SKAlphaType.Unpremul?AlphaFormat.Straight:AlphaFormat.Premultiplied,adopted,releaseProc is null?null:()=>releaseProc(releaseContext!));return FromGlSnapshot(new(texture.Width,texture.Height,colorType,alpha,colorspace),image);
    }
    private SKImage UploadMetalTexture(GRContext context)
    {
        var device=context.MetalDevice;if(gpuImage is MetalImage existing&&ReferenceEquals(existing.Device,device))return this;nint texture=device.CreateTexture(Width,Height,ColorType==SKColorType.Bgra8888);MetalImage? image=null;
        try{image=new(device,texture,Width,Height,ColorType==SKColorType.Bgra8888,false,Buffer.Alpha,true);texture=0;image.Write(Buffer);return FromGlSnapshot(info,image);}catch{image?.Dispose();throw;}finally{MetalApi.Release(ref texture);}
    }
}
