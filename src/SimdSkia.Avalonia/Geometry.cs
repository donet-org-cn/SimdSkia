using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Media.Immutable;
using Clipper2Lib;
using SPath=SimdSkia.PathGeometry;
using AFill=Avalonia.Media.FillRule;
using CFill=Clipper2Lib.FillRule;

namespace SimdSkia.Avalonia;

internal class Geometry : IGeometryImpl
{
    internal SPath Path {get;set;}
    internal SPath StrokePath {get;set;}
    private readonly record struct StrokeKey(double Width,PenLineCap Cap,PenLineJoin Join,double Miter,ImmutableDashStyle? Dash);
    private readonly record struct SharedStrokeKey(PathCacheKey Path, StrokeKey Pen);
    private const int SharedStrokeBudget = 2 * 1024 * 1024;
    private static readonly object sharedStrokeGate = new();
    private static readonly Dictionary<SharedStrokeKey, LinkedListNode<(SharedStrokeKey Key, Geometry Value, long Bytes)>> sharedStrokes = [];
    private static readonly LinkedList<(SharedStrokeKey Key, Geometry Value, long Bytes)> sharedStrokeOrder = [];
    private static long sharedStrokeBytes;
    private static ClipperD? smallStrokeUnion;
    private readonly object strokeGate=new();
    private sealed class FirstStroke(StrokeKey key,Geometry geometry)
    {
        internal readonly StrokeKey Key=key;
        internal readonly Geometry Geometry=geometry;
        internal Dictionary<StrokeKey,Geometry>? Other;
    }
    private FirstStroke? strokes;
    private WeakReference<PointF[][]>? hitContours;
    private sealed record CachedBounds(SPath Path,Rect Bounds);
    private CachedBounds? bounds;
    internal void Replace(SPath path,SPath stroke)
    {
        lock(strokeGate){Path=path;StrokePath=stroke;strokes=null;hitContours=null;Volatile.Write(ref bounds,null);}
    }
    internal Geometry(SPath path,SPath? stroke=null){Path=path;StrokePath=stroke??path;}
    internal virtual void Validate() { }
    public Rect Bounds
    {
        get
        {
            Validate();var path=StrokePath;var cached=Volatile.Read(ref bounds);
            if(cached is not null&&ReferenceEquals(cached.Path,path))return cached.Bounds;
            var value=ConvertTo.Rect(path.GetTightBounds(Matrix3.Identity));
            Volatile.Write(ref bounds,new(path,value));return value;
        }
    }
    internal PointF[][] Contours=>StrokePath.Flatten(Matrix3.Identity,0.015625f);
    public double ContourLength=>Edges().Sum(e=>e.Length);
    private IEnumerable<(PointF A,PointF B,double Length)> Edges()
    {foreach(var contour in Contours)for(int i=1;i<contour.Length;i++){var a=contour[i-1];var b=contour[i];yield return(a,b,Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y)));}}
    public Rect GetRenderBounds(IPen? pen)=>pen is null||pen.Thickness==0?Bounds:((Geometry)GetWidenedGeometry(pen)).Bounds.Union(Bounds);
    public IGeometryImpl GetWidenedGeometry(IPen pen)
    {
        Validate();
        if(!double.IsFinite(pen.Thickness)||pen.Thickness<0)throw new ArgumentOutOfRangeException(nameof(pen));
        if(!double.IsFinite(pen.MiterLimit)||pen.MiterLimit<0)throw new ArgumentOutOfRangeException(nameof(pen));
        var dashSnapshot=pen.DashStyle is ImmutableDashStyle immutable?immutable:pen.DashStyle is {} mutable?new ImmutableDashStyle(mutable.Dashes,mutable.Offset):null;
        var key=new StrokeKey(pen.Thickness,pen.LineCap,pen.LineJoin,pen.MiterLimit,dashSnapshot);
        lock(strokeGate)
        {
            if(strokes is {} first)
            {
                if(key==first.Key)return first.Geometry;
                if(first.Other is {} other&&other.TryGetValue(key,out var cached))return cached;
            }
            var result=GetSharedStroke(StrokePath,key);
            if(strokes is null||strokes.Other?.Count>=31)strokes=new(key,result);
            else (strokes.Other??=[]).Add(key,result);
            return result;
        }
    }
    private static Geometry GetSharedStroke(SPath path, StrokeKey pen)
    {
        if (path.Segments.Length > PathCacheKey.ContentLimit || pen.Dash?.Dashes?.Count > 32)
            return CreateStroke(path, pen);

        var key = new SharedStrokeKey(new(path), pen);
        lock (sharedStrokeGate)
        {
            if (sharedStrokes.TryGetValue(key, out var existing))
            {
                sharedStrokeOrder.Remove(existing);
                sharedStrokeOrder.AddLast(existing);
                return existing.Value.Value;
            }
        }

        var stroke = CreateStroke(path, pen);
        long bytes = ((long)path.Segments.Length + stroke.Path.SegmentCount) * 32 + 256;
        if (bytes > SharedStrokeBudget / 4)
            return stroke;

        lock (sharedStrokeGate)
        {
            if (sharedStrokes.TryGetValue(key, out var existing))
                return existing.Value.Value;
            while (sharedStrokes.Count >= 128 || sharedStrokeBytes + bytes > SharedStrokeBudget)
            {
                var oldest = sharedStrokeOrder.First!;
                sharedStrokes.Remove(oldest.Value.Key);
                sharedStrokeBytes -= oldest.Value.Bytes;
                sharedStrokeOrder.RemoveFirst();
            }
            sharedStrokes.Add(key, sharedStrokeOrder.AddLast((key, stroke, bytes)));
            sharedStrokeBytes += bytes;
            return stroke;
        }
    }

    private static Geometry CreateStroke(SPath path, StrokeKey key)
    {
        if (TryCreateFlatAxisStroke(path, key) is { } aligned)
            return aligned;

        var paths=new PathsD();
        if(key.Width>0)foreach(var contour in path.Flatten(Matrix3.Identity,0.015625f))
        {
            if(contour.Length<2)continue;
            bool closed=contour[0]==contour[^1];
            var input=ToPathD(contour);
            var end=closed?EndType.Joined:key.Cap switch {PenLineCap.Flat=>EndType.Butt,PenLineCap.Round=>EndType.Round,PenLineCap.Square=>EndType.Square,_=>throw new NotSupportedException("Unknown line cap.")};
            var join=key.Join switch {PenLineJoin.Miter=>JoinType.Miter,PenLineJoin.Round=>JoinType.Round,PenLineJoin.Bevel=>JoinType.Bevel,_=>throw new NotSupportedException("Unknown line join.")};
            var pieces=key.Dash is {Dashes.Count:>0} dash?Dash(input,dash,key.Width,closed,key.Cap):new PathsD{input};
            if(key.Dash is {Dashes.Count:>0})end=key.Cap switch {PenLineCap.Flat=>EndType.Butt,PenLineCap.Round=>EndType.Round,_=>EndType.Square};
            paths.AddRange(Clipper.InflatePaths(pieces,key.Width/2,join,end,key.Miter,5,0.015625));
        }
        int vertices = 0;
        foreach (var polygon in paths)
        {
            if (polygon.Count > 64 - vertices)
                return new Geometry(FromPaths(Clipper.Union(paths,CFill.NonZero)));
            vertices += polygon.Count;
        }
        // Clipper owns reusable vertex and output pools. Lend one engine to one
        // small union at a time. Large or failed unions release their scratch.
        var union = Interlocked.Exchange(ref smallStrokeUnion, null) ?? new ClipperD(2);
        var solution = new PathsD();
        union.AddSubject(paths);
        bool succeeded = union.Execute(ClipType.Union,CFill.NonZero,solution);
        var outline = FromPaths(solution);
        if (succeeded && outline.SegmentCount <= 128)
        {
            union.Clear();
            Interlocked.CompareExchange(ref smallStrokeUnion,union,null);
        }
        return new Geometry(outline);
    }

    private static Geometry? TryCreateFlatAxisStroke(SPath path, StrokeKey pen)
    {
        var segments = path.Segments;
        if (pen.Cap != PenLineCap.Flat || pen.Join is not (PenLineJoin.Miter or PenLineJoin.Bevel or PenLineJoin.Round) ||
            segments.Length != 2 ||
            segments[0].Verb != PathVerb.Move || segments[1].Verb != PathVerb.Line)
            return null;

        var start = segments[0].A;
        var end = segments[1].A;
        bool horizontal = start.Y == end.Y;
        if (start == end || (!horizontal && start.X != end.X))
            return null;

        // Match the offset precision and final union precision used by Clipper.
        const double offsetScale = 100_000;
        double half = pen.Width / 2 * offsetScale;
        if (half < 1 || half != Math.Truncate(half) ||
            Math.Max(Math.Max(Math.Abs(start.X), Math.Abs(end.X)), Math.Max(Math.Abs(start.Y), Math.Abs(end.Y))) +
            pen.Width / 2 > 1_000_000)
            return null;

        static double OffsetCoordinate(double coordinate) => Math.Round(coordinate * offsetScale, MidpointRounding.AwayFromZero);
        static float FinalCoordinate(double coordinate) => (float)(Math.Round(coordinate * (1 / offsetScale) * 100,
            MidpointRounding.AwayFromZero) * .01);

        double axis = OffsetCoordinate(horizontal ? start.Y : start.X);
        float near = FinalCoordinate(axis - half), far = FinalCoordinate(axis + half);
        var rectangles = new List<RectF>();
        bool Add(double begin, double finish)
        {
            double first = OffsetCoordinate(begin), last = OffsetCoordinate(finish);
            if (first == last) return false;
            float low = FinalCoordinate(Math.Min(first, last)), high = FinalCoordinate(Math.Max(first, last));
            if (low == high || near == far) return true;
            var box = horizontal ? new RectF(low, near, high, far) : new RectF(near, low, far, high);
            if (rectangles.Count > 0)
            {
                var previous = rectangles[^1];
                if (previous.Left <= box.Right && box.Left <= previous.Right &&
                    previous.Top <= box.Bottom && box.Top <= previous.Bottom)
                {
                    rectangles[^1] = new RectF(Math.Min(previous.Left, box.Left), Math.Min(previous.Top, box.Top),
                        Math.Max(previous.Right, box.Right), Math.Max(previous.Bottom, box.Bottom));
                    return true;
                }
            }
            rectangles.Add(box);
            return true;
        }

        double origin = horizontal ? start.X : start.Y;
        double finish = horizontal ? end.X : end.Y;
        if (pen.Dash is { Dashes.Count: > 0 } dash)
        {
            var cursor = new DashCursor(dash, pen.Width);
            double delta = finish - origin, length = Math.Sqrt(delta * delta);
            if (dash.Dashes is { Count: 2 } pattern && pattern[0] > 0 && pattern[1] > 0)
            {
                double capacity = Math.Ceiling(length / ((pattern[0] + pattern[1]) * pen.Width)) + 1;
                if (capacity <= 4096) rectangles.EnsureCapacity((int)capacity);
            }
            for (double at = 0; at < length;)
            {
                double take = Math.Min(cursor.Remaining, length - at);
                double begin = at;
                at += take;
                if (cursor.IsOn && take > 0 && !Add(origin + delta * begin / length, origin + delta * at / length))
                    return null;
                cursor.Consume(take);
            }
        }
        else if (!Add(origin, finish))
            return null;

        return new Geometry(SPath.FromAxisRectangles(rectangles));
    }

    private struct DashCursor
    {
        private readonly double[] pattern;
        private int index;
        internal double Remaining { get; private set; }
        internal bool IsOn => (index & 1) == 0;

        internal DashCursor(IDashStyle dash, double width)
        {
            pattern = dash.Dashes!.Select(length => length * width).ToArray();
            if (pattern.Any(length => !double.IsFinite(length) || length < 0) || !double.IsFinite(dash.Offset))
                throw new ArgumentException("Dash lengths must be nonnegative and finite, and the offset must be finite.");
            if ((pattern.Length & 1) != 0) pattern = pattern.Concat(pattern).ToArray();
            double total = pattern.Sum();
            if (total <= 0 || !double.IsFinite(total) || !double.IsFinite(dash.Offset * width))
                throw new ArgumentException("The dash pattern must have a positive finite period.");

            double phase = ((dash.Offset * width) % total + total) % total;
            index = 0;
            while (phase > 0 && phase >= pattern[index])
            {
                phase -= pattern[index];
                index = (index + 1) % pattern.Length;
            }
            Remaining = pattern[index] - phase;
        }

        internal bool Consume(double length)
        {
            Remaining -= length;
            if (Remaining < 1e-10)
            {
                index = (index + 1) % pattern.Length;
                Remaining = pattern[index];
                return true;
            }
            return false;
        }
    }

    private static PathsD Dash(PathD path,IDashStyle dash,double width,bool closed,PenLineCap cap)
    {
        var cursor = new DashCursor(dash, width);
        var result=new PathsD();PathD? piece=null;
        for(int j=1;j<path.Count;j++)
        {
            var a=path[j-1];var b=path[j];double dx=b.x-a.x,dy=b.y-a.y,length=Math.Sqrt(dx*dx+dy*dy),at=0;
            while(at<length)
            {
                double take=Math.Min(cursor.Remaining,length-at);var start=new PointD(a.x+dx*at/length,a.y+dy*at/length);at+=take;var stop=new PointD(a.x+dx*at/length,a.y+dy*at/length);
                if(cursor.IsOn&&(take>0||cap!=PenLineCap.Flat)){if(piece is null){piece=new PathD{start};result.Add(piece);}piece.Add(stop);}
                if(cursor.Consume(take))piece=null;
            }
        }
        if(cursor.Remaining==0&&cursor.IsOn&&cap!=PenLineCap.Flat)result.Add(new PathD{path[^1]});
        if(closed&&result.Count>1&&result[0][0]==result[^1][^1]){result[^1].AddRange(result[0].Skip(1));result.RemoveAt(0);}
        return result;
    }
    private static PathD ToPathD(PointF[] contour)
    {
        var path=new PathD(contour.Length);
        foreach(var point in contour)path.Add(new PointD(point.X,point.Y));
        return path;
    }
    internal static PathsD Polygons(SPath path)
    {
        var paths=new PathsD();
        foreach(var contour in path.Flatten(Matrix3.Identity,0.015625f))
            if(contour.Length>=3)paths.Add(ToPathD(contour));
        return paths;
    }
    internal static SPath FromPaths(PathsD paths)
    {
        int count = 0;
        foreach (var p in paths) if (p.Count > 0) count = checked(count + p.Count + 1);
        var segments = new PathSegment[count];
        int index = 0;
        foreach (var p in paths)
        {
            if (p.Count == 0) continue;
            for (int i = 0; i < p.Count; i++)
                segments[index++] = new(i == 0 ? PathVerb.Move : PathVerb.Line, new((float)p[i].x, (float)p[i].y));
            segments[index++] = new(PathVerb.Close, default);
        }
        return SPath.FromOwnedSegments(segments);
    }
    internal static SPath Combine(SPath a,SPath b,GeometryCombineMode mode)
    {
        PathsD Normalize(SPath p)=>Clipper.Union(Polygons(p),p.FillRule==SimdSkia.FillRule.EvenOdd?CFill.EvenOdd:CFill.NonZero);
        return FromPaths(Clipper.BooleanOp(mode switch {GeometryCombineMode.Union=>ClipType.Union,GeometryCombineMode.Intersect=>ClipType.Intersection,GeometryCombineMode.Xor=>ClipType.Xor,GeometryCombineMode.Exclude=>ClipType.Difference,_=>throw new NotSupportedException("Unknown geometry operation.")},Normalize(a),Normalize(b),CFill.NonZero,5));
    }
    public bool FillContains(Point point)
    {
        Validate();
        var contours=GetHitContours(out var rule);int winding=0;bool odd=false;
        foreach(var contour in contours)
        {
            if(contour.Length<3)continue;
            var previous=contour[^1];
            foreach(var current in contour)
            {
                double ax=previous.X,ay=previous.Y,bx=current.X,by=current.Y;
                double cross=(bx-ax)*(point.Y-ay)-(point.X-ax)*(by-ay);
                if(Math.Abs(cross)<1e-8&&point.X>=Math.Min(ax,bx)&&point.X<=Math.Max(ax,bx)&&point.Y>=Math.Min(ay,by)&&point.Y<=Math.Max(ay,by))return true;
                if(ay<=point.Y&&by>point.Y&&cross>0){winding++;odd=!odd;}
                else if(ay>point.Y&&by<=point.Y&&cross<0){winding--;odd=!odd;}
                previous=current;
            }
        }
        return rule==SimdSkia.FillRule.EvenOdd?odd:winding!=0;
    }
    private PointF[][] GetHitContours(out SimdSkia.FillRule rule)
    {
        lock(strokeGate)
        {
            rule=Path.FillRule;
            if(hitContours is not null&&hitContours.TryGetTarget(out var cached))return cached;
            // Nearby pointer events share the identical subdivision. The UI can
            // keep the geometry alive without permanently retaining its contour arrays.
            var contours=Path.Flatten(Matrix3.Identity,0.015625f);
            if(hitContours is null)hitContours=new(contours);
            else hitContours.SetTarget(contours);
            return contours;
        }
    }
    public IGeometryImpl? Intersect(IGeometryImpl geometry)=>new Geometry(Combine(Path,((Geometry)geometry).Path,GeometryCombineMode.Intersect));
    public bool StrokeContains(IPen? pen,Point point)=>pen is not null&&GetWidenedGeometry(pen).FillContains(point);
    public ITransformedGeometryImpl WithTransform(Matrix transform)=>new Transformed(this,transform);
    public bool TryGetPointAtDistance(double distance,out Point point)=>TryGetPointAndTangentAtDistance(distance,out point,out _);
    public bool TryGetPointAndTangentAtDistance(double distance,out Point point,out Point tangent)
    {
        point=tangent=default;if(!double.IsFinite(distance)||distance<0)return false;
        foreach(var e in Edges()){if(e.Length==0)continue;if(distance<=e.Length){double t=distance/e.Length;point=new(e.A.X+(e.B.X-e.A.X)*t,e.A.Y+(e.B.Y-e.A.Y)*t);tangent=new((e.B.X-e.A.X)/e.Length,(e.B.Y-e.A.Y)/e.Length);return true;}distance-=e.Length;}return false;
    }
    public bool TryGetSegment(double start,double stop,bool startOnBeginFigure,[NotNullWhen(true)] out IGeometryImpl? segmentGeometry)
    {
        segmentGeometry=null;if(!double.IsFinite(start)||!double.IsFinite(stop)||start<0||stop<=start)return false;
        var segments=new List<PathSegment>();double at=0;PointF? previous=null;
        foreach(var e in Edges())
        {
            double end=at+e.Length;if(end>start&&at<stop&&e.Length>0)
            {
                PointF Lerp(double value)=>e.A+(e.B-e.A)*(float)((value-at)/e.Length);
                var a=Lerp(Math.Max(start,at));var b=Lerp(Math.Min(stop,end));
                if(previous!=a)segments.Add(new(startOnBeginFigure?PathVerb.Move:PathVerb.Line,a));
                segments.Add(new(PathVerb.Line,b));previous=b;
            }
            at=end;if(at>=stop)break;
        }
        if(segments.Count==0)return false;segmentGeometry=new Geometry(new SPath(segments));return true;
    }
    private sealed class Transformed:Geometry,ITransformedGeometryImpl
    {
        public IGeometryImpl SourceGeometry {get;}
        public Matrix Transform {get;}
        internal Transformed(Geometry source,Matrix transform):base(source.Path.Transform(ConvertTo.Matrix(transform)),
            ReferenceEquals(source.Path,source.StrokePath)?null:source.StrokePath.Transform(ConvertTo.Matrix(transform)))
        {SourceGeometry=source;Transform=transform;}
    }
    internal static Geometry Rectangle(Rect rect)=>new(SPath.Rectangle(ConvertTo.Rect(rect)));
    internal static Geometry Ellipse(Rect r)
    {
        const double k=.5522847498307936;double cx=r.Center.X,cy=r.Center.Y,rx=r.Width/2,ry=r.Height/2;
        var s=new StreamGeometry();using var c=s.Open();c.BeginFigure(new(cx+rx,cy),true);
        c.CubicBezierTo(new(cx+rx,cy+k*ry),new(cx+k*rx,cy+ry),new(cx,cy+ry));c.CubicBezierTo(new(cx-k*rx,cy+ry),new(cx-rx,cy+k*ry),new(cx-rx,cy));
        c.CubicBezierTo(new(cx-rx,cy-k*ry),new(cx-k*rx,cy-ry),new(cx,cy-ry));c.CubicBezierTo(new(cx+k*rx,cy-ry),new(cx+rx,cy-k*ry),new(cx+rx,cy));c.EndFigure(true);return s;
    }
    internal static Geometry Rounded(RoundedRect r)
    {
        var s=new StreamGeometry();var rect=r.Rect;var tl=r.RadiiTopLeft;var tr=r.RadiiTopRight;var br=r.RadiiBottomRight;var bl=r.RadiiBottomLeft;
        double scale=Math.Min(1,Math.Min(Math.Min(rect.Width/Math.Max(tl.X+tr.X,bl.X+br.X),rect.Height/Math.Max(tl.Y+bl.Y,tr.Y+br.Y)),1));
        if(!double.IsFinite(scale))scale=1;tl*=scale;tr*=scale;br*=scale;bl*=scale;
        using var c=s.Open();c.BeginFigure(new(rect.Left+tl.X,rect.Top),true);c.LineTo(new(rect.Right-tr.X,rect.Top));c.ArcTo(new(rect.Right,rect.Top+tr.Y),new(tr.X,tr.Y),0,false,SweepDirection.Clockwise);
        c.LineTo(new(rect.Right,rect.Bottom-br.Y));c.ArcTo(new(rect.Right-br.X,rect.Bottom),new(br.X,br.Y),0,false,SweepDirection.Clockwise);
        c.LineTo(new(rect.Left+bl.X,rect.Bottom));c.ArcTo(new(rect.Left,rect.Bottom-bl.Y),new(bl.X,bl.Y),0,false,SweepDirection.Clockwise);
        c.LineTo(new(rect.Left,rect.Top+tl.Y));c.ArcTo(new(rect.Left+tl.X,rect.Top),new(tl.X,tl.Y),0,false,SweepDirection.Clockwise);c.EndFigure(true);return s;
    }
}

