using SimdSkia;
using SimdSkia.Cpu;
using System.Runtime.CompilerServices;
namespace SkiaSharp;

public unsafe partial class SKImageFilter : SKObject
{
    private RasterImageFilter? filter;
    private ShaderResources? resources;
    private SKImageFilter(RasterImageFilter filter,ShaderResources? resources=null){this.filter=filter;this.resources=resources;}
    internal RasterImageFilter ToCore(){ThrowIfDisposed();return filter!;}
    internal SKImageFilter RetainForPicture()=>new(ToCore(),RetainResources());
    internal ShaderResources? RetainResources(){ThrowIfDisposed();return resources?.Retain();}
    private static RasterImageFilter? Node(SKImageFilter? input)=>input?.ToCore();
    private static SKImageFilter Own(RasterImageFilter node,params SKImageFilter?[] inputs)
    {
        var retained=new List<IDisposable>();
        try{foreach(var input in inputs)if(input?.RetainResources() is {} owner)retained.Add(owner);return new(node,retained.Count==0?null:new(retained.ToArray()));}
        catch{foreach(var item in retained)item.Dispose();throw;}
    }
    private static RectF? Crop(SKRect* crop)=>crop is null?null:crop->ToCore();
    private static ImageSampling Sampling(SKSamplingOptions sampling)
    {
        if(sampling.IsAniso||sampling.Mipmap!=SKMipmapMode.None)throw new NotSupportedException("Filter sampling does not yet implement mipmaps or anisotropy.");
        if(sampling.UseCubic)
        {if(sampling.Cubic!=SKCubicResampler.Mitchell)throw new NotSupportedException("Filter matrix cubic sampling currently supports Mitchell.");return ImageSampling.Mitchell;}
        return sampling.Filter switch{SKFilterMode.Nearest=>ImageSampling.Nearest,SKFilterMode.Linear=>ImageSampling.Bilinear,_=>throw new ArgumentOutOfRangeException(nameof(sampling))};
    }
    protected override void Dispose(bool disposing){filter=null;var r=resources;resources=null;r?.Dispose();}
    [Obsolete("Use SetMatrix(in SKMatrix) instead.",true)]
    public static SKImageFilter CreateMatrix(SKMatrix matrix)=>CreateMatrix(in matrix);
    [Obsolete("Use SetMatrix(in SKMatrix, SKSamplingOptions, SKImageFilter) instead.",true)]
    public static SKImageFilter CreateMatrix(SKMatrix matrix,SKFilterQuality quality,SKImageFilter? input)=>CreateMatrix(in matrix,quality.ToSamplingOptions(),input);
    [OverloadResolutionPriority(1)]
    public static SKImageFilter CreateMatrix(in SKMatrix matrix)=>CreateMatrix(in matrix,SKSamplingOptions.Default,null);
    public static SKImageFilter CreateMatrix(in SKMatrix matrix,SKSamplingOptions sampling)=>CreateMatrix(in matrix,sampling,null);
    public static SKImageFilter CreateMatrix(in SKMatrix matrix,SKSamplingOptions sampling,SKImageFilter? input)=>Own(RasterImageFilter.Matrix(matrix.ToCore(),Sampling(sampling),Node(input)),input);
    public static SKImageFilter CreateBlur(float sigmaX,float sigmaY)=>CreateBlur(sigmaX,sigmaY,SKShaderTileMode.Decal,null,null);
    public static SKImageFilter CreateBlur(float sigmaX,float sigmaY,SKImageFilter? input)=>CreateBlur(sigmaX,sigmaY,SKShaderTileMode.Decal,input,null);
    public static SKImageFilter CreateBlur(float sigmaX,float sigmaY,SKImageFilter? input,SKRect cropRect)=>CreateBlur(sigmaX,sigmaY,SKShaderTileMode.Decal,input,&cropRect);
    public static SKImageFilter CreateBlur(float sigmaX,float sigmaY,SKShaderTileMode tileMode)=>CreateBlur(sigmaX,sigmaY,tileMode,null,null);
    public static SKImageFilter CreateBlur(float sigmaX,float sigmaY,SKShaderTileMode tileMode,SKImageFilter? input)=>CreateBlur(sigmaX,sigmaY,tileMode,input,null);
    public static SKImageFilter CreateBlur(float sigmaX,float sigmaY,SKShaderTileMode tileMode,SKImageFilter? input,SKRect cropRect)=>CreateBlur(sigmaX,sigmaY,tileMode,input,&cropRect);
    private static SKImageFilter CreateBlur(float sigmaX,float sigmaY,SKShaderTileMode tileMode,SKImageFilter? input,SKRect* cropRect)=>Own(RasterImageFilter.Blur(sigmaX,sigmaY,(int)tileMode,Node(input),Crop(cropRect)),input);
    public static SKImageFilter CreateColorFilter(SKColorFilter cf)=>CreateColorFilter(cf,null,null);
    public static SKImageFilter CreateColorFilter(SKColorFilter cf,SKImageFilter? input)=>CreateColorFilter(cf,input,null);
    public static SKImageFilter CreateColorFilter(SKColorFilter cf,SKImageFilter? input,SKRect cropRect)=>CreateColorFilter(cf,input,&cropRect);
    private static SKImageFilter CreateColorFilter(SKColorFilter cf,SKImageFilter? input,SKRect* cropRect)
    {ArgumentNullException.ThrowIfNull(cf);return Own(RasterImageFilter.Color(cf.ToCore(),Node(input),Crop(cropRect)),input);}
    public static SKImageFilter CreateCompose(SKImageFilter outer,SKImageFilter inner)
    {ArgumentNullException.ThrowIfNull(outer);ArgumentNullException.ThrowIfNull(inner);return Own(RasterImageFilter.Compose(outer.ToCore(),inner.ToCore()),outer,inner);}
    public static SKImageFilter CreateOffset(float radiusX,float radiusY)=>CreateOffset(radiusX,radiusY,null,null);
    public static SKImageFilter CreateOffset(float radiusX,float radiusY,SKImageFilter? input)=>CreateOffset(radiusX,radiusY,input,null);
    public static SKImageFilter CreateOffset(float radiusX,float radiusY,SKImageFilter? input,SKRect cropRect)=>CreateOffset(radiusX,radiusY,input,&cropRect);
    private static SKImageFilter CreateOffset(float radiusX,float radiusY,SKImageFilter? input,SKRect* cropRect)=>Own(RasterImageFilter.Offset(radiusX,radiusY,Node(input),Crop(cropRect)),input);
    public static SKImageFilter CreateMerge(SKImageFilter? first,SKImageFilter? second)=>CreateMerge(first,second,null);
    public static SKImageFilter CreateMerge(SKImageFilter? first,SKImageFilter? second,SKRect cropRect)=>CreateMerge(first,second,&cropRect);
    private static SKImageFilter CreateMerge(SKImageFilter? first,SKImageFilter? second,SKRect* cropRect)=>Own(RasterImageFilter.Merge([Node(first),Node(second)],Crop(cropRect)),first,second);
    public static SKImageFilter CreateMerge(ReadOnlySpan<SKImageFilter> filters)=>CreateMerge(filters,null);
    public static SKImageFilter CreateMerge(ReadOnlySpan<SKImageFilter> filters,SKRect cropRect)=>CreateMerge(filters,&cropRect);
    public static SKImageFilter CreateMerge(ReadOnlySpan<SKImageFilter> filters,SKRect* cropRect)
    {var nodes=new RasterImageFilter?[filters.Length];for(int i=0;i<nodes.Length;i++)nodes[i]=Node(filters[i]);return Own(RasterImageFilter.Merge(nodes,Crop(cropRect)),filters.ToArray());}
    public static SKImageFilter CreateBlendMode(SKBlendMode mode,SKImageFilter? background)=>CreateBlendMode(mode,background,null,null);
    public static SKImageFilter CreateBlendMode(SKBlendMode mode,SKImageFilter? background,SKImageFilter? foreground)=>CreateBlendMode(mode,background,foreground,null);
    public static SKImageFilter CreateBlendMode(SKBlendMode mode,SKImageFilter? background,SKImageFilter? foreground,SKRect cropRect)=>CreateBlendMode(mode,background,foreground,&cropRect);
    private static SKImageFilter CreateBlendMode(SKBlendMode mode,SKImageFilter? background,SKImageFilter? foreground,SKRect* cropRect)=>CreateBlendMode(SKBlender.CreateBlendMode(mode),background,foreground,cropRect);
    public static SKImageFilter CreateBlendMode(SKBlender blender,SKImageFilter? background)=>CreateBlendMode(blender,background,null,null);
    public static SKImageFilter CreateBlendMode(SKBlender blender,SKImageFilter? background,SKImageFilter? foreground)=>CreateBlendMode(blender,background,foreground,null);
    public static SKImageFilter CreateBlendMode(SKBlender blender,SKImageFilter? background,SKImageFilter? foreground,SKRect cropRect)=>CreateBlendMode(blender,background,foreground,&cropRect);
    private static SKImageFilter CreateBlendMode(SKBlender blender,SKImageFilter? background,SKImageFilter? foreground,SKRect* cropRect)
    {ArgumentNullException.ThrowIfNull(blender);return Own(RasterImageFilter.Blend(blender.Capture(),Node(background),Node(foreground),Crop(cropRect)),background,foreground);}
    public static SKImageFilter CreateArithmetic(float k1,float k2,float k3,float k4,bool enforcePMColor,SKImageFilter? background)=>CreateArithmetic(k1,k2,k3,k4,enforcePMColor,background,null,null);
    public static SKImageFilter CreateArithmetic(float k1,float k2,float k3,float k4,bool enforcePMColor,SKImageFilter? background,SKImageFilter? foreground)=>CreateArithmetic(k1,k2,k3,k4,enforcePMColor,background,foreground,null);
    public static SKImageFilter CreateArithmetic(float k1,float k2,float k3,float k4,bool enforcePMColor,SKImageFilter? background,SKImageFilter? foreground,SKRect cropRect)=>CreateArithmetic(k1,k2,k3,k4,enforcePMColor,background,foreground,&cropRect);
    private static SKImageFilter CreateArithmetic(float k1,float k2,float k3,float k4,bool enforcePMColor,SKImageFilter? background,SKImageFilter? foreground,SKRect* cropRect)
    {using var blender=SKBlender.CreateArithmetic(k1,k2,k3,k4,enforcePMColor);return CreateBlendMode(blender,background,foreground,cropRect);}
}
