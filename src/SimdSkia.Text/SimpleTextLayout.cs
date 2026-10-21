using System.Buffers;
using System.Globalization;
using System.Text;

namespace SimdSkia.Text;

public sealed record TextLine(string Text,float Width,float Baseline);
public sealed record TextLayoutResult(TextLine[] Lines,float Width,float Height);

/// <summary>Basic LTR layout: Latin words, CJK character boundaries, explicit lines and four-space tabs.</summary>
public static class SimpleTextLayout
{
    public static TextLayoutResult Create(TrueTypeFont font,string text,float size,float maxWidth,float lineSpacing=1.2f,float scaleX=1)
    {
        ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(text);
        if(!float.IsFinite(size)||size<0||!float.IsFinite(maxWidth)||maxWidth<=0||!float.IsFinite(lineSpacing)||lineSpacing<=0||!float.IsFinite(scaleX)||scaleX<=0)throw new ArgumentOutOfRangeException(nameof(maxWidth));
        var tokens=new List<string>();var word=new StringBuilder();ReadOnlySpan<char> remaining=text;
        void FinishWord(){if(word.Length!=0){tokens.Add(word.ToString());word.Clear();}}
        while(!remaining.IsEmpty)
        {
            if(Rune.DecodeFromUtf16(remaining,out var rune,out int consumed)!=OperationStatus.Done)throw new ArgumentException("Malformed UTF-16.",nameof(text));remaining=remaining[consumed..];
            int value=rune.Value;var category=Rune.GetUnicodeCategory(rune);
            if(value is >=0x0590 and <=0x1cff||category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)throw new NotSupportedException("Complex/mark/Bidi shaping is not implemented in basic LTR layout.");
            if(value=='\r'){FinishWord();if(!remaining.IsEmpty&&remaining[0]=='\n')remaining=remaining[1..];tokens.Add("\n");}
            else if(value is '\n' or 0x2028 or 0x2029){FinishWord();tokens.Add("\n");}
            else if(value=='\t'){FinishWord();tokens.Add("    ");}
            else if(Rune.IsLetterOrDigit(rune)&&!Cjk(value))word.Append(rune);
            else{FinishWord();tokens.Add(rune.ToString());}
        }
        FinishWord();var lines=new List<TextLine>();var line=new StringBuilder();float width=0,factor=size/font.UnitsPerEm;
        float lineHeight=(font.Ascender-font.Descender+font.LineGap)*factor*lineSpacing;
        if(lineHeight<0)throw new InvalidDataException("Invalid font line metrics.");float baseline=font.Ascender*factor;
        float Advance(Rune rune)=>font.Advance(font.GetGlyph(rune.Value))*factor*scaleX;
        float Measure(string value){float result=0;foreach(var rune in value.EnumerateRunes())result+=Advance(rune);return result;}
        void FinishLine(){lines.Add(new(line.ToString(),width,baseline));line.Clear();width=0;baseline+=lineHeight;}
        foreach(string token in tokens)
        {
            if(token=="\n"){FinishLine();continue;}
            float tokenWidth=Measure(token);
            if(width>0&&width+tokenWidth>maxWidth)FinishLine();
            foreach(var rune in token.EnumerateRunes())
            {
                float advance=Advance(rune);if(width>0&&width+advance>maxWidth)FinishLine();
                line.Append(rune);width+=advance;
            }
        }
        if(line.Length>0||lines.Count==0||text.EndsWith('\n')||text.EndsWith('\r'))FinishLine();
        return new(lines.ToArray(),lines.Max(l=>l.Width),lines.Count*lineHeight);
    }
    private static bool Cjk(int value)=>value is >=0x3400 and <=0x9fff or >=0xf900 and <=0xfaff or >=0x20000 and <=0x2fa1f;
}