internal sealed class StreamGeometry:Geometry,IStreamGeometryImpl
{
    private bool editing;
    private static readonly SPath empty = new([]);
    internal StreamGeometry():base(empty) { }
    public IStreamGeometryImpl Clone()=>new StreamGeometry{Path=Path,StrokePath=StrokePath};
    public IStreamGeometryContextImpl Open(){if(editing)throw new InvalidOperationException("Geometry is already open.");editing=true;return new Context(this);}
    internal override void Validate(){if(editing)throw new InvalidOperationException("Dispose the geometry context before using its geometry.");}
    private sealed class Context(StreamGeometry owner):IStreamGeometryContextImpl
    {
        private readonly List<PathSegment> stroke=[];
        // Both streams share the common prefix until a fill/stroke flag or a
        // closing edge requires different geometry.
        private List<PathSegment>? fill;
        private SimdSkia.FillRule rule;private bool disposed,figure,filled;private Point current,start;
        private void Check(){ObjectDisposedException.ThrowIf(disposed,this);}
        public void SetFillRule(AFill value){Check();rule=value==AFill.EvenOdd?SimdSkia.FillRule.EvenOdd:SimdSkia.FillRule.Winding;}
        public void BeginFigure(Point p, bool isFilled = true)
        {
            Check();
            // A move starts a separate contour and leaves any preceding figure open.
            var move = new PathSegment(PathVerb.Move, ConvertTo.Point(p));
            figure = true;
            filled = isFilled;
            current = start = p;
            if (!filled) fill ??= new(stroke);
            else fill?.Add(move);
            stroke.Add(move);
        }
        private void Add(PathSegment segment,Point end,bool isStroked)
        {
            Check();if(!figure)throw new InvalidOperationException("BeginFigure is required.");
            if (!isStroked) fill ??= new(stroke);
            if (filled) fill?.Add(segment);
            stroke.Add(isStroked?segment:new(PathVerb.Move,ConvertTo.Point(end)));current=end;
        }
        public void LineTo(Point p,bool isStroked=true)=>Add(new(PathVerb.Line,ConvertTo.Point(p)),p,isStroked);
        public void QuadraticBezierTo(Point a,Point b,bool isStroked=true)=>Add(new(PathVerb.Quadratic,ConvertTo.Point(a),ConvertTo.Point(b)),b,isStroked);
        public void CubicBezierTo(Point a,Point b,Point c,bool isStroked=true)=>Add(new(PathVerb.Cubic,ConvertTo.Point(a),ConvertTo.Point(b),ConvertTo.Point(c)),c,isStroked);
        public void ArcTo(Point p,Size size,double rotation,bool large,SweepDirection sweep,bool isStroked=true)
        {
            Check();if(!figure)throw new InvalidOperationException("BeginFigure is required.");
            if(!double.IsFinite(rotation)||!double.IsFinite(size.Width)||!double.IsFinite(size.Height)||!double.IsFinite(p.X)||!double.IsFinite(p.Y))throw new ArgumentException("Arc parameters must be finite.");
            double rx=Math.Abs(size.Width),ry=Math.Abs(size.Height);if(current==p)return;if(rx==0||ry==0){LineTo(p,isStroked);return;}
            double angle=rotation*Math.PI/180,cos=Math.Cos(angle),sin=Math.Sin(angle),dx=(current.X-p.X)/2,dy=(current.Y-p.Y)/2,x=cos*dx+sin*dy,y=-sin*dx+cos*dy;
            double factor=x*x/(rx*rx)+y*y/(ry*ry);if(factor>1){rx*=Math.Sqrt(factor);ry*=Math.Sqrt(factor);}
            double q=Math.Sqrt(Math.Max(0,(rx*rx*ry*ry-rx*rx*y*y-ry*ry*x*x)/(rx*rx*y*y+ry*ry*x*x)))*(large==(sweep==SweepDirection.Clockwise)?-1:1);
            double cx=q*rx*y/ry,cy=-q*ry*x/rx,centerX=cos*cx-sin*cy+(current.X+p.X)/2,centerY=sin*cx+cos*cy+(current.Y+p.Y)/2;
            double t0=Math.Atan2((y-cy)/ry,(x-cx)/rx),t1=Math.Atan2((-y-cy)/ry,(-x-cx)/rx),delta=t1-t0;
            if(sweep==SweepDirection.Clockwise&&delta<0)delta+=2*Math.PI;if(sweep==SweepDirection.CounterClockwise&&delta>0)delta-=2*Math.PI;
            int count=(int)Math.Ceiling(Math.Abs(delta)/(Math.PI/2));
            Point Map(double a,double b)=>new(centerX+cos*rx*a-sin*ry*b,centerY+sin*rx*a+cos*ry*b);
            for(int i=0;i<count;i++){double a=t0+i*delta/count,b=t0+(i+1)*delta/count,k=4.0/3*Math.Tan((b-a)/4);CubicBezierTo(Map(Math.Cos(a)-k*Math.Sin(a),Math.Sin(a)+k*Math.Cos(a)),Map(Math.Cos(b)+k*Math.Sin(b),Math.Sin(b)-k*Math.Cos(b)),i==count-1?p:Map(Math.Cos(b),Math.Sin(b)),isStroked);}
        }
        public void EndFigure(bool closed)
        {
            Check();if(!figure)throw new InvalidOperationException("No open figure.");
            if(closed)
            {
                fill ??= new(stroke);
                if(filled)fill.Add(new(PathVerb.Close,default));
                stroke.Add(new(PathVerb.Line,ConvertTo.Point(start)));
            }
            figure=false;
        }
        public void Dispose()
        {
            if(disposed)return;if(figure)EndFigure(false);
            var strokePath=new SPath(stroke,rule);
            owner.Replace(fill is null ? strokePath : new(fill,rule),strokePath);
            owner.editing=false;disposed=true;
        }
    }
}
