// Fixed line setup/rounding derived from SkEdge, Google BSD-3. See licenses/Skia-BSD.txt.
namespace SimdSkia.Cpu;
internal static class TriangleCoverage
{
    private readonly record struct Edge(int Top,int Bottom,long X,long Slope);
    internal static CoverageMask Rasterize(PointF[][] contours,int width,int height,RectF? clip)
    {
        if(contours.Length!=1)throw new ArgumentException("Mesh coverage requires a single triangle.");
        var points=contours[0];int count=points.Length;if(count>1&&points[0]==points[^1])count--;if(count!=3)throw new ArgumentException("Mesh coverage requires three vertices.");
        Span<Edge> edges=stackalloc Edge[3];int used=0,top=height,bottom=0;
        for(int i=0;i<3;i++)
        {
            var a=points[i];var b=points[(i+1)%3];
            if(MathF.Abs(a.X)>32760||MathF.Abs(a.Y)>32760||MathF.Abs(b.X)>32760||MathF.Abs(b.Y)>32760)throw new NotSupportedException("Fixed mesh coverage supports transformed coordinates within +/-32760; split or pre-clip larger geometry.");
            // The baseline build truncates to signed FDot6 before setting up the
            // 16.16 slope, then quantizes the first X back through FDot6.
            int x0=(int)(a.X*64),y0=(int)(a.Y*64),x1=(int)(b.X*64),y1=(int)(b.Y*64);
            if(y0>y1){(x0,x1)=(x1,x0);(y0,y1)=(y1,y0);}
            int first=(y0+32)>>6,last=(y1+32)>>6;if(first==last)continue;
            long slope=((long)(x1-x0)<<16)/(y1-y0);slope=Math.Clamp(slope,int.MinValue,int.MaxValue);
            int dy=(first<<6)+32-y0;long x=((long)x0+((slope*dy)>>16))<<10;
            edges[used++]=new(first,last,x,slope);top=Math.Min(top,first);bottom=Math.Max(bottom,last);
        }
        int clipLeft=0,clipRight=width;top=Math.Clamp(top,0,height);bottom=Math.Clamp(bottom,0,height);
        if(clip is {} box)
        {clipLeft=(int)Math.Clamp(MathF.Ceiling(box.Left-.5f),0,width);clipRight=(int)Math.Clamp(MathF.Ceiling(box.Right-.5f),clipLeft,width);top=Math.Max(top,(int)Math.Clamp(MathF.Ceiling(box.Top-.5f),0,height));bottom=Math.Min(bottom,(int)Math.Clamp(MathF.Ceiling(box.Bottom-.5f),0,height));}
        var runs=new List<CoverageRun>();int left=width,right=0;
        for(int y=top;y<bottom;y++)
        {
            long min=long.MaxValue,max=long.MinValue;int crossings=0;
            for(int i=0;i<used;i++){ref readonly var edge=ref edges[i];if(y<edge.Top||y>=edge.Bottom)continue;long value=edge.X+edge.Slope*(y-edge.Top);min=Math.Min(min,value);max=Math.Max(max,value);crossings++;}
            if(crossings<2)continue;
            int x0=(int)Math.Clamp((min+32768)>>16,clipLeft,clipRight),x1=(int)Math.Clamp((max+32768)>>16,clipLeft,clipRight);if(x0>=x1)continue;
            runs.Add(new(y,x0,x1-x0,255));left=Math.Min(left,x0);right=Math.Max(right,x1);
        }
        return runs.Count==0?new(0,0,0,0,[],[]):new(left,runs[0].Y,right-left,runs[^1].Y-runs[0].Y+1,[],runs.ToArray());
    }
}
