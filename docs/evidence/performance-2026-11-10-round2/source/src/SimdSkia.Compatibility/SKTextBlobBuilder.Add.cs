// Managed forwarding/placement from SkiaSharp v3.119.4 (MIT).
namespace SkiaSharp;
public partial class SKTextBlobBuilder
{
		public void AddRun (ReadOnlySpan<ushort> glyphs, SKFont font, SKPoint origin = default)
		{
			var buffer = AllocateRawPositionedRun (font, glyphs.Length);
			glyphs.CopyTo (buffer.Glyphs);
			font.GetGlyphPositions (buffer.Glyphs, buffer.Positions, origin);
		}

		// AddHorizontalRun

		public void AddHorizontalRun (ReadOnlySpan<ushort> glyphs, SKFont font, ReadOnlySpan<float> positions, float y)
		{
			var buffer = AllocateRawHorizontalRun (font, glyphs.Length, y);
			glyphs.CopyTo (buffer.Glyphs);
			positions.CopyTo (buffer.Positions);
		}

		// AddPositionedRun

		public void AddPositionedRun (ReadOnlySpan<ushort> glyphs, SKFont font, ReadOnlySpan<SKPoint> positions)
		{
			var buffer = AllocateRawPositionedRun (font, glyphs.Length);
			glyphs.CopyTo (buffer.Glyphs);
			positions.CopyTo (buffer.Positions);
		}

		// AddRotationScaleRun

		public void AddRotationScaleRun (ReadOnlySpan<ushort> glyphs, SKFont font, ReadOnlySpan<SKRotationScaleMatrix> positions)
		{
			var buffer = AllocateRawRotationScaleRun (font, glyphs.Length);
			glyphs.CopyTo (buffer.Glyphs);
			positions.CopyTo (buffer.Positions);
		}

		// AddPathPositionedRun

		public void AddPathPositionedRun (ReadOnlySpan<ushort> glyphs, SKFont font, ReadOnlySpan<float> glyphWidths, ReadOnlySpan<SKPoint> glyphOffsets, SKPath path, SKTextAlign textAlign = SKTextAlign.Left)
		{
			using var pathMeasure = new SKPathMeasure (path);

			var contourLength = pathMeasure.Length;

			var textLength = glyphOffsets[glyphs.Length - 1].X + glyphWidths[glyphs.Length - 1];
			var alignment = (int)textAlign * 0.5f;
			var startOffset = glyphOffsets[0].X + (contourLength - textLength) * alignment;

			var firstGlyphIndex = 0;
			var pathGlyphCount = 0;

			var rented = System.Buffers.ArrayPool<SKRotationScaleMatrix>.Shared.Rent(glyphs.Length);
            try {
            Span<SKRotationScaleMatrix> glyphTransforms=rented.AsSpan(0,glyphs.Length);

			// TODO: deal with multiple contours?
			for (var index = 0; index < glyphOffsets.Length; index++) {
				var glyphOffset = glyphOffsets[index];
				var halfWidth = glyphWidths[index] * 0.5f;
				var pathOffset = startOffset + glyphOffset.X + halfWidth;

				// TODO: clip glyphs on both ends of paths
				if (pathOffset >= 0 && pathOffset < contourLength && pathMeasure.GetPositionAndTangent (pathOffset, out var position, out var tangent)) {
					if (pathGlyphCount == 0)
						firstGlyphIndex = index;

					var tx = tangent.X;
					var ty = tangent.Y;

					var px = position.X;
					var py = position.Y;

					// horizontally offset the position using the tangent vector
					px -= tx * halfWidth;
					py -= ty * halfWidth;

					// vertically offset the position using the normal vector  (-ty, tx)
					var dy = glyphOffset.Y;
					px -= dy * ty;
					py += dy * tx;

					glyphTransforms[pathGlyphCount++] = new SKRotationScaleMatrix (tx, ty, px, py);
				}
			}

			var glyphSubset = glyphs.Slice (firstGlyphIndex, pathGlyphCount);
			var positions = glyphTransforms.Slice (0, pathGlyphCount);

			AddRotationScaleRun (glyphSubset, font, positions);
            } finally {System.Buffers.ArrayPool<SKRotationScaleMatrix>.Shared.Return(rented);}
		}

}
