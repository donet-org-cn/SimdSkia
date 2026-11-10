using SimdSkia;
namespace SkiaSharp;
public partial class SKImage
{
    public static SKImage FromPicture(SKPicture picture,SKSizeI dimensions)=>PictureImage(picture,dimensions,SKMatrix.Identity,null);
    public static SKImage FromPicture(SKPicture picture,SKSizeI dimensions,SKMatrix matrix)=>PictureImage(picture,dimensions,matrix,null);
    public static SKImage FromPicture(SKPicture picture,SKSizeI dimensions,SKPaint paint)=>PictureImage(picture,dimensions,SKMatrix.Identity,paint);
    public static SKImage FromPicture(SKPicture picture,SKSizeI dimensions,SKMatrix matrix,SKPaint paint)=>PictureImage(picture,dimensions,matrix,paint);
    private static SKImage PictureImage(SKPicture picture,SKSizeI dimensions,SKMatrix matrix,SKPaint? paint)
    {
        ArgumentNullException.ThrowIfNull(picture);picture.ThrowIfDisposed();paint?.ThrowIfDisposed();
        if(dimensions.Width<=0||dimensions.Height<=0)return null!;
        if((long)dimensions.Width*dimensions.Height>16_777_216)throw new NotSupportedException("Picture images exceed the 16 megapixel raster budget.");
        if(!matrix.ToCore().IsFinite||!matrix.ToCore().IsAffine)throw new NotSupportedException("Picture images require finite affine transforms.");
        var recorded=picture.Share();SKPaint? captured=null;SKColorSpace? space=null;
        try
        {
            captured=paint?.Clone();var heldPaint=captured;space=SKColorSpace.CreateSrgb();var info=new SKImageInfo(dimensions.Width,dimensions.Height,SKImageInfo.PlatformColorType,SKAlphaType.Premul,space);
            return new(info,()=>{using var bitmap=new SKBitmap(info);using(var canvas=new SKCanvas(bitmap))canvas.DrawPicture(recorded,matrix,heldPaint);return bitmap.Buffer.Share();},captured is null?new ShaderResources(recorded,space):new ShaderResources(recorded,captured,space));
        }
        catch{space?.Dispose();captured?.Dispose();recorded.Dispose();throw;}
    }
}
