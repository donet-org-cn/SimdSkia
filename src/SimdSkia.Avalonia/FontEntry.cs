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
    internal string Family {get;}
    internal FontWeight Weight {get;}
    internal FontStyle Style {get;}
    internal FontStretch Stretch {get;}
    internal bool IsLoaded=>data.IsValueCreated;
    internal FontData Data=>data.Value;
    internal FontEntry(FontData value)
    {Family=value.Family;Weight=value.Weight;Style=value.Style;Stretch=value.Stretch;data=new(()=>value);_ = data.Value;}
    private FontEntry(string family,FontWeight weight,FontStyle style,FontStretch stretch,Lazy<FontSource> file,int face)
    {
        Family=family;Weight=weight;Style=style;Stretch=stretch;
        data=new(()=>
        {
            var value=new FontData(file.Value,face);
            if(value.Family!=Family||value.Weight!=Weight||value.Style!=Style||value.Stretch!=Stretch)
                throw new IOException("Installed font metadata changed between discovery and use.");
            return value;
        });
    }
    internal static IReadOnlyList<FontEntry> ReadSystemFile(string path)
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
        if(U32(header,0)==0x74746366)
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
            if(!tables.ContainsKey("glyf")||new[]{"fvar","gvar","COLR","CPAL","CBDT","CBLC","sbix","SVG "}.Any(tables.ContainsKey))
            {Excluded(face,"unsupported variable/color/CFF rendering");continue;}
            byte[] Table(string name,int minimum,int maximum)
            {
                if(!tables.TryGetValue(name,out var value)||value.Length<minimum||value.Length>maximum)throw new InvalidDataException($"Missing/truncated/oversized font metadata: {name}.");
                return Read(value.Offset,value.Length);
            }
            var names=Table("name",6,1024*1024);
            string family=FontData.ReadName(names,16)??FontData.ReadName(names,1)??throw new InvalidDataException("Font has no family name.");
            var os2=tables.ContainsKey("OS/2")?Table("OS/2",0,4096):[];var head=Table("head",54,4096);
            var weight=os2.Length>=8?(FontWeight)U16(os2,4):FontWeight.Normal;
            var stretch=os2.Length>=8?(FontStretch)U16(os2,6):FontStretch.Normal;
            var style=(U16(head,44)&2)!=0?FontStyle.Italic:FontStyle.Normal;
            result.Add(new(family,weight,style,stretch,snapshot,face));
        }
        return result;
        void Excluded(int face,string reason)=>System.Diagnostics.Trace.WriteLine($"Excluded installed font {path}, face {face}: {reason}.");
    }
}
