using System.Buffers;

namespace SimdSkia;

public static partial class StrokeGeometry
{
    public static PathGeometry Create(PointF[][] contours,float width,float miterLimit)
    {
        if(!float.IsFinite(width)||width<=0||!float.IsFinite(miterLimit)||miterLimit<1)throw new ArgumentOutOfRangeException(nameof(width));
        var output=new List<PathSegment>();float half=width*0.5f;
        void Add(List<PointF> points)
        {
            if(points.Count==0)return;
            output.Add(new(PathVerb.Move,points[0]));for(int i=1;i<points.Count;i++)output.Add(new(PathVerb.Line,points[i]));output.Add(new(PathVerb.Close,default));
        }
        Span<PointF> smallNormals=stackalloc PointF[256];
        foreach(var source in contours)
        {
            var points=new List<PointF>(source.Length);foreach(var p in source)if(points.Count==0||p!=points[^1])points.Add(p);bool closed=points.Count>2&&points[0]==points[^1];
            if(closed)points.RemoveAt(points.Count-1);if(points.Count<2)continue;
            var left=new List<PointF>(points.Count);var right=new List<PointF>(points.Count);
            int edgeCount=closed?points.Count:points.Count-1;PointF[]? rented=null;
            Span<PointF> normals=edgeCount<=256?smallNormals[..edgeCount]:(rented=ArrayPool<PointF>.Shared.Rent(edgeCount));
            for(int i=0;i<edgeCount;i++)
            {var d=points[(i+1)%points.Count]-points[i];float length=MathF.Sqrt(d.X*d.X+d.Y*d.Y);normals[i]=new(d.Y/length,-d.X/length);}
            try
            {
            for(int i=0;i<points.Count;i++)
            {
                var previous=normals[!closed&&i==0?0:(i-1+edgeCount)%edgeCount];
                var next=normals[!closed&&i==points.Count-1?edgeCount-1:i];
                var sum=previous+next;float divisor=sum.X*next.X+sum.Y*next.Y;
                if(MathF.Abs(divisor)>1e-6f&&MathF.Sqrt(sum.X*sum.X+sum.Y*sum.Y)/MathF.Abs(divisor)<=miterLimit)
                {var offset=sum*(half/divisor);left.Add(points[i]+offset);right.Add(points[i]-offset);}
                else
                {left.Add(points[i]+previous*half);left.Add(points[i]+next*half);right.Add(points[i]-previous*half);right.Add(points[i]-next*half);}
            }
            right.Reverse();if(closed){Add(left);Add(right);}else{left.AddRange(right);Add(left);}
            }
            finally{if(rented is not null)ArrayPool<PointF>.Shared.Return(rented);}
        }
        return new(output);
    }
}
