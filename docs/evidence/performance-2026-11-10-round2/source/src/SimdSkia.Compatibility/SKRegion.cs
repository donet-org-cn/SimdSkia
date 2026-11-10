using SimdSkia;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace SkiaSharp;

/// <summary>Canonical integer scan bands. Boolean operations depend on run count,
/// rather than allocating a bitmap covering the coordinate range.</summary>
public class SKRegion : SKObject
{
    internal readonly record struct Run(int Left,int Right);
    internal sealed record Band(int Top,int Bottom,Run[] Runs);
    private Band[] bands=[];
    private Band[]? boundsOwner;
    private SKRectI cachedBounds;
    public SKRegion(){ }
    public SKRegion(SKRegion region)=>SetRegion(region);
    public SKRegion(SKRectI rect)=>SetRect(rect);
    public SKRegion(SKPath path)=>SetPath(path);
    public bool IsEmpty{get{ThrowIfDisposed();return bands.Length==0;}}
    public bool IsRect{get{ThrowIfDisposed();return bands.Length==1&&bands[0].Runs.Length==1;}}
    public bool IsComplex=>!IsEmpty&&!IsRect;
    public SKRectI Bounds
    {get{ThrowIfDisposed();if(bands.Length==0)return default;if(ReferenceEquals(boundsOwner,bands))return cachedBounds;int left=int.MaxValue,right=int.MinValue;foreach(var band in bands){left=Math.Min(left,band.Runs[0].Left);right=Math.Max(right,band.Runs[^1].Right);}boundsOwner=bands;return cachedBounds=new(left,bands[0].Top,right,bands[^1].Bottom);}}
    public void SetEmpty(){ThrowIfDisposed();bands=[];}
    public bool SetRect(SKRectI rect){ThrowIfDisposed();bands=rect.Left>=rect.Right||rect.Top>=rect.Bottom||rect.Right==int.MaxValue||rect.Bottom==int.MaxValue?[]:[new(rect.Top,rect.Bottom,[new(rect.Left,rect.Right)])];return !IsEmpty;}
    public bool SetRegion(SKRegion region){ThrowIfDisposed();ArgumentNullException.ThrowIfNull(region);region.ThrowIfDisposed();bands=region.bands;return !IsEmpty;}
    public bool SetRects(ReadOnlySpan<SKRectI> rects)
    {ThrowIfDisposed();if(Unsafe.IsNullRef(ref MemoryMarshal.GetReference(rects)))throw new ArgumentNullException(nameof(rects));Band[] result=[];foreach(var rect in rects)if(rect.Left<rect.Right&&rect.Top<rect.Bottom&&rect.Right!=int.MaxValue&&rect.Bottom!=int.MaxValue)result=Combine(result,[new(rect.Top,rect.Bottom,[new(rect.Left,rect.Right)])],SKRegionOperation.Union);bands=result;return !IsEmpty;}
    public bool Contains(int x,int y)
    {
        ThrowIfDisposed();int lo=0,hi=bands.Length-1;while(lo<=hi){int mid=(lo+hi)>>1;var b=bands[mid];if(y<b.Top)hi=mid-1;else if(y>=b.Bottom)lo=mid+1;else{foreach(var r in b.Runs){if(x<r.Left)return false;if(x<r.Right)return true;}return false;}}return false;
    }
    public bool Contains(SKPointI xy)=>Contains(xy.X,xy.Y);
    private static bool Covers(Run[] outer,int left,int right)
    {foreach(var run in outer){if(run.Left>left)return false;if(run.Left<=left&&run.Right>=right)return true;}return false;}
    public bool Contains(SKRectI rect)
    {
        ThrowIfDisposed();if(rect.Left>=rect.Right||rect.Top>=rect.Bottom||IsEmpty||!Bounds.Contains(rect))return false;if(IsRect)return true;
        int y=rect.Top;foreach(var band in bands){if(band.Bottom<=y)continue;if(band.Top>y||!Covers(band.Runs,rect.Left,rect.Right))return false;y=Math.Min(rect.Bottom,band.Bottom);if(y==rect.Bottom)return true;}return false;
    }
    public bool Contains(SKRegion src)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(src);src.ThrowIfDisposed();if(src.IsEmpty||IsEmpty||!Bounds.Contains(src.Bounds))return false;if(IsRect)return true;int at=0;
        foreach(var input in src.bands){int y=input.Top;while(y<input.Bottom){while(at<bands.Length&&bands[at].Bottom<=y)at++;if(at==bands.Length||bands[at].Top>y)return false;foreach(var run in input.Runs)if(!Covers(bands[at].Runs,run.Left,run.Right))return false;y=Math.Min(input.Bottom,bands[at].Bottom);}}return true;
    }
    public bool Contains(SKPath path){using var region=new SKRegion(path);return Contains(region);}
    public bool QuickContains(SKRectI rect)=>IsRect&&rect.Left<rect.Right&&rect.Top<rect.Bottom&&Bounds.Contains(rect);
    public bool QuickReject(SKRectI rect)=>IsEmpty||rect.Left>=rect.Right||rect.Top>=rect.Bottom||!Bounds.IntersectsWith(rect);
    public bool QuickReject(SKRegion region){ArgumentNullException.ThrowIfNull(region);return QuickReject(region.Bounds);}
    public bool QuickReject(SKPath path){using var region=new SKRegion(path);return QuickReject(region);}
    public bool Intersects(SKRectI rect)
    {ThrowIfDisposed();if(QuickReject(rect))return false;foreach(var band in bands){if(band.Top>=rect.Bottom)break;if(band.Bottom<=rect.Top)continue;foreach(var run in band.Runs){if(run.Left>=rect.Right)break;if(run.Right>rect.Left)return true;}}return false;}
    public bool Intersects(SKRegion region)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(region);region.ThrowIfDisposed();if(QuickReject(region))return false;int ai=0,bi=0;
        while(ai<bands.Length&&bi<region.bands.Length){var a=bands[ai];var b=region.bands[bi];if(a.Bottom>b.Top&&b.Bottom>a.Top){int ar=0,br=0;while(ar<a.Runs.Length&&br<b.Runs.Length){var x=a.Runs[ar];var y=b.Runs[br];if(x.Left<y.Right&&y.Left<x.Right)return true;if(x.Right<y.Right)ar++;else br++;}}if(a.Bottom<b.Bottom)ai++;else bi++;}return false;
    }
    public bool Intersects(SKPath path){using var region=new SKRegion(path);return Intersects(region);}
    public void Translate(int x,int y)
    {
        ThrowIfDisposed();if(IsEmpty)return;var bounds=Bounds;
        x=(int)Math.Clamp((long)x,(long)int.MinValue-bounds.Left,(long)int.MaxValue-bounds.Right);y=(int)Math.Clamp((long)y,(long)int.MinValue-bounds.Top,(long)int.MaxValue-bounds.Bottom);
        if(IsRect){SetRect(new(bounds.Left+x,bounds.Top+y,bounds.Right+x,bounds.Bottom+y));return;}
        var next=new Band[bands.Length];for(int i=0;i<next.Length;i++){var b=bands[i];var runs=new Run[b.Runs.Length];for(int j=0;j<runs.Length;j++)runs[j]=new(b.Runs[j].Left+x,b.Runs[j].Right+x);next[i]=new(b.Top+y,b.Bottom+y,runs);}bands=next;
    }
    public bool Op(SKRectI rect,SKRegionOperation op){using var other=new SKRegion(rect);return Op(other,op);}
    public bool Op(int left,int top,int right,int bottom,SKRegionOperation op)=>Op(new SKRectI(left,top,right,bottom),op);
    public bool Op(SKPath path,SKRegionOperation op){using var other=new SKRegion(path);return Op(other,op);}
    public bool Op(SKRegion region,SKRegionOperation op)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(region);region.ThrowIfDisposed();if(!Enum.IsDefined(op))throw new ArgumentOutOfRangeException(nameof(op));bands=Combine(bands,region.bands,op);return !IsEmpty;}
    private static void Append(List<Band> result,int top,int bottom,Run[] runs)
    {if(runs.Length==0||top>=bottom)return;if(result.Count>0&&result[^1].Bottom==top&&result[^1].Runs.AsSpan().SequenceEqual(runs)){result[^1]=result[^1] with{Bottom=bottom};return;}result.Add(new(top,bottom,runs));}
    private static Band[] Combine(Band[] a,Band[] b,SKRegionOperation op)
    {
        if(op==SKRegionOperation.Replace)return b;if(a.Length==0)return op is SKRegionOperation.Union or SKRegionOperation.XOR or SKRegionOperation.ReverseDifference?b:[];
        if(b.Length==0)return op is SKRegionOperation.Union or SKRegionOperation.XOR or SKRegionOperation.Difference?a:[];
        int count=checked((a.Length+b.Length)*2);int[]? rented=null;Span<int> ys=count<=128?stackalloc int[count]:(rented=ArrayPool<int>.Shared.Rent(count)).AsSpan(0,count);
        try
        {
            int at=0;foreach(var band in a){ys[at++]=band.Top;ys[at++]=band.Bottom;}foreach(var band in b){ys[at++]=band.Top;ys[at++]=band.Bottom;}ys.Sort();
            int unique=0;for(int i=0;i<ys.Length;i++)if(unique==0||ys[i]!=ys[unique-1])ys[unique++]=ys[i];var result=new List<Band>(Math.Min(a.Length+b.Length+2,4096));int ai=0,bi=0;
            for(int y=0;y<unique-1;y++)
            {
                int top=ys[y],bottom=ys[y+1];while(ai<a.Length&&a[ai].Bottom<=top)ai++;while(bi<b.Length&&b[bi].Bottom<=top)bi++;var ar=ai<a.Length&&a[ai].Top<=top?a[ai].Runs:[];var br=bi<b.Length&&b[bi].Top<=top?b[bi].Runs:[];var runs=CombineRuns(ar,br,op);if(runs.Length==0)continue;
                Band? reuse=ai<a.Length&&a[ai].Top==top&&a[ai].Bottom==bottom&&ReferenceEquals(runs,a[ai].Runs)?a[ai]:bi<b.Length&&b[bi].Top==top&&b[bi].Bottom==bottom&&ReferenceEquals(runs,b[bi].Runs)?b[bi]:null;
                if(reuse is not null&&(result.Count==0||result[^1].Bottom!=top||!result[^1].Runs.AsSpan().SequenceEqual(runs)))result.Add(reuse);else Append(result,top,bottom,runs);
            }
            return result.ToArray();
        }
        finally{if(rented is not null)ArrayPool<int>.Shared.Return(rented);}
    }
    private static Run[] CombineRuns(Run[] a,Run[] b,SKRegionOperation op)
    {
        if(a.Length==0)return op is SKRegionOperation.Union or SKRegionOperation.XOR or SKRegionOperation.ReverseDifference?b:[];
        if(b.Length==0)return op is SKRegionOperation.Union or SKRegionOperation.XOR or SKRegionOperation.Difference?a:[];
        int capacity=checked(a.Length+b.Length);Run[]? rented=null;Span<Run> output=capacity<=64?stackalloc Run[capacity]:(rented=ArrayPool<Run>.Shared.Rent(capacity)).AsSpan(0,capacity);
        try
        {
            int length=0,ai=0,bi=0,start=0;bool inside=false;
            while(ai<a.Length*2||bi<b.Length*2)
            {
                int ax=ai<a.Length*2?(ai%2==0?a[ai/2].Left:a[ai/2].Right):int.MaxValue,bx=bi<b.Length*2?(bi%2==0?b[bi/2].Left:b[bi/2].Right):int.MaxValue,x=Math.Min(ax,bx);
                if(ai<a.Length*2&&ax==x)ai++;if(bi<b.Length*2&&bx==x)bi++;bool ia=(ai&1)!=0,ib=(bi&1)!=0;
                bool next=op switch{SKRegionOperation.Union=>ia||ib,SKRegionOperation.Intersect=>ia&&ib,SKRegionOperation.Difference=>ia&&!ib,SKRegionOperation.ReverseDifference=>ib&&!ia,SKRegionOperation.XOR=>ia!=ib,_=>ib};
                if(next&&!inside)start=x;else if(inside&&!next&&start<x)output[length++]=new(start,x);inside=next;
            }
            var result=output[..length];if(result.SequenceEqual(a))return a;if(result.SequenceEqual(b))return b;return result.ToArray();
        }
        finally{if(rented is not null)ArrayPool<Run>.Shared.Return(rented);}
    }

    public bool SetPath(SKPath path)
    {ArgumentNullException.ThrowIfNull(path);using var clip=new SKRegion(SKRectI.Ceiling(path.Bounds));return SetPath(path,clip);}
    public bool SetPath(SKPath path,SKRegion clip)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(path);ArgumentNullException.ThrowIfNull(clip);clip.ThrowIfDisposed();var geometry=path.ToCore();var result=new List<Band>();if(clip.IsEmpty){bands=[];return false;}
        var bounds=clip.Bounds;if((long)bounds.Bottom-bounds.Top>2000000)throw new NotSupportedException("Path-to-region conversion exceeds the two million scanline budget.");
        var contours=geometry.Flatten(Matrix3.Identity,.125f);var crossings=new List<(float X,int Winding)>();var spans=new List<Run>();bool odd=geometry.FillRule is FillRule.EvenOdd or FillRule.InverseEvenOdd,inverse=geometry.FillRule is FillRule.InverseEvenOdd or FillRule.InverseWinding;
        foreach(var band in clip.bands)for(int y=band.Top;y<band.Bottom;y++)
        {
            crossings.Clear();spans.Clear();float sample=y+.5f;
            foreach(var contour in contours)for(int i=0;i<contour.Length;i++){var a=contour[i];var b=contour[(i+1)%contour.Length];if(a.Y==b.Y||sample<=MathF.Min(a.Y,b.Y)||sample>MathF.Max(a.Y,b.Y))continue;float x=a.X+(sample-a.Y)*(b.X-a.X)/(b.Y-a.Y);crossings.Add((x,b.Y>a.Y?1:-1));}
            crossings.Sort(static(a,b)=>a.X.CompareTo(b.X));int winding=0,start=0;
            for(int i=0;i<crossings.Count;){float x=crossings[i].X;bool before=odd?(winding&1)!=0:winding!=0;do{winding+=crossings[i++].Winding;}while(i<crossings.Count&&crossings[i].X==x);bool after=odd?(winding&1)!=0:winding!=0;int edge=(int)Math.Clamp(Math.Floor((double)x+.5),int.MinValue,int.MaxValue);if(!before&&after)start=edge;else if(before&&!after&&edge>start)spans.Add(new(start,edge));}
            var runs=CombineRuns(band.Runs,spans.ToArray(),inverse?SKRegionOperation.Difference:SKRegionOperation.Intersect);Append(result,y,checked(y+1),runs);
        }
        bands=result.ToArray();return !IsEmpty;
    }
    public SKPath GetBoundaryPath()
    {
        ThrowIfDisposed();if(IsEmpty)return null!;var edges=new List<(SKPointI A,SKPointI B)>();
        void Horizontal(Run[] runs,int y,bool reverse){foreach(var r in runs)edges.Add(reverse?(new(r.Right,y),new(r.Left,y)):(new(r.Left,y),new(r.Right,y)));}
        for(int i=0;i<bands.Length;i++)
        {
            var b=bands[i];foreach(var r in b.Runs){edges.Add((new(r.Right,b.Top),new(r.Right,b.Bottom)));edges.Add((new(r.Left,b.Bottom),new(r.Left,b.Top)));}
            var above=i>0&&bands[i-1].Bottom==b.Top?bands[i-1].Runs:[];var below=i+1<bands.Length&&bands[i+1].Top==b.Bottom?bands[i+1].Runs:[];
            Horizontal(CombineRuns(b.Runs,above,SKRegionOperation.Difference),b.Top,false);Horizontal(CombineRuns(b.Runs,below,SKRegionOperation.Difference),b.Bottom,true);
        }
        var outgoing=new Dictionary<SKPointI,List<int>>();for(int i=0;i<edges.Count;i++){if(!outgoing.TryGetValue(edges[i].A,out var list))outgoing.Add(edges[i].A,list=[]);list.Add(i);}var visited=new bool[edges.Count];var path=new SKPath();
        try
        {
            for(int first=0;first<edges.Count;first++)if(!visited[first])
            {
                int index=first;var origin=edges[first].A;path.MoveTo(origin.X,origin.Y);
                while(true){if(visited[index])throw new InvalidOperationException("Invalid region boundary topology.");visited[index]=true;var edge=edges[index];if(edge.B==origin){path.Close();break;}path.LineTo(edge.B.X,edge.B.Y);int next=-1;double best=double.PositiveInfinity;
                    foreach(int candidate in outgoing[edge.B])if(!visited[candidate]){var target=edges[candidate].B;double dx=(double)edge.B.X-edge.A.X,dy=(double)edge.B.Y-edge.A.Y,ex=(double)target.X-edge.B.X,ey=(double)target.Y-edge.B.Y,angle=Math.Atan2(dx*ey-dy*ex,dx*ex+dy*ey);if(angle<0)angle+=Math.Tau;if(angle<best){best=angle;next=candidate;}}
                    if(next<0)throw new InvalidOperationException("Open region boundary.");index=next;
                }
            }
            return path;
        }catch{path.Dispose();throw;}
    }
    public RectIterator CreateRectIterator(){ThrowIfDisposed();return new(bands);}
    public ClipIterator CreateClipIterator(SKRectI clip){ThrowIfDisposed();return new(bands,clip);}
    public SpanIterator CreateSpanIterator(int y,int left,int right)
    {ThrowIfDisposed();int lo=0,hi=bands.Length-1;while(lo<=hi){int mid=(lo+hi)>>1;var b=bands[mid];if(y<b.Top)hi=mid-1;else if(y>=b.Bottom)lo=mid+1;else return new(b.Runs,left,right);}return new([],left,right);}
    public class RectIterator:SKObject
    {
        private Band[] bands;private int band,run;internal RectIterator(Band[] bands)=>this.bands=bands;
        public bool Next(out SKRectI rect){ThrowIfDisposed();if(band>=bands.Length){rect=default;return false;}var b=bands[band];var r=b.Runs[run++];rect=new(r.Left,b.Top,r.Right,b.Bottom);if(run==b.Runs.Length){run=0;band++;}return true;}
        protected override void Dispose(bool disposing)=>bands=[];
    }
    public class ClipIterator:SKObject
    {
        private Band[] bands;private readonly SKRectI clip;private int band,run;internal ClipIterator(Band[] bands,SKRectI clip){this.bands=bands;this.clip=clip;}
        public bool Next(out SKRectI rect)
        {
            ThrowIfDisposed();rect=default;if(clip.Left>=clip.Right||clip.Top>=clip.Bottom)return false;
            while(band<bands.Length){var b=bands[band];if(b.Top>=clip.Bottom)return false;if(b.Bottom<=clip.Top){band++;run=0;continue;}var r=b.Runs[run++];if(run==b.Runs.Length){run=0;band++;}if(r.Left>=clip.Right||r.Right<=clip.Left)continue;
                // Baseline 3.119.4's managed Next calls sk_region_iterator_rect, not
                // sk_region_cliperator_rect: clip selects runs but returned rectangles
                // remain untrimmed. Preserve this measured, memory-safe behavior.
                rect=new(r.Left,b.Top,r.Right,b.Bottom);return true;}return false;
        }
        protected override void Dispose(bool disposing)=>bands=[];
    }
    public class SpanIterator:SKObject
    {
        private Run[] runs;private int index;private readonly int minimum,maximum;
        internal SpanIterator(Run[] runs,int left,int right){this.runs=runs;minimum=left;maximum=right;}
        public bool Next(out int left,out int right)
        {ThrowIfDisposed();while(minimum<maximum&&index<runs.Length){var run=runs[index++];left=Math.Max(minimum,run.Left);right=Math.Min(maximum,run.Right);if(left<right)return true;}left=right=0;return false;}
        protected override void Dispose(bool disposing)=>runs=[];
    }
    protected override void Dispose(bool disposing){bands=[];boundsOwner=null;}
}
