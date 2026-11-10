using System.Collections;
using System.Text;
using SimdSkia.Text;

namespace SkiaSharp;

/// <summary>Managed system catalog of supported static TrueType faces.</summary>
public class SKFontManager : SKObject
{
    private static readonly Lazy<InstalledFace[][]> catalog=new(()=>InstalledFonts.Faces.GroupBy(f=>f.Family,StringComparer.OrdinalIgnoreCase).OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.ToArray()).ToArray());
    private readonly bool permanent;
    private SKFontManager(bool permanent=false)=>this.permanent=permanent;
    protected override bool IgnoreDispose=>permanent;
    public static SKFontManager Default{get;}=new(true);
    public static SKFontManager CreateDefault()=>new();
    public int FontFamilyCount{get{ThrowIfDisposed();return catalog.Value.Length;}}
    public IEnumerable<string> FontFamilies{get{ThrowIfDisposed();foreach(var family in catalog.Value){ThrowIfDisposed();yield return family[0].Family;}}}
    public string[] GetFontFamilies()=>FontFamilies.ToArray();
    public string GetFamilyName(int index){ThrowIfDisposed();if((uint)index>=(uint)catalog.Value.Length)throw new ArgumentOutOfRangeException(nameof(index));return catalog.Value[index][0].Family;}
    public SKFontStyleSet GetFontStyles(int index){ThrowIfDisposed();if((uint)index>=(uint)catalog.Value.Length)throw new ArgumentOutOfRangeException(nameof(index));return new(catalog.Value[index]);}
    public SKFontStyleSet GetFontStyles(string familyName)
    {ThrowIfDisposed();return new(FindFamily(familyName));}
    private static InstalledFace[] FindFamily(string? name)
    {return catalog.Value.FirstOrDefault(group=>group.Any(f=>f.Names.Contains(name??"",StringComparer.OrdinalIgnoreCase)))??[];}
    public SKTypeface MatchFamily(string familyName){using var style=new SKFontStyle();return MatchFamily(familyName,style);}
    public SKTypeface MatchFamily(string familyName,SKFontStyle style){ThrowIfDisposed();using var set=GetFontStyles(familyName);return set.CreateTypeface(style);}
    public SKTypeface CreateTypeface(string path,int index=0){ThrowIfDisposed();return SKTypeface.FromFile(path,index);}
    public SKTypeface CreateTypeface(Stream stream,int index=0){ThrowIfDisposed();return SKTypeface.FromStream(stream,index);}
    public SKTypeface CreateTypeface(SKStreamAsset stream,int index=0){ThrowIfDisposed();return SKTypeface.FromStream(stream,index);}
    public SKTypeface CreateTypeface(SKData data,int index=0){ThrowIfDisposed();return SKTypeface.FromData(data,index);}
    public SKTypeface MatchCharacter(char character)=>MatchCharacter(null!,character);
    public SKTypeface MatchCharacter(int character)=>MatchCharacter(null!,character);
    public SKTypeface MatchCharacter(string familyName,char character)=>MatchCharacter(familyName,(int)character);
    public SKTypeface MatchCharacter(string familyName,int character){using var style=new SKFontStyle();return MatchCharacter(familyName,style,null!,character);}
    public SKTypeface MatchCharacter(string familyName,string[] bcp47,char character)=>MatchCharacter(familyName,bcp47,(int)character);
    public SKTypeface MatchCharacter(string familyName,string[] bcp47,int character){using var style=new SKFontStyle();return MatchCharacter(familyName,style,bcp47,character);}
    public SKTypeface MatchCharacter(string familyName,SKFontStyleWeight weight,SKFontStyleWidth width,SKFontStyleSlant slant,string[] bcp47,char character)=>MatchCharacter(familyName,(int)weight,(int)width,slant,bcp47,character);
    public SKTypeface MatchCharacter(string familyName,SKFontStyleWeight weight,SKFontStyleWidth width,SKFontStyleSlant slant,string[] bcp47,int character)=>MatchCharacter(familyName,(int)weight,(int)width,slant,bcp47,character);
    public SKTypeface MatchCharacter(string familyName,int weight,int width,SKFontStyleSlant slant,string[] bcp47,int character)
    {using var style=new SKFontStyle(weight,width,slant);return MatchCharacter(familyName,style,bcp47,character);}
    public SKTypeface MatchCharacter(string familyName,SKFontStyle style,string[] bcp47,int character)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(style);style.ThrowIfDisposed();if(!Rune.IsValid(character))return null!;
        // BCP47 values are preferences. Prefer a requested family, then language
        // specific CJK families, then other installed faces that contain the scalar.
        IEnumerable<InstalledFace> Rank(IEnumerable<InstalledFace> entries)=>entries.OrderBy(f=>SKFontStyleSet.Score(f,style));
        IEnumerable<InstalledFace> candidates=Rank(FindFamily(familyName));
        if(bcp47 is not null)foreach(var language in bcp47.Reverse())foreach(var family in LanguageFamilies(language))candidates=candidates.Concat(Rank(FindFamily(family)));
        candidates=candidates.Concat(Rank(catalog.Value.SelectMany(g=>g)));var visited=new HashSet<(string,int)>();
        foreach(var face in candidates)
        {if(!visited.Add((face.Path,face.Index)))continue;var font=InstalledFonts.Load(face);if(font.GetGlyph(character)!=0)return SKTypeface.Retain(font);}
        return null!;
    }
    private static string[] LanguageFamilies(string language)
    {
        if(language.StartsWith("ja",StringComparison.OrdinalIgnoreCase))return ["Yu Gothic","Meiryo","Noto Sans CJK JP"];
        if(language.StartsWith("ko",StringComparison.OrdinalIgnoreCase))return ["Malgun Gothic","Noto Sans CJK KR"];
        if(language.StartsWith("zh-Hant",StringComparison.OrdinalIgnoreCase)||language.Equals("zh-TW",StringComparison.OrdinalIgnoreCase)||language.Equals("zh-HK",StringComparison.OrdinalIgnoreCase))return ["Microsoft JhengHei","Noto Sans CJK TC"];
        if(language.StartsWith("zh",StringComparison.OrdinalIgnoreCase))return ["Microsoft YaHei","SimSun","Noto Sans CJK SC"];
        return [];
    }
}

