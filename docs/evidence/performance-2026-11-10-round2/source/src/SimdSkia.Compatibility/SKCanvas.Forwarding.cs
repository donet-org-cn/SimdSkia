#pragma warning disable CS0618
namespace SkiaSharp;
public partial class SKCanvas
{
    public void DrawText(string text,SKPoint p,SKFont font,SKPaint paint)=>DrawText(text,p.X,p.Y,font,paint);
    public void DrawText(string text,SKPoint p,SKTextAlign textAlign,SKFont font,SKPaint paint)=>DrawText(text,p.X,p.Y,textAlign,font,paint);
    [Obsolete("Use DrawText(string text, float x, float y, SKTextAlign textAlign, SKFont font, SKPaint paint) instead.")]
    public void DrawText(string text,float x,float y,SKPaint paint){ArgumentNullException.ThrowIfNull(paint);DrawText(text,x,y,paint.TextAlign,paint.GetFont(),paint);}
    [Obsolete("Use DrawText(string text, SKPoint p, SKTextAlign textAlign, SKFont font, SKPaint paint) instead.")]
    public void DrawText(string text,SKPoint p,SKPaint paint){ArgumentNullException.ThrowIfNull(paint);DrawText(text,p.X,p.Y,paint.TextAlign,paint.GetFont(),paint);}
    public void DrawSurface(SKSurface surface,float x,float y,SKPaint? paint=null){ArgumentNullException.ThrowIfNull(surface);surface.Draw(this,x,y,paint!);}
    public void DrawSurface(SKSurface surface,SKPoint p,SKPaint? paint=null)=>DrawSurface(surface,p.X,p.Y,paint);
    public void DrawBitmap(SKBitmap bitmap,SKPoint p,SKPaint? paint=null)=>DrawBitmap(bitmap,p.X,p.Y,paint);
    public void DrawImage(SKImage image,SKPoint p,SKPaint? paint=null)=>DrawImage(image,p.X,p.Y,paint);
    public void DrawImage(SKImage image,SKPoint p,SKSamplingOptions sampling,SKPaint? paint=null)=>DrawImage(image,p.X,p.Y,sampling,paint);
}
