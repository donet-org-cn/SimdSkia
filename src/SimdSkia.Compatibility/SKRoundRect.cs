namespace SkiaSharp;

/// <summary>A managed rounded rectangle with independently scaled corner radii.</summary>
public class SKRoundRect : SKObject
{
    private SKRect rect;
    private readonly SKPoint[] radii=new SKPoint[4];
    public SKRoundRect() { }
    public SKRoundRect(SKRect rect)=>SetRect(rect);
    public SKRoundRect(SKRect rect,float radius):this(rect,radius,radius) { }
    public SKRoundRect(SKRect rect,float xRadius,float yRadius)=>SetRect(rect,xRadius,yRadius);
    public SKRoundRect(SKRoundRect rrect) { ArgumentNullException.ThrowIfNull(rrect); SetRectRadii(rrect.Rect,rrect.Radii); }
    public SKRect Rect {get{ThrowIfDisposed();return rect;}}
    public float Width=>Rect.Width;
    public float Height=>Rect.Height;
    public SKPoint[] Radii {get{ThrowIfDisposed();return (SKPoint[])radii.Clone();}}
    public SKPoint GetRadii(SKRoundRectCorner corner) {ThrowIfDisposed();if((uint)corner>3)throw new ArgumentOutOfRangeException(nameof(corner));return radii[(int)corner];}
    public bool IsValid {get{ThrowIfDisposed();return rect.ToCore().IsFinite&&float.IsFinite(Width)&&float.IsFinite(Height)&&Width>=0&&Height>=0&&radii.All(r=>float.IsFinite(r.X)&&float.IsFinite(r.Y)&&r.X>=0&&r.Y>=0&&r.X<=Width&&r.Y<=Height);}}
    public SKRoundRectType Type
    {
        get
        {
            ThrowIfDisposed();if(rect.Width<=0||rect.Height<=0)return SKRoundRectType.Empty;
            if(radii.All(r=>r.IsEmpty))return SKRoundRectType.Rect;
            if(radii.All(r=>r==radii[0]))return radii[0].X==Width/2&&radii[0].Y==Height/2?SKRoundRectType.Oval:SKRoundRectType.Simple;
            return radii[0].X==radii[3].X&&radii[1].X==radii[2].X&&radii[0].Y==radii[1].Y&&radii[2].Y==radii[3].Y?SKRoundRectType.NinePatch:SKRoundRectType.Complex;
        }
    }
    public bool AllCornersCircular=>CheckAllCornersCircular(1f/4096);
    public bool CheckAllCornersCircular(float tolerance) {ThrowIfDisposed();return radii.All(r=>MathF.Abs(r.X-r.Y)<=tolerance);}
    public void SetEmpty() {ThrowIfDisposed();rect=default;Array.Clear(radii);}
    public void SetRect(SKRect rect)=>SetRect(rect,0,0);
    public void SetRect(SKRect rect,float xRadius,float yRadius)=>SetRectRadii(rect,new SKPoint[]{new(xRadius,yRadius),new(xRadius,yRadius),new(xRadius,yRadius),new(xRadius,yRadius)});
    public void SetOval(SKRect rect)=>SetRect(rect,MathF.Abs(rect.Width)/2,MathF.Abs(rect.Height)/2);
    public void SetNinePatch(SKRect rect,float leftRadius,float topRadius,float rightRadius,float bottomRadius)=>SetRectRadii(rect,new SKPoint[]{new(leftRadius,topRadius),new(rightRadius,topRadius),new(rightRadius,bottomRadius),new(leftRadius,bottomRadius)});
    public void SetRectRadii(SKRect rect,SKPoint[] radii) {ArgumentNullException.ThrowIfNull(radii);SetRectRadii(rect,radii.AsSpan());}
    public void SetRectRadii(SKRect rect,ReadOnlySpan<SKPoint> radii)
    {
        ThrowIfDisposed();if(radii.Length!=4)throw new ArgumentException("Radii must have a length of 4.",nameof(radii));
        if(!rect.ToCore().IsFinite){SetEmpty();return;}
        this.rect=new(MathF.Min(rect.Left,rect.Right),MathF.Min(rect.Top,rect.Bottom),MathF.Max(rect.Left,rect.Right),MathF.Max(rect.Top,rect.Bottom));
        if(Width<=0||Height<=0||!float.IsFinite(Width)||!float.IsFinite(Height)){SetEmpty();return;}
        for(int i=0;i<4;i++){var r=radii[i];this.radii[i]=r.X>0&&r.Y>0&&float.IsFinite(r.X)&&float.IsFinite(r.Y)?r:default;}
        double scale=1;
        void Fit(double sum,double extent){if(sum>extent)scale=Math.Min(scale,extent/sum);}
        Fit((double)this.radii[0].X+this.radii[1].X,Width);Fit((double)this.radii[3].X+this.radii[2].X,Width);
        Fit((double)this.radii[0].Y+this.radii[3].Y,Height);Fit((double)this.radii[1].Y+this.radii[2].Y,Height);
        if(scale<1)for(int i=0;i<4;i++)this.radii[i]=new(this.radii[i].X*(float)scale,this.radii[i].Y*(float)scale);
    }
    public void Offset(float dx,float dy){ThrowIfDisposed();var next=rect;next.Offset(dx,dy);SetRectRadii(next,radii);}
    public void Offset(SKPoint pos)=>Offset(pos.X,pos.Y);
    public void Inflate(float dx,float dy)=>Deflate(-dx,-dy);
    public void Inflate(SKSize size)=>Inflate(size.Width,size.Height);
    public void Deflate(SKSize size)=>Deflate(size.Width,size.Height);
    public void Deflate(float dx,float dy)
    {
        ThrowIfDisposed();var next=rect;next.Inflate(-dx,-dy);
        if(next.Width<=0||next.Height<=0){SetEmpty();return;}
        var updated=Radii;for(int i=0;i<4;i++)if(!updated[i].IsEmpty)updated[i]=new(Math.Max(0,updated[i].X-dx),Math.Max(0,updated[i].Y-dy));SetRectRadii(next,updated);
    }
    public bool Contains(SKRect rect)
    {
        ThrowIfDisposed();if(rect.Width<=0||rect.Height<=0||rect.Left<this.rect.Left||rect.Top<this.rect.Top||rect.Right>this.rect.Right||rect.Bottom>this.rect.Bottom)return false;
        bool Point(float x,float y)
        {
            for(int i=0;i<4;i++)
            {
                var r=radii[i];if(r.IsEmpty)continue;bool left=i is 0 or 3,top=i<2;
                float cx=left?this.rect.Left+r.X:this.rect.Right-r.X,cy=top?this.rect.Top+r.Y:this.rect.Bottom-r.Y;
                if((left?x<cx:x>cx)&&(top?y<cy:y>cy)){double dx=(x-cx)/r.X,dy=(y-cy)/r.Y;if(dx*dx+dy*dy>1)return false;}
            }return true;
        }
        return Point(rect.Left,rect.Top)&&Point(rect.Right,rect.Top)&&Point(rect.Right,rect.Bottom)&&Point(rect.Left,rect.Bottom);
    }
    public bool TryTransform(SKMatrix matrix,out SKRoundRect? transformed)
    {
        ThrowIfDisposed();transformed=null;var m=matrix.ToCore();
        if(!m.IsFinite||!m.IsAffine||!((m.M01==0&&m.M10==0)||(m.M00==0&&m.M11==0)))return false;
        var points=new[]{m.Map(rect.Left,rect.Top),m.Map(rect.Right,rect.Top),m.Map(rect.Right,rect.Bottom),m.Map(rect.Left,rect.Bottom)};
        var bounds=new SKRect(points.Min(p=>p.X),points.Min(p=>p.Y),points.Max(p=>p.X),points.Max(p=>p.Y));var updated=new SKPoint[4];
        for(int i=0;i<4;i++){var p=points[i];int corner=p.Y==bounds.Top?(p.X==bounds.Left?0:1):(p.X==bounds.Left?3:2);updated[corner]=new(MathF.Abs(m.M00)*radii[i].X+MathF.Abs(m.M01)*radii[i].Y,MathF.Abs(m.M10)*radii[i].X+MathF.Abs(m.M11)*radii[i].Y);}
        transformed=new();transformed.SetRectRadii(bounds,updated);return true;
    }
    public SKRoundRect? Transform(SKMatrix matrix)=>TryTransform(matrix,out var transformed)?transformed:null;
    internal SKPath ToPath(SKPathDirection direction=SKPathDirection.Clockwise)
    {
        ThrowIfDisposed();if(!Enum.IsDefined(direction))throw new ArgumentOutOfRangeException(nameof(direction));var path=new SKPath();
        if(Type==SKRoundRectType.Empty)return path;
        const float k=.5522847498307936f;var r=radii;float l=rect.Left,t=rect.Top,b=rect.Bottom,right=rect.Right;
        // Build clockwise then reverse segment endpoints and cubic controls when requested.
        var geometry=new List<(SKPoint Start,SKPoint C1,SKPoint C2,SKPoint End,bool Curve)>();SKPoint at=new(l+r[0].X,t),first=at;
        void Line(SKPoint to){geometry.Add((at,default,default,to,false));at=to;}
        void Curve(SKPoint c1,SKPoint c2,SKPoint to){geometry.Add((at,c1,c2,to,true));at=to;}
        Line(new(right-r[1].X,t));Curve(new(right-r[1].X*(1-k),t),new(right,t+r[1].Y*(1-k)),new(right,t+r[1].Y));
        Line(new(right,b-r[2].Y));Curve(new(right,b-r[2].Y*(1-k)),new(right-r[2].X*(1-k),b),new(right-r[2].X,b));
        Line(new(l+r[3].X,b));Curve(new(l+r[3].X*(1-k),b),new(l,b-r[3].Y*(1-k)),new(l,b-r[3].Y));
        Line(new(l,t+r[0].Y));Curve(new(l,t+r[0].Y*(1-k)),new(l+r[0].X*(1-k),t),first);
        path.MoveTo(first);
        if(direction==SKPathDirection.Clockwise)foreach(var e in geometry){if(e.Curve)path.CubicTo(e.C1,e.C2,e.End);else path.LineTo(e.End);}
        else for(int i=geometry.Count-1;i>=0;i--){var e=geometry[i];if(e.Curve)path.CubicTo(e.C2,e.C1,e.Start);else path.LineTo(e.Start);}
        path.Close();return path;
    }
}
