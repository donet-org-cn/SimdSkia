// Forwarding signatures from the fixed SkiaSharp 3.119.4 public metadata.
namespace SkiaSharp;
public partial class SKImage
{
    public static SKImage FromAdoptedTexture(GRContext context,GRBackendTexture texture,SKColorType colorType)=>ImportTexture(context,texture,GRSurfaceOrigin.BottomLeft,colorType,SKAlphaType.Premul,null,true,null,null);
    public static SKImage FromAdoptedTexture(GRContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType)=>ImportTexture(context,texture,origin,colorType,SKAlphaType.Premul,null,true,null,null);
    public static SKImage FromAdoptedTexture(GRContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha)=>ImportTexture(context,texture,origin,colorType,alpha,null,true,null,null);
    public static SKImage FromAdoptedTexture(GRContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace colorspace)=>ImportTexture(context,texture,origin,colorType,alpha,colorspace,true,null,null);
    public static SKImage FromAdoptedTexture(GRRecordingContext context,GRBackendTexture texture,SKColorType colorType)=>ImportTexture(context,texture,GRSurfaceOrigin.BottomLeft,colorType,SKAlphaType.Premul,null,true,null,null);
    public static SKImage FromAdoptedTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType)=>ImportTexture(context,texture,origin,colorType,SKAlphaType.Premul,null,true,null,null);
    public static SKImage FromAdoptedTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha)=>ImportTexture(context,texture,origin,colorType,alpha,null,true,null,null);
    public static SKImage FromAdoptedTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace colorspace)=>ImportTexture(context,texture,origin,colorType,alpha,colorspace,true,null,null);
    public static SKImage FromTexture(GRContext context,GRBackendTexture texture,SKColorType colorType)=>ImportTexture(context,texture,GRSurfaceOrigin.BottomLeft,colorType,SKAlphaType.Premul,null,false,null,null);
    public static SKImage FromTexture(GRContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType)=>ImportTexture(context,texture,origin,colorType,SKAlphaType.Premul,null,false,null,null);
    public static SKImage FromTexture(GRContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha)=>ImportTexture(context,texture,origin,colorType,alpha,null,false,null,null);
    public static SKImage FromTexture(GRContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace colorspace)=>ImportTexture(context,texture,origin,colorType,alpha,colorspace,false,null,null);
    public static SKImage FromTexture(GRContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace colorspace,SKImageTextureReleaseDelegate releaseProc)=>ImportTexture(context,texture,origin,colorType,alpha,colorspace,false,releaseProc,null);
    public static SKImage FromTexture(GRContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace colorspace,SKImageTextureReleaseDelegate releaseProc,object releaseContext)=>ImportTexture(context,texture,origin,colorType,alpha,colorspace,false,releaseProc,releaseContext);
    public static SKImage FromTexture(GRRecordingContext context,GRBackendTexture texture,SKColorType colorType)=>ImportTexture(context,texture,GRSurfaceOrigin.BottomLeft,colorType,SKAlphaType.Premul,null,false,null,null);
    public static SKImage FromTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType)=>ImportTexture(context,texture,origin,colorType,SKAlphaType.Premul,null,false,null,null);
    public static SKImage FromTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha)=>ImportTexture(context,texture,origin,colorType,alpha,null,false,null,null);
    public static SKImage FromTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace colorspace)=>ImportTexture(context,texture,origin,colorType,alpha,colorspace,false,null,null);
    public static SKImage FromTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace colorspace,SKImageTextureReleaseDelegate releaseProc)=>ImportTexture(context,texture,origin,colorType,alpha,colorspace,false,releaseProc,null);
    public static SKImage FromTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace colorspace,SKImageTextureReleaseDelegate releaseProc,object releaseContext)=>ImportTexture(context,texture,origin,colorType,alpha,colorspace,false,releaseProc,releaseContext);
    public SKImage ToTextureImage(GRContext context)=>UploadTexture(context,false,false);
    public SKImage ToTextureImage(GRContext context,bool mipmapped)=>UploadTexture(context,mipmapped,false);
    public SKImage ToTextureImage(GRContext context,bool mipmapped,bool budgeted)=>UploadTexture(context,mipmapped,budgeted);
}
