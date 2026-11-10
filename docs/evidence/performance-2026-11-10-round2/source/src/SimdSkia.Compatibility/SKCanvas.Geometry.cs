using SimdSkia;

namespace SkiaSharp;

public partial class SKCanvas
{
    private Dictionary<(PointF,PointF),PathGeometry>? lines;
    private Dictionary<(float,float,float),PathGeometry>? circles;
    private void DrawCachedCircle(float cx,float cy,float radius,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(paint);
        if(!float.IsFinite(cx)||!float.IsFinite(cy))throw new ArgumentException("Circle coordinates must be finite.");
        var key=(cx,cy,radius);
        circles??=[];
        if(!circles.TryGetValue(key,out var geometry))
        {
            using var path=new SKPath();path.AddCircle(cx,cy,radius);geometry=path.ToCore();
            if(circles.Count>=64)circles.Clear();circles.Add(key,geometry);
        }
        DrawGeometry(geometry,paint);
    }
    public void RotateDegrees(float degrees,float px,float py){var m=SKMatrix.CreateRotationDegrees(degrees,px,py);Concat(in m);}
    public void RotateRadians(float radians,float px,float py){var m=SKMatrix.CreateRotation(radians,px,py);Concat(in m);}
    public void Scale(float sx,float sy,float px,float py){var m=SKMatrix.CreateScale(sx,sy,px,py);Concat(in m);}
    public void Skew(float sx,float sy){var m=SKMatrix.CreateSkew(sx,sy);Concat(in m);}
    public void Skew(SKPoint skew)=>Skew(skew.X,skew.Y);
}
