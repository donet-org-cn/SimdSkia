using System.Buffers;

namespace SimdSkia.OptimizationReference;

public static class BaselinePathRasterizer
{
    private readonly record struct Crossing(float X,int Winding);
    private sealed class CrossingComparer : IComparer<Crossing>
    {
        public static readonly CrossingComparer Instance=new();
        public int Compare(Crossing a,Crossing b)=>a.X.CompareTo(b.X);
    }
    public static byte[] Coverage(PointF[][] contours,FillRule rule,int width,int height,bool antialias,RectF? clip=null)
    {
        var output=new byte[checked(width*height)];
        int edges=contours.Sum(c=>c.Length); var crossings=ArrayPool<Crossing>.Shared.Rent(Math.Max(1,edges));
        var row=ArrayPool<int>.Shared.Rent(width); int samples=antialias?16:1;
        try
        {
            for(int y=0;y<height;y++)
            {
                row.AsSpan(0,width).Clear();
                for(int sy=0;sy<samples;sy++)
                {
                    float sampleY=y+(sy+0.5f)/samples;
                    if(clip is {} bound&&(sampleY<bound.Top||sampleY>=bound.Bottom))continue;
                    int count=0;
                    foreach(var contour in contours)
                    {
                        if(contour.Length<2)continue;
                        for(int j=0;j<contour.Length;j++)
                        {
                            var a=contour[j];var b=contour[(j+1)%contour.Length];
                            if((a.Y<=sampleY&&b.Y>sampleY)||(b.Y<=sampleY&&a.Y>sampleY))
                                crossings[count++]=new(a.X+(sampleY-a.Y)*(b.X-a.X)/(b.Y-a.Y),b.Y>a.Y?1:-1);
                        }
                    }
                    Array.Sort(crossings,0,count,CrossingComparer.Instance);
                    bool inverse=rule is FillRule.InverseEvenOdd or FillRule.InverseWinding;
                    bool odd=rule is FillRule.EvenOdd or FillRule.InverseEvenOdd;
                    int winding=0;float previous=0;
                    for(int j=0;j<=count;j++)
                    {
                        float next=j<count?crossings[j].X:width;
                        bool inside=(odd ? (winding&1)!=0 : winding!=0)^inverse;
                        if(inside)AddSpan(row,width,previous,next,samples,clip);
                        if(j<count){winding+=crossings[j].Winding;previous=next;}
                    }
                }
                for(int x=0;x<width;x++)output[y*width+x]=(byte)((row[x]*255+samples*samples/2)/(samples*samples));
            }
        }
        finally {ArrayPool<Crossing>.Shared.Return(crossings);ArrayPool<int>.Shared.Return(row);}
        return output;
    }
    private static void AddSpan(int[] row,int width,float left,float right,int samples,RectF? clip)
    {
        left=MathF.Max(left,0);right=MathF.Min(right,width);
        if(clip is {} c){left=MathF.Max(left,c.Left);right=MathF.Min(right,c.Right);}
        if(left>=right)return;
        int first=(int)Math.Clamp(MathF.Ceiling(left*samples-0.5f),0,width*samples);
        int end=(int)Math.Clamp(MathF.Ceiling(right*samples-0.5f),0,width*samples);
        for(int x=first/samples;x<Math.Min(width,(end+samples-1)/samples);x++)
            row[x]+=Math.Max(0,Math.Min(end,(x+1)*samples)-Math.Max(first,x*samples));
    }
}
