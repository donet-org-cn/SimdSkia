using SimdSkia;

namespace SkiaSharp;

public class SKPath : SKObject
{
    private readonly List<PathSegment> segments=[];
    private PathGeometry? snapshot;
    private SKPathFillType fillType;
    public SKPath(){ }
    internal SKPath(PathGeometry geometry){segments.AddRange(geometry.Segments.ToArray());fillType=(SKPathFillType)geometry.FillRule;snapshot=geometry;}
    public SKPath(SKPath path){ArgumentNullException.ThrowIfNull(path);path.ThrowIfDisposed();segments.AddRange(path.segments);fillType=path.fillType;}
    public SKPathFillType FillType {get{ThrowIfDisposed();return fillType;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));fillType=value;snapshot=null;}}
    public bool IsEmpty {get{ThrowIfDisposed();return segments.Count==0;}}
    public SKRect Bounds
    {
        get
        {
            ThrowIfDisposed();var points=new List<PointF>();foreach(var segment in segments)
            {if(segment.Verb==PathVerb.Close)continue;points.Add(segment.A);if(segment.Verb is PathVerb.Quadratic or PathVerb.Cubic)points.Add(segment.B);if(segment.Verb==PathVerb.Cubic)points.Add(segment.C);}
            return points.Count==0?SKRect.Empty:new(points.Min(p=>p.X),points.Min(p=>p.Y),points.Max(p=>p.X),points.Max(p=>p.Y));
        }
    }
    private void Add(PathSegment s){ThrowIfDisposed();if(!float.IsFinite(s.A.X)||!float.IsFinite(s.A.Y)||!float.IsFinite(s.B.X)||!float.IsFinite(s.B.Y)||!float.IsFinite(s.C.X)||!float.IsFinite(s.C.Y))throw new ArgumentException("Path coordinates must be finite.");segments.Add(s);snapshot=null;}
    public void MoveTo(float x,float y)=>Add(new(PathVerb.Move,new(x,y)));
    public void MoveTo(SKPoint point)=>MoveTo(point.X,point.Y);
    public void LineTo(float x,float y)=>Add(new(PathVerb.Line,new(x,y)));
    public void LineTo(SKPoint point)=>LineTo(point.X,point.Y);
    public void QuadTo(float x0,float y0,float x1,float y1)=>Add(new(PathVerb.Quadratic,new(x0,y0),new(x1,y1)));
    public void QuadTo(SKPoint point0,SKPoint point1)=>QuadTo(point0.X,point0.Y,point1.X,point1.Y);
    public void CubicTo(float x0,float y0,float x1,float y1,float x2,float y2)=>Add(new(PathVerb.Cubic,new(x0,y0),new(x1,y1),new(x2,y2)));
    public void CubicTo(SKPoint point0,SKPoint point1,SKPoint point2)=>CubicTo(point0.X,point0.Y,point1.X,point1.Y,point2.X,point2.Y);
    public void Close()=>Add(new(PathVerb.Close,default));
    public void Reset(){ThrowIfDisposed();segments.Clear();fillType=SKPathFillType.Winding;snapshot=null;}
    public void Rewind(){ThrowIfDisposed();segments.Clear();snapshot=null;}
    public void AddRect(SKRect rect,SKPathDirection direction=SKPathDirection.Clockwise)
    {
        if(!Enum.IsDefined(direction))throw new ArgumentOutOfRangeException(nameof(direction));
        MoveTo(rect.Left,rect.Top);
        if(direction==SKPathDirection.Clockwise){LineTo(rect.Right,rect.Top);LineTo(rect.Right,rect.Bottom);LineTo(rect.Left,rect.Bottom);}
        else{LineTo(rect.Left,rect.Bottom);LineTo(rect.Right,rect.Bottom);LineTo(rect.Right,rect.Top);}Close();
    }
    public void AddCircle(float x,float y,float radius,SKPathDirection dir=SKPathDirection.Clockwise)
    {
        if(radius<0||!float.IsFinite(radius))throw new ArgumentOutOfRangeException(nameof(radius));
        if(!Enum.IsDefined(dir))throw new ArgumentOutOfRangeException(nameof(dir));
        float r=radius,k=r*0.5522847498307936f,sign=dir==SKPathDirection.Clockwise?1:-1;
        MoveTo(x+r,y);CubicTo(x+r,y+k*sign,x+k,y+r*sign,x,y+r*sign);
        CubicTo(x-k,y+r*sign,x-r,y+k*sign,x-r,y);CubicTo(x-r,y-k*sign,x-k,y-r*sign,x,y-r*sign);
        CubicTo(x+k,y-r*sign,x+r,y-k*sign,x+r,y);Close();
    }
    public void AddRoundRect(SKRect rect,float rx,float ry,SKPathDirection dir=SKPathDirection.Clockwise)
    {using var rounded=new SKRoundRect(rect,rx,ry);AddRoundRect(rounded,dir);}
    public void AddRoundRect(SKRoundRect rect,SKPathDirection direction=SKPathDirection.Clockwise)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(rect);using var path=rect.ToPath(direction);segments.AddRange(path.segments);snapshot=null;}
    internal PathGeometry ToCore(){ThrowIfDisposed();return snapshot??=new(segments,(FillRule)fillType);}
    protected override void Dispose(bool disposing){segments.Clear();snapshot=null;}
}
