using System.Runtime.CompilerServices;
using SimdSkia;
using SimdSkia.Text;

namespace SkiaSharp;

// A font owns this cache through an ephemeron: releasing the font also releases
// the cache. Keys copy the caller's mutable run buffers. Hash collisions always
// require full glyph/position comparison; colors and explicit bounds aren't keys.
internal sealed class PositionedGeometryCache
{
    private sealed record Entry(int Hash,Matrix3 Transform,ushort[] Glyphs,SKPoint[] Positions,PathGeometry Geometry,SKRect Bounds,long Bytes);
    private static readonly ConditionalWeakTable<TrueTypeFont,PositionedGeometryCache> owners=new();
    private readonly Dictionary<int,List<LinkedListNode<Entry>>> buckets=[];
    private readonly LinkedList<Entry> order=[];
    private long bytes;
    private const int MaximumEntries=64;
    private const long MaximumBytes=4*1024*1024;
    internal static (PathGeometry Geometry,SKRect Bounds) Get(SKPositionedRunBuffer run)
    {
        var cache=owners.GetValue(run.Font.Font,static _=>new());
        lock(cache)return cache.GetCore(run);
    }
    private (PathGeometry Geometry,SKRect Bounds) GetCore(SKPositionedRunBuffer run)
    {
        var ids=run.Glyphs;var positions=run.Positions;var font=run.Font;var hash=new HashCode();hash.Add(font.Transform);
        for(int i=0;i<ids.Length;i++)
        {
            if(ids[i]>=font.Font.GlyphCount)throw new ArgumentOutOfRangeException("glyphs");
            if(!float.IsFinite(positions[i].X)||!float.IsFinite(positions[i].Y))throw new ArgumentException("Glyph positions must be finite.");
            hash.Add(ids[i]);hash.Add(positions[i]);
        }
        int key=hash.ToHashCode();
        if(buckets.TryGetValue(key,out var candidates))foreach(var candidate in candidates)
        {
            var e=candidate.Value;
            if(e.Transform==font.Transform&&ids.SequenceEqual(e.Glyphs)&&positions.SequenceEqual(e.Positions))
            {order.Remove(candidate);order.AddLast(candidate);return(e.Geometry,e.Bounds);}
        }
        var segments=new List<PathSegment>();SKRect? bounds=null;var globalBounds=font.GlobalBounds();
        for(int i=0;i<ids.Length;i++)
        {
            var position=positions[i];font.Font.Outline(ids[i]).AppendTransformedTo(segments,font.Transform with {M02=position.X,M12=position.Y});
            var b=globalBounds;
            if(!b.ToCore().IsEmpty)
            {
                b=new(b.Left+position.X,b.Top+position.Y,b.Right+position.X,b.Bottom+position.Y);
                bounds=bounds is {} a?new(MathF.Min(a.Left,b.Left),MathF.Min(a.Top,b.Top),MathF.Max(a.Right,b.Right),MathF.Max(a.Bottom,b.Bottom)):b;
            }
        }
        var geometry=new PathGeometry(segments);var runBounds=bounds??SKRect.Empty;
        long size=(long)geometry.Segments.Length*32+(long)ids.Length*10;
        if(size<=MaximumBytes)
        {
            while(order.Count>=MaximumEntries||bytes+size>MaximumBytes)
            {
                var node=order.First!;var e=node.Value;var oldBucket=buckets[e.Hash];oldBucket.Remove(node);if(oldBucket.Count==0)buckets.Remove(e.Hash);
                bytes-=e.Bytes;order.RemoveFirst();
            }
            var entry=new Entry(key,font.Transform,ids.ToArray(),positions.ToArray(),geometry,runBounds,size);
            var added=order.AddLast(entry);if(!buckets.TryGetValue(key,out var bucket))buckets.Add(key,bucket=[]);bucket.Add(added);bytes+=size;
        }
        return(geometry,runBounds);
    }
}
