namespace SimdSkia;

public static class StrokeGeometry
{
    public static PathGeometry Create(PointF[][] contours,float width,float miterLimit)
    {
        if(!float.IsFinite(width)||width<=0||!float.IsFinite(miterLimit)||miterLimit<1)throw new ArgumentOutOfRangeException(nameof(width));
        var output=new List<PathSegment>();float half=width*0.5f;
        void Add(List<PointF> points)
        {
            if(points.Count==0)return;
            output.Add(new(PathVerb.Move,points[0]));foreach(var p in points.Skip(1))output.Add(new(PathVerb.Line,p));output.Add(new(PathVerb.Close,default));
        }
        foreach(var source in contours)
        {
            var points=source.Where((p,i)=>i==0||p!=source[i-1]).ToList();bool closed=points.Count>2&&points[0]==points[^1];
            if(closed)points.RemoveAt(points.Count-1);if(points.Count<2)continue;
            var left=new List<PointF>();var right=new List<PointF>();
            for(int i=0;i<points.Count;i++)
            {
                PointF Normal(int a,int b){var d=points[b]-points[a];float length=MathF.Sqrt(d.X*d.X+d.Y*d.Y);return new(d.Y/length,-d.X/length);}
                var previous=Normal((i-1+points.Count)%points.Count,i);
                var next=Normal(i,(i+1)%points.Count);
                if(!closed&&i==0)previous=next;if(!closed&&i==points.Count-1)next=previous;
                var sum=previous+next;float divisor=sum.X*next.X+sum.Y*next.Y;
                if(MathF.Abs(divisor)>1e-6f&&MathF.Sqrt(sum.X*sum.X+sum.Y*sum.Y)/MathF.Abs(divisor)<=miterLimit)
                {var offset=sum*(half/divisor);left.Add(points[i]+offset);right.Add(points[i]-offset);}
                else
                {left.Add(points[i]+previous*half);left.Add(points[i]+next*half);right.Add(points[i]-previous*half);right.Add(points[i]-next*half);}
            }
            right.Reverse();if(closed){Add(left);Add(right);}else{left.AddRange(right);Add(left);}
        }
        return new(output);
    }
}
