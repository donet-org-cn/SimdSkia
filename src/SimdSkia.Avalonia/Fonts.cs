using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using SimdSkia.Text;

namespace SimdSkia.Avalonia;

internal sealed class FontData
{
    internal TrueTypeFont Font {get;}
    internal string Family {get;}
    internal FontWeight Weight {get;}
    internal FontStyle Style {get;}
    internal FontStretch Stretch {get;}
    internal byte[] Digest {get;}
    internal FontData(ReadOnlySpan<byte> data,int faceIndex=0)
    {
        Font=new(data,faceIndex);using(var stream=Font.OpenStream())Digest=SHA256.HashData(stream);Family=ReadName(Font,16)??ReadName(Font,1)??throw new InvalidDataException("The font has no family name.");
        Weight=FontWeight.Normal;Stretch=FontStretch.Normal;
        if(Font.TryGetTable("OS/2",out var table)&&table.Length>=8)
        {Weight=(FontWeight)BinaryPrimitives.ReadUInt16BigEndian(table.Span[4..]);Stretch=(FontStretch)BinaryPrimitives.ReadUInt16BigEndian(table.Span[6..]);}
        if(Font.TryGetTable("head",out var head)&&head.Length>=46&&(BinaryPrimitives.ReadUInt16BigEndian(head.Span[44..])&2)!=0)Style=FontStyle.Italic;
    }
    private static string? ReadName(TrueTypeFont font,int id)
    {
        if(!font.TryGetTable("name",out var memory)||memory.Length<6)return null;var data=memory.Span;
        int count=BinaryPrimitives.ReadUInt16BigEndian(data[2..]),offset=BinaryPrimitives.ReadUInt16BigEndian(data[4..]);string? result=null;int best=-1;
        if(count>(data.Length-6)/12)throw new InvalidDataException("Truncated font name records.");
        for(int i=0;i<count;i++)
        {
            var entry=data.Slice(6+i*12,12);int platform=BinaryPrimitives.ReadUInt16BigEndian(entry),language=BinaryPrimitives.ReadUInt16BigEndian(entry[4..]);
            if(BinaryPrimitives.ReadUInt16BigEndian(entry[6..])!=id||platform is not (0 or 3))continue;
            int length=BinaryPrimitives.ReadUInt16BigEndian(entry[8..]),start=offset+BinaryPrimitives.ReadUInt16BigEndian(entry[10..]);
            if(start>data.Length-length||(length&1)!=0)throw new InvalidDataException("Invalid font name offset.");
            int priority=(language==0x409?2:0)+(platform==3?1:0);if(priority<=best)continue;
            result=Encoding.BigEndianUnicode.GetString(data.Slice(start,length));best=priority;
        }
        return result;
    }
}

internal sealed class PlatformTypeface(FontData data):IPlatformTypeface
{
    private FontData? data=data;
    internal FontData Data=>data??throw new ObjectDisposedException(nameof(PlatformTypeface));
    public string FamilyName=>Data.Family;
    public FontWeight Weight=>Data.Weight;
    public FontStyle Style=>Data.Style;
    public FontStretch Stretch=>Data.Stretch;
    public FontSimulations FontSimulations=>FontSimulations.None;
    public bool TryGetTable(OpenTypeTag tag,out ReadOnlyMemory<byte> table)=>Data.Font.TryGetTable(tag.ToString(),out table);
    public bool TryGetStream([NotNullWhen(true)] out Stream? stream){stream=Data.Font.OpenStream();return true;}
    public void Dispose()=>data=null;
}

