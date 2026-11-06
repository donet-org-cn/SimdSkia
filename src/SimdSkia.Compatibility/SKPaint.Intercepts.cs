// SkiaSharp v3.119.4 legacy forwarding contracts, MIT.
#nullable disable
#pragma warning disable CS0618
#pragma warning disable CA2265 // Preserve the baseline's default-span identity check and its exception behavior.
namespace SkiaSharp;
public partial class SKPaint
{
		// GetTextIntercepts

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetTextIntercepts (string text, float x, float y, float upperBounds, float lowerBounds) =>
			GetTextIntercepts (text.AsSpan (), x, y, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetTextIntercepts (ReadOnlySpan<char> text, float x, float y, float upperBounds, float lowerBounds)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.Create (text, GetFont (), new SKPoint (x, y));
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetTextIntercepts (byte[] text, float x, float y, float upperBounds, float lowerBounds) =>
			GetTextIntercepts (text.AsSpan (), x, y, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetTextIntercepts (ReadOnlySpan<byte> text, float x, float y, float upperBounds, float lowerBounds)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.Create (text, TextEncoding, GetFont (), new SKPoint (x, y));
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetTextIntercepts (IntPtr text, IntPtr length, float x, float y, float upperBounds, float lowerBounds) =>
			GetTextIntercepts (text, (int)length, x, y, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetTextIntercepts (IntPtr text, int length, float x, float y, float upperBounds, float lowerBounds)
		{
			if (text == IntPtr.Zero && length != 0)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.Create (text, length, TextEncoding, GetFont (), new SKPoint (x, y));
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

		// GetTextIntercepts (SKTextBlob)

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetTextIntercepts (SKTextBlob text, float upperBounds, float lowerBounds)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			return text.GetIntercepts (upperBounds, lowerBounds, this);
		}

		// GetPositionedTextIntercepts

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetPositionedTextIntercepts (string text, SKPoint[] positions, float upperBounds, float lowerBounds) =>
			GetPositionedTextIntercepts (text.AsSpan (), positions, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetPositionedTextIntercepts (ReadOnlySpan<char> text, ReadOnlySpan<SKPoint> positions, float upperBounds, float lowerBounds)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.CreatePositioned (text, GetFont (), positions);
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetPositionedTextIntercepts (byte[] text, SKPoint[] positions, float upperBounds, float lowerBounds) =>
			GetPositionedTextIntercepts (text.AsSpan (), positions, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetPositionedTextIntercepts (ReadOnlySpan<byte> text, ReadOnlySpan<SKPoint> positions, float upperBounds, float lowerBounds)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.CreatePositioned (text, TextEncoding, GetFont (), positions);
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetPositionedTextIntercepts (IntPtr text, int length, SKPoint[] positions, float upperBounds, float lowerBounds) =>
			GetPositionedTextIntercepts (text, (IntPtr)length, positions, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetPositionedTextIntercepts (IntPtr text, IntPtr length, SKPoint[] positions, float upperBounds, float lowerBounds)
		{
			if (text == IntPtr.Zero && length != IntPtr.Zero)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.CreatePositioned (text, (int)length, TextEncoding, GetFont (), positions);
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

		// GetHorizontalTextIntercepts

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetHorizontalTextIntercepts (string text, float[] xpositions, float y, float upperBounds, float lowerBounds) =>
			GetHorizontalTextIntercepts (text.AsSpan (), xpositions, y, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetHorizontalTextIntercepts (ReadOnlySpan<char> text, ReadOnlySpan<float> xpositions, float y, float upperBounds, float lowerBounds)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.CreateHorizontal (text, GetFont (), xpositions, y);
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetHorizontalTextIntercepts (byte[] text, float[] xpositions, float y, float upperBounds, float lowerBounds) =>
			GetHorizontalTextIntercepts (text.AsSpan (), xpositions, y, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetHorizontalTextIntercepts (ReadOnlySpan<byte> text, ReadOnlySpan<float> xpositions, float y, float upperBounds, float lowerBounds)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.CreateHorizontal (text, TextEncoding, GetFont (), xpositions, y);
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetHorizontalTextIntercepts (IntPtr text, int length, float[] xpositions, float y, float upperBounds, float lowerBounds) =>
			GetHorizontalTextIntercepts (text, (IntPtr)length, xpositions, y, upperBounds, lowerBounds);

		[Obsolete ($"Use {nameof (SKTextBlob)}.{nameof (SKTextBlob.GetIntercepts)}() instead.")]
		public float[] GetHorizontalTextIntercepts (IntPtr text, IntPtr length, float[] xpositions, float y, float upperBounds, float lowerBounds)
		{
			if (text == IntPtr.Zero && length != IntPtr.Zero)
				throw new ArgumentNullException (nameof (text));

			using var blob = SKTextBlob.CreateHorizontal (text, (int)length, TextEncoding, GetFont (), xpositions, y);
			return blob.GetIntercepts (upperBounds, lowerBounds, this);
		}

}
