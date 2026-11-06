using SimdSkia;

namespace SkiaSharp;

public partial class SKPaint
{
    private readonly record struct StrokeKey(PathGeometry Source,float Width,float Miter,SKStrokeCap Cap,SKStrokeJoin Join,float Tolerance);
    private sealed record StrokeCache(PathGeometry Source,float Width,float Miter,SKStrokeCap Cap,SKStrokeJoin Join,float Tolerance,PathGeometry Outline)
    {internal long Used;internal long Bytes=>(long)(Source.Segments.Length+Outline.Segments.Length)*28;}
    private StrokeCache? strokeCache;
    private Dictionary<StrokeKey,StrokeCache>? strokeEntries;
    private long strokeBytes,strokeClock;
    private void ClearStrokeCache(){strokeCache=null;strokeEntries?.Clear();strokeBytes=0;strokeClock=0;}
    public SKPath GetFillPath(SKPath src)=>CreateFillPath(src,null,SKMatrix.Identity);
    public SKPath GetFillPath(SKPath src,float resScale)=>CreateFillPath(src,null,SKMatrix.CreateScale(resScale,resScale));
    public SKPath GetFillPath(SKPath src,SKMatrix matrix)=>CreateFillPath(src,null,matrix);
    public SKPath GetFillPath(SKPath src,SKRect cullRect)=>CreateFillPath(src,cullRect,SKMatrix.Identity);
    public SKPath GetFillPath(SKPath src,SKRect cullRect,float resScale)=>CreateFillPath(src,cullRect,SKMatrix.CreateScale(resScale,resScale));
    public SKPath GetFillPath(SKPath src,SKRect cullRect,SKMatrix matrix)=>CreateFillPath(src,cullRect,matrix);
    public bool GetFillPath(SKPath src,SKPath dst)=>FillPath(src,dst,null,SKMatrix.Identity);
    public bool GetFillPath(SKPath src,SKPath dst,float resScale)=>FillPath(src,dst,null,SKMatrix.CreateScale(resScale,resScale));
    public bool GetFillPath(SKPath src,SKPath dst,SKMatrix matrix)=>FillPath(src,dst,null,matrix);
    public bool GetFillPath(SKPath src,SKPath dst,SKRect cullRect)=>FillPath(src,dst,cullRect,SKMatrix.Identity);
    public bool GetFillPath(SKPath src,SKPath dst,SKRect cullRect,float resScale)=>FillPath(src,dst,cullRect,SKMatrix.CreateScale(resScale,resScale));
    public bool GetFillPath(SKPath src,SKPath dst,SKRect cullRect,SKMatrix matrix)=>FillPath(src,dst,cullRect,matrix);
    private SKPath CreateFillPath(SKPath src,SKRect? cull,SKMatrix matrix)
    {
        var dst=new SKPath();try{if(FillPath(src,dst,cull,matrix))return dst;dst.Dispose();return null!;}catch{dst.Dispose();throw;}
    }
    private bool FillPath(SKPath src,SKPath dst,SKRect? cull,SKMatrix matrix)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(src);ArgumentNullException.ThrowIfNull(dst);src.ThrowIfDisposed();dst.ThrowIfDisposed();
        if(cull is {} rect&&!rect.ToCore().IsFinite)throw new ArgumentException("Cull bounds must be finite.",nameof(cull));
        var m=matrix.ToCore();if(!m.IsFinite||!m.IsAffine)throw new NotSupportedException("Stroke resolution requires a finite affine matrix.");
        var effected=ApplyPathEffect(src.ToCore());float width=effected.Width;
        if(effected.Style==SKPaintStyle.StrokeAndFill)throw new NotSupportedException("StrokeAndFill requires a fill/stroke union.");
        if(effected.Style==SKPaintStyle.Fill){dst.ReplaceGeometry(effected.Geometry);return true;}
        if(width==0){dst.ReplaceGeometry(effected.Geometry);return false;}
        double scale=Math.Sqrt((double)m.M00*m.M00+(double)m.M01*m.M01+(double)m.M10*m.M10+(double)m.M11*m.M11);
        float tolerance=(float)(.125/Math.Max(1,scale));if(tolerance<=0)throw new NotSupportedException("Stroke resolution exceeds the subdivision budget.");
        var geometry=effected.Geometry;var cached=strokeCache;
        if(cached is null||cached.Source!=geometry||cached.Width!=width||cached.Miter!=strokeMiter||cached.Cap!=strokeCap||cached.Join!=strokeJoin||cached.Tolerance!=tolerance)
        {
            var key=new StrokeKey(geometry,width,strokeMiter,strokeCap,strokeJoin,tolerance);
            if(strokeEntries is null||!strokeEntries.TryGetValue(key,out cached))
            {
                var outline=StrokeGeometry.Create(geometry.Flatten(Matrix3.Identity,tolerance),width,strokeMiter,(StrokeCap)strokeCap,(StrokeJoin)strokeJoin);
                cached=new(geometry,width,strokeMiter,strokeCap,strokeJoin,tolerance,outline);
                // Bounded per paint, not a global path root. Large results are never retained.
                if(cached.Bytes<=1024*1024)
                {
                    strokeEntries??=[];
                    while(strokeEntries.Count>=16||strokeBytes+cached.Bytes>1024*1024)
                    {
                        StrokeKey victim=default;long oldest=long.MaxValue;
                        foreach(var entry in strokeEntries)if(entry.Value.Used<oldest){victim=entry.Key;oldest=entry.Value.Used;}
                        strokeBytes-=strokeEntries[victim].Bytes;strokeEntries.Remove(victim);
                    }
                    strokeEntries.Add(key,cached);strokeBytes+=cached.Bytes;
                }
            }
            strokeCache=cached.Bytes<=1024*1024?cached:null;
        }
        cached.Used=++strokeClock;
        dst.ReplaceGeometry(cached.Outline);return true;
    }
    protected override void Dispose(bool disposing){retainedBlender=null;blender=null;cachedStages=null;floatingColor=null;maskFilter=null;retainedMaskFilter=null;ClearStrokeCache();ClearEffectCache();pathEffect=null;retainedPathEffect=null;colorFilter=null;retainedColorFilter=null;filteredConstant=null;shaderResources?.Dispose();shaderResources=null;retainedGradient=null;shader=null;imageFilterResources?.Dispose();imageFilterResources=null;imageFilter=null;retainedImageFilter=null;legacyFont?.Dispose();legacyFont=null;}
}
