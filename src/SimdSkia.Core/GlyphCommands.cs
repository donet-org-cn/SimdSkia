namespace SimdSkia;

/// <summary>Converts managed glyph outlines into individual GPU path commands, preserving input order.</summary>
public static class GlyphCommands
{
    /// <summary>Packs consecutive outlines whose antialiased device bounds cannot overlap.</summary>
    public static PathGeometry[] PackDisjoint(in DrawingCommand command,GlyphRunGeometry run)
    {
        var result=new List<PathGeometry>();var segments=new List<PathSegment>();RectF? area=null;FillRule rule=FillRule.Winding;
        void Flush(){if(segments.Count==0)return;result.Add(new PathGeometry(segments,rule));segments.Clear();area=null;}
        var layout = new GlyphLayout(command,run);
        foreach(var glyph in run.Glyphs)
        {
            var path=glyph.Outline;if(path.Segments.IsEmpty)continue;
            var transform=layout.Transform(glyph.Position);var bounds=path.GetTightBounds(transform);
            bounds=new(MathF.Floor(bounds.Left),MathF.Floor(bounds.Top),MathF.Ceiling(bounds.Right),MathF.Ceiling(bounds.Bottom));
            if(area is {} a&&(path.FillRule!=rule||bounds.Left<a.Right&&bounds.Right>a.Left&&bounds.Top<a.Bottom&&bounds.Bottom>a.Top))Flush();
            rule=path.FillRule;path.AppendTransformedTo(segments,transform);
            area=area is {} old?new(Math.Min(old.Left,bounds.Left),Math.Min(old.Top,bounds.Top),Math.Max(old.Right,bounds.Right),Math.Max(old.Bottom,bounds.Bottom)):bounds;
        }
        Flush();return result.ToArray();
    }
    public static DrawingCommand[] Expand(in DrawingCommand command, GlyphRunGeometry run)
    {
        var layout = new GlyphLayout(command,run);
        var result = new DrawingCommand[run.Glyphs.Length]; int index = 0;
        foreach (var glyph in run.Glyphs)
            result[index++] = command with { Kind = CommandKind.FillPath, Path = glyph.Outline, Transform = layout.Transform(glyph.Position), ShaderTransform = command.ShaderTransform ?? command.Transform, GlyphRun = null };
        return result;
    }
    internal static RectF? InkBounds(in DrawingCommand command, GlyphRunGeometry run)
    {
        var layout = new GlyphLayout(command,run);
        RectF? bounds = null;
        foreach (var glyph in run.Glyphs)
        {
            if (glyph.Outline.SegmentCount == 0) continue;
            var b = glyph.Outline.GetTightBounds(layout.Transform(glyph.Position));
            bounds = bounds is {} a ? new(Math.Min(a.Left,b.Left),Math.Min(a.Top,b.Top),Math.Max(a.Right,b.Right),Math.Max(a.Bottom,b.Bottom)) : b;
        }
        return bounds;
    }
    private readonly struct GlyphLayout
    {
        private readonly Matrix3 local, shape;
        private readonly float x, y, snapX, snapY;
        internal GlyphLayout(in DrawingCommand command, GlyphRunGeometry run)
        {
            ArgumentNullException.ThrowIfNull(run);
            var matrix = command.Transform;
            if (!matrix.IsFinite || !matrix.IsAffine) throw new NotSupportedException("Glyph transforms must be finite and affine.");
            if (MathF.Abs(matrix.M02) > 65536 || MathF.Abs(matrix.M12) > 65536) throw new NotSupportedException("Glyph origin exceeds +/-65536 device pixels.");
            x = MathF.Floor(matrix.M02); y = MathF.Floor(matrix.M12);
            local = matrix with { M02 = matrix.M02 - x, M12 = matrix.M12 - y };
            shape = (local with { M02 = 0, M12 = 0 }) * run.FontTransform;
            if (run.Positioning == GlyphPositioning.QuarterPixel)
            {
                snapX = snapY = command.Paint.Antialias ? 4 : 1;
                if (command.SnapGlyphBaseline) { if (matrix.M10 == 0) snapY = 1; else if (matrix.M00 == 0) snapX = 1; }
            }
        }
        internal Matrix3 Transform(PointF position)
        {
            var (px,py) = local.Map(position.X,position.Y);
            if (snapX != 0)
            {
                px = MathF.Floor(px * snapX + .5f) / snapX;
                py = MathF.Floor(py * snapY + .5f) / snapY;
            }
            return shape with { M02 = shape.M02 + px + x, M12 = shape.M12 + py + y };
        }
    }
}
