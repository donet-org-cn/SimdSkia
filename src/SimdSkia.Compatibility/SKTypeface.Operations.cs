using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
namespace SkiaSharp;
public partial class SKTypeface
{
    private SKFont? measurementFont;
    private SKFont GetFont(){ThrowIfDisposed();return measurementFont??=new(this);}
    public bool IsFixedPitch{get{ThrowIfDisposed();return Font.TryGetTable("post",out var t)&&t.Length>=16&&BinaryPrimitives.ReadUInt32BigEndian(t.Span[12..])!=0;}}
    public string PostScriptName=>ReadFontName(6)!;
    internal string? ReadFontName(int id)
    {
            ThrowIfDisposed();if(!Font.TryGetTable("name",out var table)||table.Length<6)return null!;var t=table.Span;int count=U16(t,2),baseAt=U16(t,4);string? best=null;int priority=-1;
            for(int i=0;i<count;i++){int r=6+i*12;if(r>t.Length-12)throw new InvalidDataException("Truncated name table.");if(U16(t,r+6)!=id)continue;int platform=U16(t,r),at=baseAt+U16(t,r+10),length=U16(t,r+8);if(at>t.Length-length)throw new InvalidDataException("Truncated font name.");int score=platform==3?3:platform==0?2:platform==1?1:0;if(score<=priority)continue;best=platform is 0 or 3?Encoding.BigEndianUnicode.GetString(t.Slice(at,length)):Encoding.Latin1.GetString(t.Slice(at,length));priority=score;}
            return best!;
    }
    private static ushort U16(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16BigEndian(bytes[at..]);
    private static string TableTag(uint tag)=>string.Create(4,tag,(chars,value)=>{chars[0]=(char)(value>>24);chars[1]=(char)((value>>16)&255);chars[2]=(char)((value>>8)&255);chars[3]=(char)(value&255);});
    public int TableCount{get{ThrowIfDisposed();return Font.GetTableTags().Length;}}
    public uint[] GetTableTags(){ThrowIfDisposed();return Font.GetTableTags();}
    public bool TryGetTableTags(out uint[] tags){tags=GetTableTags();return tags.Length!=0;}
    public int GetTableSize(uint tag){ThrowIfDisposed();return Font.TryGetTable(TableTag(tag),out var table)?table.Length:0;}
    public byte[] GetTableData(uint tag){if(!TryGetTableData(tag,out var data))throw new Exception("Unable to read the data table.");return data;}
    public bool TryGetTableData(uint tag,out byte[] tableData){ThrowIfDisposed();if(!Font.TryGetTable(TableTag(tag),out var table)||table.IsEmpty){tableData=null!;return false;}tableData=table.ToArray();return true;}
    public unsafe bool TryGetTableData(uint tag,int offset,int length,IntPtr tableData)
    {ThrowIfDisposed();if(offset<0||length<=0||!Font.TryGetTable(TableTag(tag),out var table)||offset>=table.Length)return false;int count=Math.Min(length,table.Length-offset);if(tableData!=IntPtr.Zero)table.Span.Slice(offset,count).CopyTo(new Span<byte>((void*)tableData,count));return true;}
    public static SKTypeface CreateDefault()=>FromFamilyName(null);
    public SKFont ToFont()=>new(this);
    public SKFont ToFont(float size,float scaleX=1,float skewX=0)=>new(this,size,scaleX,skewX);
    public SKStreamAsset OpenStream()=>OpenStream(out _);
    public SKStreamAsset OpenStream(out int ttcIndex){ThrowIfDisposed();ttcIndex=0;using var stream=Font.OpenStream();using var data=SKData.Create(stream);return new SKMemoryStream(data);}
    public static SKTypeface FromStream(SKStreamAsset stream,int index=0){ArgumentNullException.ThrowIfNull(stream);using var data=SKData.Create(stream);return FromData(data,index);}
    public int CountGlyphs(string str)=>GetFont().CountGlyphs(str);
    public int CountGlyphs(ReadOnlySpan<char> str)=>GetFont().CountGlyphs(str);
    public int CountGlyphs(byte[] str,SKTextEncoding encoding)=>GetFont().CountGlyphs(str,encoding);
    public int CountGlyphs(ReadOnlySpan<byte> str,SKTextEncoding encoding)=>GetFont().CountGlyphs(str,encoding);
    public int CountGlyphs(IntPtr str,int strLen,SKTextEncoding encoding)=>GetFont().CountGlyphs(str,checked(strLen*CharacterSize(encoding)),encoding);
    private static int CharacterSize(SKTextEncoding encoding)=>encoding switch{SKTextEncoding.Utf8=>1,SKTextEncoding.Utf16 or SKTextEncoding.GlyphId=>2,SKTextEncoding.Utf32=>4,_=>throw new ArgumentOutOfRangeException(nameof(encoding))};
    public ushort[] GetGlyphs(ReadOnlySpan<int> codepoints)=>GetFont().GetGlyphs(codepoints);
    public ushort[] GetGlyphs(string text)=>GetFont().GetGlyphs(text);
    public ushort[] GetGlyphs(ReadOnlySpan<char> text)=>GetFont().GetGlyphs(text);
    public ushort[] GetGlyphs(ReadOnlySpan<byte> text,SKTextEncoding encoding)=>GetFont().GetGlyphs(text,encoding);
    public ushort[] GetGlyphs(IntPtr text,int length,SKTextEncoding encoding)=>GetFont().GetGlyphs(text,checked(length*CharacterSize(encoding)),encoding);
    public bool ContainsGlyph(int codepoint)=>GetFont().ContainsGlyph(codepoint);
    public bool ContainsGlyphs(ReadOnlySpan<int> codepoints)=>GetFont().ContainsGlyphs(codepoints);
    public bool ContainsGlyphs(string text)=>GetFont().ContainsGlyphs(text);
    public bool ContainsGlyphs(ReadOnlySpan<char> text)=>GetFont().ContainsGlyphs(text);
    public bool ContainsGlyphs(ReadOnlySpan<byte> text,SKTextEncoding encoding)=>GetFont().ContainsGlyphs(text,encoding);
    public bool ContainsGlyphs(IntPtr text,int length,SKTextEncoding encoding)=>GetFont().ContainsGlyphs(text,checked(length*CharacterSize(encoding)),encoding);
    public bool HasGetKerningPairAdjustments{get{ThrowIfDisposed();return Font.TryGetTable("kern",out var table)&&table.Length>=4&&U16(table.Span,2)!=0;}}
    public int[] GetKerningPairAdjustments(ReadOnlySpan<ushort> glyphs){var result=new int[glyphs.Length];GetKerningPairAdjustments(glyphs,result);return result;}
    public bool GetKerningPairAdjustments(ReadOnlySpan<ushort> glyphs,Span<int> adjustments)
    {
        ThrowIfDisposed();int count=Math.Max(0,glyphs.Length-1);if(adjustments.Length<count)throw new ArgumentException("One adjustment per adjacent pair is required.",nameof(adjustments));adjustments[..count].Clear();if(!Font.TryGetTable("kern",out var memory)||memory.Length<4)return false;var table=memory.Span;if(U16(table,0)!=0)throw new NotSupportedException("Apple kern v1 requires its own parser.");int at=4;bool supported=false;
        for(int sub=0;sub<U16(table,2);sub++)
        {
            if(at>table.Length-6)throw new InvalidDataException("Truncated kern subtable.");int length=U16(table,at+2),flags=U16(table,at+4);if(length<6||at>table.Length-length)throw new InvalidDataException("Invalid kern length.");
            if((flags>>8)==0&&(flags&7)==1)
            {
                if(length<14)throw new InvalidDataException("Truncated pair table.");int pairs=U16(table,at+6);if(pairs>(length-14)/6)throw new InvalidDataException("Invalid kern pair count.");supported=true;
                for(int i=0;i<count;i++){uint key=((uint)glyphs[i]<<16)|glyphs[i+1];int lo=0,hi=pairs-1;while(lo<=hi){int mid=(lo+hi)>>1,p=at+14+mid*6;uint candidate=BinaryPrimitives.ReadUInt32BigEndian(table[p..]);if(key<candidate)hi=mid-1;else if(key>candidate)lo=mid+1;else{int value=BinaryPrimitives.ReadInt16BigEndian(table[(p+4)..]);if((flags&8)!=0)adjustments[i]=value;else adjustments[i]+=value;break;}}}
            }
            at+=length;
        }
        return supported;
    }
}
