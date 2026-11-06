// Forwarding API from SkiaSharp v3.119.4 (MIT).
#pragma warning disable CS0618
namespace SkiaSharp;
public partial class SKBitmap
{
		public SKShader ToShader () =>
			ToShader (SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKSamplingOptions.Default, SKMatrix.Identity);
		public SKShader ToShader (SKShaderTileMode tmx, SKShaderTileMode tmy) =>
			ToShader (tmx, tmy, SKSamplingOptions.Default, SKMatrix.Identity);

		public SKShader ToShader (SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling) =>
			ToShader (tmx, tmy, sampling, SKMatrix.Identity);

		[Obsolete ("Use ToShader(SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling) instead.")]
		public SKShader ToShader (SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterQuality quality) =>
			ToShader (tmx, tmy, quality.ToSamplingOptions(), SKMatrix.Identity);

		public SKShader ToShader (SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix) =>
			ToShader (tmx, tmy, SKSamplingOptions.Default, localMatrix);

		public SKShader ToShader (SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling, SKMatrix localMatrix) =>
            SKShader.Raster(Buffer,ColorSpace,!IsImmutable,tmx,tmy,sampling,localMatrix);

		[Obsolete ("Use ToShader(SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling, SKMatrix localMatrix) instead.")]
		public SKShader ToShader (SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterQuality quality, SKMatrix localMatrix) =>
			ToShader (tmx, tmy, quality.ToSamplingOptions(), localMatrix);

}
