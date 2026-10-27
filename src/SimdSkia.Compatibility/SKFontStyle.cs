namespace SkiaSharp;

public class SKFontStyle : SKObject
{
    private readonly int weight,width;
    private readonly SKFontStyleSlant slant;
    public SKFontStyle():this(400,5,SKFontStyleSlant.Upright) { }
    public SKFontStyle(SKFontStyleWeight weight,SKFontStyleWidth width,SKFontStyleSlant slant):this((int)weight,(int)width,slant) { }
    public SKFontStyle(int weight,int width,SKFontStyleSlant slant){this.weight=Math.Clamp(weight,0,1000);this.width=Math.Clamp(width,1,9);this.slant=slant;}
    public int Weight {get{ThrowIfDisposed();return weight;}}
    public int Width {get{ThrowIfDisposed();return width;}}
    public SKFontStyleSlant Slant {get{ThrowIfDisposed();return slant;}}
    public static SKFontStyle Normal=>new();
    public static SKFontStyle Bold=>new(700,5,SKFontStyleSlant.Upright);
    public static SKFontStyle Italic=>new(400,5,SKFontStyleSlant.Italic);
    public static SKFontStyle BoldItalic=>new(700,5,SKFontStyleSlant.Italic);
}
