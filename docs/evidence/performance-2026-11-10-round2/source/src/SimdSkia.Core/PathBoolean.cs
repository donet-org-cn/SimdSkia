using Clipper2Lib;
using System.Runtime.CompilerServices;
namespace SimdSkia;

/// <summary>Managed polygon Boolean geometry. Curves are adaptively flattened at
/// 1/1024 source units; Clipper2 then clips at six decimal places. Result paths
/// use line segments and preserve inverse-fill semantics, not Skia curve verbs.</summary>
public static class PathBoolean
{
    private sealed class Shape(PointF[][] contours)
    {
        internal readonly PointF[][] Contours=contours;
        internal PathsD? Normalized;
        internal bool? Convex;
    }
    private static readonly ConditionalWeakTable<PathGeometry,Shape> shapes=new();
    private static Shape Flatten(PathGeometry path)=>shapes.GetValue(path,static p=>
    {
        var contours=p.Flatten(Matrix3.Identity,1f/1024);long points=contours.Sum(c=>(long)c.Length);
        if(points>1_000_000)throw new NotSupportedException("Path queries/Boolean operations are limited to one million flattened points.");
        foreach(var contour in contours)foreach(var point in contour)if(MathF.Abs(point.X)>1_000_000_000||MathF.Abs(point.Y)>1_000_000_000)throw new NotSupportedException("Boolean path coordinates exceed +/-1e9 source units.");
        return new(contours);
    });
    private static PathsD Normalize(PathGeometry path)
    {
        var shape=Flatten(path);lock(shape)
        {
            if(shape.Normalized is {} cached)return cached;
            var paths=new PathsD(shape.Contours.Length);foreach(var contour in shape.Contours)
            {if(contour.Length<3)continue;var polygon=new PathD(contour.Length);foreach(var point in contour)polygon.Add(new(point.X,point.Y));paths.Add(polygon);}
            var normalized=Clipper.BooleanOp(ClipType.Union,paths,[],(path.FillRule is FillRule.EvenOdd or FillRule.InverseEvenOdd)?Clipper2Lib.FillRule.EvenOdd:Clipper2Lib.FillRule.NonZero,6);
            // Huge results are returned to the caller without extending the
            // lifetime of a second polygon representation through a weak cache.
            if(shape.Contours.Sum(c=>c.Length)<=8192)shape.Normalized=normalized;
            return normalized;
        }
    }
    public static PathGeometry Simplify(PathGeometry path)=>FromPaths(Normalize(path),path.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd);
    public static PathGeometry Combine(PathGeometry first,PathGeometry second,int operation)
    {
        ArgumentNullException.ThrowIfNull(first);ArgumentNullException.ThrowIfNull(second);if((uint)operation>4)throw new ArgumentOutOfRangeException(nameof(operation));
        bool ia=(int)first.FillRule>=2,ib=(int)second.FillRule>=2,inverse=false;var a=Normalize(first);var b=Normalize(second);ClipType kind;
        // Difference is intersection with the complement; reverse difference
        // swaps operands. De Morgan identities avoid any finite "infinity box".
        if(operation==4){(a,b)=(b,a);(ia,ib)=(ib,ia);operation=0;}
        if(operation==0){ib=!ib;operation=1;}
        if(operation==3){kind=ClipType.Xor;inverse=ia!=ib;}
        else if(operation==1)
        {
            if(!ia&&!ib)kind=ClipType.Intersection;
            else if(ia&&ib){kind=ClipType.Union;inverse=true;}
            else{kind=ClipType.Difference;if(ia)(a,b)=(b,a);}
        }
        else
        {
            if(!ia&&!ib)kind=ClipType.Union;
            else if(ia&&ib){kind=ClipType.Intersection;inverse=true;}
            else{kind=ClipType.Difference;inverse=true;if(ib)(a,b)=(b,a);}
        }
        return FromPaths(Clipper.BooleanOp(kind,a,b,Clipper2Lib.FillRule.NonZero,6),inverse);
    }
    private static PathGeometry FromPaths(PathsD paths,bool inverse)
    {
        var segments=new List<PathSegment>();foreach(var path in paths)
        {if(path.Count<3)continue;segments.Add(new(PathVerb.Move,new((float)path[0].x,(float)path[0].y)));for(int i=1;i<path.Count;i++)segments.Add(new(PathVerb.Line,new((float)path[i].x,(float)path[i].y)));segments.Add(new(PathVerb.Close,default));}
        return new(segments,inverse?FillRule.InverseWinding:FillRule.Winding);
    }
    public static bool Contains(PathGeometry path,float x,float y)
    {
        if(!float.IsFinite(x)||!float.IsFinite(y))return false;int winding=0;bool boundary=false;
        foreach(var contour in Flatten(path).Contours)
        {
            for(int i=0;i<contour.Length;i++)
            {
                var a=contour[i];var b=contour[(i+1)%contour.Length];double cross=((double)b.X-a.X)*(y-a.Y)-((double)b.Y-a.Y)*(x-a.X);
                if(cross==0&&x>=Math.Min(a.X,b.X)&&x<=Math.Max(a.X,b.X)&&y>=Math.Min(a.Y,b.Y)&&y<=Math.Max(a.Y,b.Y)&&a!=b)boundary=true;
                if(a.Y<=y&&b.Y>y&&cross>0)winding++;
                else if(a.Y>y&&b.Y<=y&&cross<0)winding--;
            }
        }
        bool filled=boundary||((int)path.FillRule&1)==0&&winding!=0||((int)path.FillRule&1)!=0&&(winding&1)!=0;
        return (int)path.FillRule>=2?!filled:filled;
    }
    public static bool IsConvex(PathGeometry path)
    {
        var shape=Flatten(path);lock(shape)
        {
            if(shape.Convex is {} known)return known;
            var contours=shape.Contours.Where(c=>c.Length>1).ToArray();if(contours.Length==0)return (shape.Convex=true).Value;if(contours.Length>1)return (shape.Convex=false).Value;
            var points=contours[0];int count=points.Length;if(points[0]==points[^1])count--;if(count<3)return (shape.Convex=true).Value;
            int sign=0;double turning=0;
            for(int i=0;i<count;i++)
            {
                var p=points[(i+count-1)%count];var q=points[i];var r=points[(i+1)%count];double ax=(double)q.X-p.X,ay=(double)q.Y-p.Y,bx=(double)r.X-q.X,by=(double)r.Y-q.Y,cross=ax*by-ay*bx;
                if(cross==0)continue;int s=Math.Sign(cross);if(sign!=0&&s!=sign)return (shape.Convex=false).Value;sign=s;turning+=Math.Atan2(cross,ax*bx+ay*by);
            }
            return (shape.Convex=sign==0||Math.Abs(Math.Abs(turning)-2*Math.PI)<1e-5).Value;
        }
    }
}
