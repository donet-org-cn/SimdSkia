// Public forwarding overloads adapted from SkiaSharp v3.119.4, MIT.
#nullable disable
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
namespace SkiaSharp;
public delegate void SKGlyphPathDelegate(SKPath path,SKMatrix matrix);
public unsafe partial class SKFont
{
		// GetGlyphs

		public ushort[] GetGlyphs (string text) =>
			GetGlyphs (text.AsSpan ());

		public ushort[] GetGlyphs (ReadOnlySpan<char> text)
		{
			fixed (void* t = text) {
				return GetGlyphs (t, text.Length * 2, SKTextEncoding.Utf16);
			}
		}

		public ushort[] GetGlyphs (ReadOnlySpan<byte> text, SKTextEncoding encoding)
		{
			fixed (void* t = text) {
				return GetGlyphs (t, text.Length, encoding);
			}
		}

		public ushort[] GetGlyphs (IntPtr text, int length, SKTextEncoding encoding) =>
			GetGlyphs ((void*)text, length, encoding);

		public void GetGlyphs (string text, Span<ushort> glyphs) =>
			GetGlyphs (text.AsSpan (), glyphs);

		public void GetGlyphs (ReadOnlySpan<char> text, Span<ushort> glyphs)
		{
			fixed (void* t = text) {
				GetGlyphs (t, text.Length * 2, SKTextEncoding.Utf16, glyphs);
			}
		}

		public void GetGlyphs (ReadOnlySpan<byte> text, SKTextEncoding encoding, Span<ushort> glyphs)
		{
			fixed (void* t = text) {
				GetGlyphs (t, text.Length, encoding, glyphs);
			}
		}

		public void GetGlyphs (IntPtr text, int length, SKTextEncoding encoding, Span<ushort> glyphs) =>
			GetGlyphs ((void*)text, length, encoding, glyphs);

		internal ushort[] GetGlyphs (void* text, int length, SKTextEncoding encoding)
		{
			if (!ValidateTextArgs (text, length, encoding))
				return new ushort[0];

			var n = CountGlyphs (text, length, encoding);
			if (n <= 0)
				return new ushort[0];

			var glyphs = new ushort[n];
			GetGlyphs (text, length, encoding, glyphs);
			return glyphs;
		}

		internal void GetGlyphs (void* text, int length, SKTextEncoding encoding, Span<ushort> glyphs)
		{
            if(!ValidateTextArgs(text,length,encoding))return;
            ThrowIfDisposed();var data=new ReadOnlySpan<byte>(text,length);int index=0;
            while(TryDecode(ref data,encoding,out int codepoint)&&index<glyphs.Length)glyphs[index++]=encoding==SKTextEncoding.GlyphId?(ushort)codepoint:MapScalar(codepoint);
		}

		// ContainsGlyph

		public bool ContainsGlyph (int codepoint) =>
			GetGlyph (codepoint) != 0;

		// ContainsGlyphs

		public bool ContainsGlyphs (ReadOnlySpan<int> codepoints) =>
			ContainsGlyphs (GetGlyphs (codepoints));

		public bool ContainsGlyphs (string text) =>
			ContainsGlyphs (GetGlyphs (text));

		public bool ContainsGlyphs (ReadOnlySpan<char> text) =>
			ContainsGlyphs (GetGlyphs (text));

		public bool ContainsGlyphs (ReadOnlySpan<byte> text, SKTextEncoding encoding) =>
			ContainsGlyphs (GetGlyphs (text, encoding));

		public bool ContainsGlyphs (IntPtr text, int length, SKTextEncoding encoding) =>
			ContainsGlyphs (GetGlyphs (text, length, encoding));

		private bool ContainsGlyphs (ushort[] glyphs) =>
			Array.IndexOf (glyphs, (ushort)0) == -1;

		// CountGlyphs

		public int CountGlyphs (string text) =>
			CountGlyphs (text.AsSpan ());

		public int CountGlyphs (ReadOnlySpan<char> text)
		{
			fixed (void* t = text) {
				return CountGlyphs (t, text.Length * 2, SKTextEncoding.Utf16);
			}
		}

		public int CountGlyphs (ReadOnlySpan<byte> text, SKTextEncoding encoding)
		{
			fixed (void* t = text) {
				return CountGlyphs (t, text.Length, encoding);
			}
		}

		public int CountGlyphs (IntPtr text, int length, SKTextEncoding encoding) =>
			CountGlyphs ((void*)text, length, encoding);

		internal int CountGlyphs (void* text, int length, SKTextEncoding encoding)
		{
            if(!ValidateTextArgs(text,length,encoding))return 0;
            ThrowIfDisposed();var data=new ReadOnlySpan<byte>(text,length);int count=0;while(TryDecode(ref data,encoding,out _))count++;return data.IsEmpty?count:0;
		}

		// MeasureText (text)

