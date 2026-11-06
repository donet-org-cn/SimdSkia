// Legacy forwarding overloads: SkiaSharp v3.119.4, MIT.
#nullable disable
#pragma warning disable CS0618
using System;
namespace SkiaSharp;
public partial class SKPaint
{
    [Obsolete("Use SKFont instead.")]
    public SKFont ToFont()=>GetFont().CloneManaged();
    private SKFont legacyFont;
    private SKTextAlign textAlign;
    private SKTextEncoding textEncoding;
    internal SKFont GetFont(){ThrowIfDisposed();return legacyFont??=new SKFont(){Edging=antialias?(lcdRenderText?SKFontEdging.SubpixelAntialias:SKFontEdging.Antialias):SKFontEdging.Alias};}
    [Obsolete("Use SKFont instead.")]
    public SKPaint(SKFont font){ArgumentNullException.ThrowIfNull(font);legacyFont=font.CloneManaged();}
    [Obsolete("Use SKTextAlign method overloads instead.")]
    public SKTextAlign TextAlign{get{ThrowIfDisposed();return textAlign;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));textAlign=value;}}
    [Obsolete("Use SKTextEncoding method overloads instead.")]
    public SKTextEncoding TextEncoding{get{ThrowIfDisposed();return textEncoding;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));textEncoding=value;}}
[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.LinearMetrics)} instead.")]
		public bool IsLinearText {
			get => GetFont ().LinearMetrics;
			set => GetFont ().LinearMetrics = value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.Subpixel)} instead.")]
		public bool SubpixelText {
			get => GetFont ().Subpixel;
			set => GetFont ().Subpixel = value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.ForceAutoHinting)} instead.")]
		public bool IsAutohinted {
			get => GetFont ().ForceAutoHinting;
			set => GetFont ().ForceAutoHinting = value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.Hinting)} instead.")]
		public SKPaintHinting HintingLevel {
			get => (SKPaintHinting)GetFont ().Hinting;
			set => GetFont ().Hinting = (SKFontHinting)value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.Embolden)} instead.")]
		public bool FakeBoldText {
			get => GetFont ().Embolden;
			set => GetFont ().Embolden = value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.Typeface)} instead.")]
		public SKTypeface Typeface {
			get => GetFont ().Typeface;
			set => GetFont ().Typeface = value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.Size)} instead.")]
		public float TextSize {
			get => GetFont ().Size;
			set => GetFont ().Size = value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.ScaleX)} instead.")]
		public float TextScaleX {
			get => GetFont ().ScaleX;
			set => GetFont ().ScaleX = value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.SkewX)} instead.")]
		public float TextSkewX {
			get => GetFont ().SkewX;
			set => GetFont ().SkewX = value;
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.Spacing)} instead.")]
		public float FontSpacing =>
			GetFont ().Spacing;

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.Metrics)} instead.")]
		public SKFontMetrics FontMetrics {
			get {
				return GetFont ().Metrics;
			}
		}

