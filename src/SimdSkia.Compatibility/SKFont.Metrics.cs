using System.Buffers.Binary;
using SimdSkia.Text;
namespace SkiaSharp;
public partial struct SKFontMetrics
{
    internal static SKFontMetrics Read(TrueTypeFont font,float size)
    {
        size=size==0?1:size;float upem=font.UnitsPerEm;var bounds=font.FontBounds;float ascent=-font.Ascender/upem,descent=-font.Descender/upem,leading=font.LineGap/upem,avg=0,xheight=0,capheight=0,ut=0,up=0,st=0,sp=0;uint flags=0;
        static short S(ReadOnlySpan<byte> t,int at)=>BinaryPrimitives.ReadInt16BigEndian(t[at..]);
        if(font.TryGetTable("OS/2",out var os)&&os.Length>=74)
        {
            var t=os.Span;avg=S(t,2)/upem;st=S(t,26)/upem;sp=-S(t,28)/upem;flags|=12;
            if((BinaryPrimitives.ReadUInt16BigEndian(t[62..])&128)!=0){ascent=-S(t,68)/upem;descent=-S(t,70)/upem;leading=S(t,72)/upem;}
            if(t.Length>=90&&BinaryPrimitives.ReadUInt16BigEndian(t)>=2){xheight=S(t,86)/upem*size;capheight=S(t,88)/upem*size;}
        }
        if(font.TryGetTable("post",out var post)&&post.Length>=12){var t=post.Span;ut=S(t,10)/upem;up=-(S(t,8)+(OperatingSystem.IsWindows()?0:S(t,10)/2))/upem;flags|=3;}
        float LetterHeight(char letter){ushort glyph=font.GetGlyph(letter);return glyph==0?0:MathF.Floor(font.GetGlyphBounds(glyph).Bottom/upem*size*64+.5f)/64;}
        if(xheight==0)xheight=LetterHeight('x');if(capheight==0)capheight=LetterHeight('H');if(xheight==0)xheight=-ascent*size;if(capheight==0)capheight=-ascent*size;if(avg==0)avg=(bounds.Right-bounds.Left)/upem;
        float xmin=bounds.Left/upem*size,xmax=bounds.Right/upem*size;
        return new(){fFlags=flags,fTop=-bounds.Bottom/upem*size,fBottom=-bounds.Top/upem*size,fAscent=ascent*size,fDescent=descent*size,fLeading=MathF.Max(0,leading)*size,fAvgCharWidth=OperatingSystem.IsWindows()?0:avg*size,fMaxCharWidth=xmax-xmin,fXMin=xmin,fXMax=xmax,fXHeight=xheight,fCapHeight=capheight,fUnderlineThickness=ut*size,fUnderlinePosition=up*size,fStrikeoutThickness=st*size,fStrikeoutPosition=sp*size};
    }
}
public partial class SKFont
{
    public float Spacing=>GetFontMetrics(out _);
    public SKFontMetrics Metrics{get{GetFontMetrics(out var metrics);return metrics;}}
    public float GetFontMetrics(out SKFontMetrics metrics){ThrowIfDisposed();metrics=SKFontMetrics.Read(font,size);return metrics.Descent-metrics.Ascent+metrics.Leading;}
}
