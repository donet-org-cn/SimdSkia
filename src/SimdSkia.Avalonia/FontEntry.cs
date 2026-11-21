using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Avalonia.Media;
using SimdSkia.Text;

namespace SimdSkia.Avalonia;

// Installed font enumeration needs family/style metadata, not every glyf table.
// One bounded, file-backed source is opened on first use and shared by TTC faces.
internal sealed class FontEntry
{
    private readonly Lazy<FontData> data;
    private readonly InstalledCmap? cmap;
    internal string Family {get;}
    internal FontWeight Weight {get;}
    internal FontStyle Style {get;}
    internal FontStretch Stretch {get;}
    internal CjkLocale Locale {get;}
    internal bool HasColorGlyphs {get;}
    internal bool IsLoaded=>data.IsValueCreated;
    internal FontData Data=>data.Value;
    internal FontEntry(FontData value)
    {Family=value.Family;Locale=CjkFontPreference.ForFamily(Family);Weight=value.Weight;Style=value.Style;Stretch=value.Stretch;HasColorGlyphs=value.Font.HasColorGlyphs;data=new(()=>value);_ = data.Value;}
    private FontEntry(string family,FontWeight weight,FontStyle style,FontStretch stretch,Lazy<FontSource> file,int face,InstalledCmap? cmap,bool hasColorGlyphs)
    {
        Family=family;Locale=CjkFontPreference.ForFamily(Family);Weight=weight;Style=style;Stretch=stretch;
        this.cmap=cmap;
        HasColorGlyphs=hasColorGlyphs;
        data=new(()=>
        {
            var value=new FontData(file.Value,face);
            if(value.Family!=Family||value.Weight!=Weight||value.Style!=Style||value.Stretch!=Stretch)
                throw new IOException("Installed font metadata changed between discovery and use.");
            return value;
        });
    }
    internal bool MayContain(int codepoint)=>cmap is null || cmap.MayContain(codepoint);
    internal bool HasGlyph(int codepoint)=>IsLoaded || cmap is null
        ? Data.Font.GetGlyph(codepoint)!=0 : cmap.HasGlyph(codepoint);

    private sealed record InstalledCmap(string Path,long FileLength,DateTime WriteTime,int Position,
        UnicodeCmap Map,int GlyphCount,ulong[] Pages)
    {
        internal bool MayContain(int codepoint)
        {
            int page=codepoint>>8, word=page>>6;
            return (uint)word<(uint)Pages.Length && (Pages[word]&(1UL<<(page&63)))!=0;
        }

        internal bool HasGlyph(int codepoint)
        {
            using var stream=File.OpenRead(Path);
            Verify();
            var rented=ArrayPool<byte>.Shared.Rent(Map.Length);
            try
            {
                var table=rented.AsSpan(0,Map.Length);
                stream.Position=Position;
                stream.ReadExactly(table);
                Verify();
                return Map.GetGlyph(table,codepoint,GlyphCount)!=0;
            }
            finally { ArrayPool<byte>.Shared.Return(rented); }

            void Verify()
            {
                if(stream.Length!=FileLength||File.GetLastWriteTimeUtc(stream.SafeFileHandle)!=WriteTime)
                    throw new IOException("Installed font changed between discovery and use.");
            }
        }
    }