		public float MeasureText (string text, SKPaint paint = null) =>
			MeasureText (text.AsSpan (), paint);

		public float MeasureText (ReadOnlySpan<char> text, SKPaint paint = null)
		{
			fixed (void* t = text) {
				return MeasureText (t, text.Length * 2, SKTextEncoding.Utf16, null, paint);
			}
		}

		public float MeasureText (ReadOnlySpan<byte> text, SKTextEncoding encoding, SKPaint paint = null)
		{
			fixed (void* t = text) {
				return MeasureText (t, text.Length, encoding, null, paint);
			}
		}

		public float MeasureText (IntPtr text, int length, SKTextEncoding encoding, SKPaint paint = null) =>
			MeasureText ((void*)text, length, encoding, null, paint);

		public float MeasureText (string text, out SKRect bounds, SKPaint paint = null) =>
			MeasureText (text.AsSpan (), out bounds, paint);

		public float MeasureText (ReadOnlySpan<char> text, out SKRect bounds, SKPaint paint = null)
		{
			fixed (void* t = text)
			fixed (SKRect* b = &bounds) {
				return MeasureText (t, text.Length * 2, SKTextEncoding.Utf16, b, paint);
			}
		}

		public float MeasureText (ReadOnlySpan<byte> text, SKTextEncoding encoding, out SKRect bounds, SKPaint paint = null)
		{
			fixed (void* t = text)
			fixed (SKRect* b = &bounds) {
				return MeasureText (t, text.Length, encoding, b, paint);
			}
		}

		public float MeasureText (IntPtr text, int length, SKTextEncoding encoding, out SKRect bounds, SKPaint paint = null)
		{
			fixed (SKRect* b = &bounds) {
				return MeasureText ((void*)text, length, encoding, b, paint);
			}
		}

		internal float MeasureText (void* text, int length, SKTextEncoding encoding, SKRect* bounds, SKPaint paint)
		{
            if(!ValidateTextArgs(text,length,encoding))return 0;
            ThrowIfDisposed();ValidateMetricsPaint(paint);var data=new ReadOnlySpan<byte>(text,length);float width=0;SKRect box=default;bool have=false;
            while(TryDecode(ref data,encoding,out int codepoint))
            {ushort glyph=encoding==SKTextEncoding.GlyphId?(ushort)codepoint:MapScalar(codepoint);if(bounds!=null){var r=GlyphBounds(glyph);if(!r.IsEmpty){r.Offset(width,0);box=have?SKRect.Union(box,r):r;have=true;}}width+=GlyphAdvance(glyph);}
            if(bounds!=null)*bounds=box;return width;
		}

		// MeasureText (glyphs)

		public float MeasureText (ReadOnlySpan<ushort> glyphs, SKPaint paint = null)
		{
			fixed (ushort* gp = glyphs) {
				return MeasureText (gp, glyphs.Length * 2, SKTextEncoding.GlyphId, null, paint);
			}
		}

		public float MeasureText (ReadOnlySpan<ushort> glyphs, out SKRect bounds, SKPaint paint = null)
		{
			fixed (ushort* gp = glyphs)
			fixed (SKRect* b = &bounds) {
				return MeasureText (gp, glyphs.Length * 2, SKTextEncoding.GlyphId, b, paint);
			}
		}

		// BreakText

		public int BreakText (string text, float maxWidth, SKPaint paint = null) =>
			BreakText (text.AsSpan (), maxWidth, out _, paint);

		public int BreakText (string text, float maxWidth, out float measuredWidth, SKPaint paint = null) =>
			BreakText (text.AsSpan (), maxWidth, out measuredWidth, paint);

		public int BreakText (ReadOnlySpan<char> text, float maxWidth, SKPaint paint = null) =>
			BreakText (text, maxWidth, out _, paint);

		public int BreakText (ReadOnlySpan<char> text, float maxWidth, out float measuredWidth, SKPaint paint = null)
		{
			fixed (void* t = text)
			fixed (float* mw = &measuredWidth) {
				var bytesRead = BreakText (t, text.Length * 2, SKTextEncoding.Utf16, maxWidth, mw, paint);
				return bytesRead / 2;
			}
		}

		public int BreakText (ReadOnlySpan<byte> text, SKTextEncoding encoding, float maxWidth, SKPaint paint = null) =>
			BreakText (text, encoding, maxWidth, out _, paint);

		public int BreakText (ReadOnlySpan<byte> text, SKTextEncoding encoding, float maxWidth, out float measuredWidth, SKPaint paint = null)
		{
			fixed (void* t = text)
			fixed (float* mw = &measuredWidth) {
				return BreakText (t, text.Length, encoding, maxWidth, mw, paint);
			}
		}

		public int BreakText (IntPtr text, int length, SKTextEncoding encoding, float maxWidth, SKPaint paint = null) =>
			BreakText (text, length, encoding, maxWidth, out _, paint);

