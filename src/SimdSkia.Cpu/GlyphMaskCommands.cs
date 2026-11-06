namespace SimdSkia.Cpu;

/// <summary>Bounded, immutable managed glyph masks for device compositing. Glyphs that overlap retain input order.</summary>
public sealed class GlyphMaskCommands
{
    private readonly record struct Key(GlyphCoverageCache.Batch Batch,int X,int Y,CoverageClip? Clip);
    private readonly Dictionary<Key,CoverageClip[]> cache=[];
    private long bytes;
    public ReadOnlySpan<CoverageClip> Resolve(in DrawingCommand command)
    {
        var run=command.GlyphRun??throw new ArgumentException("Glyph outlines required.");
        int luminance=command.TextMaskPreblend&&command.Paint.Antialias?GlyphMaskGamma.Luminance(command.Paint):-1;
        var batch=GlyphCoverageCache.Resolve(run,command.Transform,command.Paint.Antialias,command.CurveTolerance,command.SnapGlyphBaseline,luminance,out int x,out int y);
        var key=new Key(batch,x,y,command.CoverageClip);if(cache.TryGetValue(key,out var result))return result;
        result=new CoverageClip[batch.Glyphs.Length];long size=0;
        for(int i=0;i<result.Length;i++)
        {
            var p=batch.Glyphs[i];var m=p.Mask;ReadOnlySpan<byte> coverage=m.Data;
            if(coverage.Length!=checked(m.Width*m.Height))
            {
                var dense=new byte[checked(m.Width*m.Height)];
                foreach(var r in m.Runs)dense.AsSpan(checked((r.Y-m.Top)*m.Width+r.X-m.Left),r.Length).Fill(r.Coverage);
                coverage=dense;
            }
            var clip=new CoverageClip(checked(x+p.X+m.Left),checked(y+p.Y+m.Top),m.Width,m.Height,coverage);
            if(command.CoverageClip is {} inherited)clip=CoverageClip.Intersect(clip,inherited);
            size+=128L+(long)clip.Width*clip.Height;result[i]=clip;
        }
        if(size<=4*1024*1024)
        {if(cache.Count>=64||bytes+size>4*1024*1024)Clear();cache.Add(key,result);bytes+=size;}
        return result;
    }
    public void Clear(){cache.Clear();bytes=0;}
}
