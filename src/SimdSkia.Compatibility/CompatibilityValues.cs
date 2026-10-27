// API value definitions from SkiaSharp v3.119.4 (MIT). See docs/licenses/SkiaSharp-MIT.txt.
#nullable disable
using System.Runtime.InteropServices;
namespace SkiaSharp;

public enum SKCodecResult {
		// SUCCESS_SK_CODEC_RESULT = 0
		Success = 0,
		// INCOMPLETE_INPUT_SK_CODEC_RESULT = 1
		IncompleteInput = 1,
		// ERROR_IN_INPUT_SK_CODEC_RESULT = 2
		ErrorInInput = 2,
		// INVALID_CONVERSION_SK_CODEC_RESULT = 3
		InvalidConversion = 3,
		// INVALID_SCALE_SK_CODEC_RESULT = 4
		InvalidScale = 4,
		// INVALID_PARAMETERS_SK_CODEC_RESULT = 5
		InvalidParameters = 5,
		// INVALID_INPUT_SK_CODEC_RESULT = 6
		InvalidInput = 6,
		// COULD_NOT_REWIND_SK_CODEC_RESULT = 7
		CouldNotRewind = 7,
		// INTERNAL_ERROR_SK_CODEC_RESULT = 8
		InternalError = 8,
		// UNIMPLEMENTED_SK_CODEC_RESULT = 9
		Unimplemented = 9,
	}

public enum SKZeroInitialized {
		// YES_SK_CODEC_ZERO_INITIALIZED = 0
		Yes = 0,
		// NO_SK_CODEC_ZERO_INITIALIZED = 1
		No = 1,
	}

public enum SKCodecAnimationBlend {
		// SRC_OVER_SK_CODEC_ANIMATION_BLEND = 0
		SrcOver = 0,
		// SRC_SK_CODEC_ANIMATION_BLEND = 1
		Src = 1,
	}

public enum SKCodecAnimationDisposalMethod {
		// KEEP_SK_CODEC_ANIMATION_DISPOSAL_METHOD = 1
		Keep = 1,
		// RESTORE_BG_COLOR_SK_CODEC_ANIMATION_DISPOSAL_METHOD = 2
		RestoreBackgroundColor = 2,
		// RESTORE_PREVIOUS_SK_CODEC_ANIMATION_DISPOSAL_METHOD = 3
		RestorePrevious = 3,
	}

public enum SKEncodedOrigin {
		// TOP_LEFT_SK_ENCODED_ORIGIN = 1
		TopLeft = 1,
		// TOP_RIGHT_SK_ENCODED_ORIGIN = 2
		TopRight = 2,
		// BOTTOM_RIGHT_SK_ENCODED_ORIGIN = 3
		BottomRight = 3,
		// BOTTOM_LEFT_SK_ENCODED_ORIGIN = 4
		BottomLeft = 4,
		// LEFT_TOP_SK_ENCODED_ORIGIN = 5
		LeftTop = 5,
		// RIGHT_TOP_SK_ENCODED_ORIGIN = 6
		RightTop = 6,
		// RIGHT_BOTTOM_SK_ENCODED_ORIGIN = 7
		RightBottom = 7,
		// LEFT_BOTTOM_SK_ENCODED_ORIGIN = 8
		LeftBottom = 8,
		// DEFAULT_SK_ENCODED_ORIGIN = TOP_LEFT_SK_ENCODED_ORIGIN
		Default = 1,
	}

public enum SKFontStyleSlant {
		// UPRIGHT_SK_FONT_STYLE_SLANT = 0
		Upright = 0,
		// ITALIC_SK_FONT_STYLE_SLANT = 1
		Italic = 1,
		// OBLIQUE_SK_FONT_STYLE_SLANT = 2
		Oblique = 2,
	}

public enum SKRoundRectCorner {
		// UPPER_LEFT_SK_RRECT_CORNER = 0
		UpperLeft = 0,
		// UPPER_RIGHT_SK_RRECT_CORNER = 1
		UpperRight = 1,
		// LOWER_RIGHT_SK_RRECT_CORNER = 2
		LowerRight = 2,
		// LOWER_LEFT_SK_RRECT_CORNER = 3
		LowerLeft = 3,
	}

public enum SKRoundRectType {
		// EMPTY_SK_RRECT_TYPE = 0
		Empty = 0,
		// RECT_SK_RRECT_TYPE = 1
		Rect = 1,
		// OVAL_SK_RRECT_TYPE = 2
		Oval = 2,
		// SIMPLE_SK_RRECT_TYPE = 3
		Simple = 3,
		// NINE_PATCH_SK_RRECT_TYPE = 4
		NinePatch = 4,
		// COMPLEX_SK_RRECT_TYPE = 5
		Complex = 5,
	}

