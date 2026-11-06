using SimdSkia;
namespace SkiaSharp;
public partial class SKTextBlob
{
    public float[] GetIntercepts(float upperBounds,float lowerBounds,SKPaint? paint=null)
    {
        int n=CountIntercepts(upperBounds,lowerBounds,paint);var result=new float[n];GetIntercepts(upperBounds,lowerBounds,result,paint);return result;
    }
    public void GetIntercepts(float upperBounds,float lowerBounds,Span<float> intervals,SKPaint? paint=null)
    {
        ValidateIntercepts(upperBounds,lowerBounds,paint);int count=QueryIntercepts(upperBounds,lowerBounds,default,false);
        if(intervals.Length<count)throw new ArgumentException("Insufficient intercept buffer.",nameof(intervals));QueryIntercepts(upperBounds,lowerBounds,intervals,true);
    }
    public int CountIntercepts(float upperBounds,float lowerBounds,SKPaint? paint=null)
    {ValidateIntercepts(upperBounds,lowerBounds,paint);return QueryIntercepts(upperBounds,lowerBounds,default,false);}
    private void ValidateIntercepts(float top,float bottom,SKPaint? paint)
    {ThrowIfDisposed();paint?.ThrowIfDisposed();if(!float.IsFinite(top)||!float.IsFinite(bottom))throw new ArgumentException("Intercept bounds must be finite.");}
    private int QueryIntercepts(float top,float bottom,Span<float> output,bool write)
    {
        int count=0;
        // Skia m119 deliberately skips RSXform runs and returns one interval per
        // intersecting glyph in input order; it does not union/sort those gaps.
        foreach(var part in parts)
        {
            if(part.IgnoreIntercepts||part.GlyphRun is not {} run)continue;
            foreach(var glyph in run.Glyphs)
            {
                var matrix=run.FontTransform with{M02=run.FontTransform.M02+glyph.Position.X,M12=run.FontTransform.M12+glyph.Position.Y};
                if(!GlyphGap(glyph.Outline,matrix,top,bottom,out float l,out float r))continue;
                if(write){output[count]=l;output[count+1]=r;}count+=2;
            }
        }
        return count;
    }
    // Port of calculate_path_gap in SkGlyph.cpp m119 (Google, BSD-3).
    // Control points strictly inside the strip also expand the conservative gap.
    private static bool GlyphGap(PathGeometry path,Matrix3 matrix,float top,float bottom,out float left,out float right)
    {
        float l=float.PositiveInfinity,r=float.NegativeInfinity;PointF current=default,start=default;
        void Add(float x){l=MathF.Min(l,x);r=MathF.Max(r,x);}
        PointF Map(PointF p){var q=matrix.Map(p.X,p.Y);return new(q.X,q.Y);}
        void Inside(PointF p){if(top<p.Y&&p.Y<bottom)Add(p.X);}
        void Line(PointF a,PointF b)
        {
            if(top>MathF.Max(a.Y,b.Y)||MathF.Min(a.Y,b.Y)>bottom)return;
            float t=(top-a.Y)/(b.Y-a.Y);if(t>=0&&t<1)Add(a.X+t*(b.X-a.X));t=(bottom-a.Y)/(b.Y-a.Y);if(t>=0&&t<1)Add(a.X+t*(b.X-a.X));Inside(a);Inside(b);
        }
        void Curve(PointF a,PointF b,PointF c,PointF d,bool cubic)
        {
            float min=MathF.Min(a.Y,MathF.Min(b.Y,c.Y)),max=MathF.Max(a.Y,MathF.Max(b.Y,c.Y));if(cubic){min=MathF.Min(min,d.Y);max=MathF.Max(max,d.Y);}if(top>max||min>bottom)return;
            void Boundary(float y)
            {
                // A separate stack span here avoids capturing a ref local.
                Span<double> ts=stackalloc double[3];double aa=cubic?-a.Y+3d*b.Y-3d*c.Y+d.Y:0,bb=cubic?3d*a.Y-6d*b.Y+3d*c.Y:a.Y-2d*b.Y+c.Y,cc=cubic?-3d*a.Y+3d*b.Y:2d*(b.Y-a.Y),dd=a.Y-y;
                int n=Roots(aa,bb,cc,dd,ts);for(int i=0;i<n;i++){double t=ts[i],u=1-t;Add((float)(cubic?u*u*u*a.X+3*u*u*t*b.X+3*u*t*t*c.X+t*t*t*d.X:u*u*a.X+2*u*t*b.X+t*t*c.X));}
            }
            Boundary(top);Boundary(bottom);Inside(a);Inside(b);Inside(c);if(cubic)Inside(d);
        }
        foreach(var s in path.Segments)
        {
            switch(s.Verb)
            {
                case PathVerb.Move:current=start=Map(s.A);break;
                case PathVerb.Line:{var end=Map(s.A);Line(current,end);current=end;break;}
                case PathVerb.Quadratic:{var end=Map(s.B);Curve(current,Map(s.A),end,default,false);current=end;break;}
                case PathVerb.Cubic:{var end=Map(s.C);Curve(current,Map(s.A),Map(s.B),end,true);current=end;break;}
                case PathVerb.Close:Line(current,start);current=start;break;
                default:throw new NotSupportedException("TrueType glyph intercepts require line/quadratic/cubic outlines.");
            }
        }
        left=l;right=r;return l<r;
    }
    private static int Roots(double a,double b,double c,double d,Span<double> result)
    {
        double norm=Math.Max(Math.Max(Math.Abs(a),Math.Abs(b)),Math.Max(Math.Abs(c),Math.Abs(d)));if(norm==0)return 0;a/=norm;b/=norm;c/=norm;d/=norm;
        if(a==0)
        {
            if(b==0){if(c==0)return 0;double t=-d/c;if(t<0||t>1)return 0;result[0]=t;return 1;}
            double discriminant=c*c-4*b*d;if(discriminant<0)return 0;int found=0;
            if(discriminant==0){double t=-c/(2*b);if(t>=0&&t<=1){result[0]=t;return 1;}return 0;}
            double q=-.5*(c+Math.CopySign(Math.Sqrt(discriminant),c)),t0=q/b,t1=q==0?double.NaN:d/q;
            if(t0>=0&&t0<=1)result[found++]=t0;if(t1>=0&&t1<=1&&t1!=t0)result[found++]=t1;return found;
        }
        int count=0;Span<double> partitions=stackalloc double[4];int n=1;partitions[0]=0;
        if(a==0)
        {if(b!=0){double t=-c/(2*b);if(t>0&&t<1)partitions[n++]=t;}}
        else
        {double discriminant=4*b*b-12*a*c;if(discriminant>=0){double s=Math.Sqrt(discriminant),t=(-2*b-s)/(6*a),u=(-2*b+s)/(6*a);if(t>u)(t,u)=(u,t);if(t>0&&t<1)partitions[n++]=t;if(u>0&&u<1&&u!=t)partitions[n++]=u;}}
        partitions[n++]=1;
        double Eval(double t)=>((a*t+b)*t+c)*t+d;
        if(a==0&&b==0&&c==0)return 0;
        for(int i=0;i<n;i++)if(Math.Abs(Eval(partitions[i]))<=1e-10){double t=partitions[i];if(count==0||Math.Abs(result[count-1]-t)>1e-8)result[count++]=t;}
        for(int i=0;i+1<n;i++)
        {
            double lo=partitions[i],hi=partitions[i+1],fl=Eval(lo),fh=Eval(hi);if(fl==0||fh==0||Math.Sign(fl)==Math.Sign(fh))continue;
            for(int k=0;k<50;k++){double mid=(lo+hi)*.5,f=Eval(mid);if(Math.Sign(f)==Math.Sign(fl)){lo=mid;fl=f;}else hi=mid;}
            double root=(lo+hi)*.5;bool duplicate=false;for(int k=0;k<count;k++)duplicate|=Math.Abs(result[k]-root)<=1e-8;if(!duplicate)result[count++]=root;
        }
        return count;
    }
}
