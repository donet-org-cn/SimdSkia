using SimdSkia;

namespace SkiaSharp;

public partial class SKCanvas
{
    private Dictionary<SKRect,PathGeometry>? ovals;
    private Dictionary<(SKRect,float,float,bool),PathGeometry>? arcs;
    public void DrawOval(SKPoint c,SKSize r,SKPaint paint)=>DrawOval(c.X,c.Y,r.Width,r.Height,paint);
    public void DrawOval(float cx,float cy,float rx,float ry,SKPaint paint)=>DrawOval(new(cx-rx,cy-ry,cx+rx,cy+ry),paint);
    public void DrawOval(SKRect rect,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(paint);if(rect.IsEmpty)return;
        ovals??=[];if(!ovals.TryGetValue(rect,out var path))
        {using var p=new SKPath();p.AddOval(rect);path=p.ToCore();if(ovals.Count>=64)ovals.Clear();ovals.Add(rect,path);}
        DrawGeometry(path,paint);
    }
    public void DrawArc(SKRect oval,float startAngle,float sweepAngle,bool useCenter,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(paint);if(oval.IsEmpty||sweepAngle==0)return;
        var key=(oval,startAngle,sweepAngle,useCenter);arcs??=[];
        if(!arcs.TryGetValue(key,out var geometry))
        {
            using var p=new SKPath();
            if(MathF.Abs(sweepAngle)>=360)p.AddOval(oval);
            else{if(useCenter)p.MoveTo(oval.MidX,oval.MidY);p.ArcTo(oval,startAngle,sweepAngle,!useCenter);if(useCenter)p.Close();}
            geometry=p.ToCore();if(arcs.Count>=64)arcs.Clear();arcs.Add(key,geometry);
        }
        DrawGeometry(geometry,paint);
    }
}