public class SKFontStyleSet : SKObject,IEnumerable<SKFontStyle>,IReadOnlyCollection<SKFontStyle>,IReadOnlyList<SKFontStyle>
{
    private InstalledFace[] faces;
    public SKFontStyleSet()=>faces=[];
    internal SKFontStyleSet(InstalledFace[] faces)=>this.faces=faces;
    public int Count{get{ThrowIfDisposed();return faces.Length;}}
    private InstalledFace Face(int index){ThrowIfDisposed();if((uint)index>=(uint)faces.Length)throw new ArgumentOutOfRangeException(nameof(index));return faces[index];}
    public SKFontStyle this[int index]{get{var f=Face(index);return new(f.Weight,f.Width,(SKFontStyleSlant)f.Slant);}}
    public string GetStyleName(int index){using var face=CreateTypeface(index);return face.ReadFontName(2)??string.Empty;}
    public SKTypeface CreateTypeface(int index)=>SKTypeface.Retain(InstalledFonts.Load(Face(index)));
    public SKTypeface CreateTypeface(SKFontStyle style)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(style);style.ThrowIfDisposed();return faces.Length==0?null!:SKTypeface.Retain(InstalledFonts.Load(faces.MinBy(f=>Score(f,style))!));}
    internal static int Score(InstalledFace face,SKFontStyle style)=>FontStyleMatcher.Score(face.Weight,face.Width,face.Slant,style.Weight,style.Width,(int)style.Slant);
    public IEnumerator<SKFontStyle> GetEnumerator(){int count=Count;for(int i=0;i<count;i++)yield return this[i];}
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    protected override void Dispose(bool disposing)=>faces=[];
}
