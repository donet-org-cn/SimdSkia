using SimdSkia;
namespace SkiaSharp;
public partial class SKPaint
{
    private SKPathEffect? pathEffect;
    private SKPathEffect.Node? retainedPathEffect;
    private sealed record EffectCacheEntry(PathGeometry Source,float Width,SKPaintStyle Style,SKPathEffect.Result Result)
    {internal long Used;internal long Bytes=>(long)(Source.Segments.Length+Result.Geometry.Segments.Length)*28;}
    private List<EffectCacheEntry>? effectCache;
    private long effectBytes,effectClock;
    public SKPathEffect? PathEffect
    {
        get{ThrowIfDisposed();return pathEffect;}
        set{ThrowIfDisposed();var node=value?.ToCore();pathEffect=value;retainedPathEffect=node;ClearEffectCache();}
    }
    internal bool HasPathEffect=>retainedPathEffect is not null;
    private void ClearEffectCache(){effectCache?.Clear();effectBytes=effectClock=0;}
    internal SKPathEffect.Result ApplyPathEffect(PathGeometry source,bool forceStroke=false)
    {
        ThrowIfDisposed();var input=new SKPathEffect.Result(source,strokeWidth,forceStroke?SKPaintStyle.Stroke:style);
        if(retainedPathEffect is null)return input;
        if(effectCache is not null)foreach(var cached in effectCache)
            if(ReferenceEquals(cached.Source,source)&&cached.Width==input.Width&&cached.Style==input.Style){cached.Used=++effectClock;return cached.Result;}
        var result=retainedPathEffect.Apply(input);var entry=new EffectCacheEntry(source,input.Width,input.Style,result){Used=++effectClock};
        if(entry.Bytes<=2*1024*1024)
        {
            effectCache??=[];
            while(effectCache.Count>=16||effectBytes+entry.Bytes>2*1024*1024)
            {int victim=0;for(int i=1;i<effectCache.Count;i++)if(effectCache[i].Used<effectCache[victim].Used)victim=i;effectBytes-=effectCache[victim].Bytes;effectCache.RemoveAt(victim);}
            effectCache.Add(entry);effectBytes+=entry.Bytes;
        }
        return result;
    }
}
