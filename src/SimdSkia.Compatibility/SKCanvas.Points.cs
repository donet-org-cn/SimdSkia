using SimdSkia;
namespace SkiaSharp;
public partial class SKCanvas
{
    public void DrawPoint(SKPoint p,SKPaint paint)=>DrawPoint(p.X,p.Y,paint);
    public void DrawPoint(float x,float y,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(paint);paint.ThrowIfDisposed();if(!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentException("Point coordinates must be finite.");
        float width=paint.StrokeWidth;var transform=matrix;var shaderTransform=matrix;
        if(width==0){var mapped=matrix.Map(x,y);x=mapped.X;y=mapped.Y;width=1;transform=Matrix3.Identity;}
        float radius=width*.5f;var p=paint.ToCore(true);
        if(paint.StrokeCap!=SKStrokeCap.Round)
        {Execute(new DrawingCommand(CommandKind.FillRectangle,new(x-radius,y-radius,x+radius,y+radius),transform,p){ShaderTransform=shaderTransform},paint);return;}
        using var path=new SKPath();path.AddOval(new(x-radius,y-radius,x+radius,y+radius));Execute(new DrawingCommand(CommandKind.FillPath,default,transform,p){Path=path.ToCore(),ShaderTransform=shaderTransform},paint);
    }
    public void DrawPoint(SKPoint p,SKColor color)=>DrawPoint(p.X,p.Y,color);
    public void DrawPoint(float x,float y,SKColor color){using var paint=new SKPaint{Color=color,BlendMode=SKBlendMode.Src};DrawPoint(x,y,paint);}
    public void DrawPoints(SKPointMode mode,SKPoint[] points,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(paint);ArgumentNullException.ThrowIfNull(points);if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));paint.ThrowIfDisposed();foreach(var point in points)if(!float.IsFinite(point.X)||!float.IsFinite(point.Y))throw new ArgumentException("Point coordinates must be finite.",nameof(points));
        if(mode==SKPointMode.Points){foreach(var point in points)DrawPoint(point,paint);return;}
        int step=mode==SKPointMode.Lines?2:1;for(int i=0;i+1<points.Length;i+=step)DrawLine(points[i],points[i+1],paint);
    }
}
