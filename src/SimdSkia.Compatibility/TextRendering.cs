using SimdSkia.Text;
using SkiaSharp;

namespace SimdSkia;

public static class TextRendering
{
    public static TextLayoutResult DrawTextBlock(SKCanvas canvas,string text,SKFont font,SKPaint paint,SKRect bounds,SKTextAlign alignment=SKTextAlign.Left,float lineSpacing=1.2f)
    {
        ArgumentNullException.ThrowIfNull(canvas);ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(paint);
        if(!Enum.IsDefined(alignment))throw new ArgumentOutOfRangeException(nameof(alignment));
        var layout=SimpleTextLayout.Create(font.FontData,text,font.Size,bounds.Width,lineSpacing,font.ScaleX);
        int saved=canvas.Save();
        try
        {
            canvas.ClipRect(bounds);
            foreach(var line in layout.Lines)
            {
                float x=alignment==SKTextAlign.Center?bounds.MidX:alignment==SKTextAlign.Right?bounds.Right:bounds.Left;
                canvas.DrawText(line.Text,x,bounds.Top+line.Baseline,alignment,font,paint);
            }
        }
        finally{canvas.RestoreToCount(saved);}
        return layout;
    }
}
