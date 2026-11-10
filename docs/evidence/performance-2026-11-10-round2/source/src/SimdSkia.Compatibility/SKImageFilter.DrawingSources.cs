using SimdSkia;
using SimdSkia.Cpu;
namespace SkiaSharp;
public partial class SKImageFilter
{
    [Obsolete("Use CreateShader(SKShader) instead.",true)]
    public static SKImageFilter CreatePaint(SKPaint paint)=>PaintSource(paint,null);
    [Obsolete("Use CreateShader(SKShader, bool, SKRect) instead.",true)]
    public static SKImageFilter CreatePaint(SKPaint paint,SKRect cropRect)=>PaintSource(paint,cropRect);
    private static SKImageFilter PaintSource(SKPaint paint,SKRect? crop)
    {
        ArgumentNullException.ThrowIfNull(paint);paint.ThrowIfDisposed();ValidateDrawingCrop(crop);
        // The baseline obsolete wrapper forwards only Shader and IsDither.
        // It deliberately does not retain the paint color, alpha or effects.
        var shader=paint.RetainedShader;
        return new(shader is null?RasterImageFilter.DrawingSource((_,_)=>new FilterFrame(null)):RasterImageFilter.Shader(shader,false,crop?.ToCore()),paint.RetainShaderResources());
    }
    public static SKImageFilter CreatePicture(SKPicture picture)
    {ArgumentNullException.ThrowIfNull(picture);return CreatePicture(picture,picture.CullRect);}
    public static SKImageFilter CreatePicture(SKPicture picture,SKRect cropRect)
    {
        ArgumentNullException.ThrowIfNull(picture);ValidateDrawingCrop(cropRect);var bounds=picture.CullRect;cropRect=new(MathF.Max(bounds.Left,cropRect.Left),MathF.Max(bounds.Top,cropRect.Top),MathF.Min(bounds.Right,cropRect.Right),MathF.Min(bounds.Bottom,cropRect.Bottom));var captured=picture.Share();
        try{return new(RasterImageFilter.DrawingSource((m,clip)=>RenderDrawing(m,cropRect,clip,c=>captured.Playback(c))),new ShaderResources(captured));}
        catch{captured.Dispose();throw;}
    }
    private static void ValidateDrawingCrop(SKRect? crop)
    {if(crop is {} b&&!b.ToCore().IsFinite)throw new ArgumentException("Drawing source bounds must be finite.",nameof(crop));}
    private static FilterFrame RenderDrawing(Matrix3 matrix,SKRect? crop,RectF clip,Action<SKCanvas> draw)
    {
        RectF output=clip;
        if(crop is {} bounds)
        {
            var a=matrix.Map(bounds.Left,bounds.Top);var b=matrix.Map(bounds.Right,bounds.Top);var c=matrix.Map(bounds.Left,bounds.Bottom);var d=matrix.Map(bounds.Right,bounds.Bottom);
            output=new(MathF.Max(clip.Left,MathF.Min(MathF.Min(a.X,b.X),MathF.Min(c.X,d.X))),MathF.Max(clip.Top,MathF.Min(MathF.Min(a.Y,b.Y),MathF.Min(c.Y,d.Y))),MathF.Min(clip.Right,MathF.Max(MathF.Max(a.X,b.X),MathF.Max(c.X,d.X))),MathF.Min(clip.Bottom,MathF.Max(MathF.Max(a.Y,b.Y),MathF.Max(c.Y,d.Y))));
        }
        var frame=FilterFrame.Rent(output);
        try{if(frame.Pixels is {} pixels){using var canvas=new SKCanvas(pixels);var shift=new Matrix3(1,0,-frame.Left,0,1,-frame.Top,0,0,1);var transform=SKMatrix.FromCore(shift*matrix);canvas.SetMatrix(in transform);if(crop is {} local)canvas.ClipRect(local);draw(canvas);}return frame;}
        catch{frame.Dispose();throw;}
    }
}
