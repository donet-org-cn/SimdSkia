using SimdSkia;

namespace SkiaSharp;

public partial class SKPath : SKObject
{
    private readonly List<PathSegment> segments=[];
    private PathGeometry? snapshot;
    private int cachedPointCount=-1;
    private SKRect? tightBounds;
    private bool ovalTag;
    private SKRoundRect? roundRectTag;
    private int cachedSegmentMasks=-1;
    private void Invalidate(bool preserveShape=false){snapshot=null;cachedPointCount=-1;tightBounds=null;cachedSegmentMasks=-1;if(!preserveShape){ovalTag=false;roundRectTag=null;}}
    private SKPathFillType fillType;
    public SKPath(){ }
    internal SKPath(PathGeometry geometry){segments.AddRange(geometry.Segments.ToArray());fillType=(SKPathFillType)geometry.FillRule;snapshot=geometry;}
    public SKPath(SKPath path){ArgumentNullException.ThrowIfNull(path);path.ThrowIfDisposed();segments.AddRange(path.segments);fillType=path.fillType;ovalTag=path.ovalTag;roundRectTag=path.roundRectTag;}
    public SKPathFillType FillType {get{ThrowIfDisposed();return fillType;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));fillType=value;Invalidate(true);}}
    public bool IsEmpty {get{ThrowIfDisposed();return segments.Count==0;}}
    public SKRect Bounds { get { ThrowIfDisposed();var b=ToCore().ControlBounds;return new(b.Left,b.Top,b.Right,b.Bottom); } }
    private void Add(PathSegment s){ThrowIfDisposed();if(!float.IsFinite(s.A.X)||!float.IsFinite(s.A.Y)||!float.IsFinite(s.B.X)||!float.IsFinite(s.B.Y)||!float.IsFinite(s.C.X)||!float.IsFinite(s.C.Y))throw new ArgumentException("Path coordinates must be finite.");if(s.Verb==PathVerb.Close&&(segments.Count==0||segments[^1].Verb==PathVerb.Close))return;
        if(s.Verb is PathVerb.Line or PathVerb.Quadratic or PathVerb.Cubic or PathVerb.Conic)
        {
            if(segments.Count==0)segments.Add(new(PathVerb.Move,default));
            else if(segments[^1].Verb==PathVerb.Close)segments.Add(new(PathVerb.Move,CurrentPoint()));
        }
        segments.Add(s);Invalidate();}
    public void MoveTo(float x,float y)=>Add(new(PathVerb.Move,new(x,y)));
    public void MoveTo(SKPoint point)=>MoveTo(point.X,point.Y);
    public void LineTo(float x,float y)=>Add(new(PathVerb.Line,new(x,y)));
    public void LineTo(SKPoint point)=>LineTo(point.X,point.Y);
    public void QuadTo(float x0,float y0,float x1,float y1)=>Add(new(PathVerb.Quadratic,new(x0,y0),new(x1,y1)));
    public void QuadTo(SKPoint point0,SKPoint point1)=>QuadTo(point0.X,point0.Y,point1.X,point1.Y);
    public void CubicTo(float x0,float y0,float x1,float y1,float x2,float y2)=>Add(new(PathVerb.Cubic,new(x0,y0),new(x1,y1),new(x2,y2)));
    public void CubicTo(SKPoint point0,SKPoint point1,SKPoint point2)=>CubicTo(point0.X,point0.Y,point1.X,point1.Y,point2.X,point2.Y);
    public void Close()=>Add(new(PathVerb.Close,default));
    public void Reset(){ThrowIfDisposed();segments.Clear();fillType=SKPathFillType.Winding;Invalidate();}
    public void Rewind(){ThrowIfDisposed();segments.Clear();Invalidate();}
    public void AddRect(SKRect rect,SKPathDirection direction=SKPathDirection.Clockwise)
    {
        if(!Enum.IsDefined(direction))throw new ArgumentOutOfRangeException(nameof(direction));
        MoveTo(rect.Left,rect.Top);
        if(direction==SKPathDirection.Clockwise){LineTo(rect.Right,rect.Top);LineTo(rect.Right,rect.Bottom);LineTo(rect.Left,rect.Bottom);}
        else{LineTo(rect.Left,rect.Bottom);LineTo(rect.Right,rect.Bottom);LineTo(rect.Right,rect.Top);}Close();
    }
    public void AddCircle(float x,float y,float radius,SKPathDirection dir=SKPathDirection.Clockwise)
    {
        ThrowIfDisposed();if(!float.IsFinite(radius))throw new ArgumentOutOfRangeException(nameof(radius));
        if(!Enum.IsDefined(dir))throw new ArgumentOutOfRangeException(nameof(dir));
        if(radius<=0)return;AddOval(new(x-radius,y-radius,x+radius,y+radius),dir);
    }
    public void AddRoundRect(SKRect rect,float rx,float ry,SKPathDirection dir=SKPathDirection.Clockwise)
    {ThrowIfDisposed();if(rx<0||ry<0)return;using var rounded=new SKRoundRect(rect,rx,ry);AddRoundRect(rounded,dir);}
    public void AddRoundRect(SKRoundRect rect,SKPathDirection direction=SKPathDirection.Clockwise)
    =>AddRoundRect(rect,direction,direction==SKPathDirection.Clockwise?6u:7u);
    internal void AppendGeometry(PathGeometry geometry,Matrix3 matrix){ThrowIfDisposed();geometry.AppendTransformedTo(segments,matrix);Invalidate();}
    internal PathGeometry ToCore(){ThrowIfDisposed();return snapshot??=new(segments,(FillRule)fillType);}
    protected override void Dispose(bool disposing){segments.Clear();Invalidate();}
}
