using System.Buffers;
using System.Globalization;
using System.Text;
using SimdSkia;
using SimdSkia.Text;

namespace SkiaSharp;

public enum SKFontHinting { None=0,Slight=1,Normal=2,Full=3 }
public enum SKFontEdging { Alias=0,Antialias=1,SubpixelAntialias=2 }
public enum SKTextAlign { Left=0,Center=1,Right=2 }

public class SKTypeface : SKObject
{
    private TrueTypeFont? data;
    internal TrueTypeFont Font=>data??throw new ObjectDisposedException(nameof(SKTypeface));
    private SKTypeface(TrueTypeFont font)=>data=font;
    protected override void Dispose(bool disposing)=>data=null;
    public static SKTypeface FromFile(string path,int index=0)
    {ArgumentNullException.ThrowIfNull(path);if(index!=0)throw new NotSupportedException("Font collection indexes are not supported.");return new(new(File.ReadAllBytes(path)));}
    public static SKTypeface FromStream(Stream stream,int index=0)
    {ArgumentNullException.ThrowIfNull(stream);if(index!=0)throw new NotSupportedException("Font collection indexes are not supported.");using var copy=new MemoryStream();stream.CopyTo(copy);return new(new(copy.ToArray()));}
    public static SKTypeface Default
    {
        get
        {
            string[] candidates=[Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),"/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf","/System/Library/Fonts/Supplemental/Arial.ttf"];
            foreach(string path in candidates)if(File.Exists(path))return FromFile(path);
            throw new FileNotFoundException("No default TrueType font was found. Supply SKTypeface.FromFile explicitly.");
        }
    }
    public int GlyphCount {get{ThrowIfDisposed();return Font.GlyphCount;}}
    public int UnitsPerEm {get{ThrowIfDisposed();return Font.UnitsPerEm;}}
    public ushort GetGlyph(int codepoint){ThrowIfDisposed();return Font.GetGlyph(codepoint);}
}