internal sealed class FontManager:IFontManagerImpl
{
    private readonly FontData[] fonts;
    private readonly string defaultFamily;
    internal FontManager(IEnumerable<string> paths,string? defaultFamily=null)
    {
        fonts=paths.SelectMany(p=>{var data=File.ReadAllBytes(p);return Enumerable.Range(0,FontCollection.Count(data)).Select(i=>new FontData(data,i));}).ToArray();
        if(fonts.Length==0)throw new ArgumentException("Register at least one supported TrueType font in SimdSkiaOptions.FontFiles.");
        this.defaultFamily=defaultFamily??fonts[0].Family;
        if(!fonts.Any(f=>f.Family.Equals(this.defaultFamily,StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException($"The Windows interface font '{this.defaultFamily}' was not found in the supported installed TrueType font faces. Register an explicit supported font to override it.");
    }
    public string GetDefaultFontFamilyName()=>defaultFamily;
    public string[] GetInstalledFontFamilyNames(bool checkForUpdates=false)=>fonts.Select(f=>f.Family).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private IEnumerable<FontData> Order(FontStyle style,FontWeight weight,FontStretch stretch)=>fonts.OrderBy(f=>f.Style==style?0:1).ThenBy(f=>Math.Abs((int)f.Stretch-(int)stretch)).ThenBy(f=>Math.Abs((int)f.Weight-(int)weight));
    public bool TryCreateGlyphTypeface(string familyName,FontStyle style,FontWeight weight,FontStretch stretch,[NotNullWhen(true)]out IPlatformTypeface? typeface)
    {var match=Order(style,weight,stretch).FirstOrDefault(f=>f.Family.Equals(familyName,StringComparison.OrdinalIgnoreCase));typeface=match is null?null:new PlatformTypeface(match);return typeface is not null;}
    public bool TryMatchCharacter(int codepoint,FontStyle style,FontWeight weight,FontStretch stretch,string? family,CultureInfo? culture,[NotNullWhen(true)]out IPlatformTypeface? typeface)
    {var match=Order(style,weight,stretch).OrderBy(f=>f.Family.Equals(family,StringComparison.OrdinalIgnoreCase)?0:1).FirstOrDefault(f=>f.Font.GetGlyph(codepoint)!=0);typeface=match is null?null:new PlatformTypeface(match);return typeface is not null;}
    public bool TryCreateGlyphTypeface(Stream stream,FontSimulations simulations,[NotNullWhen(true)]out IPlatformTypeface? typeface)
    {
        if(simulations!=FontSimulations.None)throw new NotSupportedException("Synthetic bold/italic font outlines are not implemented.");
        using var memory=new MemoryStream();stream.CopyTo(memory);var data=memory.GetBuffer().AsSpan(0,(int)memory.Length);var digest=SHA256.HashData(data);
        var existing=fonts.FirstOrDefault(f=>f.Digest.AsSpan().SequenceEqual(digest));typeface=new PlatformTypeface(existing??new FontData(data));return true;
    }
    public bool TryGetFamilyTypefaces(string family,[NotNullWhen(true)]out IReadOnlyList<Typeface>? typefaces)
    {var found=fonts.Where(f=>f.Family.Equals(family,StringComparison.OrdinalIgnoreCase)).Select(f=>new Typeface(f.Family,f.Style,f.Weight,f.Stretch)).ToArray();typefaces=found.Length==0?null:found;return typefaces is not null;}
}

/// <summary>Unicode scalar shaping for horizontal Latin/CJK; unsupported scripts fail before producing a buffer.</summary>
internal sealed class TextShaper:ITextShaperImpl
{
    private sealed class TypefaceLease(FontData data):ITextShaperTypeface
    {private FontData? retained=data;public void Dispose()=>retained=null;}
    public ITextShaperTypeface CreateTypeface(GlyphTypeface typeface)=>new TypefaceLease(((PlatformTypeface)typeface.PlatformTypeface).Data);
    public ShapedBuffer ShapeText(ReadOnlyMemory<char> text,TextShaperOptions options)
    {
        if(options.GlyphTypeface.FontSimulations!=FontSimulations.None)throw new NotSupportedException("Synthetic bold/oblique is not implemented; register actual font faces.");
        if((options.BidiLevel&1)!=0)throw new NotSupportedException("RTL text shaping is not implemented.");
        if(options.FontFeatures is {Count:>0})throw new NotSupportedException("Explicit OpenType feature selection is not implemented.");
        if(!double.IsFinite(options.FontRenderingEmSize)||options.FontRenderingEmSize<=0)throw new ArgumentOutOfRangeException(nameof(options));
        int count=0;foreach(var rune in text.Span.EnumerateRunes())
        {
            var category=Rune.GetUnicodeCategory(rune);
            if(rune.Value is >=0x590 and <=0x1cff or >=0xa800 and <=0xabff or 0x200d or >=0x202a and <=0x202e||category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                throw new NotSupportedException($"Contextual/combining text shaping is not implemented (U+{rune.Value:X}).");
            count++;
        }
        var font=((PlatformTypeface)options.GlyphTypeface.PlatformTypeface).Data.Font;
        var result=new ShapedBuffer(text,count,options.GlyphTypeface,options.FontRenderingEmSize,options.BidiLevel);
        int index=0,cluster=0;double x=0,scale=options.FontRenderingEmSize/font.UnitsPerEm;
        try
        {
            foreach(var rune in text.Span.EnumerateRunes())
            {
                ushort glyph=font.GetGlyph(rune.Value);double advance=font.Advance(glyph)*scale;
                if(rune.Value is '\r' or '\n' or 0x200b or 0xfeff or 0x2060){glyph=font.GetGlyph(' ');advance=0;}
                else if(rune.Value=='\t'){glyph=font.GetGlyph(' ');advance=options.IncrementalTabWidth>0?options.IncrementalTabWidth-x%options.IncrementalTabWidth:font.Advance(glyph)*scale*4;}
                else advance+=options.LetterSpacing;
                result[index++]=new(glyph,cluster,advance);cluster+=rune.Utf16SequenceLength;x+=advance;
            }
            for(int i=0;i<result.Length-1;i++)
            {
                var a=result[i];var b=result[i+1];
                if(a.GlyphAdvance==0||b.GlyphAdvance==0||char.IsWhiteSpace(text.Span[a.GlyphCluster])||char.IsWhiteSpace(text.Span[b.GlyphCluster]))continue;
                var pair=font.GetDefaultHorizontalPairAdjustment(a.GlyphIndex,b.GlyphIndex);
                if(pair.First.YAdvance!=0||pair.Second.YAdvance!=0)throw new NotSupportedException("Vertical pen advances in horizontal kern lookups are not implemented.");
                result[i]=new(a.GlyphIndex,a.GlyphCluster,a.GlyphAdvance+pair.First.XAdvance*scale,a.GlyphOffset+new Vector(pair.First.XPlacement*scale,-pair.First.YPlacement*scale));
                result[i+1]=new(b.GlyphIndex,b.GlyphCluster,b.GlyphAdvance+pair.Second.XAdvance*scale,b.GlyphOffset+new Vector(pair.Second.XPlacement*scale,-pair.Second.YPlacement*scale));
            }
            return result;
        }
        catch{result.Dispose();throw;}
    }
}

internal sealed class GlyphRun:IGlyphRunImpl
{
    private SimdSkia.PathGeometry? path;
    private GlyphRunGeometry? glyphs;
    internal GlyphRunGeometry Glyphs=>glyphs??throw new ObjectDisposedException(nameof(GlyphRun));
    internal SimdSkia.PathGeometry Path
    {
        get
        {
            var run=Glyphs;if(path is not null)return path;
            var segments=new List<PathSegment>();
            foreach(var glyph in run.Glyphs)glyph.Outline.AppendTransformedTo(segments,new Matrix3(1,0,glyph.Position.X,0,1,glyph.Position.Y,0,0,1)*run.FontTransform);
            return path=new(segments);
        }
    }
    public double FontRenderingEmSize {get;}
    public Point BaselineOrigin {get;}
    public Rect Bounds {get;}
    internal GlyphRun(GlyphTypeface typeface,double size,IReadOnlyList<GlyphInfo> infos,Point baseline)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        if(typeface.FontSimulations!=FontSimulations.None)throw new NotSupportedException("Synthetic bold/oblique glyph rendering is not implemented.");
        if(!double.IsFinite(size)||size<=0)throw new ArgumentOutOfRangeException(nameof(size));
        ArgumentNullException.ThrowIfNull(infos);
        if(!double.IsFinite(baseline.X)||!double.IsFinite(baseline.Y))throw new ArgumentException("Glyph baseline must be finite.",nameof(baseline));
        var font=((PlatformTypeface)typeface.PlatformTypeface).Data.Font;float scale=(float)(size/font.UnitsPerEm);var positioned=new PositionedGlyph[infos.Count];double x=0;int index=0;
        foreach(var info in infos)
        {
            if(!double.IsFinite(info.GlyphAdvance))throw new ArgumentException("Glyph advances must be finite.",nameof(infos));
            positioned[index++]=new(font.Outline(info.GlyphIndex),new((float)(x+info.GlyphOffset.X),(float)info.GlyphOffset.Y));
            x+=info.GlyphAdvance;
        }
        glyphs=new(positioned,new(scale,0,0,0,-scale,0,0,0,1),GlyphPositioning.QuarterPixel);BaselineOrigin=baseline;FontRenderingEmSize=size;Bounds=ConvertTo.Rect(glyphs.Bounds).Translate(new Vector(baseline.X,baseline.Y));
    }
    public IReadOnlyList<float> GetIntersections(float lower,float upper)
    {
        if(!float.IsFinite(lower)||!float.IsFinite(upper)||lower>upper)throw new ArgumentOutOfRangeException(nameof(lower));
        // Clip the ink to the underline band; use its disjoint horizontal projections.
        var band=SimdSkia.PathGeometry.Rectangle(new(-1e7f,lower,1e7f,upper));
        var clipped=Geometry.Combine(Path,band,GeometryCombineMode.Intersect).Flatten(Matrix3.Identity,0.015625f);
        var intervals=clipped.Where(c=>c.Length>0).Select(c=>(Left:c.Min(p=>p.X),Right:c.Max(p=>p.X))).OrderBy(p=>p.Left);
        var values=new List<float>();foreach(var interval in intervals){if(values.Count>0&&interval.Left<=values[^1])values[^1]=Math.Max(values[^1],interval.Right);else{values.Add(interval.Left);values.Add(interval.Right);}}return values;
    }
    public void Dispose(){path=null;glyphs=null;}
}