		public int BreakText (IntPtr text, int length, SKTextEncoding encoding, float maxWidth, out float measuredWidth, SKPaint paint = null)
		{
			fixed (float* mw = &measuredWidth) {
				return BreakText ((void*)text, length, encoding, maxWidth, mw, paint);
			}
		}

		internal int BreakText (void* text, int length, SKTextEncoding encoding, float maxWidth, float* measuredWidth, SKPaint paint)
		{
            if(!ValidateTextArgs(text,length,encoding))return 0;
            ThrowIfDisposed();ValidateMetricsPaint(paint);var data=new ReadOnlySpan<byte>(text,length);float width=0;int consumed=0;
            while(!data.IsEmpty){int before=data.Length;if(!TryDecode(ref data,encoding,out int cp))break;float next=width+GlyphAdvance(encoding==SKTextEncoding.GlyphId?(ushort)cp:MapScalar(cp));if(next>maxWidth)break;width=next;consumed+=before-data.Length;}
            if(measuredWidth!=null)*measuredWidth=width;return consumed;
		}

		// GetGlyphPositions (text)

		public SKPoint[] GetGlyphPositions (string text, SKPoint origin = default) =>
			GetGlyphPositions (text.AsSpan (), origin);

		public SKPoint[] GetGlyphPositions (ReadOnlySpan<char> text, SKPoint origin = default)
		{
			fixed (void* t = text) {
				return GetGlyphPositions (t, text.Length * 2, SKTextEncoding.Utf16, origin);
			}
		}

		public SKPoint[] GetGlyphPositions (ReadOnlySpan<byte> text, SKTextEncoding encoding, SKPoint origin = default)
		{
			fixed (void* t = text) {
				return GetGlyphPositions (t, text.Length, encoding, origin);
			}
		}

		public SKPoint[] GetGlyphPositions (IntPtr text, int length, SKTextEncoding encoding, SKPoint origin = default) =>
			GetGlyphPositions ((void*)text, length, encoding, origin);

		public void GetGlyphPositions (string text, Span<SKPoint> offsets, SKPoint origin = default) =>
			GetGlyphPositions (text.AsSpan (), offsets, origin);

		public void GetGlyphPositions (ReadOnlySpan<char> text, Span<SKPoint> offsets, SKPoint origin = default)
		{
			fixed (void* t = text) {
				GetGlyphPositions (t, text.Length * 2, SKTextEncoding.Utf16, offsets, origin);
			}
		}

		public void GetGlyphPositions (ReadOnlySpan<byte> text, SKTextEncoding encoding, Span<SKPoint> offsets, SKPoint origin = default)
		{
			fixed (void* t = text) {
				GetGlyphPositions (t, text.Length, encoding, offsets, origin);
			}
		}

		public void GetGlyphPositions (IntPtr text, int length, SKTextEncoding encoding, Span<SKPoint> offsets, SKPoint origin = default) =>
			GetGlyphPositions ((void*)text, length, encoding, offsets, origin);

		internal SKPoint[] GetGlyphPositions (void* text, int length, SKTextEncoding encoding, SKPoint origin)
		{
			if (!ValidateTextArgs (text, length, encoding))
				return new SKPoint[0];

			var n = CountGlyphs (text, length, encoding);
			if (n <= 0)
				return new SKPoint[0];

			var positions = new SKPoint[n];
			GetGlyphPositions (text, length, encoding, positions, origin);
			return positions;
		}

		internal void GetGlyphPositions (void* text, int length, SKTextEncoding encoding, Span<SKPoint> offsets, SKPoint origin)
		{
            if(!ValidateTextArgs(text,length,encoding))return;var data=new ReadOnlySpan<byte>(text,length);float x=origin.X;
            for(int i=0;i<offsets.Length;i++){if(!TryDecode(ref data,encoding,out int cp))throw new ArgumentException("Destination is longer than the glyph count.",nameof(offsets));offsets[i]=new(x,origin.Y);x+=GlyphAdvance(encoding==SKTextEncoding.GlyphId?(ushort)cp:MapScalar(cp));}
		}

		// GetGlyphPositions (glyphs)

		public SKPoint[] GetGlyphPositions (ReadOnlySpan<ushort> glyphs, SKPoint origin = default)
		{
			var positions = new SKPoint[glyphs.Length];
			GetGlyphPositions (glyphs, positions, origin);
			return positions;
		}

		public void GetGlyphPositions (ReadOnlySpan<ushort> glyphs, Span<SKPoint> positions, SKPoint origin = default)
		{
            ThrowIfDisposed();if(glyphs.Length!=positions.Length)throw new ArgumentException("The length of glyphs must be the same as the length of positions.",nameof(positions));float x=origin.X;for(int i=0;i<glyphs.Length;i++){positions[i]=new(x,origin.Y);x+=GlyphAdvance(glyphs[i]);}
		}

