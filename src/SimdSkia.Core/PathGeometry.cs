namespace SimdSkia;

public readonly record struct PointF(float X,float Y)
{
    public static PointF operator +(PointF a,PointF b)=>new(a.X+b.X,a.Y+b.Y);
    public static PointF operator -(PointF a,PointF b)=>new(a.X-b.X,a.Y-b.Y);
    public static PointF operator *(PointF a,float b)=>new(a.X*b,a.Y*b);
}
public enum PathVerb { Move,Line,Quadratic,Cubic,Close }
public enum FillRule { Winding,EvenOdd,InverseWinding,InverseEvenOdd }
public readonly record struct PathSegment(PathVerb Verb,PointF A,PointF B=default,PointF C=default);
public sealed class PathGeometry
{
    private readonly PathSegment[] segments;
    public FillRule FillRule { get; }
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
    public ReadOnlySpan<PathSegment> Segments=>segments;
    public PathGeometry Transform(Matrix3 matrix)
    {
        PointF Map(PointF p){var v=matrix.Map(p.X,p.Y);return new(v.X,v.Y);}
        return new(segments.Select(s=>new PathSegment(s.Verb,Map(s.A),Map(s.B),Map(s.C))),FillRule);
    }
    public void AppendTransformedTo(List<PathSegment> destination,Matrix3 matrix)
    {
        ArgumentNullException.ThrowIfNull(destination);
        PointF Map(PointF p){var v=matrix.Map(p.X,p.Y);return new(v.X,v.Y);}
        foreach(var s in segments)destination.Add(new(s.Verb,Map(s.A),Map(s.B),Map(s.C)));
    }
    public RectF GetTightBounds(Matrix3 matrix)
    {
        if(!matrix.IsFinite||!matrix.IsAffine)throw new NotSupportedException("Bounds require a finite affine transform.");
        double left=double.PositiveInfinity,top=double.PositiveInfinity,right=double.NegativeInfinity,bottom=double.NegativeInfinity;
        PointF Map(PointF p){var v=matrix.Map(p.X,p.Y);return new(v.X,v.Y);}
        void Include(double x,double y){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
        static double Quad(double a,double b,double c,double t){double u=1-t;return u*u*a+2*u*t*b+t*t*c;}
        static double Cubic(double a,double b,double c,double d,double t){double u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        var current=Map(default);var start=current;
        foreach(var s in segments)
        {
            if(s.Verb==PathVerb.Close){current=start;continue;}
            var a=current;var b=Map(s.A);
            if(s.Verb==PathVerb.Move){current=start=b;Include(b.X,b.Y);continue;}
            if(s.Verb==PathVerb.Line){Include(a.X,a.Y);Include(b.X,b.Y);current=b;continue;}
            var c=Map(s.B);
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
        if(!Enum.IsDefined(fillRule)) throw new ArgumentOutOfRangeException(nameof(fillRule));
        this.segments=segments.ToArray(); FillRule=fillRule;
        foreach(var s in this.segments)
            if(!Finite(s.A)||!Finite(s.B)||!Finite(s.C)) throw new ArgumentException("Path coordinates must be finite.");
    }
    private RectF ComputeControlBounds()
    {
        float l=float.PositiveInfinity,t=float.PositiveInfinity,r=float.NegativeInfinity,b=float.NegativeInfinity;
        void Include(PointF p){l=MathF.Min(l,p.X);t=MathF.Min(t,p.Y);r=MathF.Max(r,p.X);b=MathF.Max(b,p.Y);}
        if(segments.Length>0&&segments[0].Verb!=PathVerb.Move)Include(default);
        foreach(var s in this.segments)
        {if(s.Verb==PathVerb.Close)continue;Include(s.A);if(s.Verb is PathVerb.Quadratic or PathVerb.Cubic)Include(s.B);if(s.Verb==PathVerb.Cubic)Include(s.C);}
        return float.IsPositiveInfinity(l)?default:new(l,t,r,b);
    }
    private static bool Finite(PointF p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y);
    public PointF[][] Flatten(Matrix3 matrix,float tolerance=0.125f)
    {
        if(!matrix.IsFinite||!matrix.IsAffine) throw new NotSupportedException("Path rasterization requires a finite affine transform.");
        var contours=new List<PointF[]>(); var points=new List<PointF>(); PointF current=default,start=default;
        PointF Map(PointF p) { var v=matrix.Map(p.X,p.Y); var mapped=new PointF(v.X,v.Y); if(!Finite(mapped))throw new ArgumentException("Transformed path exceeds finite range."); return mapped; }
        void Finish() { if(points.Count>0) contours.Add(points.ToArray()); points.Clear(); }
        foreach(var s in segments)
        {
            switch(s.Verb)
            {
                case PathVerb.Move: Finish(); current=start=Map(s.A); points.Add(current); break;
                case PathVerb.Line: if(points.Count==0)points.Add(current); current=Map(s.A); points.Add(current); break;
                case PathVerb.Quadratic:
                    if(points.Count==0)points.Add(current); var control=Map(s.A); var end=Map(s.B);
                    Quad(current,control,end,points,tolerance,0); current=end; break;
                case PathVerb.Cubic:
                    if(points.Count==0)points.Add(current); var b=Map(s.A);var c=Map(s.B);var d=Map(s.C);
                    Cubic(current,b,c,d,points,tolerance,0);current=d;break;
                case PathVerb.Close: if(points.Count>0&&points[^1]!=start)points.Add(start); current=start; Finish(); break;
                default: throw new NotSupportedException("Unknown path verb.");
            }
            if(points.Count>1_000_000)throw new InvalidOperationException("Path subdivision limit exceeded.");
        }
        Finish(); return contours.ToArray();
    }
    private static float Distance(PointF p,PointF a,PointF b)
    {
        float dx=b.X-a.X,dy=b.Y-a.Y,lengthSquared=dx*dx+dy*dy;
        float t=lengthSquared==0?0:Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/lengthSquared,0,1);
        float x=p.X-a.X-t*dx,y=p.Y-a.Y-t*dy;return MathF.Sqrt(x*x+y*y);
    }
    private static void Quad(PointF a,PointF b,PointF c,List<PointF> output,float tolerance,int depth)
    {
        if(Distance(b,a,c)<=tolerance) { output.Add(c);return; }
        if(depth>=20)throw new InvalidOperationException("Quadratic subdivision limit exceeded.");
        var ab=(a+b)*0.5f;var bc=(b+c)*0.5f;var mid=(ab+bc)*0.5f;
        Quad(a,ab,mid,output,tolerance,depth+1);Quad(mid,bc,c,output,tolerance,depth+1);
    }
    private static void Cubic(PointF a,PointF b,PointF c,PointF d,List<PointF> output,float tolerance,int depth)
    {
        if(MathF.Max(Distance(b,a,d),Distance(c,a,d))<=tolerance){output.Add(d);return;}
        if(depth>=20)throw new InvalidOperationException("Cubic subdivision limit exceeded.");
        var ab=(a+b)*0.5f;var bc=(b+c)*0.5f;var cd=(c+d)*0.5f;var abc=(ab+bc)*0.5f;var bcd=(bc+cd)*0.5f;var mid=(abc+bcd)*0.5f;
        Cubic(a,ab,abc,mid,output,tolerance,depth+1);Cubic(mid,bcd,cd,d,output,tolerance,depth+1);
    }
    public static PathGeometry Rectangle(RectF rect)=>new([
        new(PathVerb.Move,new(rect.Left,rect.Top)),new(PathVerb.Line,new(rect.Right,rect.Top)),
        new(PathVerb.Line,new(rect.Right,rect.Bottom)),new(PathVerb.Line,new(rect.Left,rect.Bottom)),new(PathVerb.Close,default)]);
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