    internal static IReadOnlyList<FontEntry> ReadSystemFile(string path,bool indexCoverage=false)
    {
        using var stream=File.OpenRead(path);long length=stream.Length;var write=File.GetLastWriteTimeUtc(stream.SafeFileHandle);
        if(length>int.MaxValue)throw new NotSupportedException("Font files larger than 2 GiB are not supported.");
        byte[] Read(long offset,int count)
        {
            if(offset<0||count<0||offset>length-count)throw new InvalidDataException("Font metadata outside file.");
            var bytes=new byte[count];stream.Position=offset;stream.ReadExactly(bytes);return bytes;
        }
        static ushort U16(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16BigEndian(bytes[at..]);
        static uint U32(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]);
        var header=Read(0,12);uint[] offsets;
        bool collection=U32(header,0)==0x74746366;
        if(collection)
        {
            uint version=U32(header,4),count=U32(header,8);
            if(version is not (0x00010000 or 0x00020000))throw new NotSupportedException("Unsupported TTC version.");
            if(count is 0 or >4096)throw new InvalidDataException("Invalid TTC face count.");
            var directory=Read(12,checked((int)count*4));offsets=new uint[count];
            for(int i=0;i<offsets.Length;i++)offsets[i]=U32(directory,i*4);
            if(version==0x00020000)
            {
                var signature=Read(12+count*4,12);uint tag=U32(signature,0),size=U32(signature,4),at=U32(signature,8);
                if(tag==0?(size!=0||at!=0):(tag!=0x44534947||at>length||size>length-at))throw new InvalidDataException("Invalid TTC signature directory.");
            }
        }
        else offsets=[0];
        var snapshot=new Lazy<FontSource>(()=>FontSource.OpenInstalled(path,length,write));var result=new List<FontEntry>();
        for(int face=0;face<offsets.Length;face++)
        {
            try
            {
                var faceHeader=Read(offsets[face],12);
                if(U32(faceHeader,0) is not (0x00010000 or 0x74727565)){Excluded(face,"non-TrueType outlines");continue;}
                int count=U16(faceHeader,4);var directory=Read(offsets[face]+12,checked(count*16));
                var tables=new Dictionary<string,(uint Offset,int Length)>();
                for(int i=0;i<count;i++)
                {
                    var entry=directory.AsSpan(i*16,16);uint at=U32(entry,8);int size=checked((int)U32(entry,12));
                    if(at>length||size>length-at)throw new InvalidDataException("Font table outside file.");
                    if(!tables.TryAdd(Encoding.ASCII.GetString(entry[..4]),(at,size)))throw new InvalidDataException("Duplicate font table.");
                }
                bool colorBitmaps=tables.ContainsKey("sbix")||tables.ContainsKey("CBDT")&&tables.ContainsKey("CBLC");
                if((!tables.ContainsKey("glyf")&&!colorBitmaps)||new[]{"fvar","gvar","SVG "}.Any(tables.ContainsKey)||
                    tables.ContainsKey("CBDT")!=tables.ContainsKey("CBLC")||
                    tables.TryGetValue("COLR",out var color)&&(!tables.ContainsKey("CPAL")||color.Length<ColorGlyphLayers.HeaderBytes||
                    !ColorGlyphLayers.HasLegacyRecords(Read(color.Offset,ColorGlyphLayers.HeaderBytes))))
                {Excluded(face,"unsupported variable/color/CFF rendering");continue;}
                byte[] Table(string name,int minimum,int maximum)
                {
                    if(!tables.TryGetValue(name,out var value)||value.Length<minimum||value.Length>maximum)throw new InvalidDataException($"Missing/truncated/oversized font metadata: {name}.");
                    return Read(value.Offset,value.Length);
                }
                var names=Table("name",6,1024*1024);
                string family=FontData.ReadFamilyName(names)??throw new InvalidDataException("Font has no family name.");
                var os2=tables.ContainsKey("OS/2")?Table("OS/2",0,4096):[];var head=Table("head",54,4096);
                var weight=os2.Length>=8?(FontWeight)U16(os2,4):FontWeight.Normal;
                var stretch=os2.Length>=8?(FontStretch)U16(os2,6):FontStretch.Normal;
                var style=(U16(head,44)&2)!=0?FontStyle.Italic:FontStyle.Normal;
                InstalledCmap? cmap=null;
                if(indexCoverage)
                {
                    if(!tables.TryGetValue("cmap",out var table)||table.Length>4*1024*1024)
                        throw new InvalidDataException("Missing or oversized cmap metadata.");
                    int glyphCount=U16(Table("maxp",6,4096),4);
                    var selected=UnicodeCmap.Select(table.Length,(offset,size)=>Read(table.Offset+offset,size));
                    int position=checked((int)table.Offset+selected.Offset);
                    var subtable=Read(position,selected.Length);
                    selected.Validate(subtable);
                    cmap=new(path,length,write,position,selected,glyphCount,selected.GetCoveragePages(subtable));
                }
                result.Add(new(family,weight,style,stretch,snapshot,face,cmap,colorBitmaps||tables.ContainsKey("COLR")));
            }
            catch (Exception error) when (collection && (error is IOException or InvalidDataException or NotSupportedException or OverflowException or ArgumentOutOfRangeException))
            {
                Excluded(face,error.Message);
            }
        }
        return result;
        void Excluded(int face,string reason)=>System.Diagnostics.Trace.WriteLine($"Excluded installed font {path}, face {face}: {reason}.");
    }
}
