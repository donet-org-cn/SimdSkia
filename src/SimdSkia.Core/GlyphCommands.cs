namespace SimdSkia;

/// <summary>Converts managed glyph outlines into individual GPU path commands, preserving input order.</summary>
public static class GlyphCommands
{
    /// <summary>Packs consecutive outlines whose antialiased device bounds cannot overlap.</summary>
    public static PathGeometry[] PackDisjoint(in DrawingCommand command,GlyphRunGeometry run)
    {
        var result=new List<PathGeometry>();var segments=new List<PathSegment>();RectF? area=null;FillRule rule=FillRule.Winding;
        void Flush(){if(segments.Count==0)return;result.Add(new PathGeometry(segments,rule));segments.Clear();area=null;}
        foreach(var item in Expand(command,run))
        {
            var path=item.Path!;if(path.Segments.IsEmpty)continue;var bounds=path.GetTightBounds(item.Transform);
            bounds=new(MathF.Floor(bounds.Left),MathF.Floor(bounds.Top),MathF.Ceiling(bounds.Right),MathF.Ceiling(bounds.Bottom));
            if(area is {} a&&(path.FillRule!=rule||bounds.Left<a.Right&&bounds.Right>a.Left&&bounds.Top<a.Bottom&&bounds.Bottom>a.Top))Flush();
            rule=path.FillRule;path.AppendTransformedTo(segments,item.Transform);
            area=area is {} old?new(Math.Min(old.Left,bounds.Left),Math.Min(old.Top,bounds.Top),Math.Max(old.Right,bounds.Right),Math.Max(old.Bottom,bounds.Bottom)):bounds;
        }
        Flush();return result.ToArray();
    }
    public static DrawingCommand[] Expand(in DrawingCommand command, GlyphRunGeometry run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var result = new DrawingCommand[run.Glyphs.Length]; int index = 0;
        var matrix = command.Transform;
        if (!matrix.IsFinite || !matrix.IsAffine) throw new NotSupportedException("Glyph transforms must be finite and affine.");
        if (MathF.Abs(matrix.M02) > 65536 || MathF.Abs(matrix.M12) > 65536) throw new NotSupportedException("Glyph origin exceeds +/-65536 device pixels.");
        float x = MathF.Floor(matrix.M02), y = MathF.Floor(matrix.M12);
        var local = matrix with { M02 = matrix.M02 - x, M12 = matrix.M12 - y };
        foreach (var glyph in run.Glyphs)
        {
            var origin = local.Map(glyph.Position.X, glyph.Position.Y);
            float px = origin.X, py = origin.Y;
            if (run.Positioning == GlyphPositioning.QuarterPixel)
            {
                float sx = command.Paint.Antialias ? 4 : 1, sy = sx;
                if (command.SnapGlyphBaseline) { if (matrix.M10 == 0) sy = 1; else if (matrix.M00 == 0) sx = 1; }
                px = MathF.Floor(px * sx + .5f) / sx; py = MathF.Floor(py * sy + .5f) / sy;
            }
            var transform = (local with { M02 = 0, M12 = 0 }) * run.FontTransform;
            transform = transform with { M02 = transform.M02 + px + x, M12 = transform.M12 + py + y };
            result[index++] = command with { Kind = CommandKind.FillPath, Path = glyph.Outline, Transform = transform, ShaderTransform = command.ShaderTransform ?? command.Transform, GlyphRun = null };
        }
        return result;
    }
}
