using SimdSkia;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace SkiaSharp;

public partial class SKPath
{
    public RawIterator CreateRawIterator(){ThrowIfDisposed();return new(ToCore());}
    public Iterator CreateIterator(bool forceClose){ThrowIfDisposed();return new(ToCore(),forceClose);}
    private readonly record struct Iteration(PathSegment Segment,PointF Start,PointF Contour,bool CloseLine,bool Closed,int ReadIndex=0);
    private static List<Iteration> Iterations(PathGeometry geometry,bool regular,bool forceClose)
    {
        var result=new List<Iteration>(geometry.Segments.Length+4);PointF current=default,start=default;int begin=0;bool active=false;
        void EndContour(bool closed,int cursor)
        {
            if(!active)return;
            if(regular&&!closed&&result.Count==begin+1){result.RemoveAt(begin);active=false;return;}
            if(regular&&(closed||forceClose))
            {
                if(current!=start)result.Add(new(new(PathVerb.Line,start),current,start,true,true,cursor));
                result.Add(new(new(PathVerb.Close,default),start,start,false,true,closed?cursor+1:cursor));
            }
            else if(!regular&&closed)result.Add(new(new(PathVerb.Close,default),current,start,false,true));
            active=false;
        }
        for(int cursor=0;cursor<geometry.Segments.Length;cursor++)
        {
            var s=geometry.Segments[cursor];
            if(s.Verb==PathVerb.Move){EndContour(false,cursor);current=start=s.A;begin=result.Count;active=true;result.Add(new(s,current,start,false,false,cursor+1));continue;}
            if(s.Verb==PathVerb.Close){EndContour(true,cursor);current=start;continue;}
            result.Add(new(s,current,start,false,false,cursor+1));current=End(s);
        }
        EndContour(false,geometry.Segments.Length);
        if(regular)for(int i=0;i<result.Count;i++)
        {
            int cursor=result[i].ReadIndex;bool closed=false;
            if(cursor<geometry.Segments.Length)
            {
                if(forceClose)closed=true;
                else for(int j=cursor;j<geometry.Segments.Length;j++){var v=geometry.Segments[j].Verb;if(v==PathVerb.Close){closed=true;break;}if(v==PathVerb.Move)break;}
            }
            result[i]=result[i] with {Closed=closed};
        }
        return result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKPathVerb Output(in Iteration value,Span<SKPoint> points)
    {
        var s=value.Segment;SKPoint Point(PointF p)=>new(p.X,p.Y);
        switch(s.Verb)
        {
            case PathVerb.Move:points[0]=Point(s.A);return SKPathVerb.Move;
            case PathVerb.Line:points[0]=Point(value.Start);points[1]=Point(s.A);return SKPathVerb.Line;
            case PathVerb.Quadratic:points[0]=Point(value.Start);points[1]=Point(s.A);points[2]=Point(s.B);return SKPathVerb.Quad;
            case PathVerb.Conic:points[0]=Point(value.Start);points[1]=Point(s.A);points[2]=Point(s.B);return SKPathVerb.Conic;
            case PathVerb.Cubic:points[0]=Point(value.Start);points[1]=Point(s.A);points[2]=Point(s.B);points[3]=Point(s.C);return SKPathVerb.Cubic;
            default:points[0]=Point(value.Contour);return SKPathVerb.Close;
        }
    }
    private static void ValidateIteratorSpan(Span<SKPoint> points){if(points.IsEmpty&&Unsafe.IsNullRef(ref MemoryMarshal.GetReference(points)))throw new ArgumentNullException(nameof(points));if(points.Length!=4)throw new ArgumentException("Must be an array of four elements.",nameof(points));}
    public class RawIterator:SKObject
    {
        private PathGeometry? geometry;
        private int position;
        private float weight;
        private PointF current,contour;
        internal RawIterator(PathGeometry geometry)=>this.geometry=geometry;
        public SKPathVerb Next(SKPoint[] points){ArgumentNullException.ThrowIfNull(points);return Next(points.AsSpan());}
        public SKPathVerb Next(Span<SKPoint> points)
        {ThrowIfDisposed();ValidateIteratorSpan(points);if(position>=geometry!.Segments.Length)return SKPathVerb.Done;var s=geometry.Segments[position++];if(s.Verb==PathVerb.Move)contour=current=s.A;var entry=new Iteration(s,current,contour,false,false);if(s.Verb==PathVerb.Conic)weight=s.ConicWeight;current=s.Verb==PathVerb.Close?contour:End(s);return Output(entry,points);}
        public float ConicWeight(){ThrowIfDisposed();return weight;}
        public SKPathVerb Peek(){ThrowIfDisposed();return position<geometry!.Segments.Length?geometry.Segments[position].Verb switch{PathVerb.Move=>SKPathVerb.Move,PathVerb.Line=>SKPathVerb.Line,PathVerb.Quadratic=>SKPathVerb.Quad,PathVerb.Conic=>SKPathVerb.Conic,PathVerb.Cubic=>SKPathVerb.Cubic,_=>SKPathVerb.Close}:SKPathVerb.Done;}
        protected override void Dispose(bool disposing)=>geometry=null;
    }
    public class Iterator:SKObject
    {
        private PathGeometry? geometry;
        private readonly bool forceClose;
        private int cursor,closeCursor,closedThrough;
        private bool cachedClosed;
        private float weight;
        private bool closeLine,closed,active,pendingClose;
        private PointF current,start;
        internal Iterator(PathGeometry geometry,bool forceClose){this.geometry=geometry;this.forceClose=forceClose;}
        public SKPathVerb Next(SKPoint[] points){ArgumentNullException.ThrowIfNull(points);return Next(points.AsSpan());}
        private bool IsClosedAt(int at)
        {var data=geometry!.Segments;if(at>=data.Length)return false;if(forceClose)return true;if(at<closedThrough)return cachedClosed;for(int i=at;i<data.Length;i++){if(data[i].Verb is PathVerb.Close or PathVerb.Move){closedThrough=i+1;return cachedClosed=data[i].Verb==PathVerb.Close;}}closedThrough=data.Length;return cachedClosed=false;}
        private SKPathVerb Emit(Iteration entry,Span<SKPoint> points,int readIndex)
        {closeLine=entry.CloseLine;closed=IsClosedAt(readIndex);if(entry.Segment.Verb==PathVerb.Conic)weight=entry.Segment.ConicWeight;return Output(entry,points);}
        public SKPathVerb Next(Span<SKPoint> points)
        {
            ThrowIfDisposed();ValidateIteratorSpan(points);var data=geometry!.Segments;
            if(pendingClose){pendingClose=false;return Emit(new(new(PathVerb.Close,default),start,start,false,false),points,closeCursor);}
            while(true)
            {
                bool closing=cursor<data.Length&&data[cursor].Verb==PathVerb.Close;
                if(active&&(cursor==data.Length||closing||data[cursor].Verb==PathVerb.Move))
                {
                    active=false;
                    if(closing||forceClose)
                    {
                        int before=cursor;if(closing)cursor++;closeCursor=cursor;
                        if(current!=start){var old=current;current=start;pendingClose=true;return Emit(new(new(PathVerb.Line,start),old,start,true,true),points,before);}
                        return Emit(new(new(PathVerb.Close,default),start,start,false,true),points,cursor);
                    }
                }
                if(cursor==data.Length)return SKPathVerb.Done;
                var segment=data[cursor++];
                if(segment.Verb==PathVerb.Close)continue;
                if(segment.Verb==PathVerb.Move)
                {
                    current=start=segment.A;
                    // Native regular iteration drops a move-only open contour.
                    if(cursor==data.Length||data[cursor].Verb==PathVerb.Move)continue;
                    active=true;return Emit(new(segment,current,start,false,false),points,cursor);
                }
                var value=new Iteration(segment,current,start,false,false);current=End(segment);return Emit(value,points,cursor);
            }
        }
        public float ConicWeight(){ThrowIfDisposed();return weight;}
        public bool IsCloseLine(){ThrowIfDisposed();return closeLine;}
        public bool IsCloseContour(){ThrowIfDisposed();return closed;}
        protected override void Dispose(bool disposing)=>geometry=null;
    }
    public void AddPathReverse(SKPath other)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(other);other.ThrowIfDisposed();var all=Iterations(other.ToCore(),false,false);
        int end=all.Count;
        while(end>0)
        {
            int start=end-1;while(start>0&&all[start].Segment.Verb!=PathVerb.Move)start--;
            int last=end-1;bool closed=all[last].Segment.Verb==PathVerb.Close;if(closed)last--;
            if(last>=start)
            {
                var lastPoint=End(all[last].Segment);MoveTo(lastPoint.X,lastPoint.Y);
                for(int i=last;i>start;i--)
                {
                    var entry=all[i];var s=entry.Segment;var p=entry.Start;
                    switch(s.Verb)
                    {
                        case PathVerb.Line:LineTo(p.X,p.Y);break;
                        case PathVerb.Quadratic:QuadTo(s.A.X,s.A.Y,p.X,p.Y);break;
                        case PathVerb.Conic:ConicTo(new(s.A.X,s.A.Y),new(p.X,p.Y),s.ConicWeight);break;
                        case PathVerb.Cubic:CubicTo(s.B.X,s.B.Y,s.A.X,s.A.Y,p.X,p.Y);break;
                    }
                }
                if(closed)Close();
            }
            end=start;
        }
    }
}
