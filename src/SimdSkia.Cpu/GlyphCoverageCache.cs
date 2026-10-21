namespace SimdSkia.Cpu;

// Glyph masks are shared by unrelated labels. Integer scrolling changes only
// placement. Raw masks exclude color, brush, clip and destination; packed runs
// also key the optional preblend luminance (not opacity or brush identity).
internal static class GlyphCoverageCache
{
    internal readonly record struct Placement(CoverageMask Mask,int X,int Y);
    internal sealed record Batch(Placement[] Glyphs,long Bytes);
    private readonly record struct GlyphKey(PathGeometry Outline,Matrix3 Matrix,bool Antialias,float Tolerance);
    private readonly record struct RunKey(GlyphRunGeometry Run,Matrix3 Matrix,bool Antialias,float Tolerance,bool SnapBaseline,int Luminance);
    private sealed record GlyphEntry(GlyphKey Key,CoverageMask Mask,long Bytes);
    private sealed record RunEntry(RunKey Key,Batch Batch);
    private static readonly object gate=new();
    private static readonly Dictionary<GlyphKey,LinkedListNode<GlyphEntry>> glyphs=[];
    private static readonly LinkedList<GlyphEntry> glyphOrder=[];
    private static readonly Dictionary<RunKey,LinkedListNode<RunEntry>> runs=[];
    private static readonly LinkedList<RunEntry> runOrder=[];
    private static long glyphBytes,runBytes;
    internal const long MaximumGlyphBytes=8*1024*1024,MaximumRunBytes=2*1024*1024;
    internal const int MaximumGlyphs=4096,MaximumRuns=512;
    internal static bool Enabled=true;
    internal static (int Glyphs,int Runs,long GlyphBytes,long RunBytes) Statistics{get{lock(gate)return(glyphs.Count,runs.Count,glyphBytes,runBytes);}}
    internal static void Clear(){lock(gate){glyphs.Clear();glyphOrder.Clear();runs.Clear();runOrder.Clear();glyphBytes=runBytes=0;}}
    internal static Batch Resolve(GlyphRunGeometry run,Matrix3 matrix,bool aa,float tolerance,bool snapBaseline,int luminance,out int x,out int y)
    {
        // Keep the normalized arithmetic bounded and deterministic under scrolling.
        if(MathF.Abs(matrix.M02)>65536||MathF.Abs(matrix.M12)>65536)
            throw new NotSupportedException("Glyph origin exceeds the supported device coordinate range (+/-65536).");
        x=(int)MathF.Floor(matrix.M02);y=(int)MathF.Floor(matrix.M12);
        var local=matrix with{M02=matrix.M02-x,M12=matrix.M12-y};var key=new RunKey(run,local,aa,tolerance,snapBaseline,luminance);
        if(Enabled)lock(gate)if(runs.TryGetValue(key,out var cached)){runOrder.Remove(cached);runOrder.AddLast(cached);return cached.Value.Batch;}
        var placements=new Placement[run.Glyphs.Length];int index=0;long geometryBytes=128L+placements.Length*24L,retained=geometryBytes;
        bool quantized=run.Positioning==GlyphPositioning.QuarterPixel;
        foreach(var glyph in run.Glyphs)
        {
            var origin=local.Map(glyph.Position.X,glyph.Position.Y);
            float px=origin.X,py=origin.Y;
            if(quantized)
            {
                float sx=aa?4:1,sy=sx;
                if(snapBaseline){if(matrix.M10==0)sy=1;else if(matrix.M00==0)sx=1;}
                px=MathF.Floor(px*sx+.5f)/sx;py=MathF.Floor(py*sy+.5f)/sy;
            }
            var transform=(local with{M02=0,M12=0})*run.FontTransform;
            transform=transform with{M02=transform.M02+px,M12=transform.M12+py};
            var mask=ResolveGlyph(glyph.Outline,transform,aa,tolerance,out int dx,out int dy);
            placements[index++]=new(mask,dx,dy);
            // Count all referenced mask/outline data, even when shared or evicted
            // from the glyph dictionary, so run entries cannot bypass a budget.
            retained+=Size(mask,glyph.Outline);
            geometryBytes+=glyph.Outline.Segments.Length*32L;
        }
        // Pack consecutive glyphs with disjoint ink. Shared covered pixels start
        // a new batch, preserving input-order alpha/composite semantics.
        var packed=PackDisjoint(placements);
        var batch=packed is not null?new Batch(packed,geometryBytes+packed.Sum(p=>128L+p.Mask.Data.LongLength)):new Batch(placements,retained);
        if(luminance>=0)
        {
            var table=GlyphMaskGamma.Table(luminance);
            foreach(ref var placement in batch.Glyphs.AsSpan())placement=placement with{Mask=Correct(placement.Mask,table)};
        }
        retained=batch.Bytes;
        if(Enabled&&retained<=MaximumRunBytes)lock(gate)
        {
            if(runs.TryGetValue(key,out var existing))return existing.Value.Batch;
            while(runs.Count>=MaximumRuns||runBytes+retained>MaximumRunBytes)
            {var first=runOrder.First!;runBytes-=first.Value.Batch.Bytes;runs.Remove(first.Value.Key);runOrder.RemoveFirst();}
            runs.Add(key,runOrder.AddLast(new RunEntry(key,batch)));runBytes+=retained;
        }
        return batch;
    }
    private static CoverageMask Correct(CoverageMask mask,ReadOnlySpan<byte> table)
    {
        var data=new byte[mask.Data.Length];for(int i=0;i<data.Length;i++)data[i]=table[mask.Data[i]];
        var runs=new CoverageRun[mask.Runs.Length];for(int i=0;i<runs.Length;i++)runs[i]=mask.Runs[i] with{Coverage=table[mask.Runs[i].Coverage]};
        return mask with{Data=data,Runs=runs};
    }
    private static Placement[]? PackDisjoint(Placement[] placements)
    {
        int left=int.MaxValue,top=int.MaxValue,right=int.MinValue,bottom=int.MinValue;
        foreach(var p in placements)
        {
            if(p.Mask.Width==0||p.Mask.Height==0)continue;
            left=Math.Min(left,p.X+p.Mask.Left);top=Math.Min(top,p.Y+p.Mask.Top);
            right=Math.Max(right,p.X+p.Mask.Left+p.Mask.Width);bottom=Math.Max(bottom,p.Y+p.Mask.Top+p.Mask.Height);
        }
        if(left==int.MaxValue)return [];
        int width=right-left,height=bottom-top;
        if((long)width*height>512*1024)return null;
        var data=new byte[width*height];var result=new List<Placement>();
        int groupLeft=right,groupTop=bottom,groupRight=left,groupBottom=top;
        void Flush()
        {
            if(groupLeft>=groupRight||groupTop>=groupBottom)return;
            int w=groupRight-groupLeft,h=groupBottom-groupTop;var packed=new byte[w*h];
            for(int y=0;y<h;y++)data.AsSpan((groupTop-top+y)*width+groupLeft-left,w).CopyTo(packed.AsSpan(y*w,w));
            result.Add(new(new(0,0,w,h,packed,[],true),groupLeft,groupTop));
            data.AsSpan().Clear();groupLeft=right;groupTop=bottom;groupRight=left;groupBottom=top;
        }
        foreach(var p in placements)
        {
            if(p.Mask.Width==0||p.Mask.Height==0)continue;
            if(!Write(p,data,width,left,top,true))Flush();
            Write(p,data,width,left,top,false);
            groupLeft=Math.Min(groupLeft,p.X+p.Mask.Left);groupTop=Math.Min(groupTop,p.Y+p.Mask.Top);
            groupRight=Math.Max(groupRight,p.X+p.Mask.Left+p.Mask.Width);groupBottom=Math.Max(groupBottom,p.Y+p.Mask.Top+p.Mask.Height);
        }
        Flush();return result.ToArray();
    }
    private static bool Write(Placement p,byte[] data,int width,int left,int top,bool checkOnly)
    {
        var mask=p.Mask;
        if(mask.Data.Length>0)
        {
            for(int y=0;y<mask.Height;y++)for(int x=0;x<mask.Width;x++)
            {
                byte coverage=mask.Data[y*mask.Width+x];if(coverage==0)continue;
                int offset=(y+mask.Top+p.Y-top)*width+x+mask.Left+p.X-left;
                if(checkOnly){if(data[offset]!=0)return false;}else data[offset]=coverage;
            }
        }
        else foreach(var run in mask.Runs)
        {
            int offset=(run.Y+p.Y-top)*width+run.X+p.X-left;
            for(int x=0;x<run.Length;x++){if(checkOnly){if(data[offset+x]!=0)return false;}else data[offset+x]=run.Coverage;}
        }
        return true;
    }
    private static long Size(CoverageMask mask,PathGeometry outline)=>128L+mask.Data.LongLength+mask.Runs.LongLength*16+outline.Segments.Length*32L;
    private static CoverageMask ResolveGlyph(PathGeometry outline,Matrix3 transform,bool aa,float tolerance,out int x,out int y)
    {
        var b=outline.ControlBounds;
        if(outline.Segments.IsEmpty){x=y=0;return new(0,0,0,0,[],[]);}
        var a=transform.Map(b.Left,b.Top);var c=transform.Map(b.Right,b.Top);var d=transform.Map(b.Left,b.Bottom);var e=transform.Map(b.Right,b.Bottom);
        float left=Math.Min(Math.Min(a.X,c.X),Math.Min(d.X,e.X)),top=Math.Min(Math.Min(a.Y,c.Y),Math.Min(d.Y,e.Y));
        float right=Math.Max(Math.Max(a.X,c.X),Math.Max(d.X,e.X)),bottom=Math.Max(Math.Max(a.Y,c.Y),Math.Max(d.Y,e.Y));
        if(!float.IsFinite(left)||!float.IsFinite(top)||!float.IsFinite(right)||!float.IsFinite(bottom)||left < -65536||top < -65536||right>65536||bottom>65536)
            throw new NotSupportedException("Glyph bounds exceed the supported device coordinate range (+/-65536).");
        x=(int)MathF.Floor(left)-1;y=(int)MathF.Floor(top)-1;
        int width=(int)MathF.Ceiling(right)+1-x,height=(int)MathF.Ceiling(bottom)+1-y;
        if((long)width*height>4*1024*1024)throw new NotSupportedException("One glyph exceeds the 4 MiB raster mask limit.");
        var local=transform with{M02=transform.M02-x,M12=transform.M12-y};var key=new GlyphKey(outline,local,aa,tolerance);
        if(Enabled)lock(gate)if(glyphs.TryGetValue(key,out var cached)){glyphOrder.Remove(cached);glyphOrder.AddLast(cached);return cached.Value.Mask;}
        var mask=PathRasterizer.Rasterize(outline.Flatten(local,tolerance),outline.FillRule,width,height,aa,compactRuns:true);
        long size=Size(mask,outline);
        if(Enabled&&size<=MaximumGlyphBytes)lock(gate)
        {
            if(glyphs.TryGetValue(key,out var existing))return existing.Value.Mask;
            while(glyphs.Count>=MaximumGlyphs||glyphBytes+size>MaximumGlyphBytes)
            {var first=glyphOrder.First!;glyphBytes-=first.Value.Bytes;glyphs.Remove(first.Value.Key);glyphOrder.RemoveFirst();}
            glyphs.Add(key,glyphOrder.AddLast(new GlyphEntry(key,mask,size)));glyphBytes+=size;
        }
        return mask;
    }
}
