// Adaptive distance table follows Skia m119 SkContourMeasure.cpp (Google, BSD-3).
// Geometry stays analytic; extracting a segment never flattens the returned path.
using SimdSkia;
namespace SkiaSharp;

public class SKPathMeasure : SKObject
{
    private const int MaxT=0x3fffffff;
    private readonly record struct Curve(PathVerb Verb,PointF A,PointF B,PointF C,PointF D,float Weight=1);
    private readonly record struct Sample(float Distance,int Curve,int T);
    private sealed record Contour(Curve[] Curves,Sample[] Samples,bool Closed)
    {internal float Length=>Samples[^1].Distance;}
    private List<Contour> contours=[];
    private int contour;
    private float resolution=1;
    public SKPathMeasure(){ }
    public SKPathMeasure(SKPath path,bool forceClosed=false,float resScale=1)
    {ArgumentNullException.ThrowIfNull(path);if(!float.IsFinite(resScale)||resScale<=0)throw new ArgumentOutOfRangeException(nameof(resScale));resolution=resScale;SetPath(path,forceClosed);}
    public float Length{get{ThrowIfDisposed();return contour<contours.Count?contours[contour].Length:0;}}
    public bool IsClosed{get{ThrowIfDisposed();return contour<contours.Count&&contours[contour].Closed;}}
    public void SetPath(SKPath? path)=>SetPath(path,false);
    public void SetPath(SKPath? path,bool forceClosed)
    {
        ThrowIfDisposed();var built=new List<Contour>();if(path is not null)
        {
            var curves=new List<Curve>();var samples=new List<Sample>();PointF current=default,start=default;bool closed=forceClosed;float length=0,tolerance=.5f/resolution;
            void Leaf(int index,int t,PointF a,PointF b)
            {float dx=b.X-a.X,dy=b.Y-a.Y,next=length+MathF.Sqrt(dx*dx+dy*dy);if(next>length){length=next;samples.Add(new(length,index,t));}}
            void Subdivide(Curve original,Curve sub,int index,int min,int max,PointF a,PointF b)
            {
                int mid=(min+max)>>1;bool curvy=false;PointF half=default;
                if(sub.Verb==PathVerb.Quadratic)curvy=MathF.Max(MathF.Abs(sub.B.X*.5f-(sub.A.X+sub.C.X)*.25f),MathF.Abs(sub.B.Y*.5f-(sub.A.Y+sub.C.Y)*.25f))>tolerance;
                else if(sub.Verb==PathVerb.Cubic)
                {var p=Lerp(sub.A,sub.D,1f/3);var q=Lerp(sub.A,sub.D,2f/3);curvy=MathF.Max(MathF.Max(MathF.Abs(p.X-sub.B.X),MathF.Abs(p.Y-sub.B.Y)),MathF.Max(MathF.Abs(q.X-sub.C.X),MathF.Abs(q.Y-sub.C.Y)))>tolerance;}
                else if(sub.Verb==PathVerb.Conic){half=Evaluate(original,Scalar(mid));curvy=MathF.Max(MathF.Abs(half.X-(a.X+b.X)*.5f),MathF.Abs(half.Y-(a.Y+b.Y)*.5f))>tolerance;}
                if(((max-min)>>10)!=0&&curvy)
                {
                    if(sub.Verb==PathVerb.Conic){Subdivide(original,sub,index,min,mid,a,half);Subdivide(original,sub,index,mid,max,half,b);}
                    else{Split(sub,.5f,out var left,out var right);Subdivide(original,left,index,min,mid,left.A,End(left));Subdivide(original,right,index,mid,max,right.A,End(right));}
                }
                else Leaf(index,max,a,b);
            }
            void Add(Curve c){int index=curves.Count;curves.Add(c);if(c.Verb==PathVerb.Line)Leaf(index,MaxT,c.A,c.B);else Subdivide(c,c,index,0,MaxT,c.A,End(c));}
            void Finish()
            {if(samples.Count!=0){if(closed&&current!=start)Add(new(PathVerb.Line,current,start,default,default));if(float.IsFinite(length))built.Add(new(curves.ToArray(),samples.ToArray(),closed));}curves.Clear();samples.Clear();length=0;closed=forceClosed;}
            foreach(var s in path.ToCore().Segments)
            {
                switch(s.Verb)
                {
                    case PathVerb.Move:Finish();start=current=s.A;break;
                    case PathVerb.Close:closed=true;break;
                    case PathVerb.Line:Add(new(s.Verb,current,s.A,default,default));current=s.A;break;
                    case PathVerb.Quadratic:Add(new(s.Verb,current,s.A,s.B,default));current=s.B;break;
                    case PathVerb.Conic:Add(new(s.Verb,current,s.A,s.B,default,s.ConicWeight));current=s.B;break;
                    case PathVerb.Cubic:Add(new(s.Verb,current,s.A,s.B,s.C));current=s.C;break;
                }
            }
            Finish();
        }
        contours=built;contour=0;
    }
    public bool NextContour(){ThrowIfDisposed();if(contour<contours.Count)contour++;return contour<contours.Count;}
    private static float Scalar(int t)=>t*(1f/(float)MaxT);
    private static PointF Lerp(PointF a,PointF b,float t)=>a+(b-a)*t;
    private static PointF End(Curve c)=>c.Verb==PathVerb.Line?c.B:c.Verb==PathVerb.Cubic?c.D:c.C;
    private static PointF Evaluate(Curve c,float t)
    {
        if(c.Verb==PathVerb.Line)return Lerp(c.A,c.B,t);
        if(c.Verb==PathVerb.Conic){float u=1-t,a=u*u,b=2*c.Weight*u*t,d=t*t,den=a+b+d;return (c.A*a+c.B*b+c.C*d)*(1/den);}
        var ab=Lerp(c.A,c.B,t);var bc=Lerp(c.B,c.C,t);var p=Lerp(ab,bc,t);
        return c.Verb==PathVerb.Cubic?Lerp(p,Lerp(bc,Lerp(c.C,c.D,t),t),t):p;
    }
    private static PointF Tangent(Curve c,float t)
    {
        PointF result;
        if(c.Verb==PathVerb.Line)result=c.B-c.A;
        else if(c.Verb==PathVerb.Quadratic){result=Lerp(c.B-c.A,c.C-c.B,t);if(result==default)result=c.C-c.A;}
        else if(c.Verb==PathVerb.Cubic){result=Lerp(Lerp(c.B-c.A,c.C-c.B,t),Lerp(c.C-c.B,c.D-c.C,t),t);if(result==default)result=t==0?(c.C!=c.A?c.C-c.A:c.D-c.A):(c.D!=c.B?c.D-c.B:c.D-c.A);}
        else{float u=1-t,d=u*u+2*c.Weight*u*t+t*t,dd=2*((c.Weight-1)*u+(1-c.Weight)*t);var numerator=c.A*(u*u)+c.B*(2*c.Weight*u*t)+c.C*(t*t);var derivative=(c.B*c.Weight-c.A)*(2*u)+(c.C-c.B*c.Weight)*(2*t);result=derivative*d-numerator*dd;}
        double length=Math.Sqrt((double)result.X*result.X+(double)result.Y*result.Y);return length>0?result*(float)(1/length):default;
    }
    private static int Locate(Contour c,float distance,out float t)
    {
        var data=c.Samples;int low=0,high=data.Length-1;while(low<high){int mid=(low+high)>>1;if(data[mid].Distance<distance)low=mid+1;else high=mid;}
        var s=data[low];float previous=low>0?data[low-1].Distance:0;float min=low>0&&data[low-1].Curve==s.Curve?Scalar(data[low-1].T):0;
        t=min+(Scalar(s.T)-min)*((distance-previous)/(s.Distance-previous));return s.Curve;
    }
    public bool GetPositionAndTangent(float distance,out SKPoint position,out SKPoint tangent)
    {ThrowIfDisposed();position=tangent=default;if(contour>=contours.Count||float.IsNaN(distance))return false;var c=contours[contour];int i=Locate(c,Math.Clamp(distance,0,c.Length),out float t);var p=Evaluate(c.Curves[i],t);var d=Tangent(c.Curves[i],t);position=new(p.X,p.Y);tangent=new(d.X,d.Y);return true;}
    public SKPoint GetPosition(float distance){GetPosition(distance,out var point);return point;}
    public bool GetPosition(float distance,out SKPoint position)=>GetPositionAndTangent(distance,out position,out _);
    public SKPoint GetTangent(float distance){GetTangent(distance,out var tangent);return tangent;}
    public bool GetTangent(float distance,out SKPoint tangent)=>GetPositionAndTangent(distance,out _,out tangent);
    public SKMatrix GetMatrix(float distance,SKPathMeasureMatrixFlags flags){GetMatrix(distance,out var matrix,flags);return matrix;}
    public bool GetMatrix(float distance,out SKMatrix matrix,SKPathMeasureMatrixFlags flags)
    {matrix=default;if(!GetPositionAndTangent(distance,out var p,out var t))return false;matrix=(flags&SKPathMeasureMatrixFlags.GetTangent)!=0?new(t.X,-t.Y,0,t.Y,t.X,0,0,0,1):SKMatrix.Identity;if((flags&SKPathMeasureMatrixFlags.GetPosition)!=0){matrix.TransX=p.X;matrix.TransY=p.Y;}return true;}
    public SKPath GetSegment(float start,float stop,bool startWithMoveTo){var path=new SKPath();if(GetSegment(start,stop,path,startWithMoveTo))return path;path.Dispose();return null!;}
    public bool GetSegment(float start,float stop,SKPath dst,bool startWithMoveTo)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(dst);dst.ThrowIfDisposed();if(contour>=contours.Count)return false;var c=contours[contour];start=MathF.Max(start,0);stop=MathF.Min(stop,c.Length);if(!(start<=stop))return false;
        int first=Locate(c,start,out float a),last=Locate(c,stop,out float b);if(!float.IsFinite(a)||!float.IsFinite(b))return false;
        if(startWithMoveTo){var p=Evaluate(c.Curves[first],a);dst.MoveTo(p.X,p.Y);}
        for(int i=first;i<=last;i++)
        {
            float low=i==first?a:0,high=i==last?b:1;
            if(low==high){if(!dst.IsEmpty)dst.LineTo(dst.LastPoint);continue;}
            var curve=c.Curves[i];if(curve.Verb==PathVerb.Conic){curve=ConicPart(curve,low,high);low=0;high=1;}if(low>0){Split(curve,low,out _,out curve);high=(high-low)/(1-low);}if(high<1)Split(curve,high,out curve,out _);
            switch(curve.Verb)
            {case PathVerb.Line:dst.LineTo(curve.B.X,curve.B.Y);break;case PathVerb.Quadratic:dst.QuadTo(curve.B.X,curve.B.Y,curve.C.X,curve.C.Y);break;case PathVerb.Conic:dst.ConicTo(curve.B.X,curve.B.Y,curve.C.X,curve.C.Y,curve.Weight);break;case PathVerb.Cubic:dst.CubicTo(curve.B.X,curve.B.Y,curve.C.X,curve.C.Y,curve.D.X,curve.D.Y);break;}
        }
        return true;
    }
    private static Curve ConicPart(Curve c,float start,float stop)
    {
        if(start==0){if(stop==1)return c;Split(c,stop,out var result,out _);return result;}
        if(stop==1){Split(c,start,out _,out var result);return result;}
        // Direct two-parameter homogeneous restriction. A normalized first chop
        // changes the parameterization, so reusing (stop-start)/(1-start) is wrong.
        (PointF XY,float Z) Coeff(float t)
        {var b=c.B*c.Weight;var a=c.C+c.A-b*2;var d=(b-c.A)*2;var xy=(a*t+d)*t+c.A;float z=(2*(1-c.Weight)*t+2*(c.Weight-1))*t+1;return(xy,z);}
        var a=Coeff(start);var d=Coeff((start+stop)/2);var b=Coeff(stop);var control=d.XY*2-(a.XY+b.XY)*.5f;float weight=d.Z*2-(a.Z+b.Z)*.5f;
        return c with{A=a.XY*(1/a.Z),B=control*(1/weight),C=b.XY*(1/b.Z),Weight=weight/MathF.Sqrt(a.Z*b.Z)};
    }
    private static void Split(Curve c,float t,out Curve left,out Curve right)
    {
        if(c.Verb==PathVerb.Line){var p=Lerp(c.A,c.B,t);left=c with{B=p};right=c with{A=p};return;}
        if(c.Verb==PathVerb.Conic)
        {
            // Homogeneous de Casteljau; normalize endpoint weights after the split.
            double u=1-t,w=c.Weight,ab=u+t*w,bc=u*w+t,m=u*ab+t*bc;
            PointF Project(double x,double y,double z)=>new((float)(x/z),(float)(y/z));
            var p=Project(u*u*c.A.X+2*u*t*w*c.B.X+t*t*c.C.X,u*u*c.A.Y+2*u*t*w*c.B.Y+t*t*c.C.Y,m);
            var a=Project(u*c.A.X+t*w*c.B.X,u*c.A.Y+t*w*c.B.Y,ab);var b=Project(u*w*c.B.X+t*c.C.X,u*w*c.B.Y+t*c.C.Y,bc);
            left=c with{B=a,C=p,Weight=(float)(ab/Math.Sqrt(m))};right=c with{A=p,B=b,Weight=(float)(bc/Math.Sqrt(m))};return;
        }
        var ab0=Lerp(c.A,c.B,t);var bc0=Lerp(c.B,c.C,t);var abc=Lerp(ab0,bc0,t);
        if(c.Verb==PathVerb.Quadratic){left=c with{B=ab0,C=abc};right=c with{A=abc,B=bc0};return;}
        var cd=Lerp(c.C,c.D,t);var bcd=Lerp(bc0,cd,t);var end=Lerp(abc,bcd,t);
        left=c with{B=ab0,C=abc,D=end};right=c with{A=end,B=bcd,C=cd};
    }
    protected override void Dispose(bool disposing)=>contours.Clear();
}
