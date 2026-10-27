namespace SimdSkia.Cpu;

public sealed partial class RasterCanvas
{
    private void DrawGlyphRun(in DrawingCommand command)
    {
        var run=command.GlyphRun??throw new ArgumentException("A glyph command requires shaped outlines.");
        var matrix=command.Transform;
        // Execute already validated a finite affine matrix. A pure translation
        // always has a finite inverse; avoid computing nine unused quotients.
        bool translation=matrix.M00==1&&matrix.M01==0&&matrix.M10==0&&matrix.M11==1;
        if(!translation&&!matrix.TryInvert(out _))return;
        // Resolve the whole run before compositing: validation failures must not
        // leave a partially drawn run. Overlapping glyphs blend in input order.
        int luminance=command.TextMaskPreblend&&command.Paint.Antialias?GlyphMaskGamma.Luminance(command.Paint):-1;
        var batch=GlyphCoverageCache.Resolve(run,command.Transform,command.Paint.Antialias,command.CurveTolerance,command.SnapGlyphBaseline,luminance,out int x,out int y);
        foreach(var glyph in batch.Glyphs)FillMask(glyph.Mask,command,x+glyph.X,y+glyph.Y,true);
    }
}
