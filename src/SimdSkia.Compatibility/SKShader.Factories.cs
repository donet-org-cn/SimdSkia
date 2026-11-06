// Forwarding API from SkiaSharp v3.119.4 (MIT).
#pragma warning disable CS0618
namespace SkiaSharp;
public partial class SKShader
{
		public static SKShader CreateBitmap (SKBitmap src) =>
			CreateBitmap (src, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);

		public static SKShader CreateBitmap (SKBitmap src, SKShaderTileMode tmx, SKShaderTileMode tmy)
		{
			if (src == null)
				throw new ArgumentNullException (nameof (src));

			return src.ToShader (tmx, tmy);
		}

		public static SKShader CreateBitmap (SKBitmap src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix)
		{
			if (src == null)
				throw new ArgumentNullException (nameof (src));

			return src.ToShader (tmx, tmy, localMatrix);
		}

		// CreateImage

		public static SKShader CreateImage (SKImage src) =>
			src?.ToShader () ?? throw new ArgumentNullException (nameof (src));

		public static SKShader CreateImage (SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy) =>
			src?.ToShader (tmx, tmy) ?? throw new ArgumentNullException (nameof (src));

		public static SKShader CreateImage (SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling) =>
			src?.ToShader (tmx, tmy, sampling) ?? throw new ArgumentNullException (nameof (src));

		[Obsolete ("Use CreateImage(SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling) instead.")]
		public static SKShader CreateImage (SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterQuality quality) =>
			src?.ToShader (tmx, tmy, quality.ToSamplingOptions()) ?? throw new ArgumentNullException (nameof (src));

		public static SKShader CreateImage (SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix) =>
			src?.ToShader (tmx, tmy, localMatrix) ?? throw new ArgumentNullException (nameof (src));

		public static SKShader CreateImage (SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling, SKMatrix localMatrix) =>
			src?.ToShader (tmx, tmy, sampling, localMatrix) ?? throw new ArgumentNullException (nameof (src));

		[Obsolete ("Use CreateImage(SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling, SKMatrix localMatrix) instead.")]
		public static SKShader CreateImage (SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterQuality quality, SKMatrix localMatrix) =>
			src?.ToShader (tmx, tmy, quality.ToSamplingOptions(), localMatrix) ?? throw new ArgumentNullException (nameof (src));

}
