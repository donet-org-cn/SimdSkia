namespace SkiaSharp;
public partial class SKPaint
{
    private bool lcdRenderText;
    [Obsolete("Use SKFont.Edging instead.")]
    public bool LcdRenderText
    {get{ThrowIfDisposed();return lcdRenderText;}set{ThrowIfDisposed();lcdRenderText=value;var font=GetFont();font.Edging=antialias?(value?SKFontEdging.SubpixelAntialias:SKFontEdging.Antialias):SKFontEdging.Alias;}}
}
