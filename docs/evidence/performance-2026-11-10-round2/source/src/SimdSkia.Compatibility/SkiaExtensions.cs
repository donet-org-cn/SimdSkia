// SkiaSharp v3.119.4 managed value/format helpers, MIT.
#nullable disable
using System;
namespace SkiaSharp;
	public static partial class SkiaExtensions
	{
		public static bool IsBgr (this SKPixelGeometry pg) =>
			pg == SKPixelGeometry.BgrHorizontal || pg == SKPixelGeometry.BgrVertical;

		public static bool IsRgb (this SKPixelGeometry pg) =>
			pg == SKPixelGeometry.RgbHorizontal || pg == SKPixelGeometry.RgbVertical;

		public static bool IsVertical (this SKPixelGeometry pg) =>
			pg == SKPixelGeometry.BgrVertical || pg == SKPixelGeometry.RgbVertical;

		public static bool IsHorizontal (this SKPixelGeometry pg) =>
			pg == SKPixelGeometry.BgrHorizontal || pg == SKPixelGeometry.RgbHorizontal;

		// SkImageInfo.cpp - SkColorTypeBytesPerPixel
		public static int GetBytesPerPixel (this SKColorType colorType) =>
			colorType switch {
				// 0
				SKColorType.Unknown => 0,
				// 1
				SKColorType.Alpha8 => 1,
				SKColorType.Gray8 => 1,
				SKColorType.R8Unorm => 1,
				// 2
				SKColorType.Rgb565 => 2,
				SKColorType.Argb4444 => 2,
				SKColorType.Rg88 => 2,
				SKColorType.Alpha16 => 2,
				SKColorType.AlphaF16 => 2,
				// 4
				SKColorType.Bgra8888 => 4,
				SKColorType.Bgra1010102 => 4,
				SKColorType.Bgr101010x => 4,
				SKColorType.Bgr101010xXR => 4,
				SKColorType.Rgba8888 => 4,
				SKColorType.Rgb888x => 4,
				SKColorType.Rgba1010102 => 4,
				SKColorType.Rgb101010x => 4,
				SKColorType.Rg1616 => 4,
				SKColorType.RgF16 => 4,
				SKColorType.Srgba8888 => 4,
				// 8
				SKColorType.RgbaF16Clamped => 8,
				SKColorType.RgbaF16 => 8,
				SKColorType.Rgba16161616 => 8,
				SKColorType.Rgba10x6 => 8,
				// 16
				SKColorType.RgbaF32 => 16,
				//
				_ => throw new ArgumentOutOfRangeException (nameof (colorType), $"Unknown color type: '{colorType}'"),
			};

		// SkImageInfoPriv.h - SkColorTypeShiftPerPixel
		public static int GetBitShiftPerPixel (this SKColorType colorType) =>
			colorType switch {
				// 0
				SKColorType.Unknown => 0,
				// 0
				SKColorType.Alpha8 => 0,
				SKColorType.Gray8 => 0,
				SKColorType.R8Unorm => 0,
				// 1
				SKColorType.Rgb565 => 1,
				SKColorType.Argb4444 => 1,
				SKColorType.Rg88 => 1,
				SKColorType.Alpha16 => 1,
				SKColorType.AlphaF16 => 1,
				// 2
				SKColorType.Bgra8888 => 2,
				SKColorType.Bgra1010102 => 2,
				SKColorType.Bgr101010x => 2,
				SKColorType.Bgr101010xXR => 2,
				SKColorType.Rgba8888 => 2,
				SKColorType.Rgb888x => 2,
				SKColorType.Rgba1010102 => 2,
				SKColorType.Rgb101010x => 2,
				SKColorType.Rg1616 => 2,
				SKColorType.RgF16 => 2,
				SKColorType.Srgba8888 => 2,
				// 3
				SKColorType.RgbaF16Clamped => 3,
				SKColorType.RgbaF16 => 3,
				SKColorType.Rgba16161616 => 3,
				SKColorType.Rgba10x6 => 3,
				// 4
				SKColorType.RgbaF32 => 4,
				//
				_ => throw new ArgumentOutOfRangeException (nameof (colorType), $"Unknown color type: '{colorType}'"),
			};

		// SkImageInfo.cpp - SkColorTypeValidateAlphaType
		public static SKAlphaType GetAlphaType (this SKColorType colorType, SKAlphaType alphaType = SKAlphaType.Premul)
		{
			switch (colorType) {
				case SKColorType.Unknown:
					alphaType = SKAlphaType.Unknown;
					break;

				// opaque or premul
				case SKColorType.Alpha8:
				case SKColorType.Alpha16:
				case SKColorType.AlphaF16:
					if (SKAlphaType.Unpremul == alphaType) {
						alphaType = SKAlphaType.Premul;
					}
					break;

				// any
				case SKColorType.Argb4444:
				case SKColorType.Rgba8888:
				case SKColorType.Bgra8888:
				case SKColorType.Srgba8888:
				case SKColorType.Rgba1010102:
				case SKColorType.Bgra1010102:
				case SKColorType.RgbaF16Clamped:
				case SKColorType.RgbaF16:
				case SKColorType.RgbaF32:
				case SKColorType.Rgba16161616:
				case SKColorType.Rgba10x6:
					break;

				// opaque
				case SKColorType.Gray8:
				case SKColorType.Rg88:
				case SKColorType.Rg1616:
				case SKColorType.RgF16:
				case SKColorType.Rgb565:
				case SKColorType.Rgb888x:
				case SKColorType.Rgb101010x:
				case SKColorType.Bgr101010x:
				case SKColorType.Bgr101010xXR:
				case SKColorType.R8Unorm:
					alphaType = SKAlphaType.Opaque;
					break;

				default:
					throw new ArgumentOutOfRangeException (nameof (colorType), $"Unknown color type: '{colorType}'");
			}

			return alphaType;
		}
	}

	public static partial class SkiaExtensions
	{
		public static uint ToGlSizedFormat (this SKColorType colorType) =>
			colorType switch {
				SKColorType.Unknown => 0,
				SKColorType.Alpha8 => GRGlSizedFormat.ALPHA8,
				SKColorType.Gray8 => GRGlSizedFormat.LUMINANCE8,
				SKColorType.Rgb565 => GRGlSizedFormat.RGB565,
				SKColorType.Argb4444 => GRGlSizedFormat.RGBA4,
				SKColorType.Rgba8888 => GRGlSizedFormat.RGBA8,
				SKColorType.Rgb888x => GRGlSizedFormat.RGB8,
				SKColorType.Bgra8888 => GRGlSizedFormat.BGRA8,
				SKColorType.Rgba1010102 => GRGlSizedFormat.RGB10_A2,
				SKColorType.AlphaF16 => GRGlSizedFormat.R16F,
				SKColorType.RgbaF16 => GRGlSizedFormat.RGBA16F,
				SKColorType.RgbaF16Clamped => GRGlSizedFormat.RGBA16F,
				SKColorType.Alpha16 => GRGlSizedFormat.R16,
				SKColorType.Rg1616 => GRGlSizedFormat.RG16,
				SKColorType.Rgba16161616 => GRGlSizedFormat.RGBA16,
				SKColorType.Rgba10x6 => 0,
				SKColorType.RgF16 => GRGlSizedFormat.RG16F,
				SKColorType.Rg88 => GRGlSizedFormat.RG8,
				SKColorType.Rgb101010x => 0,
				SKColorType.RgbaF32 => 0,
				SKColorType.Bgra1010102 => 0,
				SKColorType.Bgr101010x => 0,
				SKColorType.Bgr101010xXR => 0,
				SKColorType.Srgba8888 => GRGlSizedFormat.SRGB8_ALPHA8,
				SKColorType.R8Unorm => GRGlSizedFormat.R8,
				_ => throw new ArgumentOutOfRangeException (nameof (colorType), $"Unknown color type: '{colorType}'"),
			};
	}


	internal static class GRGlSizedFormat
	{
		// Unsized formats
		internal const uint STENCIL_INDEX = 0x1901;
		internal const uint DEPTH_COMPONENT = 0x1902;
		internal const uint DEPTH_STENCIL = 0x84F9;
		internal const uint RED = 0x1903;
		internal const uint RED_INTEGER = 0x8D94;
		internal const uint GREEN = 0x1904;
		internal const uint BLUE = 0x1905;
		internal const uint ALPHA = 0x1906;
		internal const uint LUMINANCE = 0x1909;
		internal const uint LUMINANCE_ALPHA = 0x190A;
		internal const uint RG_INTEGER = 0x8228;
		internal const uint RGB = 0x1907;
		internal const uint RGB_INTEGER = 0x8D98;
		internal const uint SRGB = 0x8C40;
		internal const uint RGBA = 0x1908;
		internal const uint RG = 0x8227;
		internal const uint SRGB_ALPHA = 0x8C42;
		internal const uint RGBA_INTEGER = 0x8D99;
		internal const uint BGRA = 0x80E1;

		// Stencil index sized formats
		internal const uint STENCIL_INDEX4 = 0x8D47;
		internal const uint STENCIL_INDEX8 = 0x8D48;
		internal const uint STENCIL_INDEX16 = 0x8D49;

		// Depth component sized formats
		internal const uint DEPTH_COMPONENT16 = 0x81A5;

		// Depth stencil sized formats
		internal const uint DEPTH24_STENCIL8 = 0x88F0;

		// Red sized formats
		internal const uint R8 = 0x8229;
		internal const uint R16 = 0x822A;
		internal const uint R16F = 0x822D;
		internal const uint R32F = 0x822E;

		// Red integer sized formats
		internal const uint R8I = 0x8231;
		internal const uint R8UI = 0x8232;
		internal const uint R16I = 0x8233;
		internal const uint R16UI = 0x8234;
		internal const uint R32I = 0x8235;
		internal const uint R32UI = 0x8236;

		// Luminance sized formats
		internal const uint LUMINANCE8 = 0x8040;
		internal const uint LUMINANCE16F = 0x881E;

		// Alpha sized formats
		internal const uint ALPHA8 = 0x803C;
		internal const uint ALPHA16 = 0x803E;
		internal const uint ALPHA16F = 0x881C;
		internal const uint ALPHA32F = 0x8816;

		// Alpha integer sized formats
		internal const uint ALPHA8I = 0x8D90;
		internal const uint ALPHA8UI = 0x8D7E;
		internal const uint ALPHA16I = 0x8D8A;
		internal const uint ALPHA16UI = 0x8D78;
		internal const uint ALPHA32I = 0x8D84;
		internal const uint ALPHA32UI = 0x8D72;

		// RG sized formats
		internal const uint RG8 = 0x822B;
		internal const uint RG16 = 0x822C;
		//internal const uint R16F = 0x822D;
		//internal const uint R32F = 0x822E;
		internal const uint RG16F = 0x822F;

		// RG sized integer formats
		internal const uint RG8I = 0x8237;
		internal const uint RG8UI = 0x8238;
		internal const uint RG16I = 0x8239;
		internal const uint RG16UI = 0x823A;
		internal const uint RG32I = 0x823B;
		internal const uint RG32UI = 0x823C;

		// RGB sized formats
		internal const uint RGB5 = 0x8050;
		internal const uint RGB565 = 0x8D62;
		internal const uint RGB8 = 0x8051;
		internal const uint SRGB8 = 0x8C41;

		// RGB integer sized formats
		internal const uint RGB8I = 0x8D8F;
		internal const uint RGB8UI = 0x8D7D;
		internal const uint RGB16I = 0x8D89;
		internal const uint RGB16UI = 0x8D77;
		internal const uint RGB32I = 0x8D83;
		internal const uint RGB32UI = 0x8D71;

		// RGBA sized formats
		internal const uint RGBA4 = 0x8056;
		internal const uint RGB5_A1 = 0x8057;
		internal const uint RGBA8 = 0x8058;
		internal const uint RGB10_A2 = 0x8059;
		internal const uint SRGB8_ALPHA8 = 0x8C43;
		internal const uint RGBA16F = 0x881A;
		internal const uint RGBA32F = 0x8814;
		internal const uint RG32F = 0x8230;
		internal const uint RGBA16 = 0x805B;

		// RGBA integer sized formats
		internal const uint RGBA8I = 0x8D8E;
		internal const uint RGBA8UI = 0x8D7C;
		internal const uint RGBA16I = 0x8D88;
		internal const uint RGBA16UI = 0x8D76;
		internal const uint RGBA32I = 0x8D82;
		internal const uint RGBA32UI = 0x8D70;

		// BGRA sized formats
		internal const uint BGRA8 = 0x93A1;

		// Compressed texture sized formats
		internal const uint COMPRESSED_ETC1_RGB8 = 0x8D64;
	}

#pragma warning disable CS0618
	public static partial class SkiaExtensions
	{
		[Obsolete ($"Use {nameof (SKSamplingOptions)} instead.")]
		public static SKSamplingOptions ToSamplingOptions (this SKFilterQuality quality) =>
			quality switch {
				SKFilterQuality.None => new SKSamplingOptions (SKFilterMode.Nearest, SKMipmapMode.None),
				SKFilterQuality.Low => new SKSamplingOptions (SKFilterMode.Linear, SKMipmapMode.None),
				SKFilterQuality.Medium => new SKSamplingOptions (SKFilterMode.Linear, SKMipmapMode.Linear),
				SKFilterQuality.High => new SKSamplingOptions (SKCubicResampler.Mitchell),
				_ => throw new ArgumentOutOfRangeException (nameof (quality), $"Unknown filter quality: '{quality}'"),
			};
	}