		// GetGlyphOffsets (text)

		public float[] GetGlyphOffsets (string text, float origin = 0f) =>
			GetGlyphOffsets (text.AsSpan (), origin);

		public float[] GetGlyphOffsets (ReadOnlySpan<char> text, float origin = 0f)
		{
			fixed (void* t = text) {
				return GetGlyphOffsets (t, text.Length * 2, SKTextEncoding.Utf16, origin);
			}
		}

		public float[] GetGlyphOffsets (ReadOnlySpan<byte> text, SKTextEncoding encoding, float origin = 0f)
		{
			fixed (void* t = text) {
				return GetGlyphOffsets (t, text.Length, encoding, origin);
			}
		}

		public float[] GetGlyphOffsets (IntPtr text, int length, SKTextEncoding encoding, float origin = 0f) =>
			GetGlyphOffsets ((void*)text, length, encoding, origin);

		public void GetGlyphOffsets (string text, Span<float> offsets, float origin = 0f) =>
			GetGlyphOffsets (text.AsSpan (), offsets, origin);

		public void GetGlyphOffsets (ReadOnlySpan<char> text, Span<float> offsets, float origin = 0f)
		{
			fixed (void* t = text) {
				GetGlyphOffsets (t, text.Length * 2, SKTextEncoding.Utf16, offsets, origin);
			}
		}

		public void GetGlyphOffsets (ReadOnlySpan<byte> text, SKTextEncoding encoding, Span<float> offsets, float origin = 0f)
		{
			fixed (void* t = text) {
				GetGlyphOffsets (t, text.Length, encoding, offsets, origin);
			}
		}

		public void GetGlyphOffsets (IntPtr text, int length, SKTextEncoding encoding, Span<float> offsets, float origin = 0f) =>
			GetGlyphOffsets ((void*)text, length, encoding, offsets, origin);

		internal float[] GetGlyphOffsets (void* text, int length, SKTextEncoding encoding, float origin)
		{
			if (!ValidateTextArgs (text, length, encoding))
				return new float[0];

			var n = CountGlyphs (text, length, encoding);
			if (n <= 0)
				return new float[0];

			var offsets = new float[n];
			GetGlyphOffsets (text, length, encoding, offsets, origin);
			return offsets;
		}

		internal void GetGlyphOffsets (void* text, int length, SKTextEncoding encoding, Span<float> offsets, float origin)
		{
            if(!ValidateTextArgs(text,length,encoding))return;var data=new ReadOnlySpan<byte>(text,length);
            for(int i=0;i<offsets.Length;i++){if(!TryDecode(ref data,encoding,out int cp))throw new ArgumentException("Destination is longer than the glyph count.",nameof(offsets));offsets[i]=origin;origin+=GlyphAdvance(encoding==SKTextEncoding.GlyphId?(ushort)cp:MapScalar(cp));}
		}

		// GetGlyphOffsets (glyphs)

		public float[] GetGlyphOffsets (ReadOnlySpan<ushort> glyphs, float origin = 0f)
		{
			var offsets = new float[glyphs.Length];
			GetGlyphOffsets (glyphs, offsets, origin);
			return offsets;
		}

		public void GetGlyphOffsets (ReadOnlySpan<ushort> glyphs, Span<float> offsets, float origin = 0f)
		{
            ThrowIfDisposed();if(glyphs.Length!=offsets.Length)throw new ArgumentException("The length of glyphs must be the same as the length of offsets.",nameof(offsets));for(int i=0;i<glyphs.Length;i++){offsets[i]=origin;origin+=GlyphAdvance(glyphs[i]);}
		}

		// GetGlyphWidths (text)

		public float[] GetGlyphWidths (string text, SKPaint paint = null) =>
			GetGlyphWidths (text.AsSpan (), paint);

		public float[] GetGlyphWidths (ReadOnlySpan<char> text, SKPaint paint = null)
		{
			fixed (void* t = text) {
				return GetGlyphWidths (t, text.Length * 2, SKTextEncoding.Utf16, paint);
			}
		}

		public float[] GetGlyphWidths (ReadOnlySpan<byte> text, SKTextEncoding encoding, SKPaint paint = null)
		{
			fixed (void* t = text) {
				return GetGlyphWidths (t, text.Length, encoding, paint);
			}
		}

		public float[] GetGlyphWidths (IntPtr text, int length, SKTextEncoding encoding, SKPaint paint = null) =>
			GetGlyphWidths ((void*)text, length, encoding, paint);

		public float[] GetGlyphWidths (string text, out SKRect[] bounds, SKPaint paint = null) =>
			GetGlyphWidths (text.AsSpan (), out bounds, paint);

		public float[] GetGlyphWidths (ReadOnlySpan<char> text, out SKRect[] bounds, SKPaint paint = null)
		{
			fixed (void* t = text) {
				return GetGlyphWidths (t, text.Length * 2, SKTextEncoding.Utf16, out bounds, paint);
			}
		}

