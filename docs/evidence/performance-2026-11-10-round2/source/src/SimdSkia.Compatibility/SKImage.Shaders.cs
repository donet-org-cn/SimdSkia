// Forwarding API from SkiaSharp v3.119.4 (MIT).
#pragma warning disable CS0618
namespace SkiaSharp;
public partial class SKImage
{
		public SKShader ToShader () =>
			ToShader (SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKSamplingOptions.Default, SKMatrix.Identity);

		public SKShader ToShader (SKShaderTileMode tileX, SKShaderTileMode tileY) =>
			ToShader (tileX, tileY, SKSamplingOptions.Default, SKMatrix.Identity);

		public SKShader ToShader (SKShaderTileMode tileX, SKShaderTileMode tileY, SKMatrix localMatrix) =>
			ToShader (tileX, tileY, SKSamplingOptions.Default, localMatrix);

		public SKShader ToShader (SKShaderTileMode tileX, SKShaderTileMode tileY, SKSamplingOptions sampling) =>
			ToShader (tileX, tileY, sampling, SKMatrix.Identity);

		[Obsolete ("Use ToShader(SKShaderTileMode tileX, SKShaderTileMode tileY, SKSamplingOptions sampling) instead.")]
		public SKShader ToShader (SKShaderTileMode tileX, SKShaderTileMode tileY, SKFilterQuality quality) =>
			ToShader (tileX, tileY, quality.ToSamplingOptions(), SKMatrix.Identity);

		public SKShader ToShader (SKShaderTileMode tileX, SKShaderTileMode tileY, SKSamplingOptions sampling, SKMatrix localMatrix) =>
            SKShader.Raster(Buffer,ColorSpace,false,tileX,tileY,sampling,localMatrix);

		[Obsolete ("Use ToShader(SKShaderTileMode tileX, SKShaderTileMode tileY, SKSamplingOptions sampling, SKMatrix localMatrix) instead.")]
		public SKShader ToShader (SKShaderTileMode tileX, SKShaderTileMode tileY, SKFilterQuality quality, SKMatrix localMatrix) =>
			ToShader (tileX, tileY, quality.ToSamplingOptions(), localMatrix);

}
