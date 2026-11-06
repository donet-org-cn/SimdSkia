using SimdSkia;
using SimdSkia.Gpu.Direct2D;
using System.Runtime.Versioning;
namespace SkiaSharp;
public partial class SKImage
{
    [SupportedOSPlatform("windows")]
    private static SKImage ImportD3DTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace? colorspace,bool adopted,SKImageTextureReleaseDelegate? releaseProc,object? releaseContext)
    {
        var description=texture.D3DInfo;if(description.Protected||description.SampleCount!=1||description.LevelCount!=1)throw new NotSupportedException("D3D12 image imports require unprotected, single-sample, single-level resources.");
        if((description.Format==87)!=(colorType==SKColorType.Bgra8888))return null!;
        var image=new D3D12Image(context.Direct.D3DDevice,description.Resource,description.ResourceState,description.Format,texture.Width,texture.Height,origin==GRSurfaceOrigin.BottomLeft,alpha==SKAlphaType.Opaque?AlphaFormat.Opaque:alpha==SKAlphaType.Unpremul?AlphaFormat.Straight:AlphaFormat.Premultiplied,adopted,false,releaseProc is null?null:()=>releaseProc(releaseContext!));return FromGlSnapshot(new(texture.Width,texture.Height,colorType,alpha,colorspace),image);
    }
    [SupportedOSPlatform("windows")]
    private SKImage UploadD3DTexture(GRContext context)
    {
        var device=context.D3DDevice;if(gpuImage is D3D12Image existing&&ReferenceEquals(existing.Device,device))return this;
        uint format=ColorType==SKColorType.Bgra8888?87u:28u;nint resource=device.CreateTexture(Width,Height,format);D3D12Image? image=null;
        try{image=new(device,resource,0,format,Width,Height,false,Buffer.Alpha,true,false);resource=0;image.Write(Buffer);return FromGlSnapshot(info,image);}catch{image?.Dispose();throw;}finally{if(resource!=0)D2DNative.Release(ref resource);}
    }
}
