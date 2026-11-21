using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
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
    private byte[]? digest;
    internal byte[] Digest
    {
        get
        {
            var existing=Volatile.Read(ref digest);if(existing is not null)return existing;
            using var stream=Font.OpenStream();var computed=SHA256.HashData(stream);
            return Interlocked.CompareExchange(ref digest,computed,null)??computed;
        }
    }
    internal FontData(ReadOnlySpan<byte> data,int faceIndex=0):this(new TrueTypeFont(data,faceIndex)) { }
    internal FontData(byte[] ownedData,int faceIndex,bool owned):this(TrueTypeFont.FromOwnedCollection(ownedData,faceIndex)) { }
    internal FontData(FontSource source,int faceIndex):this(TrueTypeFont.FromInstalledSource(source,faceIndex)) { }
    private FontData(TrueTypeFont font)
    {
        Font=font;
        Family=Font.TryGetTable("name",out var names)&&ReadFamilyName(names.Span) is {} family
            ? family : throw new InvalidDataException("The font has no family name.");
        Weight=FontWeight.Normal;Stretch=FontStretch.Normal;
        if(Font.TryGetTable("OS/2",out var table)&&table.Length>=8)
        {Weight=(FontWeight)BinaryPrimitives.ReadUInt16BigEndian(table.Span[4..]);Stretch=(FontStretch)BinaryPrimitives.ReadUInt16BigEndian(table.Span[6..]);}
        if(Font.TryGetTable("head",out var head)&&head.Length>=46&&(BinaryPrimitives.ReadUInt16BigEndian(head.Span[44..])&2)!=0)Style=FontStyle.Italic;
    }
    internal static string? ReadFamilyName(ReadOnlySpan<byte> data) =>
        ReadName(data,16,false) ?? ReadName(data,1,false) ?? ReadName(data,16,true) ?? ReadName(data,1,true);

    private static string? ReadName(ReadOnlySpan<byte> data,int id,bool macRoman)
    {
        if(data.Length<6)return null;
        int count=BinaryPrimitives.ReadUInt16BigEndian(data[2..]),offset=BinaryPrimitives.ReadUInt16BigEndian(data[4..]);string? result=null;int best=-1;
        if(count>(data.Length-6)/12)throw new InvalidDataException("Truncated font name records.");
        for(int i=0;i<count;i++)
        {
            var entry=data.Slice(6+i*12,12);int platform=BinaryPrimitives.ReadUInt16BigEndian(entry),language=BinaryPrimitives.ReadUInt16BigEndian(entry[4..]);
            if(BinaryPrimitives.ReadUInt16BigEndian(entry[6..])!=id)continue;
            if(macRoman ? platform!=1||BinaryPrimitives.ReadUInt16BigEndian(entry[2..])!=0 : platform is not (0 or 3))continue;
            int length=BinaryPrimitives.ReadUInt16BigEndian(entry[8..]),start=offset+BinaryPrimitives.ReadUInt16BigEndian(entry[10..]);
            if(start>data.Length-length||(!macRoman&&(length&1)!=0))throw new InvalidDataException("Invalid font name offset.");
            int priority=macRoman?(language==0?1:0):(language==0x409?2:0)+(platform==3?1:0);
            if(priority<=best)continue;
            var value=data.Slice(start,length);
            string candidate=macRoman?DecodeMacRoman(value):Encoding.BigEndianUnicode.GetString(value);
            if(string.IsNullOrWhiteSpace(candidate))continue;
            result=candidate;best=priority;
        }
        return result;
    }

    private static string DecodeMacRoman(ReadOnlySpan<byte> value)
    {
        // OpenType Macintosh platform encoding 0. Keep the table local so the
        // renderer does not need to initialize the legacy code-page provider.
        const string extended = "ÄÅÇÉÑÖÜáàâäãåçéèêëíìîïñóòôöõúùûü"
            + "†°¢£§•¶ß®©™´¨≠ÆØ∞±≤≥¥µ∂∑∏π∫ªºΩæø"
            + "¿¡¬√ƒ≈∆«»…\u00A0ÀÃÕŒœ–—“”‘’÷◊ÿŸ⁄€‹›ﬁﬂ"
            + "‡·‚„‰ÂÊÁËÈÍÎÏÌÓÔ\uF8FFÒÚÛÙıˆ˜¯˘˙˚¸˝˛ˇ";
        Span<char> result = value.Length <= 256 ? stackalloc char[value.Length] : new char[value.Length];
        for(int i=0;i<value.Length;i++)result[i]=value[i]<128?(char)value[i]:extended[value[i]-128];
        return new string(result);
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
    private readonly FontEntry[] fonts;
    private readonly string defaultFamily;
    private readonly bool installedFonts;
    private readonly Lazy<FontEntry[]>? supplementary;
    private readonly Dictionary<SupplementaryRequest,FontData?> supplementaryMatches=[];
    private FaceOrder? fallbackOrder;
    private FaceOrder? supplementaryOrder;
    internal int LoadedFaceCount=>fonts.Count(f=>f.IsLoaded)+(supplementary is {IsValueCreated:true}?supplementary.Value.Count(f=>f.IsLoaded):0);
    internal bool SupplementaryIndexCreated=>supplementary?.IsValueCreated==true;
    internal FontManager(IEnumerable<string> paths,string? defaultFamily=null,bool supportedFacesOnly=false,string[]? installedFallbackFiles=null)
    {
        installedFonts=supportedFacesOnly;
        if(supportedFacesOnly&&installedFallbackFiles is {Length:>0})
            supplementary=new(()=>IndexSupplementary(installedFallbackFiles));
        var loaded=new List<FontEntry>();
        foreach(string path in paths)
        {
            if(supportedFacesOnly){loaded.AddRange(FontEntry.ReadSystemFile(path));continue;}
            var data=File.ReadAllBytes(path);
            int faces=FontCollection.Count(data);
            for(int i=0;i<faces;i++)
                loaded.Add(new FontEntry(new FontData(data,i,true)));
        }
        fonts=loaded.ToArray();
        if(fonts.Length==0)throw new ArgumentException("Register at least one supported TrueType font in SimdSkiaOptions.FontFiles.");
        this.defaultFamily=defaultFamily??fonts[0].Family;
        if(!fonts.Any(f=>f.Family.Equals(this.defaultFamily,StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException($"The default font '{this.defaultFamily}' was not found in the supported installed TrueType font faces. Register an explicit supported font to override it.");
    }
    public string GetDefaultFontFamilyName()=>defaultFamily;
    public string[] GetInstalledFontFamilyNames(bool checkForUpdates=false)=>fonts.Concat(supplementary?.Value??[])
        .Select(f=>f.Family).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static FontEntry[] IndexSupplementary(string[] paths)
    {
        var faces=new List<FontEntry>();
        foreach(string path in paths)
        {
            try { faces.AddRange(FontEntry.ReadSystemFile(path,indexCoverage:true)); }
            catch(Exception error) when(IsFontFailure(error))
            { System.Diagnostics.Trace.WriteLine($"Excluded fallback font {path}: {error.Message}"); }
        }
        return faces.ToArray();
    }

    private static bool IsFontFailure(Exception error)
        => error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or OverflowException or ArgumentOutOfRangeException;
    private readonly record struct FaceRequest(FontStyle Style,FontWeight Weight,FontStretch Stretch)
    {
        internal int Compare(FontEntry a,FontEntry b)
        {
            int result=(b.Style==Style).CompareTo(a.Style==Style);
            if(result==0)result=Math.Abs((int)a.Stretch-(int)Stretch).CompareTo(Math.Abs((int)b.Stretch-(int)Stretch));
            if(result==0)result=Math.Abs((int)a.Weight-(int)Weight).CompareTo(Math.Abs((int)b.Weight-(int)Weight));
            return result;
        }
    }
    private sealed class FaceOrder
    {
        internal readonly FaceRequest Request;
        internal readonly int[] Indices;
        internal FaceOrder(FontEntry[] faces,FaceRequest request)
        {
            Request=request;
            Indices=new int[faces.Length];
            for(int i=0;i<Indices.Length;i++)Indices[i]=i;
            Array.Sort(Indices,(a,b)=>
            {
                int result=request.Compare(faces[a],faces[b]);
                return result!=0?result:a.CompareTo(b);
            });
        }
    }
    private static FaceOrder Order(FontEntry[] faces,FaceRequest request,ref FaceOrder? previous)
    {
        var order=Volatile.Read(ref previous);
        if(order is not null&&order.Request==request)return order;
        order=new(faces,request);
        Volatile.Write(ref previous,order);
        return order;
    }

    public bool TryCreateGlyphTypeface(string familyName,FontStyle style,FontWeight weight,FontStretch stretch,[NotNullWhen(true)]out IPlatformTypeface? typeface)
    {
        var request=new FaceRequest(style,weight,stretch);FontEntry? match=null;
        foreach(var face in fonts)
        {
            if(!face.Family.Equals(familyName,StringComparison.OrdinalIgnoreCase))continue;
            if(match is null||request.Compare(face,match)<0)match=face;
            if(face.Style==style&&face.Weight==weight&&face.Stretch==stretch)break;
        }
        if(match is not null){typeface=new PlatformTypeface(match.Data);return true;}
        if(supplementary is not null)
        {
            var faces=supplementary.Value;
            foreach(int index in Order(faces,request,ref supplementaryOrder).Indices)
            {
                var face=faces[index];
                if(!face.Family.Equals(familyName,StringComparison.OrdinalIgnoreCase))continue;
                try{typeface=new PlatformTypeface(face.Data);return true;}
                catch(Exception error) when(IsFontFailure(error)) { }
            }
        }
        typeface=null;return false;
    }
    public bool TryMatchCharacter(int codepoint,FontStyle style,FontWeight weight,FontStretch stretch,string? family,CultureInfo? culture,[NotNullWhen(true)]out IPlatformTypeface? typeface)
    {
        var request=new FaceRequest(style,weight,stretch);
        if(!Rune.IsValid(codepoint))throw new ArgumentOutOfRangeException(nameof(codepoint));
        var order=Order(fonts,request,ref fallbackOrder);
        // Check an explicitly requested family before locale-specific fallback.
        if(family is not null)
        {
            var exact=new SupplementaryRequest(codepoint,request,family,true,CjkLocale.Unspecified,0);
            var match=MatchCharacter(fonts,order,exact,skipUnavailable:false);
            if(match is null&&!fonts.Any(f=>f.Family.Equals(family,StringComparison.OrdinalIgnoreCase)))
                match=MatchSupplementary(exact);
            if(match is not null){typeface=new PlatformTypeface(match);return true;}
        }
        // Pictographic emoji can also be mapped by a legacy monochrome symbol
        // font. Prefer a drawable palette glyph during installed fallback.
        if(installedFonts&&codepoint is >=0x1f1e6 and <=0x1faff)
        {
            var color=new SupplementaryRequest(codepoint,request,family,false,CjkLocale.Unspecified,0,ColorOnly:true);
            var match=MatchCharacter(fonts,order,color,skipUnavailable:false)??MatchSupplementary(color);
            if(match is not null){typeface=new PlatformTypeface(match);return true;}
        }
        var locale=installedFonts&&CjkFontPreference.AppliesTo(codepoint)
            ? CjkFontPreference.ForCulture(culture??CultureInfo.CurrentUICulture) : CjkLocale.Unspecified;
        int lastRank=locale==CjkLocale.Unspecified?0:2;
        for(int rank=0;rank<=lastRank;rank++)
        {
            var fallback=new SupplementaryRequest(codepoint,request,family,false,locale,rank);
            var match=MatchCharacter(fonts,order,fallback,skipUnavailable:false)??MatchSupplementary(fallback);
            if(match is not null){typeface=new PlatformTypeface(match);return true;}
        }
        typeface=null;return false;
    }

    private readonly record struct SupplementaryRequest(int Codepoint,FaceRequest Style,string? Family,
        bool RequireFamily,CjkLocale Locale,int Rank,bool ColorOnly=false);

    private FontData? MatchSupplementary(SupplementaryRequest request)
    {
        if(supplementary is null)return null;
        lock(supplementaryMatches)
            if(supplementaryMatches.TryGetValue(request,out var known))return known;

        var faces=supplementary.Value;
        var match=MatchCharacter(faces,Order(faces,request.Style,ref supplementaryOrder),request,skipUnavailable:true);
        lock(supplementaryMatches)
        {
            if(supplementaryMatches.Count>=256)supplementaryMatches.Clear();
            supplementaryMatches[request]=match;
        }
        return match;
    }

    private static FontData? MatchCharacter(FontEntry[] faces,FaceOrder order,SupplementaryRequest request,bool skipUnavailable)
    {
        foreach(int index in order.Indices)
        {
            var face=faces[index];
            bool family=face.Family.Equals(request.Family,StringComparison.OrdinalIgnoreCase);
            if(family!=request.RequireFamily||CjkFontPreference.Rank(request.Locale,face.Locale)!=request.Rank
                ||request.ColorOnly&&!face.HasColorGlyphs||!face.MayContain(request.Codepoint))continue;
            try
            {
                if(!face.HasGlyph(request.Codepoint))continue;
                var data=face.Data;
                if(!request.ColorOnly||data.Font.HasColorGlyph(data.Font.GetGlyph(request.Codepoint)))return data;
            }
            catch(Exception error) when(skipUnavailable&&IsFontFailure(error)) { }
        }
        return null;
    }
    public bool TryCreateGlyphTypeface(Stream stream,FontSimulations simulations,[NotNullWhen(true)]out IPlatformTypeface? typeface)
    {
        ArgumentNullException.ThrowIfNull(stream);
        // Avalonia uses a failed Try result to retain the nearest real face.
        // Do not manufacture a simulated face or abort the window's layout.
        if(simulations!=FontSimulations.None){typeface=null;return false;}
        // EmbeddedFontCollection can reopen a file already registered in
        // FontFiles. Hash a seekable stream before making a font-sized snapshot,
        // avoiding a temporary growable LOH buffer on every matching open.
        if(stream.GetType()==typeof(MemoryStream)&&((MemoryStream)stream).TryGetBuffer(out var segment))
        {
            var memory=(MemoryStream)stream;
            int length=(int)Math.Max(0,memory.Length-memory.Position);
            var contents=length==0?ReadOnlySpan<byte>.Empty:segment.AsSpan((int)memory.Position,length);
            if(length!=0)memory.Position=memory.Length;
            typeface=new PlatformTypeface(FindLoaded(SHA256.HashData(contents))??new FontData(contents));
            return true;
        }
        int capacity=0;
        if(stream.CanSeek)
        {
            long start=stream.Position;var digest=SHA256.HashData(stream);
            var existing=FindLoaded(digest);
            if(existing is not null){typeface=new PlatformTypeface(existing);return true;}
            stream.Position=start;
            if(stream.GetType()==typeof(FileStream)||stream.GetType()==typeof(MemoryStream))
            {
                long remaining=stream.Length-start;
                if(remaining is >0 and <=64*1024*1024)capacity=(int)remaining;
            }
        }
        using var snapshot=new MemoryStream(capacity);stream.CopyTo(snapshot);
        var buffer=snapshot.GetBuffer();var data=buffer.AsSpan(0,(int)snapshot.Length);
        // An external stream need not load unrelated installed fallback files.
        var shared=stream.CanSeek?null:FindLoaded(SHA256.HashData(data));
        // Hand over only exact, privately owned standalone files. TTC input still
        // exports the selected standalone face through the existing validation path.
        typeface=new PlatformTypeface(shared??(data.Length==buffer.Length&&data.Length>=4&&
            BinaryPrimitives.ReadUInt32BigEndian(data)!=0x74746366?new FontData(buffer,0,true):new FontData(data)));
        return true;
    }
    private FontData? FindLoaded(ReadOnlySpan<byte> digest)
    {
        var known=MatchDigest(fonts,digest);
        return known??(supplementary is {IsValueCreated:true}?MatchDigest(supplementary.Value,digest):null);
    }
    private static FontData? MatchDigest(FontEntry[] faces,ReadOnlySpan<byte> digest)
    {foreach(var font in faces)if(font.IsLoaded&&font.Data.Digest.AsSpan().SequenceEqual(digest))return font.Data;return null;}
    public bool TryGetFamilyTypefaces(string family,[NotNullWhen(true)]out IReadOnlyList<Typeface>? typefaces)
    {
        var found=GetFaces(fonts,family);
        if(found.Length==0&&supplementary is not null)found=GetFaces(supplementary.Value,family);
        typefaces=found.Length==0?null:found;return typefaces is not null;
    }
    private static Typeface[] GetFaces(FontEntry[] faces,string family)=>faces.Where(f=>f.Family.Equals(family,StringComparison.OrdinalIgnoreCase))
        .Select(f=>new Typeface(f.Family,f.Style,f.Weight,f.Stretch)).ToArray();
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
        var font=((PlatformTypeface)options.GlyphTypeface.PlatformTypeface).Data.Font;
        int count=0;bool compose=false,colorFont=font.HasColorGlyphs;
        foreach(var rune in text.Span.EnumerateRunes())
        {
            if(colorFont&&(IsVariation(rune.Value)||IsEmojiJoiner(rune.Value)||rune.Value==0x20e3||
                rune.Value is >=0x1f1e6 and <=0x1f1ff or >=0x1f3fb and <=0x1f3ff))
            {count++;compose=true;continue;}
            var category=Rune.GetUnicodeCategory(rune);
            if(rune.Value is >=0x590 and <=0x1cff or >=0xa800 and <=0xabff or 0x200d or >=0x202a and <=0x202e||category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                throw new NotSupportedException($"Contextual/combining text shaping is not implemented (U+{rune.Value:X}).");
            count++;
        }
        if(compose)return ShapeColorText(text,options,font,count);
        var result=new ShapedBuffer(text,count,options.GlyphTypeface,options.FontRenderingEmSize,options.BidiLevel);
        int index=0,cluster=0;double x=0,scale=options.FontRenderingEmSize/font.UnitsPerEm;
        try
        {
            foreach(var rune in text.Span.EnumerateRunes())
            {
                var glyph=new GlyphCluster(font.GetGlyph(rune.Value),cluster,rune.Value);
                var info=Position(glyph,options,font,scale,x);
                result[index++]=info;cluster+=rune.Utf16SequenceLength;x+=info.GlyphAdvance;
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
    private static bool IsVariation(int scalar)=>scalar is >=0xfe00 and <=0xfe0f or >=0xe0100 and <=0xe01ef;
    private static bool IsEmojiJoiner(int scalar)=>scalar is 0x200d or >=0xe0020 and <=0xe007f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GlyphInfo Position(GlyphCluster item,TextShaperOptions options,TrueTypeFont font,double scale,double x)
    {
        ushort glyph=item.Glyph;double advance=font.Advance(glyph)*scale;
        if(item.Codepoint is '\r' or '\n' or 0x200b or 0xfeff or 0x2060){glyph=font.GetGlyph(' ');advance=0;}
        else if(item.Codepoint=='\t'){glyph=font.GetGlyph(' ');advance=options.IncrementalTabWidth>0?options.IncrementalTabWidth-x%options.IncrementalTabWidth:font.Advance(glyph)*scale*4;}
        else advance+=options.LetterSpacing;
        return new(glyph,item.Cluster,advance);
    }

    private static ShapedBuffer ShapeColorText(ReadOnlyMemory<char> text,TextShaperOptions options,TrueTypeFont font,int capacity)
    {
        GlyphCluster[]? rented=null;
        Span<GlyphCluster> glyphs=capacity<=64?stackalloc GlyphCluster[capacity]:(rented=ArrayPool<GlyphCluster>.Shared.Rent(capacity));
        try
        {
            var composition=font.ColorComposition;
            int count=0,cluster=0;
            foreach(var rune in text.Span.EnumerateRunes())
            {
                if(IsVariation(rune.Value))
                {
                    if(count!=0)
                    {
                        var previous=glyphs[count-1];
                        glyphs[count-1]=previous with {Glyph=composition.Variant(previous.Glyph,previous.Codepoint,rune.Value)};
                    }
                }
                else glyphs[count++]=new(font.GetGlyph(rune.Value),cluster,rune.Value);
                cluster+=rune.Utf16SequenceLength;
            }
            if(count>1)count=composition.Apply(glyphs[..count]);
            int visible=0;
            for(int i=0;i<count;i++)
                if(!IsEmojiJoiner(glyphs[i].Codepoint))glyphs[visible++]=glyphs[i];
            var result=new ShapedBuffer(text,visible,options.GlyphTypeface,options.FontRenderingEmSize,options.BidiLevel);
            try
            {
                double x=0,scale=options.FontRenderingEmSize/font.UnitsPerEm;
                for(int i=0;i<visible;i++)
                {
                    var glyph=Position(glyphs[i],options,font,scale,x);
                    result[i]=glyph;x+=glyph.GlyphAdvance;
                }
                return result;
            }
            catch{result.Dispose();throw;}
        }
        finally{if(rented is not null)ArrayPool<GlyphCluster>.Shared.Return(rented);}
    }
}

internal sealed class GlyphRun:IGlyphRunImpl
{
    private SimdSkia.PathGeometry? path;
    private GlyphRunGeometry? glyphs;
    internal sealed record BitmapDraw(TrueTypeFont Font, ushort Glyph, GlyphBitmap Bitmap, PointF Position, RectF OutlineOrigin)
    {
        internal RectF Bounds(float size)
        {
            var bounds=Bitmap.Bounds(size,OutlineOrigin);
            return new(bounds.Left+Position.X,bounds.Top+Position.Y,bounds.Right+Position.X,bounds.Bottom+Position.Y);
        }
    }
    internal readonly record struct LayerDraw(GlyphRunGeometry? Geometry,Rgba32? Color,BitmapDraw? Bitmap=null);
    internal LayerDraw[]? LayerDraws {get;private set;}
    internal GlyphRunGeometry Glyphs=>glyphs??throw new ObjectDisposedException(nameof(GlyphRun));
    internal RectF LocalInkBounds {get;}
    internal SimdSkia.PathGeometry Path
    {
        get
        {
            var run=Glyphs;if(path is not null)return path;
            var segments=new List<PathSegment>();
            foreach(var glyph in run.Glyphs)glyph.Outline.AppendTransformedTo(segments,new Matrix3(1,0,glyph.Position.X,0,1,glyph.Position.Y,0,0,1)*run.FontTransform);
            if(LayerDraws is {} layers)
                foreach(var layer in layers)
                    if(layer.Bitmap is {} bitmap)
                        SimdSkia.PathGeometry.Rectangle(bitmap.Bounds((float)FontRenderingEmSize)).AppendTransformedTo(segments,Matrix3.Identity);
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
        var font=((PlatformTypeface)typeface.PlatformTypeface).Data.Font;float scale=(float)(size/font.UnitsPerEm);
        var fontTransform=new Matrix3(scale,0,0,0,-scale,0,0,0,1);
        var positioned=new PositionedGlyph[infos.Count];double x=0;int index=0;
        List<PositionedGlyph>? ink=null;
        List<LayerDraw>? draws=null;
        RectF? imageBounds=null;
        foreach(var info in infos)
        {
            if(!double.IsFinite(info.GlyphAdvance))throw new ArgumentException("Glyph advances must be finite.",nameof(infos));
            var position=new PointF((float)(x+info.GlyphOffset.X),(float)info.GlyphOffset.Y);
            var layers=font.GetColorLayers(info.GlyphIndex);
            var bitmap=layers.IsEmpty?font.GetBitmapGlyph(info.GlyphIndex,(float)size):null;
            if(layers.IsEmpty&&bitmap is null)
            {
                var outline=new PositionedGlyph(font.Outline(info.GlyphIndex),position);
                if(draws is null)positioned[index++]=outline;
                else AddOutline(outline,null);
            }
            else
            {
                if(draws is null)
                {
                    ink=new(infos.Count);
                    draws=new(infos.Count);
                    for(int i=0;i<index;i++)AddOutline(positioned[i],null);
                }
                if(bitmap is {} image)
                {
                    var outlineOrigin=image.UseOutlineOrigin?font.GetGlyphBounds(info.GlyphIndex):default;
                    var bitmapDraw=new BitmapDraw(font,info.GlyphIndex,image,position,
                        new(outlineOrigin.Left*scale,outlineOrigin.Top*scale,0,0));
                    imageBounds=Union(imageBounds??default,bitmapDraw.Bounds((float)size));
                    draws.Add(new(null,null,bitmapDraw));
                    if(image.OverlayOutline)AddOutline(new(font.Outline(info.GlyphIndex),position),null);
                }
                foreach(var layer in layers)
                    AddOutline(new(font.Outline(layer.Glyph),position),layer.Color);
            }
            x+=info.GlyphAdvance;
        }
        if(draws is not null)
        {
            positioned=ink!.ToArray();
            LayerDraws=draws.ToArray();
        }
        glyphs=GlyphRunGeometry.FromOwnedGlyphs(positioned,fontTransform,GlyphPositioning.QuarterPixel);
        LocalInkBounds=imageBounds is {} rasterBounds?Union(glyphs.Bounds,rasterBounds):glyphs.Bounds;
        BaselineOrigin=baseline;FontRenderingEmSize=size;Bounds=ConvertTo.Rect(LocalInkBounds).Translate(new Vector(baseline.X,baseline.Y));

        void AddOutline(PositionedGlyph outline,Rgba32? color)
        {
            ink!.Add(outline);
            draws!.Add(new(GlyphRunGeometry.FromOwnedGlyphs([outline],fontTransform,GlyphPositioning.QuarterPixel),color));
        }
    }
    private static RectF Union(RectF a,RectF b)=>a.IsEmpty?b:new(
        Math.Min(a.Left,b.Left),Math.Min(a.Top,b.Top),Math.Max(a.Right,b.Right),Math.Max(a.Bottom,b.Bottom));
    public IReadOnlyList<float> GetIntersections(float lower,float upper)
    {
        if(!float.IsFinite(lower)||!float.IsFinite(upper)||lower>upper)throw new ArgumentOutOfRangeException(nameof(lower));
        // Clip the ink to the underline band; use its disjoint horizontal projections.
        var band=SimdSkia.PathGeometry.Rectangle(new(-1e7f,lower,1e7f,upper));
        var clipped=Geometry.Combine(Path,band,GeometryCombineMode.Intersect).Flatten(Matrix3.Identity,0.015625f);
        var intervals=clipped.Where(c=>c.Length>0).Select(c=>(Left:c.Min(p=>p.X),Right:c.Max(p=>p.X))).OrderBy(p=>p.Left);
        var values=new List<float>();foreach(var interval in intervals){if(values.Count>0&&interval.Left<=values[^1])values[^1]=Math.Max(values[^1],interval.Right);else{values.Add(interval.Left);values.Add(interval.Right);}}return values;
    }
    public void Dispose(){path=null;glyphs=null;LayerDraws=null;}
}
