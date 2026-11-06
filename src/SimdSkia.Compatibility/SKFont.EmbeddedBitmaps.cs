namespace SkiaSharp;
public partial class SKFont
{
    private bool embeddedBitmaps;
    public bool EmbeddedBitmaps
    {get{ThrowIfDisposed();return embeddedBitmaps;}set{ThrowIfDisposed();if(embeddedBitmaps!=value){embeddedBitmaps=value;ClearLayouts();}}}
    private SimdSkia.PathGeometry RenderOutline(ushort glyph)
        =>embeddedBitmaps&&skewX==0&&font.TryEmbeddedOutline(glyph,MathF.Abs(size*scaleX),size,out var bitmap)?bitmap:GlyphOutline(glyph);
}
public partial class SKPaint
{
    [Obsolete("Use SKFont.EmbeddedBitmaps instead.")]
    public bool IsEmbeddedBitmapText {get=>GetFont().EmbeddedBitmaps;set=>GetFont().EmbeddedBitmaps=value;}
}