public partial struct SKCodecFrameInfo : IEquatable<SKCodecFrameInfo> {
		// public int fRequiredFrame
		private Int32 fRequiredFrame;
		public Int32 RequiredFrame {
			readonly get => fRequiredFrame;
			set => fRequiredFrame = value;
		}

		// public int fDuration
		private Int32 fDuration;
		public Int32 Duration {
			readonly get => fDuration;
			set => fDuration = value;
		}

		// public bool fFullyReceived
		private Byte fFullyReceived;
		public bool FullyRecieved {
			readonly get => fFullyReceived > 0;
			set => fFullyReceived = value ? (byte)1 : (byte)0;
		}

		// public sk_alphatype_t fAlphaType
		private SKAlphaType fAlphaType;
		public SKAlphaType AlphaType {
			readonly get => fAlphaType;
			set => fAlphaType = value;
		}

		// public bool fHasAlphaWithinBounds
		private Byte fHasAlphaWithinBounds;
		public bool HasAlphaWithinBounds {
			readonly get => fHasAlphaWithinBounds > 0;
			set => fHasAlphaWithinBounds = value ? (byte)1 : (byte)0;
		}

		// public sk_codecanimation_disposalmethod_t fDisposalMethod
		private SKCodecAnimationDisposalMethod fDisposalMethod;
		public SKCodecAnimationDisposalMethod DisposalMethod {
			readonly get => fDisposalMethod;
			set => fDisposalMethod = value;
		}

		// public sk_codecanimation_blend_t fBlend
		private SKCodecAnimationBlend fBlend;
		public SKCodecAnimationBlend Blend {
			readonly get => fBlend;
			set => fBlend = value;
		}

		// public sk_irect_t fFrameRect
		private SKRectI fFrameRect;
		public SKRectI FrameRect {
			readonly get => fFrameRect;
			set => fFrameRect = value;
		}

		public readonly bool Equals (SKCodecFrameInfo obj) =>
#pragma warning disable CS8909
			fRequiredFrame == obj.fRequiredFrame && fDuration == obj.fDuration && fFullyReceived == obj.fFullyReceived && fAlphaType == obj.fAlphaType && fHasAlphaWithinBounds == obj.fHasAlphaWithinBounds && fDisposalMethod == obj.fDisposalMethod && fBlend == obj.fBlend && fFrameRect == obj.fFrameRect;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKCodecFrameInfo f && Equals (f);

		public static bool operator == (SKCodecFrameInfo left, SKCodecFrameInfo right) =>
			left.Equals (right);

		public static bool operator != (SKCodecFrameInfo left, SKCodecFrameInfo right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fRequiredFrame);
			hash.Add (fDuration);
			hash.Add (fFullyReceived);
			hash.Add (fAlphaType);
			hash.Add (fHasAlphaWithinBounds);
			hash.Add (fDisposalMethod);
			hash.Add (fBlend);
			hash.Add (fFrameRect);
			return hash.ToHashCode ();
		}

	}

public struct SKCodecOptions : IEquatable<SKCodecOptions>
	{
		public static readonly SKCodecOptions Default;

		static SKCodecOptions ()
		{
			Default = new SKCodecOptions (SKZeroInitialized.No);
		}

		public SKCodecOptions (SKZeroInitialized zeroInitialized)
		{
			ZeroInitialized = zeroInitialized;
			Subset = null;
			FrameIndex = 0;
			PriorFrame = -1;
		}
		public SKCodecOptions (SKZeroInitialized zeroInitialized, SKRectI subset)
		{
			ZeroInitialized = zeroInitialized;
			Subset = subset;
			FrameIndex = 0;
			PriorFrame = -1;
		}
		public SKCodecOptions (SKRectI subset)
		{
			ZeroInitialized = SKZeroInitialized.No;
			Subset = subset;
			FrameIndex = 0;
			PriorFrame = -1;
		}
		public SKCodecOptions (int frameIndex)
		{
			ZeroInitialized = SKZeroInitialized.No;
			Subset = null;
			FrameIndex = frameIndex;
			PriorFrame = -1;
		}
		public SKCodecOptions (int frameIndex, int priorFrame)
		{
			ZeroInitialized = SKZeroInitialized.No;
			Subset = null;
			FrameIndex = frameIndex;
			PriorFrame = priorFrame;
		}

		public SKZeroInitialized ZeroInitialized { readonly get; set; }
		public SKRectI? Subset { readonly get; set; }
		public readonly bool HasSubset => Subset != null;
		public int FrameIndex { readonly get; set; }
		public int PriorFrame { readonly get; set; }

		public readonly bool Equals (SKCodecOptions obj) =>
			ZeroInitialized == obj.ZeroInitialized &&
			Subset == obj.Subset &&
			FrameIndex == obj.FrameIndex &&
			PriorFrame == obj.PriorFrame;

		public readonly override bool Equals (object obj) =>
			obj is SKCodecOptions f && Equals (f);

		public static bool operator == (SKCodecOptions left, SKCodecOptions right) =>
			left.Equals (right);

		public static bool operator != (SKCodecOptions left, SKCodecOptions right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (ZeroInitialized);
			hash.Add (Subset);
			hash.Add (FrameIndex);
			hash.Add (PriorFrame);
			return hash.ToHashCode ();
		}
	}

