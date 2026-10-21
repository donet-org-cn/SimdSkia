namespace SimdSkia;

/// <summary>Immutable outlines and shaped positions, independent of a rendering backend.</summary>
public readonly record struct PositionedGlyph(PathGeometry Outline,PointF Position);

/// <summary>Device origin precision used by a raster font cache. Exact retains continuous positions.</summary>
public enum GlyphPositioning { Exact,QuarterPixel }

public sealed class GlyphRunGeometry
{
    private readonly PositionedGlyph[] glyphs;
    public ReadOnlySpan<PositionedGlyph> Glyphs=>glyphs;
    public Matrix3 FontTransform {get;}
    public GlyphPositioning Positioning {get;}
    public RectF Bounds {get;}
    public GlyphRunGeometry(IEnumerable<PositionedGlyph> glyphs,Matrix3 fontTransform,GlyphPositioning positioning=GlyphPositioning.Exact)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        if(!fontTransform.IsFinite||!fontTransform.IsAffine)throw new ArgumentException("A finite affine font transform is required.",nameof(fontTransform));
        if(!Enum.IsDefined(positioning))throw new ArgumentOutOfRangeException(nameof(positioning));
        this.glyphs=glyphs.ToArray();FontTransform=fontTransform;Positioning=positioning;
        RectF? bounds=null;
        foreach(var glyph in this.glyphs)
        {
            ArgumentNullException.ThrowIfNull(glyph.Outline);
            if(!float.IsFinite(glyph.Position.X)||!float.IsFinite(glyph.Position.Y))throw new ArgumentException("Glyph positions must be finite.",nameof(glyphs));
            if(glyph.Outline.FillRule is FillRule.InverseEvenOdd or FillRule.InverseWinding)throw new NotSupportedException("Inverse glyph outlines are not supported.");
            if(glyph.Outline.Segments.IsEmpty)continue;
            var b=glyph.Outline.GetTightBounds(fontTransform);
            b=new(b.Left+glyph.Position.X,b.Top+glyph.Position.Y,b.Right+glyph.Position.X,b.Bottom+glyph.Position.Y);
            bounds=bounds is {} old?new(Math.Min(old.Left,b.Left),Math.Min(old.Top,b.Top),Math.Max(old.Right,b.Right),Math.Max(old.Bottom,b.Bottom)):b;
        }
        Bounds=bounds??default;
    }
}