		public float[] GetGlyphWidths (ReadOnlySpan<byte> text, SKTextEncoding encoding, out SKRect[] bounds, SKPaint paint = null)
		{
			fixed (void* t = text) {
				return GetGlyphWidths (t, text.Length, encoding, out bounds, paint);
			}
		}

		public float[] GetGlyphWidths (IntPtr text, int length, SKTextEncoding encoding, out SKRect[] bounds, SKPaint paint = null) =>
			GetGlyphWidths ((void*)text, length, encoding, out bounds, paint);

		public void GetGlyphWidths (string text, Span<float> widths, Span<SKRect> bounds, SKPaint paint = null) =>
			GetGlyphWidths (text.AsSpan (), widths, bounds, paint);

		public void GetGlyphWidths (ReadOnlySpan<char> text, Span<float> widths, Span<SKRect> bounds, SKPaint paint = null)
		{
			fixed (void* t = text) {
				GetGlyphWidths (t, text.Length * 2, SKTextEncoding.Utf16, widths, bounds, paint);
			}
		}

		public void GetGlyphWidths (ReadOnlySpan<byte> text, SKTextEncoding encoding, Span<float> widths, Span<SKRect> bounds, SKPaint paint = null)
		{
			fixed (void* t = text) {
				GetGlyphWidths (t, text.Length, encoding, widths, bounds, paint);
			}
		}

		public void GetGlyphWidths (IntPtr text, int length, SKTextEncoding encoding, Span<float> widths, Span<SKRect> bounds, SKPaint paint = null) =>
			GetGlyphWidths ((void*)text, length, encoding, widths, bounds, paint);

		internal float[] GetGlyphWidths (void* text, int length, SKTextEncoding encoding, SKPaint paint)
		{
			if (!ValidateTextArgs (text, length, encoding))
				return new float[0];

			var n = CountGlyphs (text, length, encoding);
			if (n <= 0)
				return new float[0];

			var widths = new float[n];
			GetGlyphWidths (text, length, encoding, widths, Span<SKRect>.Empty, paint);
			return widths;
		}

		internal float[] GetGlyphWidths (void* text, int length, SKTextEncoding encoding, out SKRect[] bounds, SKPaint paint)
		{
			if (!ValidateTextArgs (text, length, encoding)) {
				bounds = new SKRect[0];
				return new float[0];
			}

			var n = CountGlyphs (text, length, encoding);
			if (n <= 0) {
				bounds = new SKRect[0];
				return new float[0];
			}

			bounds = new SKRect[n];
			var widths = new float[n];
			GetGlyphWidths (text, length, encoding, widths, bounds, paint);
			return widths;
		}

		internal void GetGlyphWidths (void* text, int length, SKTextEncoding encoding, Span<float> widths, Span<SKRect> bounds, SKPaint paint)
		{
            if(!ValidateTextArgs(text,length,encoding))return;ValidateMetricsPaint(paint);int count=Math.Max(widths.Length,bounds.Length);if((widths.Length!=0&&widths.Length!=count)||(bounds.Length!=0&&bounds.Length!=count))throw new ArgumentException("Metric spans must have equal lengths or be empty.");var data=new ReadOnlySpan<byte>(text,length);
            for(int i=0;i<count;i++){if(!TryDecode(ref data,encoding,out int cp))throw new ArgumentException("Destination is longer than the glyph count.");ushort glyph=encoding==SKTextEncoding.GlyphId?(ushort)cp:MapScalar(cp);if(!widths.IsEmpty)widths[i]=GlyphAdvance(glyph);if(!bounds.IsEmpty)bounds[i]=GlyphBounds(glyph);}
		}

		// GetGlyphWidths (glyphs)

		public float[] GetGlyphWidths (ReadOnlySpan<ushort> glyphs, SKPaint paint = null)
		{
			var widths = new float[glyphs.Length];
			GetGlyphWidths (glyphs, widths, Span<SKRect>.Empty, paint);
			return widths;
		}

		public float[] GetGlyphWidths (ReadOnlySpan<ushort> glyphs, out SKRect[] bounds, SKPaint paint = null)
		{
			var widths = new float[glyphs.Length];
			bounds = new SKRect[glyphs.Length];
			GetGlyphWidths (glyphs, widths, bounds, paint);
			return widths;
		}


		// GetTextPath (text)

		public SKPath GetTextPath (string text, SKPoint origin = default) =>
			GetTextPath (text.AsSpan (), origin);

		public SKPath GetTextPath (ReadOnlySpan<char> text, SKPoint origin = default)
		{
			fixed (void* t = text) {
				return GetTextPath (t, text.Length * 2, SKTextEncoding.Utf16, origin);
			}
		}

