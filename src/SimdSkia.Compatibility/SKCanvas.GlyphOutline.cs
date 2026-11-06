using SimdSkia;
namespace SkiaSharp;
public partial class SKCanvas
{
    private static PathGeometry GlyphOutline(GlyphRunGeometry run)
    {
        var segments=new List<PathSegment>();
        foreach(var glyph in run.Glyphs)
            glyph.Outline.AppendTransformedTo(segments,new Matrix3(1,0,glyph.Position.X,0,1,glyph.Position.Y,0,0,1)*run.FontTransform);
        return new(segments);
    }
}