[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetFontMetrics)}() instead.")]
		public float GetFontMetrics (out SKFontMetrics metrics) =>
			GetFont ().GetFontMetrics (out metrics);
		// MeasureText

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (string text) =>
			GetFont ().MeasureText (text, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (ReadOnlySpan<char> text) =>
			GetFont ().MeasureText (text, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (byte[] text) =>
			GetFont ().MeasureText (text, TextEncoding, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (ReadOnlySpan<byte> text) =>
			GetFont ().MeasureText (text, TextEncoding, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (IntPtr buffer, int length) =>
			GetFont ().MeasureText (buffer, length, TextEncoding, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (IntPtr buffer, IntPtr length) =>
			GetFont ().MeasureText (buffer, (int)length, TextEncoding, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (string text, ref SKRect bounds) =>
			GetFont ().MeasureText (text, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (ReadOnlySpan<char> text, ref SKRect bounds) =>
			GetFont ().MeasureText (text, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (byte[] text, ref SKRect bounds) =>
			GetFont ().MeasureText (text, TextEncoding, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (ReadOnlySpan<byte> text, ref SKRect bounds) =>
			GetFont ().MeasureText (text, TextEncoding, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (IntPtr buffer, int length, ref SKRect bounds) =>
			GetFont ().MeasureText (buffer, length, TextEncoding, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.MeasureText)}() instead.")]
		public float MeasureText (IntPtr buffer, IntPtr length, ref SKRect bounds) =>
			GetFont ().MeasureText (buffer, (int)length, TextEncoding, out bounds, this);

		// BreakText

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (string text, float maxWidth) =>
			GetFont ().BreakText (text, maxWidth, out _, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (string text, float maxWidth, out float measuredWidth) =>
			GetFont ().BreakText (text, maxWidth, out measuredWidth, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (string text, float maxWidth, out float measuredWidth, out string measuredText)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			var charsRead = GetFont ().BreakText (text, maxWidth, out measuredWidth, this);
			if (charsRead == 0) {
				measuredText = string.Empty;
				return 0;
			}
			if (charsRead == text.Length) {
				measuredText = text;
				return text.Length;
			}
			measuredText = text.Substring (0, charsRead);
			return charsRead;
		}

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (ReadOnlySpan<char> text, float maxWidth) =>
			GetFont ().BreakText (text, maxWidth, out _, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (ReadOnlySpan<char> text, float maxWidth, out float measuredWidth) =>
			GetFont ().BreakText (text, maxWidth, out measuredWidth, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (byte[] text, float maxWidth) =>
			GetFont ().BreakText (text, TextEncoding, maxWidth, out _, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (byte[] text, float maxWidth, out float measuredWidth) =>
			GetFont ().BreakText (text, TextEncoding, maxWidth, out measuredWidth, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (ReadOnlySpan<byte> text, float maxWidth) =>
			GetFont ().BreakText (text, TextEncoding, maxWidth, out _, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (ReadOnlySpan<byte> text, float maxWidth, out float measuredWidth) =>
			GetFont ().BreakText (text, TextEncoding, maxWidth, out measuredWidth, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (IntPtr buffer, int length, float maxWidth) =>
			GetFont ().BreakText (buffer, length, TextEncoding, maxWidth, out _, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (IntPtr buffer, int length, float maxWidth, out float measuredWidth) =>
			GetFont ().BreakText (buffer, length, TextEncoding, maxWidth, out measuredWidth, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (IntPtr buffer, IntPtr length, float maxWidth) =>
			GetFont ().BreakText (buffer, (int)length, TextEncoding, maxWidth, out _, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.BreakText)}() instead.")]
		public long BreakText (IntPtr buffer, IntPtr length, float maxWidth, out float measuredWidth) =>
			GetFont ().BreakText (buffer, (int)length, TextEncoding, maxWidth, out measuredWidth, this);

		// GetTextPath

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (string text, float x, float y) =>
			GetFont ().GetTextPath (text, new SKPoint (x, y));

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (ReadOnlySpan<char> text, float x, float y) =>
			GetFont ().GetTextPath (text, new SKPoint (x, y));

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (byte[] text, float x, float y) =>
			GetFont ().GetTextPath (text, TextEncoding, new SKPoint (x, y));

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (ReadOnlySpan<byte> text, float x, float y) =>
			GetFont ().GetTextPath (text, TextEncoding, new SKPoint (x, y));

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (IntPtr buffer, int length, float x, float y) =>
			GetFont ().GetTextPath (buffer, length, TextEncoding, new SKPoint (x, y));

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (IntPtr buffer, IntPtr length, float x, float y) =>
			GetFont ().GetTextPath (buffer, (int)length, TextEncoding, new SKPoint (x, y));

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (string text, SKPoint[] points) =>
			GetFont ().GetTextPath (text, points);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (ReadOnlySpan<char> text, ReadOnlySpan<SKPoint> points) =>
			GetFont ().GetTextPath (text, points);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (byte[] text, SKPoint[] points) =>
			GetFont ().GetTextPath (text, TextEncoding, points);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (ReadOnlySpan<byte> text, ReadOnlySpan<SKPoint> points) =>
			GetFont ().GetTextPath (text, TextEncoding, points);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (IntPtr buffer, int length, SKPoint[] points) =>
			GetFont ().GetTextPath (buffer, length, TextEncoding, points);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (IntPtr buffer, int length, ReadOnlySpan<SKPoint> points) =>
			GetFont ().GetTextPath (buffer, length, TextEncoding, points);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetTextPath)}() instead.")]
		public SKPath GetTextPath (IntPtr buffer, IntPtr length, SKPoint[] points) =>
			GetFont ().GetTextPath (buffer, (int)length, TextEncoding, points);
		// CountGlyphs

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.CountGlyphs)}() instead.")]
		public int CountGlyphs (string text) =>
			GetFont ().CountGlyphs (text);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.CountGlyphs)}() instead.")]
		public int CountGlyphs (ReadOnlySpan<char> text) =>
			GetFont ().CountGlyphs (text);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.CountGlyphs)}() instead.")]
		public int CountGlyphs (byte[] text) =>
			GetFont ().CountGlyphs (text, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.CountGlyphs)}() instead.")]
		public int CountGlyphs (ReadOnlySpan<byte> text) =>
			GetFont ().CountGlyphs (text, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.CountGlyphs)}() instead.")]
		public int CountGlyphs (IntPtr text, int length) =>
			GetFont ().CountGlyphs (text, length, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.CountGlyphs)}() instead.")]
		public int CountGlyphs (IntPtr text, IntPtr length) =>
			GetFont ().CountGlyphs (text, (int)length, TextEncoding);

		// GetGlyphs

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphs)}() instead.")]
		public ushort[] GetGlyphs (string text) =>
			GetFont ().GetGlyphs (text);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphs)}() instead.")]
		public ushort[] GetGlyphs (ReadOnlySpan<char> text) =>
			GetFont ().GetGlyphs (text);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphs)}() instead.")]
		public ushort[] GetGlyphs (byte[] text) =>
			GetFont ().GetGlyphs (text, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphs)}() instead.")]
		public ushort[] GetGlyphs (ReadOnlySpan<byte> text) =>
			GetFont ().GetGlyphs (text, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphs)}() instead.")]
		public ushort[] GetGlyphs (IntPtr text, int length) =>
			GetFont ().GetGlyphs (text, length, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphs)}() instead.")]
		public ushort[] GetGlyphs (IntPtr text, IntPtr length) =>
			GetFont ().GetGlyphs (text, (int)length, TextEncoding);

		// ContainsGlyphs

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.ContainsGlyphs)}() instead.")]
		public bool ContainsGlyphs (string text) =>
			GetFont ().ContainsGlyphs (text);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.ContainsGlyphs)}() instead.")]
		public bool ContainsGlyphs (ReadOnlySpan<char> text) =>
			GetFont ().ContainsGlyphs (text);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.ContainsGlyphs)}() instead.")]
		public bool ContainsGlyphs (byte[] text) =>
			GetFont ().ContainsGlyphs (text, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.ContainsGlyphs)}() instead.")]
		public bool ContainsGlyphs (ReadOnlySpan<byte> text) =>
			GetFont ().ContainsGlyphs (text, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.ContainsGlyphs)}() instead.")]
		public bool ContainsGlyphs (IntPtr text, int length) =>
			GetFont ().ContainsGlyphs (text, length, TextEncoding);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.ContainsGlyphs)}() instead.")]
		public bool ContainsGlyphs (IntPtr text, IntPtr length) =>
			GetFont ().ContainsGlyphs (text, (int)length, TextEncoding);

		// GetGlyphPositions

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphPositions)}() instead.")]
		public SKPoint[] GetGlyphPositions (string text, SKPoint origin = default) =>
			GetFont ().GetGlyphPositions (text, origin);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphPositions)}() instead.")]
		public SKPoint[] GetGlyphPositions (ReadOnlySpan<char> text, SKPoint origin = default) =>
			GetFont ().GetGlyphPositions (text, origin);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphPositions)}() instead.")]
		public SKPoint[] GetGlyphPositions (ReadOnlySpan<byte> text, SKPoint origin = default) =>
			GetFont ().GetGlyphPositions (text, TextEncoding, origin);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphPositions)}() instead.")]
		public SKPoint[] GetGlyphPositions (IntPtr text, int length, SKPoint origin = default) =>
			GetFont ().GetGlyphPositions (text, length, TextEncoding, origin);

		// GetGlyphOffsets

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphOffsets)}() instead.")]
		public float[] GetGlyphOffsets (string text, float origin = 0f) =>
			GetFont ().GetGlyphOffsets (text, origin);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphOffsets)}() instead.")]
		public float[] GetGlyphOffsets (ReadOnlySpan<char> text, float origin = 0f) =>
			GetFont ().GetGlyphOffsets (text, origin);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphOffsets)}() instead.")]
		public float[] GetGlyphOffsets (ReadOnlySpan<byte> text, float origin = 0f) =>
			GetFont ().GetGlyphOffsets (text, TextEncoding, origin);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphOffsets)}() instead.")]
		public float[] GetGlyphOffsets (IntPtr text, int length, float origin = 0f) =>
			GetFont ().GetGlyphOffsets (text, length, TextEncoding, origin);

		// GetGlyphWidths

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (string text) =>
			GetFont ().GetGlyphWidths (text, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (ReadOnlySpan<char> text) =>
			GetFont ().GetGlyphWidths (text, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (byte[] text) =>
			GetFont ().GetGlyphWidths (text, TextEncoding, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (ReadOnlySpan<byte> text) =>
			GetFont ().GetGlyphWidths (text, TextEncoding, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (IntPtr text, int length) =>
			GetFont ().GetGlyphWidths (text, length, TextEncoding, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (IntPtr text, IntPtr length) =>
			GetFont ().GetGlyphWidths (text, (int)length, TextEncoding, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (string text, out SKRect[] bounds) =>
			GetFont ().GetGlyphWidths (text, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (ReadOnlySpan<char> text, out SKRect[] bounds) =>
			GetFont ().GetGlyphWidths (text, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (byte[] text, out SKRect[] bounds) =>
			GetFont ().GetGlyphWidths (text, TextEncoding, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (ReadOnlySpan<byte> text, out SKRect[] bounds) =>
			GetFont ().GetGlyphWidths (text, TextEncoding, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (IntPtr text, int length, out SKRect[] bounds) =>
			GetFont ().GetGlyphWidths (text, length, TextEncoding, out bounds, this);

		[Obsolete ($"Use {nameof (SKFont)}.{nameof (SKFont.GetGlyphWidths)}() instead.")]
		public float[] GetGlyphWidths (IntPtr text, IntPtr length, out SKRect[] bounds) =>
			GetFont ().GetGlyphWidths (text, (int)length, TextEncoding, out bounds, this);

}