		public SKPath GetTextPath (ReadOnlySpan<byte> text, SKTextEncoding encoding, SKPoint origin = default)
		{
			fixed (void* t = text) {
				return GetTextPath (t, text.Length, encoding, origin);
			}
		}

		public SKPath GetTextPath (IntPtr text, int length, SKTextEncoding encoding, SKPoint origin = default) =>
			GetTextPath ((void*)text, length, encoding, origin);

		internal SKPath GetTextPath (void* text, int length, SKTextEncoding encoding, SKPoint origin)
		{
            if(!ValidateTextArgs(text,length,encoding))return new SKPath();ThrowIfDisposed();var data=new ReadOnlySpan<byte>(text,length);var path=new SKPath();float x=origin.X;
            try{while(TryDecode(ref data,encoding,out int cp)){ushort glyph=encoding==SKTextEncoding.GlyphId?(ushort)cp:MapScalar(cp);path.AppendGeometry(GlyphOutline(glyph),GlyphTransform(x,origin.Y));x+=GlyphAdvance(glyph);}return path;}catch{path.Dispose();throw;}
		}

		// GetTextPath (positioned)

		public SKPath GetTextPath (string text, ReadOnlySpan<SKPoint> positions) =>
			GetTextPath (text.AsSpan (), positions);

		public SKPath GetTextPath (ReadOnlySpan<char> text, ReadOnlySpan<SKPoint> positions)
		{
			fixed (void* t = text) {
				return GetTextPath (t, text.Length * 2, SKTextEncoding.Utf16, positions);
			}
		}

		public SKPath GetTextPath (ReadOnlySpan<byte> text, SKTextEncoding encoding, ReadOnlySpan<SKPoint> positions)
		{
			fixed (void* t = text) {
				return GetTextPath (t, text.Length, encoding, positions);
			}
		}

		public SKPath GetTextPath (IntPtr text, int length, SKTextEncoding encoding, ReadOnlySpan<SKPoint> positions) =>
			GetTextPath ((void*)text, length, encoding, positions);

		internal SKPath GetTextPath (void* text, int length, SKTextEncoding encoding, ReadOnlySpan<SKPoint> positions)
		{
            if(!ValidateTextArgs(text,length,encoding))return new SKPath();ThrowIfDisposed();if(CountGlyphs(text,length,encoding)!=positions.Length)throw new ArgumentException("One position is required per glyph.",nameof(positions));var data=new ReadOnlySpan<byte>(text,length);var path=new SKPath();int index=0;
            try{while(TryDecode(ref data,encoding,out int cp)){ushort glyph=encoding==SKTextEncoding.GlyphId?(ushort)cp:MapScalar(cp);var p=positions[index++];path.AppendGeometry(GlyphOutline(glyph),GlyphTransform(p.X,p.Y));}return path;}catch{path.Dispose();throw;}
		}

		// GetGlyphPaths

		public void GetGlyphPaths (ReadOnlySpan<ushort> glyphs, SKGlyphPathDelegate glyphPathDelegate)
		{
            ThrowIfDisposed();ArgumentNullException.ThrowIfNull(glyphPathDelegate);foreach(ushort glyph in glyphs){using var path=new SKPath(GlyphOutline(glyph));glyphPathDelegate(path,SKMatrix.FromCore(GlyphTransform(0,0)));}
		}

		// GetTextPathOnPath (text)

		public SKPath GetTextPathOnPath (string text, SKPath path, SKTextAlign textAlign = SKTextAlign.Left, SKPoint origin = default) =>
			GetTextPathOnPath (text.AsSpan (), path, textAlign, origin);

		public SKPath GetTextPathOnPath (ReadOnlySpan<char> text, SKPath path, SKTextAlign textAlign = SKTextAlign.Left, SKPoint origin = default)
		{
			fixed (void* t = text) {
				return GetTextPathOnPath (t, text.Length * 2, SKTextEncoding.Utf16, path, textAlign, origin);
			}
		}

		public SKPath GetTextPathOnPath (ReadOnlySpan<byte> text, SKTextEncoding encoding, SKPath path, SKTextAlign textAlign = SKTextAlign.Left, SKPoint origin = default)
		{
			fixed (void* t = text) {
				return GetTextPathOnPath (t, text.Length, encoding, path, textAlign, origin);
			}
		}

		public SKPath GetTextPathOnPath (IntPtr text, int length, SKTextEncoding encoding, SKPath path, SKTextAlign textAlign = SKTextAlign.Left, SKPoint origin = default) =>
			GetTextPathOnPath ((void*)text, length, encoding, path, textAlign, origin);

		internal SKPath GetTextPathOnPath (void* text, int length, SKTextEncoding encoding, SKPath path, SKTextAlign textAlign = SKTextAlign.Left, SKPoint origin = default)
		{
			if (!ValidateTextArgs (text, length, encoding))
				return new SKPath ();

			var n = CountGlyphs (text, length, encoding);
			if (n <= 0)
				return new SKPath ();

			using var glyphs = FontArrayPool.Rent<ushort> (n);
			GetGlyphs (text, length, encoding, glyphs);

			return GetTextPathOnPath (glyphs, path, textAlign, origin);
		}

