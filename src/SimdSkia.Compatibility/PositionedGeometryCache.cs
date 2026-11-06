using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SimdSkia;
using SimdSkia.Text;
namespace SkiaSharp;

// One bounded ephemeron cache for all run kinds. Keys copy caller buffers and
// compare their complete contents after hashing. Text annotations and bounds
// hints do not affect glyph geometry, so they never invalidate this cache.
internal sealed class PositionedGeometryCache
{
    private sealed record Entry(int Hash,Matrix3 Transform,int Kind,bool EmbeddedBitmaps,SKPoint Origin,ushort[] Glyphs,byte[] Positions,PathGeometry Geometry,SKRect Bounds,GlyphRunGeometry GlyphRun,long Bytes);
    private static readonly ConditionalWeakTable<TrueTypeFont,PositionedGeometryCache> owners=new();
    private readonly Dictionary<int,List<LinkedListNode<Entry>>> buckets=[];
    private readonly LinkedList<Entry> order=[];
    private long bytes;
    private const int MaximumEntries=64;
    private const long MaximumBytes=4*1024*1024;
    private static ReadOnlySpan<byte> PositionBytes(SKRunBuffer run)=>run.Kind switch
    {
        1=>MemoryMarshal.AsBytes(((float[])run.PositionArray).AsSpan()),
        2=>MemoryMarshal.AsBytes(((SKPoint[])run.PositionArray).AsSpan()),
        3=>MemoryMarshal.AsBytes(((SKRotationScaleMatrix[])run.PositionArray).AsSpan()),
        _=>ReadOnlySpan<byte>.Empty
    };
    internal static (PathGeometry Geometry,SKRect Bounds,GlyphRunGeometry GlyphRun) Get(SKRunBuffer run)
    {
        var cache=owners.GetValue(run.Font.Font,static _=>new());lock(cache)return cache.GetCore(run);
    }
    private (PathGeometry Geometry,SKRect Bounds,GlyphRunGeometry GlyphRun) GetCore(SKRunBuffer run)
    {
        var ids=run.Glyphs;var positions=PositionBytes(run);var font=run.Font;var hash=new HashCode();hash.Add(font.Transform);hash.Add(font.EmbeddedBitmaps);hash.Add(run.Kind);hash.Add(run.Origin);
        foreach(ushort id in ids)if(id>=font.Font.GlyphCount)throw new ArgumentOutOfRangeException("glyphs");
        foreach(float f in MemoryMarshal.Cast<byte,float>(positions))if(!float.IsFinite(f))throw new ArgumentException("Glyph positions must be finite.");
        hash.AddBytes(MemoryMarshal.AsBytes(ids));hash.AddBytes(positions);int key=hash.ToHashCode();
        if(buckets.TryGetValue(key,out var candidates))foreach(var candidate in candidates)
        {
            var e=candidate.Value;
            if(e.Transform==font.Transform&&e.Kind==run.Kind&&e.EmbeddedBitmaps==font.EmbeddedBitmaps&&e.Origin==run.Origin&&ids.SequenceEqual(e.Glyphs)&&positions.SequenceEqual(e.Positions))
            {order.Remove(candidate);order.AddLast(candidate);return(e.Geometry,e.Bounds,e.GlyphRun);}
        }
        var result=SKTextBlobBuilder.Geometry(run);long size=(long)result.Geometry.Segments.Length*32+(long)ids.Length*26+positions.Length;
        foreach(var glyph in result.GlyphRun.Glyphs)size+=(long)glyph.Outline.Segments.Length*32;
        if(size<=MaximumBytes)
        {
            while(order.Count>=MaximumEntries||bytes+size>MaximumBytes)
            {
                var node=order.First!;var e=node.Value;var old=buckets[e.Hash];old.Remove(node);if(old.Count==0)buckets.Remove(e.Hash);bytes-=e.Bytes;order.RemoveFirst();
            }
            var entry=new Entry(key,font.Transform,run.Kind,font.EmbeddedBitmaps,run.Origin,ids.ToArray(),positions.ToArray(),result.Geometry,result.Bounds,result.GlyphRun,size);
            var added=order.AddLast(entry);if(!buckets.TryGetValue(key,out var bucket))buckets.Add(key,bucket=[]);bucket.Add(added);bytes+=size;
        }
        return result;
    }
}