public class SKFont : SKObject
{
    private SKTypeface typeface;
    private TrueTypeFont font;
    private float size,scaleX,skewX;
    private readonly Dictionary<(string Text,float X,float Y,SKTextAlign Align),PathGeometry> layouts=[];
    private long layoutBytes;
    private SKFontHinting hinting;
    private SKFontEdging edging=SKFontEdging.Antialias;
    internal TrueTypeFont FontData {get{ThrowIfDisposed();return font;}}
    public SKFont():this(SKTypeface.Default){ }
    public SKFont(SKTypeface typeface,float size=12,float scaleX=1,float skewX=0)
    {ArgumentNullException.ThrowIfNull(typeface);typeface.ThrowIfDisposed();this.typeface=typeface;font=typeface.Font;Size=size;ScaleX=scaleX;SkewX=skewX;}
    public float Size {get{ThrowIfDisposed();return size;}set{ThrowIfDisposed();if(!float.IsFinite(value)||value<0)throw new ArgumentOutOfRangeException(nameof(value));if(size!=value){size=value;ClearLayouts();}}}
    public float ScaleX {get{ThrowIfDisposed();return scaleX;}set{ThrowIfDisposed();if(!float.IsFinite(value))throw new ArgumentOutOfRangeException(nameof(value));if(scaleX!=value){scaleX=value;ClearLayouts();}}}
    public float SkewX {get{ThrowIfDisposed();return skewX;}set{ThrowIfDisposed();if(!float.IsFinite(value))throw new ArgumentOutOfRangeException(nameof(value));if(skewX!=value){skewX=value;ClearLayouts();}}}
    public SKTypeface Typeface {get{ThrowIfDisposed();return typeface;}set{ThrowIfDisposed();throw new NotSupportedException("Construct a new font to select a different typeface.");}}
    public SKFontHinting Hinting {get{ThrowIfDisposed();return hinting;}set{ThrowIfDisposed();if(value!=SKFontHinting.None)throw new NotSupportedException("TrueType hint instruction execution is not implemented; use None.");hinting=value;}}
    public SKFontEdging Edging {get{ThrowIfDisposed();return edging;}set{ThrowIfDisposed();if(value is not (SKFontEdging.Alias or SKFontEdging.Antialias))throw new NotSupportedException("LCD subpixel edging is not implemented.");edging=value;}}
    public bool Subpixel {get{ThrowIfDisposed();return true;}set{ThrowIfDisposed();if(!value)throw new NotSupportedException("Grid-fitted glyph positioning is not supported; use Subpixel=true.");}}
    public bool BaselineSnap {get{ThrowIfDisposed();return false;}set{ThrowIfDisposed();if(value)throw new NotSupportedException("Baseline grid snapping is not implemented; use BaselineSnap=false.");}}
    public bool LinearMetrics {get{ThrowIfDisposed();return true;}set{ThrowIfDisposed();if(!value)throw new NotSupportedException("Grid-fitted metrics are not implemented; use LinearMetrics=true.");}}
    public bool Embolden {get{ThrowIfDisposed();return false;}set{ThrowIfDisposed();if(value)throw new NotSupportedException("Synthetic font emboldening is not implemented.");}}
    public bool ForceAutoHinting {get{ThrowIfDisposed();return false;}set{ThrowIfDisposed();if(value)throw new NotSupportedException("Auto-hinting is not implemented.");}}
    public ushort GetGlyph(int codepoint){ThrowIfDisposed();return font.GetGlyph(codepoint);}
    public SKPath GetGlyphPath(ushort glyph){ThrowIfDisposed();return new(font.Outline(glyph).Transform(GlyphTransform(0,0)));}
    private Matrix3 GlyphTransform(float x,float y){float factor=size/font.UnitsPerEm;return new(factor*scaleX,-factor*skewX,x,0,-factor,y,0,0,1);}
    internal FontSnapshot Capture(){ThrowIfDisposed();return new(font,GlyphTransform(0,0),size/font.UnitsPerEm*scaleX,edging==SKFontEdging.Antialias);}
    public ushort[] GetGlyphs(ReadOnlySpan<int> codepoints)
    {var result=new ushort[codepoints.Length];GetGlyphs(codepoints,result);return result;}
    public void GetGlyphs(ReadOnlySpan<int> codepoints,Span<ushort> glyphs)
    {ThrowIfDisposed();if(glyphs.Length<codepoints.Length)throw new ArgumentException("Insufficient glyph buffer.",nameof(glyphs));for(int i=0;i<codepoints.Length;i++)glyphs[i]=font.GetGlyph(codepoints[i]);}
    public void GetGlyphWidths(ReadOnlySpan<ushort> glyphs,Span<float> widths,Span<SKRect> bounds,SKPaint? paint=null)
    {
        ThrowIfDisposed();if((!widths.IsEmpty&&widths.Length<glyphs.Length)||(!bounds.IsEmpty&&bounds.Length<glyphs.Length))throw new ArgumentException("Insufficient glyph metrics buffer.");
        if(paint is not null){paint.ThrowIfDisposed();if(paint.Style!=SKPaintStyle.Fill||paint.Shader is not null)throw new NotSupportedException("Glyph metrics with stroke or shader effects require separate processing.");}
        var snapshot=Capture();
        for(int i=0;i<glyphs.Length;i++)
        {
            if(!widths.IsEmpty)widths[i]=font.Advance(glyphs[i])*snapshot.AdvanceScale;
            if(!bounds.IsEmpty)bounds[i]=snapshot.Bounds(glyphs[i]);
        }
    }
    internal PathGeometry Layout(string text,float x,float y,SKTextAlign align)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(text);if(!Enum.IsDefined(align))throw new ArgumentOutOfRangeException(nameof(align));
        if(!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentException("Text origin must be finite.");
        var key=(text,x,y,align);if(layouts.TryGetValue(key,out var cached))return cached;
        var glyphIds=Glyphs(text);float width=glyphIds.Sum(g=>(float)font.Advance(g))*size/font.UnitsPerEm*scaleX;
        if(align==SKTextAlign.Center)x-=width*0.5f;else if(align==SKTextAlign.Right)x-=width;
        var segments=new List<PathSegment>();
        foreach(ushort glyph in glyphIds){segments.AddRange(font.Outline(glyph).Transform(GlyphTransform(x,y)).Segments.ToArray());x+=font.Advance(glyph)*size/font.UnitsPerEm*scaleX;}
        var geometry=new PathGeometry(segments);
        long bytes=(long)geometry.Segments.Length*32+(long)text.Length*2;
        if(bytes<=4*1024*1024)
        {if(layouts.Count>=64||layoutBytes+bytes>4*1024*1024)ClearLayouts();layouts.Add(key,geometry);layoutBytes+=bytes;}
        return geometry;
    }
    private List<ushort> Glyphs(string text)
    {
        var glyphs=new List<ushort>();ReadOnlySpan<char> remaining=text;
        while(!remaining.IsEmpty)
        {
            if(Rune.DecodeFromUtf16(remaining,out var rune,out int consumed)!=OperationStatus.Done)throw new ArgumentException("Text contains malformed UTF-16.",nameof(text));
            int value=rune.Value;var category=Rune.GetUnicodeCategory(rune);
            if(value is >=0x0590 and <=0x1cff||category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark||category==UnicodeCategory.Format)
                throw new NotSupportedException("This text needs complex-script/mark shaping; simple scalar-to-glyph layout cannot render it correctly.");
            if(value=='\n'||value=='\r'||value=='\t')throw new NotSupportedException("Use separate positioned text lines; multiline/tab layout is not implemented.");
            glyphs.Add(font.GetGlyph(value));remaining=remaining[consumed..];
        }
        return glyphs;
    }
    public float MeasureText(string text,SKPaint? paint=null)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(text);if(paint is not null&&paint.Style!=SKPaintStyle.Fill)throw new NotSupportedException("Stroked text measurement is not implemented.");return Glyphs(text).Sum(g=>(float)font.Advance(g))*size/font.UnitsPerEm*scaleX;}
    private void ClearLayouts(){layouts.Clear();layoutBytes=0;}
    protected override void Dispose(bool disposing){ClearLayouts();font=null!;typeface=null!;}
}

internal sealed record FontSnapshot(TrueTypeFont Font,Matrix3 Transform,float AdvanceScale,bool Antialias)
{
    internal SKRect Bounds(ushort glyph)
    {
        var tight=Font.Outline(glyph).GetTightBounds(Transform);
        return tight.IsEmpty?SKRect.Empty:new(MathF.Floor(tight.Left),MathF.Floor(tight.Top),MathF.Ceiling(tight.Right),MathF.Ceiling(tight.Bottom));
    }
    internal SKRect GlobalBounds()
    {
        var bounds=Font.FontBounds;
        if(bounds.IsEmpty)return SKRect.Empty;
        var a=Transform.Map(bounds.Left,bounds.Top);var b=Transform.Map(bounds.Right,bounds.Top);
        var c=Transform.Map(bounds.Left,bounds.Bottom);var d=Transform.Map(bounds.Right,bounds.Bottom);
        return new(MathF.Min(MathF.Min(a.X,b.X),MathF.Min(c.X,d.X)),MathF.Min(MathF.Min(a.Y,b.Y),MathF.Min(c.Y,d.Y)),
            MathF.Max(MathF.Max(a.X,b.X),MathF.Max(c.X,d.X)),MathF.Max(MathF.Max(a.Y,b.Y),MathF.Max(c.Y,d.Y)));
    }
}
