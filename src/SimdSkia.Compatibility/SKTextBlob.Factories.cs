using System.Runtime.InteropServices;
namespace SkiaSharp;
public unsafe partial class SKTextBlob
{
    public static SKTextBlob? Create(string text,SKFont font,SKPoint origin=default)=>Create(text.AsSpan(),font,origin);
    public static SKTextBlob? Create(ReadOnlySpan<char> text,SKFont font,SKPoint origin=default)
    {fixed(char* pointer=text)return Create((IntPtr)pointer,checked(text.Length*2),SKTextEncoding.Utf16,font,origin);}
    public static SKTextBlob? Create(ReadOnlySpan<byte> text,SKTextEncoding encoding,SKFont font,SKPoint origin=default)
    {fixed(byte* pointer=text)return Create((IntPtr)pointer,text.Length,encoding,font,origin);}
    public static SKTextBlob? Create(IntPtr text,int length,SKTextEncoding encoding,SKFont font,SKPoint origin=default)
    {
        ArgumentNullException.ThrowIfNull(font);int count=font.CountGlyphs(text,length,encoding);if(count==0)return null;
        using var builder=new SKTextBlobBuilder();var run=builder.AllocatePositionedRun(font,count);font.GetGlyphs(text,length,encoding,run.Glyphs);font.GetGlyphPositions(run.Glyphs,run.Positions,origin);return builder.Build();
    }
    public static SKTextBlob? CreateHorizontal(string text,SKFont font,ReadOnlySpan<float> positions,float y)=>CreateHorizontal(text.AsSpan(),font,positions,y);
    public static SKTextBlob? CreateHorizontal(ReadOnlySpan<char> text,SKFont font,ReadOnlySpan<float> positions,float y)
    {fixed(char* pointer=text)return CreateHorizontal((IntPtr)pointer,checked(text.Length*2),SKTextEncoding.Utf16,font,positions,y);}
    public static SKTextBlob? CreateHorizontal(ReadOnlySpan<byte> text,SKTextEncoding encoding,SKFont font,ReadOnlySpan<float> positions,float y)
    {fixed(byte* pointer=text)return CreateHorizontal((IntPtr)pointer,text.Length,encoding,font,positions,y);}
    public static SKTextBlob? CreateHorizontal(IntPtr text,int length,SKTextEncoding encoding,SKFont font,ReadOnlySpan<float> positions,float y)
    {
        ArgumentNullException.ThrowIfNull(font);int count=font.CountGlyphs(text,length,encoding);if(count==0)return null;if(positions.Length!=count)throw new ArgumentException("One position is required per glyph.",nameof(positions));
        using var builder=new SKTextBlobBuilder();var run=builder.AllocatePositionedRun(font,count);font.GetGlyphs(text,length,encoding,run.Glyphs);for(int i=0;i<count;i++)run.Positions[i]=new(positions[i],y);return builder.Build();
    }
    public static SKTextBlob? CreatePositioned(string text,SKFont font,ReadOnlySpan<SKPoint> positions)=>CreatePositioned(text.AsSpan(),font,positions);
    public static SKTextBlob? CreatePositioned(ReadOnlySpan<char> text,SKFont font,ReadOnlySpan<SKPoint> positions)
    {fixed(char* pointer=text)return CreatePositioned((IntPtr)pointer,checked(text.Length*2),SKTextEncoding.Utf16,font,positions);}
    public static SKTextBlob? CreatePositioned(ReadOnlySpan<byte> text,SKTextEncoding encoding,SKFont font,ReadOnlySpan<SKPoint> positions)
    {fixed(byte* pointer=text)return CreatePositioned((IntPtr)pointer,text.Length,encoding,font,positions);}
    public static SKTextBlob? CreatePositioned(IntPtr text,int length,SKTextEncoding encoding,SKFont font,ReadOnlySpan<SKPoint> positions)
    {
        ArgumentNullException.ThrowIfNull(font);int count=font.CountGlyphs(text,length,encoding);if(count==0)return null;if(positions.Length!=count)throw new ArgumentException("One position is required per glyph.",nameof(positions));
        using var builder=new SKTextBlobBuilder();var run=builder.AllocatePositionedRun(font,count);font.GetGlyphs(text,length,encoding,run.Glyphs);positions.CopyTo(run.Positions);return builder.Build();
    }
}
