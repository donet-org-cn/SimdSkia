// Forwarding and path text selection from SkiaSharp v3.119.4 (MIT).
#pragma warning disable CS0618
namespace SkiaSharp;
public partial class SKCanvas
{
		[Obsolete ("Use DrawTextOnPath(string text, SKPath path, float hOffset, float vOffset, SKTextAlign textAlign, SKFont font, SKPaint paint) instead.")]
		public void DrawTextOnPath (string text, SKPath path, SKPoint offset, SKPaint paint) =>
			DrawTextOnPath (text, path, offset, true, paint);

		[Obsolete ("Use DrawTextOnPath(string text, SKPath path, float hOffset, float vOffset, SKTextAlign textAlign, SKFont font, SKPaint paint) instead.")]
		public void DrawTextOnPath (string text, SKPath path, float hOffset, float vOffset, SKPaint paint) =>
			DrawTextOnPath (text, path, new SKPoint (hOffset, vOffset), true, paint);

		[Obsolete ("Use DrawTextOnPath(string text, SKPath path, SKPoint offset, bool warpGlyphs, SKTextAlign textAlign, SKFont font, SKPaint paint) instead.")]
		public void DrawTextOnPath (string text, SKPath path, SKPoint offset, bool warpGlyphs, SKPaint paint) =>
			DrawTextOnPath (text, path, offset, warpGlyphs, paint.GetFont (), paint);

		public void DrawTextOnPath (string text, SKPath path, SKPoint offset, SKFont font, SKPaint paint) =>
#pragma warning disable CS0618 // Type or member is obsolete (TODO: replace paint.TextAlign with SKTextAlign.Left)
			DrawTextOnPath (text, path, offset, true, paint.TextAlign, font, paint);
#pragma warning restore CS0618 // Type or member is obsolete

		public void DrawTextOnPath (string text, SKPath path, SKPoint offset, SKTextAlign textAlign, SKFont font, SKPaint paint) =>
			DrawTextOnPath (text, path, offset, true, textAlign, font, paint);

		public void DrawTextOnPath (string text, SKPath path, float hOffset, float vOffset, SKFont font, SKPaint paint) =>
#pragma warning disable CS0618 // Type or member is obsolete (TODO: replace paint.TextAlign with SKTextAlign.Left)
			DrawTextOnPath (text, path, new SKPoint (hOffset, vOffset), true, paint.TextAlign, font, paint);
#pragma warning restore CS0618 // Type or member is obsolete

		public void DrawTextOnPath (string text, SKPath path, float hOffset, float vOffset, SKTextAlign textAlign, SKFont font, SKPaint paint) =>
			DrawTextOnPath (text, path, new SKPoint (hOffset, vOffset), true, textAlign, font, paint);

		public void DrawTextOnPath (string text, SKPath path, SKPoint offset, bool warpGlyphs, SKFont font, SKPaint paint) =>
#pragma warning disable CS0618 // Type or member is obsolete (TODO: replace paint.TextAlign with SKTextAlign.Left)
			DrawTextOnPath (text, path, offset, warpGlyphs, paint.TextAlign, font, paint);
#pragma warning restore CS0618 // Type or member is obsolete

		public void DrawTextOnPath (string text, SKPath path, SKPoint offset, bool warpGlyphs, SKTextAlign textAlign, SKFont font, SKPaint paint)
		{
			Validate();
            if (text == null)
				throw new ArgumentNullException (nameof (text));
			if (path == null)
				throw new ArgumentNullException (nameof (path));
			if (font == null)
				throw new ArgumentNullException (nameof (font));
			if (paint == null)
				throw new ArgumentNullException (nameof (paint));

			if (warpGlyphs) {
				using var textPath = font.GetTextPathOnPath (text, path, textAlign, offset);
				DrawPath (textPath, paint);
			} else {
				using var blob = SKTextBlob.CreatePathPositioned (text, font, path, textAlign, offset);
				if (blob != null)
					DrawText (blob, 0, 0, paint);
			}
		}

}