		// GetTextPathOnPath (glyphs)

		public SKPath GetTextPathOnPath (ReadOnlySpan<ushort> glyphs, SKPath path, SKTextAlign textAlign = SKTextAlign.Left, SKPoint origin = default)
		{
			if (path == null)
				throw new ArgumentNullException (nameof (path));

			if (glyphs.Length == 0)
				return new SKPath ();

			using var glyphWidths = FontArrayPool.Rent<float> (glyphs.Length);
			using var glyphOffsets = FontArrayPool.Rent<SKPoint> (glyphs.Length);

			GetGlyphWidths (glyphs, glyphWidths, Span<SKRect>.Empty);
			GetGlyphPositions (glyphs, glyphOffsets, origin);

			return GetTextPathOnPath (glyphs, glyphWidths, glyphOffsets, path, textAlign);
		}

		public SKPath GetTextPathOnPath (ReadOnlySpan<ushort> glyphs, ReadOnlySpan<float> glyphWidths, ReadOnlySpan<SKPoint> glyphPositions, SKPath path, SKTextAlign textAlign = SKTextAlign.Left)
		{
			if (glyphs.Length != glyphWidths.Length)
				throw new ArgumentException ("The number of glyphs and glyph widths must be the same.");
			if (glyphs.Length != glyphPositions.Length)
				throw new ArgumentException ("The number of glyphs and glyph offsets must be the same.");
			if (path == null)
				throw new ArgumentNullException (nameof (path));

			if (glyphs.Length == 0)
				return new SKPath ();

			using var glyphPathCache = new GlyphPathCache (this);
			using var pathMeasure = new SKPathMeasure (path);

			var contourLength = pathMeasure.Length;

			var textLength = glyphPositions[glyphs.Length - 1].X + glyphWidths[glyphs.Length - 1];
			var alignment = (int)textAlign * 0.5f;
			var startOffset = glyphPositions[0].X + (contourLength - textLength) * alignment;

			var textPath = new SKPath ();

			// TODO: deal with multiple contours?
			for (var index = 0; index < glyphPositions.Length; index++) {
				var glyphOffset = glyphPositions[index];
				var gw = glyphWidths[index];
				var x0 = startOffset + glyphOffset.X;
				var x1 = x0 + gw;

				if (x1 >= 0 && x0 <= contourLength) {
					var glyphId = glyphs[index];
					var glyphPath = glyphPathCache.GetPath (glyphId);
					if (glyphPath != null) {
						var transformation = SKMatrix.CreateTranslation (x0, glyphOffset.Y);
						MorphPath (textPath, glyphPath, pathMeasure, transformation);
					}
				}
			}

			return textPath;

			static void MorphPath (SKPath dst, SKPath src, SKPathMeasure meas, in SKMatrix matrix)
			{
				// TODO:
				// Need differentially more subdivisions when the follow-path is curvy. Not sure how to determine
				// that, but we need it. I guess a cheap answer is let the caller tell us, but that seems like a
				// cop-out. Another answer is to get Skia's Rob Johnson to figure it out.

				using var it = src.CreateIterator (false);

				SKPathVerb verb;

				Span<SKPoint> srcP = stackalloc SKPoint[4];
				Span<SKPoint> dstP = stackalloc SKPoint[4];

				while ((verb = it.Next (srcP)) != SKPathVerb.Done) {
					switch (verb) {
						case SKPathVerb.Move:
							MorphPoints (dstP, srcP, 1, meas, matrix);
							dst.MoveTo (dstP[0]);
							break;
						case SKPathVerb.Line:
							// turn lines into quads to look bendy
							srcP[0].X = (srcP[0].X + srcP[1].X) * 0.5f;
							srcP[0].Y = (srcP[0].Y + srcP[1].Y) * 0.5f;
							MorphPoints (dstP, srcP, 2, meas, matrix);
							dst.QuadTo (dstP[0], dstP[1]);
							break;
						case SKPathVerb.Quad:
							MorphPoints (dstP, srcP.Slice (1, 2), 2, meas, matrix);
							dst.QuadTo (dstP[0], dstP[1]);
							break;
						case SKPathVerb.Conic:
							MorphPoints (dstP, srcP.Slice (1, 2), 2, meas, matrix);
							dst.ConicTo (dstP[0], dstP[1], it.ConicWeight ());
							break;
						case SKPathVerb.Cubic:
							MorphPoints (dstP, srcP.Slice (1, 3), 3, meas, matrix);
							dst.CubicTo (dstP[0], dstP[1], dstP[2]);
							break;
						case SKPathVerb.Close:
							dst.Close ();
							break;
						default:
							Debug.Fail ("Unknown verb when iterating points in glyph path.");
							break;
					}
				}
			}

			static void MorphPoints (Span<SKPoint> dst, Span<SKPoint> src, int count, SKPathMeasure meas, in SKMatrix matrix)
			{
				for (int i = 0; i < count; i++) {
					SKPoint s = matrix.MapPoint (src[i].X, src[i].Y);

					if (!meas.GetPositionAndTangent (s.X, out var p, out var t)) {
						// set to 0 if the measure failed, so that we just set dst == pos
						t = SKPoint.Empty;
					}

					// y-offset the point in the direction of the normal vector on the path.
					dst[i] = new SKPoint (p.X - t.Y * s.Y, p.Y + t.X * s.Y);
				}
			}
		}

