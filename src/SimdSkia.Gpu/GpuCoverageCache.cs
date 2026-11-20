using SimdSkia.Cpu;
namespace SimdSkia.Gpu;

// Device coverage for paths without an analytic form, shared by every canvas of
// a device (window and per-frame layers). A shape fully inside its target is
// keyed by the fractional part of its translation: moving it, or drawing it
// into a layer at another origin, reuses the bytes as an integer-offset view.
// Least-recently-used eviction keeps a frame's working set instead of
// clearing everything once the budget is reached.
internal sealed class GpuCoverageCache
{
    private readonly record struct Key(PathGeometry Path,Matrix3 Matrix,bool Antialias,bool Quad,int Width,int Height);
    private sealed record Entry(Key Key,CoverageClip Clip,long Bytes);
    private readonly Dictionary<Key,LinkedListNode<Entry>> entries=[];
    private readonly LinkedList<Entry> order=[];
    private long bytes;
    private long limit=12*1024*1024;
    private const int MaxEntries=1024;
    internal int Count=>entries.Count;
    internal long Bytes=>bytes;
    internal CoverageClip Get(PathGeometry path,in DrawingCommand command,int width,int height)
    {
        var m=command.Transform;
        bool quad=command.Kind==CommandKind.DrawImage&&command.Paint.Antialias&&m.M01==0&&m.M10==0;
        int dx=0,dy=0;var keyMatrix=m;int keyWidth=width,keyHeight=height;
        if(!quad&&Inside(path,m,width,height))
        {
            dx=(int)MathF.Floor(m.M02);dy=(int)MathF.Floor(m.M12);
            keyMatrix=m with{M02=m.M02-dx,M12=m.M12-dy};keyWidth=keyHeight=-1;
        }
        var key=new Key(path,keyMatrix,command.Paint.Antialias,quad,keyWidth,keyHeight);
        if(entries.TryGetValue(key,out var node))
        {
            if(!ReferenceEquals(order.First,node)){order.Remove(node);order.AddFirst(node);}
            return node.Value.Clip.Offset(dx,dy);
        }
        CoverageClip result;
        if(quad)
        {
            var a=m.Map(command.Rectangle.Left,command.Rectangle.Top);var b=m.Map(command.Rectangle.Right,command.Rectangle.Bottom);
            float left=MathF.Min(a.X,b.X),top=MathF.Min(a.Y,b.Y),right=MathF.Max(a.X,b.X),bottom=MathF.Max(a.Y,b.Y);
            int x0=(int)Math.Clamp(MathF.Floor(left),0,width),y0=(int)Math.Clamp(MathF.Floor(top),0,height),x1=(int)Math.Clamp(MathF.Ceiling(right),x0,width),y1=(int)Math.Clamp(MathF.Ceiling(bottom),y0,height);var values=new byte[checked((x1-x0)*(y1-y0))];
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){float cx=Math.Clamp(MathF.Min(x+1,right)-MathF.Max(x,left),0,1),cy=Math.Clamp(MathF.Min(y+1,bottom)-MathF.Max(y,top),0,1);values[(y-y0)*(x1-x0)+x-x0]=(byte)MathF.Floor(MathF.Min(cx,cy)*255+.5f);}
            result=new(x0,y0,x1-x0,y1-y0,values);
        }
        else result=RasterCanvas.CreateClipForBounds(path,m,width,height,command.Paint.Antialias);
        // Stored at the fractional origin; hits are offset back into place.
        var stored=result.Offset(-dx,-dy);
        long length=(long)result.Width*result.Height+(long)path.SegmentCount*32;
        if(limit>0&&length<=limit/4)
        {
            while(entries.Count>=MaxEntries||bytes+length>limit){var last=order.Last!;order.RemoveLast();entries.Remove(last.Value.Key);bytes-=last.Value.Bytes;}
            entries.Add(key,order.AddFirst(new Entry(key,stored,length)));bytes+=length;
        }
        return result;
    }
    // Translation invariance holds when no edge of the shape (plus its
    // antialiasing pixel) is clipped by the target bounds. An inverse fill
    // covers everything outside the path up to the target edges, so it is
    // never invariant: its key keeps the full transform and target size.
    private static bool Inside(PathGeometry path,Matrix3 m,int width,int height)
    {
        if(path.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd)return false;
        var b=path.ControlBounds;if(!b.IsFinite)return false;
        var p0=m.Map(b.Left,b.Top);var p1=m.Map(b.Right,b.Top);var p2=m.Map(b.Left,b.Bottom);var p3=m.Map(b.Right,b.Bottom);
        float left=MathF.Min(MathF.Min(p0.X,p1.X),MathF.Min(p2.X,p3.X)),right=MathF.Max(MathF.Max(p0.X,p1.X),MathF.Max(p2.X,p3.X));
        float top=MathF.Min(MathF.Min(p0.Y,p1.Y),MathF.Min(p2.Y,p3.Y)),bottom=MathF.Max(MathF.Max(p0.Y,p1.Y),MathF.Max(p2.Y,p3.Y));
        return left>=1&&top>=1&&right<=width-1&&bottom<=height-1&&float.IsFinite(left+right+top+bottom);
    }
    internal void Configure(long budget,bool enabled){limit=enabled?Math.Clamp(budget,0,16*1024*1024):0;if(bytes>limit)Clear();}
    internal void Clear(){entries.Clear();order.Clear();bytes=0;}
}
