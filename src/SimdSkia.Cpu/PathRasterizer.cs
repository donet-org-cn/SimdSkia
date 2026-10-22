using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SimdSkia.Cpu;

internal readonly record struct CoverageRun(int Y,int X,int Length,byte Coverage);
internal sealed record CoverageMask(int Left,int Top,int Width,int Height,byte[] Data,CoverageRun[] Runs,bool Dense=false);

public static class PathRasterizer
{
    private readonly record struct Crossing(float X,int Winding,int Edge);
    private readonly record struct Edge(PointF A,PointF B,float Minimum,float Maximum);
    private sealed class EdgeComparer:IComparer<Edge>
    {
        public static readonly EdgeComparer Instance=new();
        public int Compare(Edge a,Edge b)=>a.Minimum.CompareTo(b.Minimum);
    }
    private sealed class CrossingComparer : IComparer<Crossing>
    {
        public static readonly CrossingComparer Instance=new();
        public int Compare(Crossing a,Crossing b)=>a.X.CompareTo(b.X);
    }
    public static byte[] Coverage(PointF[][] contours,FillRule rule,int width,int height,bool antialias,RectF? clip=null)
    {
        var mask=Rasterize(contours,rule,width,height,antialias,clip);
        var output=new byte[checked(width*height)];
        for(int y=0;y<mask.Height;y++)mask.Data.AsSpan(y*mask.Width,mask.Width).CopyTo(output.AsSpan((y+mask.Top)*width+mask.Left,mask.Width));
        return output;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static CoverageMask Rasterize(PointF[][] contours,FillRule rule,int width,int height,bool antialias,RectF? clip=null,bool compactRuns=false)
    {
        bool inverse=rule is FillRule.InverseEvenOdd or FillRule.InverseWinding;
        bool odd=rule is FillRule.EvenOdd or FillRule.InverseEvenOdd;
        float minX=width,minY=height,maxX=0,maxY=0;
        int edgeCount=0;
        foreach(var contour in contours)foreach(var p in contour)
        {minX=MathF.Min(minX,p.X);maxX=MathF.Max(maxX,p.X);minY=MathF.Min(minY,p.Y);maxY=MathF.Max(maxY,p.Y);edgeCount++;}
        if(inverse){minX=0;minY=0;maxX=width;maxY=height;}
        if(clip is {} c){minX=MathF.Max(minX,c.Left);maxX=MathF.Min(maxX,c.Right);minY=MathF.Max(minY,c.Top);maxY=MathF.Min(maxY,c.Bottom);}
        int x0=(int)Math.Clamp(MathF.Floor(minX),0,width),y0=(int)Math.Clamp(MathF.Floor(minY),0,height);
        int x1=(int)Math.Clamp(MathF.Ceiling(maxX),0,width),y1=(int)Math.Clamp(MathF.Ceiling(maxY),0,height);
        int w=Math.Max(0,x1-x0),h=Math.Max(0,y1-y0);
        if(w==0||h==0)return new(x0,y0,0,0,[],[]);
        double edgeHeight=0;
        if(compactRuns)foreach(var contour in contours)for(int i=0;i<contour.Length;i++)
        {
            var a=contour[i];var b=contour[(i+1)%contour.Length];
            edgeHeight+=Math.Max(0,Math.Min(y1,Math.Max(a.Y,b.Y))-Math.Max(y0,Math.Min(a.Y,b.Y)));
        }
        bool sparse=compactRuns&&edgeHeight/h<=8;
        var output=sparse?Array.Empty<byte>():new byte[checked(w*h)];
        var sparseRuns=sparse?new List<CoverageRun>():null;
        Edge[]? rentedEdges=null;int[]? rentedActive=null;Crossing[]? rentedCrossings=null;
        int[]? rentedRow=null;int[]? rentedDelta=null;ulong[]? rentedEvents=null;
        Span<Edge> edges=edgeCount<=128?stackalloc Edge[edgeCount]:(rentedEdges=ArrayPool<Edge>.Shared.Rent(edgeCount));
        Span<int> active=edgeCount<=128?stackalloc int[edgeCount]:(rentedActive=ArrayPool<int>.Shared.Rent(edgeCount));
        Span<Crossing> crossings=edgeCount<=128?stackalloc Crossing[edgeCount]:(rentedCrossings=ArrayPool<Crossing>.Shared.Rent(edgeCount));
        Span<int> row=w<=256?stackalloc int[w]:(rentedRow=ArrayPool<int>.Shared.Rent(w));
        Span<int> delta=w<=256?stackalloc int[w+1]:(rentedDelta=ArrayPool<int>.Shared.Rent(w+1));
        int eventCount=(w+64)/64;
        Span<ulong> events=eventCount<=16?stackalloc ulong[eventCount]:(rentedEvents=ArrayPool<ulong>.Shared.Rent(eventCount));
        // The grid is exactly 1x1 or 16x16. Nonnegative sample indices and
        // counts can use shifts/masks without variable integer division.
        int sampleShift=antialias?4:0,samples=1<<sampleShift,totalShift=sampleShift*2,round=(samples*samples)/2;
        float sampleStep=1f/samples;
        try
        {
            int n=0;
            foreach(var contour in contours)
            {
                if(contour.Length<2)continue;
                for(int j=0;j<contour.Length;j++)
                {var a=contour[j];var b=contour[(j+1)%contour.Length];if(a.Y!=b.Y)edges[n++]=new(a,b,MathF.Min(a.Y,b.Y),MathF.Max(a.Y,b.Y));}
            }
            edges[..n].Sort(EdgeComparer.Instance);
            int nextEdge=0,activeCount=0;
            for(int y=y0;y<y1;y++)
            {
                if(!sparse)row[..w].Clear();delta[..(w+1)].Clear();
                if(sparse)events[..eventCount].Clear();
                for(int sy=0;sy<samples;sy++)
                {
                    float sampleY=y+(sy+0.5f)*sampleStep;
                    while(nextEdge<n&&edges[nextEdge].Minimum<=sampleY)active[activeCount++]=nextEdge++;
                    int retained=0;
                    bool clipped=clip is {} bound&&(sampleY<bound.Top||sampleY>=bound.Bottom);
                    for(int j=0;j<activeCount;j++)
                    {
                        int index=active[j];var e=edges[index];if(e.Maximum<=sampleY)continue;
                        active[retained]=index;
                        if(!clipped)crossings[retained]=new(e.A.X+(sampleY-e.A.Y)*(e.B.X-e.A.X)/(e.B.Y-e.A.Y),e.B.Y>e.A.Y?1:-1,index);
                        retained++;
                    }
                    activeCount=retained;int count=retained;
                    if(clipped)continue;
                    SortCoherent(crossings,count);
                    for(int j=0;j<count;j++)active[j]=crossings[j].Edge;
                    int winding=0;float previous=0;
                    for(int j=0;j<=count;j++)
                    {
                        float next=j<count?crossings[j].X:width;
                        bool inside=(odd?(winding&1)!=0:winding!=0)^inverse;
                        if(inside)
                        {
                            if(sparse)AddSparseSpan(delta,events,x0,w,previous,next,samples,sampleShift,clip);
                            else AddSpan(row,delta,x0,w,previous,next,samples,sampleShift,clip);
                        }
                        if(j<count){winding+=crossings[j].Winding;previous=next;}
                    }
                }
                int accumulated=0;
                if(sparse)
                {
                    int previous=0;
                    for(int block=0;block<(w+64)/64;block++)
                    {
                        ulong bits=events[block];
                        while(bits!=0)
                        {
                            int x=block*64+BitOperations.TrailingZeroCount(bits);bits&=bits-1;
                            byte coverage=(byte)((accumulated*255+round)>>totalShift);
                            if(x>previous&&coverage!=0)
                            {
                                var rowRuns=sparseRuns!;
                                if(rowRuns.Count>0&&rowRuns[^1].Y==y&&rowRuns[^1].X+rowRuns[^1].Length==previous+x0&&rowRuns[^1].Coverage==coverage)
                                    rowRuns[^1]=rowRuns[^1] with {Length=rowRuns[^1].Length+x-previous};
                                else rowRuns.Add(new(y,previous+x0,x-previous,coverage));
                            }
                            accumulated+=delta[x];previous=x;
                        }
                    }
                }
                else for(int x=0;x<w;x++)
                {accumulated+=delta[x];output[(y-y0)*w+x]=(byte)(((row[x]+accumulated)*255+round)>>totalShift);}
            }
        }
        finally
        {
            if(rentedEdges is not null)ArrayPool<Edge>.Shared.Return(rentedEdges);
            if(rentedActive is not null)ArrayPool<int>.Shared.Return(rentedActive);
            if(rentedCrossings is not null)ArrayPool<Crossing>.Shared.Return(rentedCrossings);
            if(rentedRow is not null)ArrayPool<int>.Shared.Return(rentedRow);
            if(rentedDelta is not null)ArrayPool<int>.Shared.Return(rentedDelta);
            if(rentedEvents is not null)ArrayPool<ulong>.Shared.Return(rentedEvents);
        }
        if(sparse)return new(x0,y0,w,h,[],sparseRuns!.ToArray());
        // Dense AA text can contain more run metadata than coverage bytes. Keep the
        // byte mask and composite it directly instead of allocating thousands of runs.
        if(compactRuns)
        {
            int count=0,limit=output.Length/16;
            for(int y=0;y<h;y++)for(int x=0;x<w;)
            {
                byte coverage=output[y*w+x];int end=x+1;
                while(end<w&&output[y*w+end]==coverage)end++;
                if(coverage!=0&&++count>limit)return new(x0,y0,w,h,output,[],true);
                x=end;
            }
        }
        var runs=new List<CoverageRun>();
        for(int y=0;y<h;y++)for(int x=0;x<w;)
        {
            byte coverage=output[y*w+x];int end=x+1;
            while(end<w&&output[y*w+end]==coverage)end++;
            if(coverage!=0)runs.Add(new(y+y0,x+x0,end-x,coverage));x=end;
        }
        return new(x0,y0,w,h,output,runs.ToArray());
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SortCoherent(Span<Crossing> values,int count)
    {
        // Insertion sort is linear for coherent scanlines. A bounded move budget
        // switches arbitrary/self-intersecting input to O(n log n) introsort.
        long budget=(long)count*4;
        for(int i=1;i<count;i++)
        {
            var current=values[i];int at=i;
            while(at>0&&values[at-1].X.CompareTo(current.X)>0)
            {
                values[at]=values[at-1];at--;
                if(--budget<0){values[at]=current;values[..count].Sort(CrossingComparer.Instance);return;}
            }
            values[at]=current;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddSpan(Span<int> row,Span<int> delta,int origin,int width,float left,float right,int samples,int sampleShift,RectF? clip)
    {
        left=MathF.Max(left,origin);right=MathF.Min(right,origin+width);
        if(clip is {} c){left=MathF.Max(left,c.Left);right=MathF.Min(right,c.Right);}
        if(left>=right)return;
        int first=(int)Math.Clamp(MathF.Ceiling(left*samples-0.5f),origin*samples,(origin+width)*samples)-origin*samples;
        int end=(int)Math.Clamp(MathF.Ceiling(right*samples-0.5f),origin*samples,(origin+width)*samples)-origin*samples;
        if(first>=end)return;
        int a=first>>sampleShift,b=(end-1)>>sampleShift;
        if(a==b){row[a]+=end-first;return;}
        row[a]+=samples-(first&(samples-1));row[b]+=((end-1)&(samples-1))+1;
        if(a+1<b){delta[a+1]+=samples;delta[b]-=samples;}
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddSparseSpan(Span<int> delta,Span<ulong> events,int origin,int width,float left,float right,int samples,int sampleShift,RectF? clip)
    {
        left=MathF.Max(left,origin);right=MathF.Min(right,origin+width);
        if(clip is {} c){left=MathF.Max(left,c.Left);right=MathF.Min(right,c.Right);}
        if(left>=right)return;
        int first=(int)Math.Clamp(MathF.Ceiling(left*samples-0.5f),origin*samples,(origin+width)*samples)-origin*samples;
        int end=(int)Math.Clamp(MathF.Ceiling(right*samples-0.5f),origin*samples,(origin+width)*samples)-origin*samples;
        if(first>=end)return;
        int a=first>>sampleShift,b=(end-1)>>sampleShift;
        if(a==b)
        {delta[a]+=end-first;delta[a+1]-=end-first;events[a/64]|=1ul<<(a%64);events[(a+1)/64]|=1ul<<((a+1)%64);return;}
        int start=samples-(first&(samples-1)),last=((end-1)&(samples-1))+1;
        delta[a]+=start;delta[a+1]+=samples-start;delta[b]+=last-samples;delta[b+1]-=last;
        events[a/64]|=1ul<<(a%64);events[(a+1)/64]|=1ul<<((a+1)%64);
        events[b/64]|=1ul<<(b%64);events[(b+1)/64]|=1ul<<((b+1)%64);
    }
}
