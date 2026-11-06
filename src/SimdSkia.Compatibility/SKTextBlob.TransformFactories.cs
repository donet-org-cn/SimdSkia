namespace SkiaSharp;
public unsafe partial class SKTextBlob
{
    public static SKTextBlob? CreateRotationScale(string text,SKFont font,ReadOnlySpan<SKRotationScaleMatrix> positions)=>CreateRotationScale(text.AsSpan(),font,positions);
    public static SKTextBlob? CreateRotationScale(ReadOnlySpan<char> text,SKFont font,ReadOnlySpan<SKRotationScaleMatrix> positions)
    {fixed(char* p=text)return CreateRotationScale((IntPtr)p,checked(text.Length*2),SKTextEncoding.Utf16,font,positions);}
    public static SKTextBlob? CreateRotationScale(ReadOnlySpan<byte> text,SKTextEncoding encoding,SKFont font,ReadOnlySpan<SKRotationScaleMatrix> positions)
    {fixed(byte* p=text)return CreateRotationScale((IntPtr)p,text.Length,encoding,font,positions);}
    public static SKTextBlob? CreateRotationScale(IntPtr text,int length,SKTextEncoding encoding,SKFont font,ReadOnlySpan<SKRotationScaleMatrix> positions)
    {
        ArgumentNullException.ThrowIfNull(font);int count=font.CountGlyphs(text,length,encoding);if(count==0)return null;if(positions.Length!=count)throw new ArgumentException("One transform is required per glyph.",nameof(positions));
        using var builder=new SKTextBlobBuilder();var run=builder.AllocateRawRotationScaleRun(font,count);font.GetGlyphs(text,length,encoding,run.Glyphs);positions.CopyTo(run.Positions);return builder.Build();
    }
    public static SKTextBlob? CreatePathPositioned(string text,SKFont font,SKPath path,SKTextAlign textAlign=SKTextAlign.Left,SKPoint origin=default)=>CreatePathPositioned(text.AsSpan(),font,path,textAlign,origin);
    public static SKTextBlob? CreatePathPositioned(ReadOnlySpan<char> text,SKFont font,SKPath path,SKTextAlign textAlign=SKTextAlign.Left,SKPoint origin=default)
    {fixed(char* p=text)return CreatePathPositioned((IntPtr)p,checked(text.Length*2),SKTextEncoding.Utf16,font,path,textAlign,origin);}
    public static SKTextBlob? CreatePathPositioned(ReadOnlySpan<byte> text,SKTextEncoding encoding,SKFont font,SKPath path,SKTextAlign textAlign=SKTextAlign.Left,SKPoint origin=default)
    {fixed(byte* p=text)return CreatePathPositioned((IntPtr)p,text.Length,encoding,font,path,textAlign,origin);}
    public static SKTextBlob? CreatePathPositioned(IntPtr text,int length,SKTextEncoding encoding,SKFont font,SKPath path,SKTextAlign textAlign=SKTextAlign.Left,SKPoint origin=default)
    {
        ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(path);int count=font.CountGlyphs(text,length,encoding);if(count==0)return null;
        var ids=System.Buffers.ArrayPool<ushort>.Shared.Rent(count);var widths=System.Buffers.ArrayPool<float>.Shared.Rent(count);var offsets=System.Buffers.ArrayPool<SKPoint>.Shared.Rent(count);
        try
        {
            font.GetGlyphs(text,length,encoding,ids.AsSpan(0,count));font.GetGlyphWidths(ids.AsSpan(0,count),widths.AsSpan(0,count),Span<SKRect>.Empty);font.GetGlyphPositions(ids.AsSpan(0,count),offsets.AsSpan(0,count),origin);
            using var builder=new SKTextBlobBuilder();builder.AddPathPositionedRun(ids.AsSpan(0,count),font,widths.AsSpan(0,count),offsets.AsSpan(0,count),path,textAlign);return builder.Build();
        }
        finally{System.Buffers.ArrayPool<ushort>.Shared.Return(ids);System.Buffers.ArrayPool<float>.Shared.Return(widths);System.Buffers.ArrayPool<SKPoint>.Shared.Return(offsets);}
    }
}
