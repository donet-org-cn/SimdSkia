namespace SimdSkia;

public enum StrokeCap { Butt,Round,Square }
public enum StrokeJoin { Miter,Round,Bevel }

public static partial class StrokeGeometry
{
    public static PathGeometry Create(PointF[][] contours,float width,float miterLimit,StrokeCap cap,StrokeJoin join)
    {
        if(!Enum.IsDefined(cap)||!Enum.IsDefined(join))throw new ArgumentOutOfRangeException(nameof(cap));
        if(!float.IsFinite(width)||width<=0||!float.IsFinite(miterLimit)||miterLimit<0)throw new ArgumentOutOfRangeException(nameof(width));
        if(cap==StrokeCap.Butt&&join==StrokeJoin.Miter&&miterLimit>=1)return Create(contours,width,miterLimit);
        var output=new List<PathSegment>();float half=width*.5f;
        void Polygon(ReadOnlySpan<PointF> points)
        {
            double area=0;for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];area+=(double)a.X*b.Y-(double)a.Y*b.X;}
            if(area==0)return;
            output.Add(new(PathVerb.Move,points[0]));
            for(int i=1;i<points.Length;i++)output.Add(new(PathVerb.Line,points[area>0?i:points.Length-i]));
            output.Add(new(PathVerb.Close,default));
        }
        void Disk(PointF p)
        {
            const float w=.7071067811865476f;
            output.Add(new(PathVerb.Move,new(p.X+half,p.Y)));
            output.Add(PathSegment.Conic(new(p.X+half,p.Y+half),new(p.X,p.Y+half),w));
            output.Add(PathSegment.Conic(new(p.X-half,p.Y+half),new(p.X-half,p.Y),w));
            output.Add(PathSegment.Conic(new(p.X-half,p.Y-half),new(p.X,p.Y-half),w));
            output.Add(PathSegment.Conic(new(p.X+half,p.Y-half),new(p.X+half,p.Y),w));
            output.Add(new(PathVerb.Close,default));
        }
        foreach(var source in contours)
        {
            var points=new List<PointF>(source.Length);
            foreach(var p in source)if(points.Count==0||points[^1]!=p)points.Add(p);
            bool closed=points.Count>2&&points[0]==points[^1];if(closed)points.RemoveAt(points.Count-1);
            int count=points.Count;
            if(count<2)
            {
                if(count==1&&source.Length>=2)
                {
                    var p=points[0];if(cap==StrokeCap.Round)Disk(p);
                    else if(cap==StrokeCap.Square)Polygon([new(p.X-half,p.Y-half),new(p.X+half,p.Y-half),new(p.X+half,p.Y+half),new(p.X-half,p.Y+half)]);
                }
                continue;
            }
            int edgeCount=closed?count:count-1;var directions=new PointF[edgeCount];
            for(int i=0;i<edgeCount;i++)
            {
                var a=points[i];var b=points[(i+1)%count];double dx=(double)b.X-a.X,dy=(double)b.Y-a.Y,length=Math.Sqrt(dx*dx+dy*dy);
                var direction=new PointF((float)(dx/length),(float)(dy/length));directions[i]=direction;
                var offset=new PointF(direction.Y*half,-direction.X*half);
                if(!closed&&cap==StrokeCap.Square){if(i==0)a-=direction*half;if(i==edgeCount-1)b+=direction*half;}
                Polygon([a+offset,b+offset,b-offset,a-offset]);
            }
            for(int i=closed?0:1;i<(closed?count:count-1);i++)
            {
                var p=points[i];var previous=directions[(i-1+edgeCount)%edgeCount];var next=directions[i%edgeCount];
                double cross=(double)previous.X*next.Y-(double)previous.Y*next.X,dot=Math.Clamp((double)previous.X*next.X+(double)previous.Y*next.Y,-1,1);
                if(join==StrokeJoin.Round){if(dot<.9999999)Disk(p);continue;}
                if(Math.Abs(cross)<1e-10)continue;
                float side=cross>0?1:-1;var n0=new PointF(previous.Y,-previous.X);var n1=new PointF(next.Y,-next.X);
                var a=p+n0*(side*half);var b=p+n1*(side*half);double denominator=1+dot;
                if(join==StrokeJoin.Miter&&denominator>0&&Math.Sqrt(2/denominator)<=miterLimit)
                {var m=p+(n0+n1)*(float)(side*half/denominator);Polygon([p,a,m,b]);}
                else Polygon([p,a,b]);
            }
            if(!closed&&cap==StrokeCap.Round){Disk(points[0]);Disk(points[^1]);}
            if(output.Count>1_000_000)throw new InvalidOperationException("Stroke geometry budget exceeded.");
        }
        // All primitives wind in the same direction. Rasterize their union once,
        // so translucent overlaps do not receive the source more than once.
        return new(output,FillRule.Winding);
    }
}
