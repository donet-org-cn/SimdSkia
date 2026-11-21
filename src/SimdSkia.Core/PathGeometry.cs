namespace SimdSkia;

public readonly record struct PointF(float X,float Y)
{
    public static PointF operator +(PointF a,PointF b)=>new(a.X+b.X,a.Y+b.Y);
    public static PointF operator -(PointF a,PointF b)=>new(a.X-b.X,a.Y-b.Y);
    public static PointF operator *(PointF a,float b)=>new(a.X*b,a.Y*b);
}
public enum PathVerb { Move,Line,Quadratic,Cubic,Close,Conic }
public enum FillRule { Winding,EvenOdd,InverseWinding,InverseEvenOdd }
public readonly record struct PathSegment(PathVerb Verb,PointF A,PointF B=default,PointF C=default)
{
    // Conics use A=control, B=end and the otherwise unused C payload for weight.
    // Keep the existing struct layout/constructor and avoid enlarging every glyph segment.
    public float ConicWeight=>C.X;
    public static PathSegment Conic(PointF control,PointF end,float weight)=>new(PathVerb.Conic,control,end,new(weight,0));
}
public sealed class PathGeometry
{
    private PathSegment[]? storedSegments;
    private readonly RectF[]? rectangleContours;
    private PathSegment[] SegmentsArray => Volatile.Read(ref storedSegments) ?? ExpandRectangles();
    internal ReadOnlySpan<RectF> RectangleContours => rectangleContours;
    internal int SegmentCount => storedSegments?.Length ?? checked(rectangleContours!.Length * 5);
    public FillRule FillRule { get; }
    public bool HasConics{get;}
    private sealed record BoundsBox(RectF Value);
    private BoundsBox? bounds;
    public RectF ControlBounds
    {
        get
        {
            var cached=Volatile.Read(ref bounds);if(cached is not null)return cached.Value;
            var computed=new BoundsBox(ComputeControlBounds());return (Interlocked.CompareExchange(ref bounds,computed,null)??computed).Value;
        }
    }
    public ReadOnlySpan<PathSegment> Segments=>SegmentsArray;
    public PathGeometry Transform(Matrix3 matrix)
    {
        if(!matrix.IsFinite||!matrix.IsAffine)throw new NotSupportedException("Path transforms require a finite affine matrix.");
        if(matrix==Matrix3.Identity)return this;
        var segments=Segments;var transformed=new PathSegment[segments.Length];
        for(int i=0;i<segments.Length;i++)transformed[i]=MapSegment(segments[i],matrix);
        return new PathGeometry(transformed,FillRule);
    }
    public void AppendTransformedTo(List<PathSegment> destination,Matrix3 matrix)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if(!matrix.IsFinite||!matrix.IsAffine)throw new NotSupportedException("Path transforms require a finite affine matrix.");
        foreach(var segment in Segments)destination.Add(MapSegment(segment,matrix));
    }
    private static PathSegment MapSegment(PathSegment segment,Matrix3 matrix)
    {
        PointF Map(PointF p){var v=matrix.Map(p.X,p.Y);return new(v.X,v.Y);}
        return new(segment.Verb,Map(segment.A),Map(segment.B),segment.Verb==PathVerb.Conic?segment.C:Map(segment.C));
    }
    public RectF GetTightBounds(Matrix3 matrix)
    {
        if(!matrix.IsFinite||!matrix.IsAffine)throw new NotSupportedException("Bounds require a finite affine transform.");
        if (rectangleContours is not null && matrix == Matrix3.Identity) return ControlBounds;
        double left=double.PositiveInfinity,top=double.PositiveInfinity,right=double.NegativeInfinity,bottom=double.NegativeInfinity;
        PointF Map(PointF p){var v=matrix.Map(p.X,p.Y);return new(v.X,v.Y);}
        void Include(double x,double y){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
        static double Quad(double a,double b,double c,double t){double u=1-t;return u*u*a+2*u*t*b+t*t*c;}
        static double Cubic(double a,double b,double c,double d,double t){double u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        var current=Map(default);var start=current;
        foreach(var s in Segments)
        {
            if(s.Verb==PathVerb.Close){current=start;continue;}
            var a=current;var b=Map(s.A);
            if(s.Verb==PathVerb.Move){current=start=b;Include(b.X,b.Y);continue;}
            if(s.Verb==PathVerb.Line){Include(a.X,a.Y);Include(b.X,b.Y);current=b;continue;}
            var c=Map(s.B);
            if(s.Verb==PathVerb.Conic)
            {
                double w=s.ConicWeight;Include(a.X,a.Y);Include(c.X,c.Y);
                double Eval(double v0,double v1,double v2,double t){double u=1-t;return (u*u*v0+2*w*u*t*v1+t*t*v2)/(u*u+2*w*u*t+t*t);}
                void Extrema(double v0,double v1,double v2)
                {
                    double n0=v0,n1=2*(w*v1-v0),n2=v0-2*w*v1+v2,d1=2*(w-1),d2=2*(1-w);
                    double aa=n2*d1-n1*d2,bb=2*(n2-n0*d2),cc=n1-n0*d1;
                    void Root(double t){if(t>0&&t<1)Include(Eval(a.X,b.X,c.X,t),Eval(a.Y,b.Y,c.Y,t));}
                    if(aa==0){if(bb!=0)Root(-cc/bb);return;}double disc=bb*bb-4*aa*cc;if(disc<0)return;
                    double q=-.5*(bb+Math.CopySign(Math.Sqrt(disc),bb));if(q==0)Root(-bb/(2*aa));else{Root(q/aa);Root(cc/q);}
                }
                Extrema(a.X,b.X,c.X);Extrema(a.Y,b.Y,c.Y);current=c;continue;
            }
            if(s.Verb==PathVerb.Quadratic)
            {
                Include(a.X,a.Y);Include(c.X,c.Y);
                void Extrema(double v0,double v1,double v2){double denominator=v0-2*v1+v2;if(denominator==0)return;double t=(v0-v1)/denominator;if(t>0&&t<1)Include(Quad(a.X,b.X,c.X,t),Quad(a.Y,b.Y,c.Y,t));}
                Extrema(a.X,b.X,c.X);Extrema(a.Y,b.Y,c.Y);current=c;continue;
            }
            if(s.Verb!=PathVerb.Cubic)throw new NotSupportedException("Unknown path verb.");
            var d=Map(s.C);Include(a.X,a.Y);Include(d.X,d.Y);
            void Roots(double v0,double v1,double v2,double v3)
            {
                double aa=-v0+3*v1-3*v2+v3,bb=2*(v0-2*v1+v2),cc=v1-v0;
                void IncludeRoot(double t){if(t>0&&t<1)Include(Cubic(a.X,b.X,c.X,d.X,t),Cubic(a.Y,b.Y,c.Y,d.Y,t));}
                if(aa==0){if(bb!=0)IncludeRoot(-cc/bb);return;}
                double discriminant=bb*bb-4*aa*cc;if(discriminant<0)return;
                double q=-.5*(bb+Math.CopySign(Math.Sqrt(discriminant),bb));
                if(q==0)IncludeRoot(-bb/(2*aa));else{IncludeRoot(q/aa);IncludeRoot(cc/q);}
            }
            Roots(a.X,b.X,c.X,d.X);Roots(a.Y,b.Y,c.Y,d.Y);current=d;
        }
        return double.IsPositiveInfinity(left)?default:new((float)left,(float)top,(float)right,(float)bottom);
    }
    public PathGeometry(IEnumerable<PathSegment> segments,FillRule fillRule=FillRule.Winding)
    {
        if(!Enum.IsDefined(fillRule))throw new ArgumentOutOfRangeException(nameof(fillRule));
        storedSegments=segments.ToArray();
        FillRule=fillRule;
        HasConics=ValidateSegments(storedSegments);
    }
    // Internal producers hand over a private array. Public callers retain ownership of their input.
    private PathGeometry(PathSegment[] owned,FillRule fillRule)
    {storedSegments=owned;FillRule=fillRule;HasConics=ValidateSegments(owned);}
    internal static PathGeometry FromOwnedSegments(PathSegment[] segments) => new(segments, FillRule.Winding);
    internal static PathGeometry FromAxisRectangles(List<RectF> rectangles)
        => rectangles.Count switch { 0 => FromOwnedSegments([]), 1 => Rectangle(rectangles[0]), _ => new(rectangles.ToArray()) };
    private PathGeometry(RectF[] rectangles)
    {
        // The axis stroker emits ordered, disjoint rectangles along one line.
        rectangleContours = rectangles;
        var first = rectangles[0];
        var last = rectangles[^1];
        bounds = new(new(Math.Min(first.Left,last.Left), Math.Min(first.Top,last.Top),
            Math.Max(first.Right,last.Right), Math.Max(first.Bottom,last.Bottom)));
        FillRule = FillRule.Winding;
    }
    private PathSegment[] ExpandRectangles()
    {
        var result = new PathSegment[SegmentCount];
        int index = 0;
        foreach (var box in rectangleContours!)
        {
            result[index++] = new(PathVerb.Move, new(box.Left, box.Top));
            result[index++] = new(PathVerb.Line, new(box.Right, box.Top));
            result[index++] = new(PathVerb.Line, new(box.Right, box.Bottom));
            result[index++] = new(PathVerb.Line, new(box.Left, box.Bottom));
            result[index++] = new(PathVerb.Close, default);
        }
        return Interlocked.CompareExchange(ref storedSegments, result, null) ?? result;
    }
    private static bool ValidateSegments(PathSegment[] segments)
    {
        bool hasConics=false;
        foreach(var s in segments)
        {
            if(s.Verb==PathVerb.Conic)hasConics=true;
            if(!Finite(s.A)||!Finite(s.B)||!Finite(s.C)||s.Verb==PathVerb.Conic&&s.ConicWeight<=0) throw new ArgumentException("Path coordinates and positive conic weights must be finite.");
        }
        return hasConics;
    }
    private RectF ComputeControlBounds()
    {
        var segments = Segments;
        float l=float.PositiveInfinity,t=float.PositiveInfinity,r=float.NegativeInfinity,b=float.NegativeInfinity;
        void Include(PointF p){l=MathF.Min(l,p.X);t=MathF.Min(t,p.Y);r=MathF.Max(r,p.X);b=MathF.Max(b,p.Y);}
        if(segments.Length>0&&segments[0].Verb!=PathVerb.Move)Include(default);
        foreach(var s in segments)
        {if(s.Verb==PathVerb.Close)continue;Include(s.A);if(s.Verb is PathVerb.Quadratic or PathVerb.Cubic or PathVerb.Conic)Include(s.B);if(s.Verb==PathVerb.Cubic)Include(s.C);}
        return float.IsPositiveInfinity(l)?default:new(l,t,r,b);
    }
    private static bool Finite(PointF p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y);
    public PointF[][] Flatten(Matrix3 matrix,float tolerance=0.125f)
    {
        using var flattened = FlattenForRasterization(matrix, tolerance);
        var result = new PointF[flattened.ContourCount][];
        for (int i = 0; i < result.Length; i++) result[i] = flattened.Contour(i).ToArray();
        return result;
    }

    internal FlattenedPath FlattenForRasterization(Matrix3 matrix,float tolerance)
    {
        if(!float.IsFinite(tolerance)||tolerance<=0)throw new ArgumentOutOfRangeException(nameof(tolerance));
        if(!matrix.IsFinite||!matrix.IsAffine) throw new NotSupportedException("Path rasterization requires a finite affine transform.");
        PointF Map(PointF p) { var v=matrix.Map(p.X,p.Y); var mapped=new PointF(v.X,v.Y); if(!Finite(mapped))throw new ArgumentException("Transformed path exceeds finite range."); return mapped; }
        var points = new FlattenedPath();
        try
        {
            if (rectangleContours is not null)
            {
                foreach (var r in rectangleContours)
                {
                    var a = Map(new(r.Left,r.Top)); var b = Map(new(r.Right,r.Top));
                    var c = Map(new(r.Right,r.Bottom)); var d = Map(new(r.Left,r.Bottom));
                    points.Add(a); points.Add(b); points.Add(c); points.Add(d);
                    if (d != a) points.Add(a);
                    points.FinishContour();
                }
                return points;
            }

            PointF current=default,start=default;
            foreach(var s in Segments)
            {
                switch(s.Verb)
                {
                    case PathVerb.Move: points.FinishContour(); current=start=Map(s.A); points.Add(current); break;
                    case PathVerb.Line: if(points.CurrentCount==0)points.Add(current); current=Map(s.A); points.Add(current); break;
                    case PathVerb.Quadratic:
                        if(points.CurrentCount==0)points.Add(current); var control=Map(s.A); var end=Map(s.B);
                        Quad(current,control,end,points,tolerance,0); current=end; break;
                    case PathVerb.Cubic:
                        if(points.CurrentCount==0)points.Add(current); var b=Map(s.A);var c=Map(s.B);var d=Map(s.C);
                        Cubic(current,b,c,d,points,tolerance,0);current=d;break;
                    case PathVerb.Conic:
                        if(points.CurrentCount==0)points.Add(current);var conicControl=Map(s.A);var conicEnd=Map(s.B);double w=s.ConicWeight;
                        RationalQuad((current.X,current.Y,1),(w*conicControl.X,w*conicControl.Y,w),(conicEnd.X,conicEnd.Y,1),points,Math.Min(tolerance,1f/64),0);
                        current=conicEnd;break;
                    case PathVerb.Close: if(points.CurrentCount>0&&points.Last!=start)points.Add(start); current=start; points.FinishContour(); break;
                    default: throw new NotSupportedException("Unknown path verb.");
                }
                if(points.CurrentCount>1_000_000)throw new InvalidOperationException("Path subdivision limit exceeded.");
            }
            points.FinishContour();
            return points;
        }
        catch { points.Dispose(); throw; }
    }
    private static void RationalQuad((double X,double Y,double W) a,(double X,double Y,double W) b,(double X,double Y,double W) c,FlattenedPath output,float tolerance,int depth)
    {
        static PointF Project((double X,double Y,double W) p)=>new((float)(p.X/p.W),(float)(p.Y/p.W));
        var start=Project(a);var control=Project(b);var end=Project(c);
        if(Distance(control,start,end)<=tolerance){output.Add(end);return;}
        if(depth>=24||output.CurrentCount>=1_000_000)throw new InvalidOperationException("Conic subdivision limit exceeded.");
        var ab=((a.X+b.X)*.5,(a.Y+b.Y)*.5,(a.W+b.W)*.5);var bc=((b.X+c.X)*.5,(b.Y+c.Y)*.5,(b.W+c.W)*.5);
        var middle=((ab.Item1+bc.Item1)*.5,(ab.Item2+bc.Item2)*.5,(ab.Item3+bc.Item3)*.5);
        RationalQuad(a,ab,middle,output,tolerance,depth+1);RationalQuad(middle,bc,c,output,tolerance,depth+1);
    }
    private static float Distance(PointF p,PointF a,PointF b)
    {
        float dx=b.X-a.X,dy=b.Y-a.Y,lengthSquared=dx*dx+dy*dy;
        float t=lengthSquared==0?0:Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/lengthSquared,0,1);
        float x=p.X-a.X-t*dx,y=p.Y-a.Y-t*dy;return MathF.Sqrt(x*x+y*y);
    }
    private static void Quad(PointF a,PointF b,PointF c,FlattenedPath output,float tolerance,int depth)
    {
        if(Distance(b,a,c)<=tolerance) { output.Add(c);return; }
        if(depth>=20)throw new InvalidOperationException("Quadratic subdivision limit exceeded.");
        var ab=(a+b)*0.5f;var bc=(b+c)*0.5f;var mid=(ab+bc)*0.5f;
        Quad(a,ab,mid,output,tolerance,depth+1);Quad(mid,bc,c,output,tolerance,depth+1);
    }
    private static void Cubic(PointF a,PointF b,PointF c,PointF d,FlattenedPath output,float tolerance,int depth)
    {
        if(MathF.Max(Distance(b,a,d),Distance(c,a,d))<=tolerance){output.Add(d);return;}
        if(depth>=20)throw new InvalidOperationException("Cubic subdivision limit exceeded.");
        var ab=(a+b)*0.5f;var bc=(b+c)*0.5f;var cd=(c+d)*0.5f;var abc=(ab+bc)*0.5f;var bcd=(bc+cd)*0.5f;var mid=(abc+bcd)*0.5f;
        Cubic(a,ab,abc,mid,output,tolerance,depth+1);Cubic(mid,bcd,cd,d,output,tolerance,depth+1);
    }
    public static PathGeometry Rectangle(RectF rect)=>new([
        new(PathVerb.Move,new(rect.Left,rect.Top)),new(PathVerb.Line,new(rect.Right,rect.Top)),
        new(PathVerb.Line,new(rect.Right,rect.Bottom)),new(PathVerb.Line,new(rect.Left,rect.Bottom)),new(PathVerb.Close,default)],FillRule.Winding);
}

public sealed record LinearGradient(PointF Start,PointF End,Rgba32 StartColor,Rgba32 EndColor):IColorShader
{
    public bool IsHorizontal=>Start.Y==End.Y;
    public bool IsOpaque=>StartColor.A==255&&EndColor.A==255;
    public Rgba32 Sample(float x,float y)
    {
        if(!float.IsFinite(Start.X)||!float.IsFinite(Start.Y)||!float.IsFinite(End.X)||!float.IsFinite(End.Y)||!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentException("Gradient coordinates must be finite.");
        double dx=(double)End.X-Start.X,dy=(double)End.Y-Start.Y,denominator=dx*dx+dy*dy;
        float t=denominator==0 ? 1 : (float)Math.Clamp((((double)x-Start.X)*dx+((double)y-Start.Y)*dy)/denominator,0,1);
        byte Mix(byte a,byte b)=>(byte)Math.Clamp((int)MathF.Round(a+(b-a)*t),0,255);
        return new(Mix(StartColor.R,EndColor.R),Mix(StartColor.G,EndColor.G),Mix(StartColor.B,EndColor.B),Mix(StartColor.A,EndColor.A));
    }
}