		// Utils

		private bool ValidateTextArgs (void* text, int length, SKTextEncoding encoding)
		{
            ThrowIfDisposed();if(length<0)throw new ArgumentOutOfRangeException(nameof(length));if(!Enum.IsDefined(encoding))throw new ArgumentOutOfRangeException(nameof(encoding));if(length==0)return false;if(text==null)throw new ArgumentNullException(nameof(text));return true;
		}

		internal sealed class GlyphPathCache : Dictionary<ushort, SKPath>, IDisposable
		{
			public SKFont Font { get; }

			public GlyphPathCache (SKFont font)
			{
				Font = font;
			}

			public SKPath GetPath (ushort glyph)
			{
				if (!TryGetValue (glyph, out var path)) {
					path = Font.GetGlyphPath (glyph);
					this[glyph] = path;
				}

				return path;
			}

			public void Dispose ()
			{
				foreach (var path in Values) {
					path?.Dispose ();
				}
				Clear ();
			}
		}
    private ushort MapScalar(int cp)=>Rune.IsValid(cp)?font.GetGlyph(cp):(ushort)0;
    private float GlyphAdvance(ushort glyph)=>glyph<font.GlyphCount?font.Advance(glyph)*size/font.UnitsPerEm*scaleX:0;
    private readonly Dictionary<ushort,SKRect> glyphBoundsCache=[];
    private SKRect GlyphBounds(ushort glyph)
    {
        if(glyph>=font.GlyphCount||size==0)return default;
        if(glyphBoundsCache.TryGetValue(glyph,out var cached))return cached;
        var bounds=GlyphOutline(glyph).GetTightBounds(GlyphTransform(0,0));
        var box=bounds.IsEmpty?default:new SKRect(MathF.Floor(bounds.Left),MathF.Floor(bounds.Top),MathF.Ceiling(bounds.Right),MathF.Ceiling(bounds.Bottom));
        if(glyphBoundsCache.Count>=512)glyphBoundsCache.Clear();glyphBoundsCache.Add(glyph,box);return box;
    }
    private static void ValidateMetricsPaint(SKPaint paint){if(paint is not null){paint.ThrowIfDisposed();if(paint.Style!=SKPaintStyle.Fill)throw new NotSupportedException("Stroked/effected glyph metrics require outline evaluation.");}}
    private static bool TryDecode(ref ReadOnlySpan<byte> bytes,SKTextEncoding encoding,out int cp)
    {
        cp=0;if(bytes.IsEmpty)return false;
        if(encoding==SKTextEncoding.GlyphId){if(bytes.Length<2)return false;cp=BinaryPrimitives.ReadUInt16LittleEndian(bytes);bytes=bytes[2..];return true;}
        if(encoding==SKTextEncoding.Utf32){if(bytes.Length<4)return false;cp=BinaryPrimitives.ReadInt32LittleEndian(bytes);bytes=bytes[4..];return true;}
        OperationStatus status;Rune rune;int consumed;
        if(encoding==SKTextEncoding.Utf16){if(bytes.Length<2)return false;status=Rune.DecodeFromUtf16(System.Runtime.InteropServices.MemoryMarshal.Cast<byte,char>(bytes),out rune,out consumed);consumed*=2;}
        else status=Rune.DecodeFromUtf8(bytes,out rune,out consumed);
        if(status!=OperationStatus.Done)return false;cp=rune.Value;bytes=bytes[consumed..];return true;
    }
}
internal static class FontArrayPool
{
    internal static Lease<T> Rent<T>(int length)=>new(length);
    internal ref struct Lease<T>
    {
        private T[] array;private readonly int length;
        internal Lease(int length){this.length=length;array=ArrayPool<T>.Shared.Rent(length);}
        public static implicit operator Span<T>(Lease<T> lease)=>lease.array.AsSpan(0,lease.length);
        public static implicit operator ReadOnlySpan<T>(Lease<T> lease)=>lease.array.AsSpan(0,lease.length);
        public void Dispose(){var value=Interlocked.Exchange(ref array,null);if(value is not null)ArrayPool<T>.Shared.Return(value,System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>());}
    }
}
