namespace SimdSkia.Cpu;

/// <summary>Bounded, immutable managed glyph masks for device compositing. Glyphs that overlap retain input order.</summary>
public sealed class GlyphMaskCommands
{
    private readonly record struct Key(GlyphCoverageCache.Batch Batch,int X,int Y,CoverageClip? Clip);
    private sealed record Entry(Key Key,CoverageClip[] Clips,long Bytes);
    private readonly Dictionary<Key,LinkedListNode<Entry>> cache=[];
    private readonly LinkedList<Entry> order=[];
    private readonly Dictionary<GlyphCoverageCache.Batch,(int Count,long Bytes)> sources=[];
    private long bytes;
    private const long Budget=4*1024*1024;
    internal (int Count,long Bytes) Statistics=>(cache.Count,bytes);
    public ReadOnlySpan<CoverageClip> Resolve(in DrawingCommand command)
    {
        var run=command.GlyphRun??throw new ArgumentException("Glyph outlines required.");
        int luminance=command.TextMaskPreblend&&command.Paint.Antialias?GlyphMaskGamma.Luminance(command.Paint):-1;
        var batch=GlyphCoverageCache.Resolve(run,command.Transform,command.Paint.Antialias,command.CurveTolerance,command.SnapGlyphBaseline,luminance,out int x,out int y);
        var key=new Key(batch,x,y,command.CoverageClip);
        if(cache.TryGetValue(key,out var found)){order.Remove(found);order.AddLast(found);return found.Value.Clips;}
        var result=new CoverageClip[batch.Glyphs.Length];long size=128L+result.Length*96L+(long)(command.CoverageClip?.Width??0)*(command.CoverageClip?.Height??0),sourceBytes=128L+batch.Glyphs.Length*32L;
        for(int i=0;i<result.Length;i++)
        {
            var p=batch.Glyphs[i];var m=p.Mask;byte[] coverage=m.Data;
            sourceBytes+=128L+m.Data.LongLength+m.Runs.LongLength*16;
            if(coverage.Length!=checked(m.Width*m.Height))
            {
                coverage=new byte[checked(m.Width*m.Height)];size+=coverage.Length;
                foreach(var r in m.Runs)coverage.AsSpan(checked((r.Y-m.Top)*m.Width+r.X-m.Left),r.Length).Fill(r.Coverage);
            }
            var clip=CoverageClip.BorrowImmutable(checked(x+p.X+m.Left),checked(y+p.Y+m.Top),m.Width,m.Height,coverage);
            if(command.CoverageClip is {} inherited){clip=CoverageClip.Intersect(clip,inherited);size+=(long)clip.Width*clip.Height;}
            result[i]=clip;
        }
        // Source masks are shared by translated views. Account the real retained
        // source bytes once; eviction removes the last source reference as well.
        if(size+sourceBytes>Budget)return result;
        long Required()=>size+(sources.ContainsKey(batch)?0:sourceBytes);
        while(cache.Count>=512||bytes+Required()>Budget)
        {
            var oldest=order.First!;order.RemoveFirst();cache.Remove(oldest.Value.Key);bytes-=oldest.Value.Bytes;
            var source=sources[oldest.Value.Key.Batch];
            if(source.Count==1){sources.Remove(oldest.Value.Key.Batch);bytes-=source.Bytes;}
            else sources[oldest.Value.Key.Batch]=(source.Count-1,source.Bytes);
        }
        if(sources.TryGetValue(batch,out var retained))sources[batch]=(retained.Count+1,retained.Bytes);
        else{sources.Add(batch,(1,sourceBytes));bytes+=sourceBytes;}
        cache.Add(key,order.AddLast(new Entry(key,result,size)));bytes+=size;
        return result;
    }
    public void Clear(){cache.Clear();order.Clear();sources.Clear();bytes=0;}
}
