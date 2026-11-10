using SimdSkia;

namespace SkiaSharp;

public partial class SKPath
{
    public void ConicTo(float x0,float y0,float x1,float y1,float w)=>ConicTo(new(x0,y0),new(x1,y1),w);
    public void ConicTo(SKPoint point0,SKPoint point1,float w)
    {
        ThrowIfDisposed();
        if(float.IsNaN(w))throw new NotSupportedException("NaN conic weights are not supported.");
        if(w<=0){LineTo(point1);return;}
        if(float.IsPositiveInfinity(w)){LineTo(point0);LineTo(point1);return;}
        if(w==1){QuadTo(point0,point1);return;}
        Add(PathSegment.Conic(new(point0.X,point0.Y),new(point1.X,point1.Y),w));
    }
    public void RConicTo(float dx0,float dy0,float dx1,float dy1,float w)
    {ThrowIfDisposed();var p=CurrentPoint();ConicTo(p.X+dx0,p.Y+dy0,p.X+dx1,p.Y+dy1,w);}
    public void RConicTo(SKPoint point0,SKPoint point1,float w)=>RConicTo(point0.X,point0.Y,point1.X,point1.Y,w);
    public void AddOval(SKRect rect,SKPathDirection direction=SKPathDirection.Clockwise)=>AddOvalCore(rect,direction,0);
    private void AddOvalCore(SKRect rect,SKPathDirection direction,int start)
    {
        ThrowIfDisposed();if(!Enum.IsDefined(direction))throw new ArgumentOutOfRangeException(nameof(direction));
        if(!rect.ToCore().IsFinite)throw new ArgumentException("Oval coordinates must be finite.",nameof(rect));
        bool shape=segments.All(s=>s.Verb==PathVerb.Move);
        float x=(rect.Left+rect.Right)*.5f,y=(rect.Top+rect.Bottom)*.5f;
        Span<SKPoint> points=[new(rect.Right,y),new(x,rect.Bottom),new(rect.Left,y),new(x,rect.Top)];
        Span<SKPoint> corners=[new(rect.Right,rect.Bottom),new(rect.Left,rect.Bottom),new(rect.Left,rect.Top),new(rect.Right,rect.Top)];
        int step=direction==SKPathDirection.Clockwise?1:-1;MoveTo(points[start]);
        for(int i=0;i<4;i++)
        {
            int at=(start+step*i+4)&3,next=(at+step+4)&3;
            ConicTo(corners[step>0?at:next],points[next],.7071067811865476f);
        }
        Close();ovalTag=shape;
    }
    public void AddArc(SKRect oval,float startAngle,float sweepAngle)
    {
        ThrowIfDisposed();ValidateArc(oval,startAngle,sweepAngle);if(sweepAngle==0||oval.Width<=0||oval.Height<=0)return;
        float quadrant=startAngle/90;
        if(MathF.Abs(sweepAngle)>=360&&quadrant==MathF.Floor(quadrant))
        {int start=((int)(quadrant%4)+4)&3;AddOvalCore(oval,sweepAngle>0?SKPathDirection.Clockwise:SKPathDirection.CounterClockwise,start);return;}
        ArcTo(oval,startAngle,sweepAngle,true);
    }
    public void ArcTo(SKRect oval,float startAngle,float sweepAngle,bool forceMoveTo)
    {
        ThrowIfDisposed();ValidateArc(oval,startAngle,sweepAngle);if(oval.Width<0||oval.Height<0)return;
        double cx=((double)oval.Left+oval.Right)*.5,cy=((double)oval.Top+oval.Bottom)*.5,rx=(double)oval.Width*.5,ry=(double)oval.Height*.5;
        static (double X,double Y) Unit(double degrees)
        {
            double radians=degrees%360*(Math.PI/180),x=Math.Cos(radians),y=Math.Sin(radians);
            if(Math.Abs(x)<1e-7)x=0;if(Math.Abs(y)<1e-7)y=0;return(x,y);
        }
        SKPoint Point((double X,double Y) p)=>new((float)(cx+rx*p.X),(float)(cy+ry*p.Y));
        double angle=startAngle%360,sweep=sweepAngle%360;var start=Point(Unit(angle));
        if(forceMoveTo||segments.Count==0)MoveTo(start);else if(CurrentPoint()!=new PointF(start.X,start.Y))LineTo(start);
        int count=(int)Math.Ceiling(Math.Abs(sweep)/90);
        for(int i=0;i<count;i++)
        {
            double step=Math.CopySign(Math.Min(90,Math.Abs(sweep)),sweep),weight=Math.Cos(step*Math.PI/360);
            var unit=Unit(angle+step*.5);var control=Point((unit.X/weight,unit.Y/weight));angle+=step;
            ConicTo(control,Point(Unit(angle)),(float)weight);sweep-=step;
        }
    }
    private static void ValidateArc(SKRect oval,float startAngle,float sweepAngle)
    {if(!oval.ToCore().IsFinite||!float.IsFinite(startAngle)||!float.IsFinite(sweepAngle))throw new ArgumentException("Arc coordinates must be finite.");}
    public void ArcTo(float x1,float y1,float x2,float y2,float radius)=>ArcTo(new(x1,y1),new(x2,y2),radius);
    public void ArcTo(SKPoint point1,SKPoint point2,float radius)
    {
        ThrowIfDisposed();if(!float.IsFinite(radius)||!float.IsFinite(point1.X)||!float.IsFinite(point1.Y)||!float.IsFinite(point2.X)||!float.IsFinite(point2.Y))throw new ArgumentException("Tangent arc coordinates must be finite.");
        var p=CurrentPoint();double ux=(double)p.X-point1.X,uy=(double)p.Y-point1.Y,vx=(double)point2.X-point1.X,vy=(double)point2.Y-point1.Y;
        double uLength=Math.Sqrt(ux*ux+uy*uy),vLength=Math.Sqrt(vx*vx+vy*vy);
        radius=MathF.Abs(radius);
        if(radius==0||uLength==0||vLength==0){LineTo(point1);return;}
        ux/=uLength;uy/=uLength;vx/=vLength;vy/=vLength;double cosine=Math.Clamp(ux*vx+uy*vy,-1,1);
        if(Math.Abs(ux*vy-uy*vx)<1e-6){LineTo(point1);return;}
        double distance=radius*Math.Sqrt((1+cosine)/(1-cosine));
        LineTo((float)(point1.X+ux*distance),(float)(point1.Y+uy*distance));
        ConicTo(point1,new((float)(point1.X+vx*distance),(float)(point1.Y+vy*distance)),(float)Math.Sqrt((1-cosine)*.5));
    }
}