public struct SKDocumentPdfMetadata : IEquatable<SKDocumentPdfMetadata>
	{
		public const float DefaultRasterDpi = SKDocument.DefaultRasterDpi;
		public const int DefaultEncodingQuality = 101;

		public static readonly SKDocumentPdfMetadata Default;

		static SKDocumentPdfMetadata ()
		{
			Default = new SKDocumentPdfMetadata () {
				RasterDpi = DefaultRasterDpi,
				PdfA = false,
				EncodingQuality = 101,
			};
		}

		public SKDocumentPdfMetadata (float rasterDpi)
		{
			Title = null;
			Author = null;
			Subject = null;
			Keywords = null;
			Creator = null;
			Producer = null;
			Creation = null;
			Modified = null;
			RasterDpi = rasterDpi;
			PdfA = false;
			EncodingQuality = DefaultEncodingQuality;
		}

		public SKDocumentPdfMetadata (int encodingQuality)
		{
			Title = null;
			Author = null;
			Subject = null;
			Keywords = null;
			Creator = null;
			Producer = null;
			Creation = null;
			Modified = null;
			RasterDpi = DefaultRasterDpi;
			PdfA = false;
			EncodingQuality = encodingQuality;
		}

		public SKDocumentPdfMetadata (float rasterDpi, int encodingQuality)
		{
			Title = null;
			Author = null;
			Subject = null;
			Keywords = null;
			Creator = null;
			Producer = null;
			Creation = null;
			Modified = null;
			RasterDpi = rasterDpi;
			PdfA = false;
			EncodingQuality = encodingQuality;
		}

		public string Title { readonly get; set; }
		public string Author { readonly get; set; }
		public string Subject { readonly get; set; }
		public string Keywords { readonly get; set; }
		public string Creator { readonly get; set; }
		public string Producer { readonly get; set; }
		public DateTime? Creation { readonly get; set; }
		public DateTime? Modified { readonly get; set; }
		public float RasterDpi { readonly get; set; }
		public bool PdfA { readonly get; set; }
		public int EncodingQuality { readonly get; set; }

		public readonly bool Equals (SKDocumentPdfMetadata obj) =>
			Title == obj.Title &&
			Author == obj.Author &&
			Subject == obj.Subject &&
			Keywords == obj.Keywords &&
			Creator == obj.Creator &&
			Producer == obj.Producer &&
			Creation == obj.Creation &&
			Modified == obj.Modified &&
			RasterDpi == obj.RasterDpi &&
			PdfA == obj.PdfA &&
			EncodingQuality == obj.EncodingQuality;

		public readonly override bool Equals (object obj) =>
			obj is SKDocumentPdfMetadata f && Equals (f);

		public static bool operator == (SKDocumentPdfMetadata left, SKDocumentPdfMetadata right) =>
			left.Equals (right);

		public static bool operator != (SKDocumentPdfMetadata left, SKDocumentPdfMetadata right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (Title);
			hash.Add (Author);
			hash.Add (Subject);
			hash.Add (Keywords);
			hash.Add (Creator);
			hash.Add (Producer);
			hash.Add (Creation);
			hash.Add (Modified);
			hash.Add (RasterDpi);
			hash.Add (PdfA);
			hash.Add (EncodingQuality);
			return hash.ToHashCode ();
		}
	}

public enum SKFontStyleWeight
	{
		Invisible = 0,
		Thin = 100,
		ExtraLight = 200,
		Light = 300,
		Normal = 400,
		Medium = 500,
		SemiBold = 600,
		Bold = 700,
		ExtraBold = 800,
		Black = 900,
		ExtraBlack = 1000,
	}

public enum SKFontStyleWidth
	{
		UltraCondensed = 1,
		ExtraCondensed = 2,
		Condensed = 3,
		SemiCondensed = 4,
		Normal = 5,
		SemiExpanded = 6,
		Expanded = 7,
		ExtraExpanded = 8,
		UltraExpanded = 9,
	}
