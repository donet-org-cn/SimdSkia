using SimdSkia;
namespace SkiaSharp;
public partial class SKShader
{
    internal static SKShader Raster(PixelBuffer source,SKColorSpace? colorSpace,bool copy,SKShaderTileMode tileX,SKShaderTileMode tileY,SKSamplingOptions sampling,SKMatrix localMatrix)
    {
        if(colorSpace is not null&&!colorSpace.IsSrgb)throw new NotSupportedException("Non-sRGB image shaders require conversion before sampling.");
        return Raster(source,copy,tileX,tileY,sampling,localMatrix);
    }
    internal ShaderResources? RetainResources(){ThrowIfDisposed();return resources?.Retain();}
    internal static SKShader Raster(PixelBuffer source,bool copy,SKShaderTileMode tileX,SKShaderTileMode tileY,SKSamplingOptions sampling,SKMatrix localMatrix,bool raw=false)
    {
        if(!Enum.IsDefined(tileX)||!Enum.IsDefined(tileY))throw new ArgumentOutOfRangeException(nameof(tileX));
        if(raw&&sampling.UseCubic)throw new NotSupportedException("Raw image shaders do not support cubic sampling.");
        if(!localMatrix.ToCore().TryInvert(out var inverse))return null!;
        if(sampling.IsAniso||sampling.Mipmap!=SKMipmapMode.None)throw new NotSupportedException("Image shaders do not yet implement mipmaps or anisotropic sampling.");
        if(!Enum.IsDefined(sampling.Filter))throw new ArgumentOutOfRangeException(nameof(sampling));
        if(sampling.UseCubic&&(!float.IsFinite(sampling.Cubic.B)||!float.IsFinite(sampling.Cubic.C)||sampling.Cubic.B<0||sampling.Cubic.C<0||sampling.Cubic.B>1||sampling.Cubic.C>1))throw new ArgumentOutOfRangeException(nameof(sampling));
        var pixels=copy?source.Clone():source.Share();
        try
        {
            IColorShader shader=new ImageColorShader(pixels,(int)tileX,(int)tileY,sampling.UseCubic?ImageSampling.Mitchell:sampling.Filter==SKFilterMode.Linear?ImageSampling.Bilinear:ImageSampling.Nearest,sampling.Cubic.B,sampling.Cubic.C,raw);
            if(source.Format==PixelFormat.Alpha8&&!raw)shader=new AlphaImageShader((IPremultipliedColorShader)shader);
            return new(localMatrix.IsIdentity?shader:shader is IContextualColorShader contextual?new ContextualTransformedShader(contextual,localMatrix.ToCore(),inverse):new PremultipliedTransformedShader((IPremultipliedColorShader)shader,inverse)){resources=new(pixels)};
        }
        catch{pixels.Dispose();throw;}
    }
    protected override void Dispose(bool disposing){var owner=resources;resources=null;gradient=null!;owner?.Dispose();}
}
