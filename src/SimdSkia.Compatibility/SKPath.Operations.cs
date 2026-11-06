using SimdSkia;

namespace SkiaSharp;

public partial class SKPath
{
    private PointF CurrentPoint()
    {
        if(segments.Count==0)return default;
        if(segments[^1].Verb!=PathVerb.Close)return End(segments[^1]);
        for(int i=segments.Count-2;i>=0;i--)if(segments[i].Verb==PathVerb.Move)return segments[i].A;
        return default;
    }
    private static PointF End(PathSegment s)=>s.Verb switch{PathVerb.Quadratic or PathVerb.Conic=>s.B,PathVerb.Cubic=>s.C,_=>s.A};
    public int VerbCount {get{ThrowIfDisposed();return segments.Count;}}
    public int PointCount
    {
        get{ThrowIfDisposed();if(cachedPointCount>=0)return cachedPointCount;int n=0;foreach(var s in segments)n+=s.Verb switch{PathVerb.Close=>0,PathVerb.Quadratic or PathVerb.Conic=>2,PathVerb.Cubic=>3,_=>1};return cachedPointCount=n;}
    }
    public SKPoint LastPoint
    {
        get{ThrowIfDisposed();for(int i=segments.Count-1;i>=0;i--)if(segments[i].Verb!=PathVerb.Close){var p=End(segments[i]);return new(p.X,p.Y);}return default;}
    }
    public SKPoint this[int index]=>GetPoint(index);
    public SKPoint[] Points=>GetPoints(PointCount);
    public SKPoint GetPoint(int index)
    {
        ThrowIfDisposed();if(index<0||index>=PointCount)throw new ArgumentOutOfRangeException(nameof(index));
        foreach(var s in segments)
        {
            int count=s.Verb switch{PathVerb.Close=>0,PathVerb.Quadratic or PathVerb.Conic=>2,PathVerb.Cubic=>3,_=>1};
            if(index<count){var p=index switch{0=>s.A,1=>s.B,_=>s.C};return new(p.X,p.Y);}index-=count;
        }
        return default;
    }
    public SKPoint[] GetPoints(int max)
    {
        ThrowIfDisposed();if(max<0)throw new OverflowException();
        var result=new SKPoint[max];GetPoints(result,result.Length);return result;
    }
    public int GetPoints(SKPoint[] points,int max)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(points);if(max<0)throw new ArgumentOutOfRangeException(nameof(max));
        int capacity=Math.Min(max,points.Length),at=0,total=0;
        foreach(var s in segments)
        {
            int n=s.Verb switch{PathVerb.Close=>0,PathVerb.Quadratic or PathVerb.Conic=>2,PathVerb.Cubic=>3,_=>1};total+=n;
            for(int i=0;i<n&&at<capacity;i++){var p=i switch{0=>s.A,1=>s.B,_=>s.C};points[at++]=new(p.X,p.Y);}
        }
        return total;
    }
    public bool GetBounds(out SKRect rect){rect=Bounds;return !IsEmpty;}
    public SKRect TightBounds=>ComputeTightBounds();
    public SKRect ComputeTightBounds(){ThrowIfDisposed();if(tightBounds is {} cached)return cached;var b=ToCore().GetTightBounds(Matrix3.Identity);var rect=new SKRect(b.Left,b.Top,b.Right,b.Bottom);tightBounds=rect;return rect;}
    public bool GetTightBounds(out SKRect result){result=ComputeTightBounds();return true;}
    public void Offset(float dx,float dy){var matrix=SKMatrix.CreateTranslation(dx,dy);Transform(in matrix);}
    public void Offset(SKPoint offset)=>Offset(offset.X,offset.Y);
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public void Transform(in SKMatrix matrix)=>Transform(in matrix,this);
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public void Transform(in SKMatrix matrix,SKPath destination)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(destination);destination.ThrowIfDisposed();
        var m=matrix.ToCore();if(!m.IsFinite)throw new ArgumentException("Path transform must be finite.",nameof(matrix));
        if(!m.IsAffine)throw new NotSupportedException("Perspective path rewriting is not implemented.");
        var transformed=ToCore().Transform(m); // Validate before changing either path, including self transforms.
        var rounded=roundRectTag?.Transform(matrix);
        bool oval=ovalTag&&((m.M01==0&&m.M10==0)||(m.M00==0&&m.M11==0));
        destination.segments.Clear();destination.segments.AddRange(transformed.Segments);destination.fillType=fillType;destination.Invalidate();destination.snapshot=transformed;
        destination.ovalTag=oval;destination.roundRectTag=rounded;
    }
    public void RMoveTo(float dx,float dy){ThrowIfDisposed();var p=CurrentPoint();MoveTo(p.X+dx,p.Y+dy);}
    public void RMoveTo(SKPoint point)=>RMoveTo(point.X,point.Y);
    public void RLineTo(float dx,float dy){ThrowIfDisposed();var p=CurrentPoint();LineTo(p.X+dx,p.Y+dy);}
    public void RLineTo(SKPoint point)=>RLineTo(point.X,point.Y);
    public void RQuadTo(float dx0,float dy0,float dx1,float dy1){ThrowIfDisposed();var p=CurrentPoint();QuadTo(p.X+dx0,p.Y+dy0,p.X+dx1,p.Y+dy1);}
    public void RQuadTo(SKPoint point0,SKPoint point1)=>RQuadTo(point0.X,point0.Y,point1.X,point1.Y);
    public void RCubicTo(float dx0,float dy0,float dx1,float dy1,float dx2,float dy2){ThrowIfDisposed();var p=CurrentPoint();CubicTo(p.X+dx0,p.Y+dy0,p.X+dx1,p.Y+dy1,p.X+dx2,p.Y+dy2);}
    public void RCubicTo(SKPoint point0,SKPoint point1,SKPoint point2)=>RCubicTo(point0.X,point0.Y,point1.X,point1.Y,point2.X,point2.Y);
    public void AddPoly(SKPoint[] points,bool close=true){ArgumentNullException.ThrowIfNull(points);AddPoly(points.AsSpan(),close);}
    public void AddPoly(ReadOnlySpan<SKPoint> points,bool close=true)
    {
        ThrowIfDisposed();foreach(var p in points)if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))throw new ArgumentException("Polygon points must be finite.",nameof(points));
        if(points.IsEmpty)return;MoveTo(points[0]);for(int i=1;i<points.Length;i++)LineTo(points[i]);if(close)Close();
    }
    public void AddPath(SKPath other,SKPathAddMode mode=SKPathAddMode.Append){var matrix=SKMatrix.Identity;AddPath(other,in matrix,mode);}
    public void AddPath(SKPath other,float dx,float dy,SKPathAddMode mode=SKPathAddMode.Append){var matrix=SKMatrix.CreateTranslation(dx,dy);AddPath(other,in matrix,mode);}
    public void AddPath(SKPath other,in SKMatrix matrix,SKPathAddMode mode=SKPathAddMode.Append)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(other);other.ThrowIfDisposed();if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        var m=matrix.ToCore();if(!m.IsFinite)throw new ArgumentException("Path transform must be finite.",nameof(matrix));
        if(!m.IsAffine)throw new NotSupportedException("Perspective path rewriting is not implemented.");
        var transformed=other.ToCore().Transform(m);bool first=true;
        foreach(var s in transformed.Segments)
        {
            if(first&&mode==SKPathAddMode.Extend&&segments.Count>0&&s.Verb==PathVerb.Move)LineTo(s.A.X,s.A.Y);
            else Add(s);
            first=false;
        }
    }
    public void AddRect(SKRect rect,SKPathDirection direction,uint startIndex)
    {
        ThrowIfDisposed();if(startIndex>3)throw new ArgumentOutOfRangeException(nameof(startIndex));if(!Enum.IsDefined(direction))throw new ArgumentOutOfRangeException(nameof(direction));
        if(!rect.ToCore().IsFinite)throw new ArgumentException("Rectangle must be finite.",nameof(rect));
        Span<SKPoint> corners=[new(rect.Left,rect.Top),new(rect.Right,rect.Top),new(rect.Right,rect.Bottom),new(rect.Left,rect.Bottom)];
        int start=(int)(startIndex&3);MoveTo(corners[start]);int step=direction==SKPathDirection.Clockwise?1:-1;
        for(int i=1;i<4;i++)LineTo(corners[(start+step*i+4)&3]);Close();
    }
}
