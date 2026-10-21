namespace SimdSkia.Cpu;

public sealed partial class RasterCanvas
{
    private void DrawGlyphRun(in DrawingCommand command)
    {
        var run=command.GlyphRun??throw new ArgumentException("A glyph command requires shaped outlines.");
        if(!command.Transform.TryInvert(out _))return;
        // Resolve the whole run before compositing: validation failures must not
        // leave a partially drawn run. Overlapping glyphs blend in input order.
        int luminance=command.TextMaskPreblend&&command.Paint.Antialias?GlyphMaskGamma.Luminance(command.Paint):-1;
        var batch=GlyphCoverageCache.Resolve(run,command.Transform,command.Paint.Antialias,command.CurveTolerance,command.SnapGlyphBaseline,luminance,out int x,out int y);
        foreach(var glyph in batch.Glyphs)FillMask(glyph.Mask,command,x+glyph.X,y+glyph.Y,true);
    }
}
