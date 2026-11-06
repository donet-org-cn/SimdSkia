using SimdSkia.Cpu;
namespace SkiaSharp;
public unsafe partial class SKImageFilter
{
    public static SKImageFilter CreateMagnifier(SKRect lensBounds,float zoomAmount,float inset,SKSamplingOptions sampling)=>CreateMagnifier(lensBounds,zoomAmount,inset,sampling,null,null);
    public static SKImageFilter CreateMagnifier(SKRect lensBounds,float zoomAmount,float inset,SKSamplingOptions sampling,SKImageFilter? input)=>CreateMagnifier(lensBounds,zoomAmount,inset,sampling,input,null);
    public static SKImageFilter CreateMagnifier(SKRect lensBounds,float zoomAmount,float inset,SKSamplingOptions sampling,SKImageFilter? input,SKRect cropRect)=>CreateMagnifier(lensBounds,zoomAmount,inset,sampling,input,&cropRect);
    private static SKImageFilter CreateMagnifier(SKRect lensBounds,float zoomAmount,float inset,SKSamplingOptions sampling,SKImageFilter? input,SKRect* cropRect)
    {
        if(!lensBounds.ToCore().IsFinite||lensBounds.IsEmpty||!float.IsFinite(zoomAmount)||zoomAmount<=0||!float.IsFinite(inset)||inset<0)return null!;
        if(zoomAmount<=1&&cropRect is null)return input is null?null!:Own(input.ToCore(),input);
        return Own(RasterImageFilter.Magnifier(lensBounds.ToCore(),zoomAmount,inset,Sampling(sampling),Node(input),Crop(cropRect)),input);
    }
}
