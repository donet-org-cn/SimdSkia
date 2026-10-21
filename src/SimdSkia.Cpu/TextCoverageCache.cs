using System.Runtime.CompilerServices;

namespace SimdSkia.Cpu;

// Text blobs outlive frame canvas wrappers. Keep a process-wide LRU, as a glyph
// cache does, with an explicit limit on both entry count and retained data.
// Coverage is independent of color, target pixels, pixel format and hard clip.
internal static class TextCoverageCache
{
    private readonly record struct Key(PathGeometry Geometry,Matrix3 Matrix,bool Antialias,float Tolerance);
    private sealed record Entry(Key Key,CoverageMask Mask,long Bytes);
    private static readonly object gate=new();
    private static readonly Dictionary<Key,LinkedListNode<Entry>> entries=[];
    private static readonly LinkedList<Entry> order=[];
    private static long bytes;
    private sealed class Use {internal int Count;}
    private static readonly ConditionalWeakTable<PathGeometry,Use> uses=new();
    internal const int MaximumEntries=512;
    internal const long MaximumBytes=16*1024*1024;
    internal static bool Enabled=true; // Internal uncached reference for regression tests.
    internal static (int Count,long Bytes) Statistics {get{lock(gate)return(entries.Count,bytes);}}
    internal static void Clear(){lock(gate){entries.Clear();order.Clear();bytes=0;uses.Clear();}}
    internal static bool TryGet(PathGeometry geometry,in DrawingCommand command,int width,int height,out CoverageMask mask,out int offsetX,out int offsetY)
    {
        mask=null!;offsetX=offsetY=0;
        var m=command.Transform;var b=geometry.ControlBounds;
        if(!m.IsFinite||!m.IsAffine||geometry.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd||b.IsEmpty)return false;
        var a=m.Map(b.Left,b.Top);var c=m.Map(b.Right,b.Top);var d=m.Map(b.Left,b.Bottom);var e=m.Map(b.Right,b.Bottom);
        float l=MathF.Min(MathF.Min(a.X,c.X),MathF.Min(d.X,e.X)),r=MathF.Max(MathF.Max(a.X,c.X),MathF.Max(d.X,e.X));
        float t=MathF.Min(MathF.Min(a.Y,c.Y),MathF.Min(d.Y,e.Y)),bottom=MathF.Max(MathF.Max(a.Y,c.Y),MathF.Max(d.Y,e.Y));
        // Bound floating point cancellation and normalized raster allocation.
        if(!float.IsFinite(l)||!float.IsFinite(r)||!float.IsFinite(t)||!float.IsFinite(bottom)||
            MathF.Max(MathF.Max(MathF.Abs(l),MathF.Abs(r)),MathF.Max(MathF.Abs(t),MathF.Abs(bottom)))>65536)return false;
        int x0=(int)MathF.Floor(l)-1,y0=(int)MathF.Floor(t)-1,x1=(int)MathF.Ceiling(r)+1,y1=(int)MathF.Ceiling(bottom)+1;
        offsetX=x0;offsetY=y0;
        if(x0>=width||y0>=height||x1<=0||y1<=0||command.Clip is {} clip&&(x0>=clip.Right||y0>=clip.Bottom||x1<=clip.Left||y1<=clip.Top))
        {mask=new(0,0,0,0,[],[]);return true;}
        int w=x1-x0,h=y1-y0;if(w<=0||h<=0||w>32768||h>32768||(long)w*h>4*1024*1024)return false;
        // Always evaluate on the local sample grid, also on cache misses. Integer
        // scrolling therefore never changes glyph shape through float cancellation.
        var local=m with {M02=m.M02-x0,M12=m.M12-y0};var key=new Key(geometry,local,command.Paint.Antialias,command.CurveTolerance);
        if(Enabled)lock(gate)if(entries.TryGetValue(key,out var node)){order.Remove(node);order.AddLast(node);mask=node.Value.Mask;return true;}
        mask=PathRasterizer.Rasterize(geometry.Flatten(local,command.CurveTolerance),geometry.FillRule,w,h,command.Paint.Antialias,compactRuns:true);
        long size=mask.Data.LongLength+(long)mask.Runs.Length*16+(long)geometry.Segments.Length*32;
        if(!Enabled||size>MaximumBytes||Interlocked.Increment(ref uses.GetValue(geometry,static _=>new()).Count)<2)return true;
        lock(gate)
        {
            if(entries.TryGetValue(key,out var existing)){mask=existing.Value.Mask;return true;}
            while(entries.Count>=MaximumEntries||bytes+size>MaximumBytes)
            {var first=order.First!;bytes-=first.Value.Bytes;entries.Remove(first.Value.Key);order.RemoveFirst();}
            entries.Add(key,order.AddLast(new Entry(key,mask,size)));bytes+=size;
        }
        return true;
    }
}
