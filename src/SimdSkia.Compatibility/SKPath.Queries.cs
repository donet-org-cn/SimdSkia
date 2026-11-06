using SimdSkia;

namespace SkiaSharp;

public partial class SKPath
{
    public bool IsLine {get{ThrowIfDisposed();return segments.Count==2&&segments[0].Verb==PathVerb.Move&&segments[1].Verb==PathVerb.Line;}}
    public SKPoint[] GetLine()=>IsLine?[new(segments[0].A.X,segments[0].A.Y),new(segments[1].A.X,segments[1].A.Y)]:null!;
    public bool IsOval {get{ThrowIfDisposed();return ovalTag;}}
    public SKRect GetOvalBounds()=>IsOval?Bounds:SKRect.Empty;
    public SKPathSegmentMask SegmentMasks
    {
        get
        {
            ThrowIfDisposed();if(cachedSegmentMasks>=0)return (SKPathSegmentMask)cachedSegmentMasks;int bits=0;
            foreach(var s in segments)bits|=s.Verb switch{PathVerb.Line=>1,PathVerb.Quadratic=>2,PathVerb.Conic=>4,PathVerb.Cubic=>8,_=>0};
            cachedSegmentMasks=bits;return (SKPathSegmentMask)bits;
        }
    }
    public bool IsRect=>TryGetRect(out _,out _,out _);
    public SKRect GetRect()=>GetRect(out _,out _);
    public SKRect GetRect(out bool isClosed,out SKPathDirection direction)
    {return TryGetRect(out var rect,out isClosed,out direction)?rect:SKRect.Empty;}
    private bool TryGetRect(out SKRect rect,out bool closed,out SKPathDirection direction)
    {
        ThrowIfDisposed();rect=default;closed=false;direction=default;
        if(segments.Count<4||segments[0].Verb!=PathVerb.Move)return false;
        PointF first=segments[0].A,previous=first;float left=first.X,top=first.Y,right=first.X,bottom=first.Y;
        int firstDirection=-1,lastDirection=-1,turnSign=0,turns=0;
        bool Edge(PointF end)
        {
            if(end==previous)return true;
            int d;if(end.Y==previous.Y)d=end.X>previous.X?0:2;else if(end.X==previous.X)d=end.Y>previous.Y?1:3;else return false;
            if(firstDirection<0)firstDirection=d;
            else if(lastDirection!=d)
            {int t=(d-lastDirection+4)&3;if(t==2)return false;int sign=t==1?1:-1;if(turnSign!=0&&sign!=turnSign)return false;turnSign=sign;turns++;}
            lastDirection=d;previous=end;left=MathF.Min(left,end.X);top=MathF.Min(top,end.Y);right=MathF.Max(right,end.X);bottom=MathF.Max(bottom,end.Y);return true;
        }
        for(int i=1;i<segments.Count;i++)
        {
            var s=segments[i];if(s.Verb==PathVerb.Close){if(i!=segments.Count-1)return false;closed=true;}
            else if(s.Verb!=PathVerb.Line||!Edge(s.A))return false;
        }
        if(!Edge(first)||firstDirection<0||left==right||top==bottom)return false;
        if(lastDirection!=firstDirection)
        {int t=(firstDirection-lastDirection+4)&3;if(t==2||(t==1?1:-1)!=turnSign)return false;turns++;}
        if(turns!=4)return false;rect=new(left,top,right,bottom);direction=turnSign>0?SKPathDirection.Clockwise:SKPathDirection.CounterClockwise;return true;
    }
    internal void CopyFrom(SKPath source)
    {
        source.ThrowIfDisposed();ThrowIfDisposed();if(ReferenceEquals(source,this))return;
        segments.Clear();segments.AddRange(source.segments);fillType=source.fillType;Invalidate();ovalTag=source.ovalTag;roundRectTag=source.roundRectTag;
    }
    internal void ReplaceGeometry(PathGeometry geometry)
    {ThrowIfDisposed();if(ReferenceEquals(snapshot,geometry))return;segments.Clear();segments.AddRange(geometry.Segments);fillType=(SKPathFillType)geometry.FillRule;Invalidate();snapshot=geometry;}
}
