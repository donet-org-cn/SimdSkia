// SkiaSharp 3.119.4 public forwarding overloads, MIT.
using SimdSkia;
using SimdSkia.Cpu;
namespace SkiaSharp;
public unsafe partial class SKImageFilter
{
    private static SKImageFilter CreateDropShadow(float dx,float dy,float sigmaX,float sigmaY,SKColor color,SKImageFilter? input,SKRect* cropRect)=>Own(RasterImageFilter.Shadow(dx,dy,sigmaX,sigmaY,color.ToCore(),false,Node(input),Crop(cropRect)),input);
    private static SKImageFilter CreateDropShadowOnly(float dx,float dy,float sigmaX,float sigmaY,SKColor color,SKImageFilter? input,SKRect* cropRect)=>Own(RasterImageFilter.Shadow(dx,dy,sigmaX,sigmaY,color.ToCore(),true,Node(input),Crop(cropRect)),input);
    private static SKImageFilter CreateDilate(float radiusX,float radiusY,SKImageFilter? input,SKRect* cropRect)=>Own(RasterImageFilter.Morphology(radiusX,radiusY,true,Node(input),Crop(cropRect)),input);
    private static SKImageFilter CreateErode(float radiusX,float radiusY,SKImageFilter? input,SKRect* cropRect)=>Own(RasterImageFilter.Morphology(radiusX,radiusY,false,Node(input),Crop(cropRect)),input);
    private static SKImageFilter CreateMatrixConvolution(SKSizeI kernelSize,ReadOnlySpan<float> kernel,float gain,float bias,SKPointI kernelOffset,SKShaderTileMode tileMode,bool convolveAlpha,SKImageFilter? input,SKRect* cropRect)=>Own(RasterImageFilter.Convolution(kernelSize.Width,kernelSize.Height,kernel,gain,bias,kernelOffset.X,kernelOffset.Y,(int)tileMode,convolveAlpha,Node(input),Crop(cropRect)),input);
    private static SKImageFilter CreateDisplacementMapEffect(SKColorChannel xChannelSelector,SKColorChannel yChannelSelector,float scale,SKImageFilter displacement,SKImageFilter? input,SKRect* cropRect)
    {ArgumentNullException.ThrowIfNull(displacement);return Own(RasterImageFilter.Displacement((int)xChannelSelector,(int)yChannelSelector,scale,displacement.ToCore(),Node(input),Crop(cropRect)),displacement,input);}
    public static SKImageFilter CreateTile(SKRect src,SKRect dst)=>CreateTile(src,dst,null);
    public static SKImageFilter CreateTile(SKRect src,SKRect dst,SKImageFilter? input)
    {ArgumentNullException.ThrowIfNull(input);return Own(RasterImageFilter.TileImage(src.ToCore(),dst.ToCore(),input.ToCore()),input);}
    public static SKImageFilter CreateImage(SKImage image)=>CreateImage(image,new SKSamplingOptions(SKCubicResampler.Mitchell));
    public static SKImageFilter CreateImage(SKImage image,SKSamplingOptions sampling)
    {ArgumentNullException.ThrowIfNull(image);return CreateImage(image,new(0,0,image.Width,image.Height),new(0,0,image.Width,image.Height),sampling);}
    public static SKImageFilter CreateImage(SKImage image,SKRect src,SKRect dst,SKSamplingOptions sampling)
    {
        ArgumentNullException.ThrowIfNull(image);if(image.ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Convert non-sRGB image-filter sources before filtering.");var mode=Sampling(sampling);var pixels=image.Buffer.Share();
        try{return new(RasterImageFilter.Image(pixels,src.ToCore(),dst.ToCore(),mode),new(pixels));}catch{pixels.Dispose();throw;}
    }
    [Obsolete("Use CreateImage(SKImage, SKRect, SKRect, SKSamplingOptions) instead.",true)]
    public static SKImageFilter CreateImage(SKImage image,SKRect src,SKRect dst,SKFilterQuality filterQuality)=>CreateImage(image,src,dst,filterQuality.ToSamplingOptions());
    public static SKImageFilter CreateShader(SKShader? shader)=>CreateShader(shader,false,null);
    public static SKImageFilter CreateShader(SKShader? shader,bool dither)=>CreateShader(shader,dither,null);
    public static SKImageFilter CreateShader(SKShader? shader,bool dither,SKRect cropRect)=>CreateShader(shader,dither,&cropRect);
    private static SKImageFilter CreateShader(SKShader? shader,bool dither,SKRect* cropRect)
    {
        var node=shader is null?RasterImageFilter.DrawingSource((_,_)=>new FilterFrame(null)):RasterImageFilter.Shader(shader.ToCore(),dither,Crop(cropRect));
        return new(node,shader?.RetainResources());
    }
		public static SKImageFilter CreateDisplacementMapEffect (SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement) =>
			CreateDisplacementMapEffect (xChannelSelector, yChannelSelector, scale, displacement, null, null);

		public static SKImageFilter CreateDisplacementMapEffect (SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement, SKImageFilter? input) =>
			CreateDisplacementMapEffect (xChannelSelector, yChannelSelector, scale, displacement, input, null);

		public static SKImageFilter CreateDisplacementMapEffect (SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement, SKImageFilter? input, SKRect cropRect) =>
			CreateDisplacementMapEffect (xChannelSelector, yChannelSelector, scale, displacement, input, &cropRect);

		public static SKImageFilter CreateDropShadow (float dx, float dy, float sigmaX, float sigmaY, SKColor color) =>
			CreateDropShadow (dx, dy, sigmaX, sigmaY, color, null, null);

		public static SKImageFilter CreateDropShadow (float dx, float dy, float sigmaX, float sigmaY, SKColor color, SKImageFilter? input) =>
			CreateDropShadow (dx, dy, sigmaX, sigmaY, color, input, null);

		public static SKImageFilter CreateDropShadow (float dx, float dy, float sigmaX, float sigmaY, SKColor color, SKImageFilter? input, SKRect cropRect) =>
			CreateDropShadow (dx, dy, sigmaX, sigmaY, color, input, &cropRect);

		public static SKImageFilter CreateDropShadowOnly (float dx, float dy, float sigmaX, float sigmaY, SKColor color) =>
			CreateDropShadowOnly (dx, dy, sigmaX, sigmaY, color, null, null);

		public static SKImageFilter CreateDropShadowOnly (float dx, float dy, float sigmaX, float sigmaY, SKColor color, SKImageFilter? input) =>
			CreateDropShadowOnly (dx, dy, sigmaX, sigmaY, color, input, null);

		public static SKImageFilter CreateDropShadowOnly (float dx, float dy, float sigmaX, float sigmaY, SKColor color, SKImageFilter? input, SKRect cropRect) =>
			CreateDropShadowOnly (dx, dy, sigmaX, sigmaY, color, input, &cropRect);

		public static SKImageFilter CreateMatrixConvolution (SKSizeI kernelSize, ReadOnlySpan<float> kernel, float gain, float bias, SKPointI kernelOffset, SKShaderTileMode tileMode, bool convolveAlpha) =>
			CreateMatrixConvolution (kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, null, null);

		public static SKImageFilter CreateMatrixConvolution (SKSizeI kernelSize, ReadOnlySpan<float> kernel, float gain, float bias, SKPointI kernelOffset, SKShaderTileMode tileMode, bool convolveAlpha, SKImageFilter? input) =>
			CreateMatrixConvolution (kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, null);

		public static SKImageFilter CreateMatrixConvolution (SKSizeI kernelSize, ReadOnlySpan<float> kernel, float gain, float bias, SKPointI kernelOffset, SKShaderTileMode tileMode, bool convolveAlpha, SKImageFilter? input, SKRect cropRect) =>
			CreateMatrixConvolution (kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, &cropRect);

		public static SKImageFilter CreateDilate (float radiusX, float radiusY) =>
			CreateDilate (radiusX, radiusY, null, null);

		public static SKImageFilter CreateDilate (float radiusX, float radiusY, SKImageFilter? input) =>
			CreateDilate (radiusX, radiusY, input, null);

		public static SKImageFilter CreateDilate (float radiusX, float radiusY, SKImageFilter? input, SKRect cropRect) =>
			CreateDilate (radiusX, radiusY, input, &cropRect);

		public static SKImageFilter CreateErode (float radiusX, float radiusY) =>
			CreateErode (radiusX, radiusY, null, null);

		public static SKImageFilter CreateErode (float radiusX, float radiusY, SKImageFilter? input) =>
			CreateErode (radiusX, radiusY, input, null);

		public static SKImageFilter CreateErode (float radiusX, float radiusY, SKImageFilter? input, SKRect cropRect) =>
			CreateErode (radiusX, radiusY, input, &cropRect);
}
