using SimdSkia;
using SimdSkia.Cpu;
namespace SkiaSharp;
public partial class SKImage
{
    public SKShader ToRawShader()=>ToRawShader(SKShaderTileMode.Clamp,SKShaderTileMode.Clamp);
    public SKShader ToRawShader(SKShaderTileMode tileX,SKShaderTileMode tileY)=>ToRawShader(tileX,tileY,SKSamplingOptions.Default,SKMatrix.Identity);
    public SKShader ToRawShader(SKShaderTileMode tileX,SKShaderTileMode tileY,SKMatrix localMatrix)=>ToRawShader(tileX,tileY,SKSamplingOptions.Default,localMatrix);
    public SKShader ToRawShader(SKShaderTileMode tileX,SKShaderTileMode tileY,SKSamplingOptions sampling)=>ToRawShader(tileX,tileY,sampling,SKMatrix.Identity);
    public SKShader ToRawShader(SKShaderTileMode tileX,SKShaderTileMode tileY,SKSamplingOptions sampling,SKMatrix localMatrix)=>SKShader.Raster(Buffer,false,tileX,tileY,sampling,localMatrix,true);
    public bool IsValid(GRContext? context)=>IsValid((GRRecordingContext?)context);
    public bool IsValid(GRRecordingContext? context){ThrowIfDisposed();context?.ThrowIfDisposed();return pixels?.IsDisposed!=true;}
    public SKImage Subset(GRRecordingContext? context,SKRectI subset){context?.ThrowIfDisposed();return Subset(subset);}
    public SKImage ApplyImageFilter(SKImageFilter filter,SKRectI subset,SKRectI clipBounds,out SKRectI outSubset,out SKPoint outOffset)
    {var result=ApplyImageFilter(filter,subset,clipBounds,out outSubset,out SKPointI offset);outOffset=new(offset.X,offset.Y);return result;}
    public SKImage ApplyImageFilter(SKImageFilter filter,SKRectI subset,SKRectI clipBounds,out SKRectI outSubset,out SKPointI outOffset)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(filter);outSubset=default;outOffset=default;
        if(ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Convert non-sRGB images before applying this filter graph.");
        using var input=Subset(subset);if(input is null||clipBounds.IsEmpty)return null!;
        using var source=new FilterFrame(input.Buffer.Share(),subset.Left,subset.Top);
        using var filtered=filter.ToCore().Apply(source,Matrix3.Identity,new(clipBounds.Left,clipBounds.Top,clipBounds.Right,clipBounds.Bottom));
        if(filtered.Pixels is not {} output)return null!;
        outSubset=new(0,0,output.Width,output.Height);outOffset=new(filtered.Left,filtered.Top);
        return new(new SKImageInfo(output.Width,output.Height,output.Format==PixelFormat.Bgra8888?SKColorType.Bgra8888:SKColorType.Rgba8888,output.Alpha==AlphaFormat.Straight?SKAlphaType.Unpremul:output.Alpha==AlphaFormat.Opaque?SKAlphaType.Opaque:SKAlphaType.Premul,ColorSpace),output.Share());
    }
    public SKImage ApplyImageFilter(GRContext? context,SKImageFilter filter,SKRectI subset,SKRectI clipBounds,out SKRectI outSubset,out SKPointI outOffset)=>ApplyImageFilter((GRRecordingContext?)context,filter,subset,clipBounds,out outSubset,out outOffset);
    public SKImage ApplyImageFilter(GRRecordingContext? context,SKImageFilter filter,SKRectI subset,SKRectI clipBounds,out SKRectI outSubset,out SKPointI outOffset)
    {if(context is not null)throw new NotSupportedException("GPU image filter graphs have not been implemented; pass null for the explicit raster operation.");return ApplyImageFilter(filter,subset,clipBounds,out outSubset,out outOffset);}
}
