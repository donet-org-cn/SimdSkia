using System.Buffers.Binary;
using System.Text;

namespace SimdSkia.Text;

/// <summary>Managed, unhinted TrueType glyf outlines; cmap 4/12, simple and XY composite glyphs.</summary>
public sealed partial class TrueTypeFont
{
    private readonly FontSource bytes;
    private readonly Dictionary<string,ReadOnlyMemory<byte>> tableMemory=[];
    private int tableMemoryBytes;
    private readonly int collectionFace;
    private readonly bool isCollection;
    private readonly Dictionary<string,(int Offset,int Length)> tables=[];
    private sealed class RawGlyphEntry(PathGeometry geometry,long bytes)
    {internal readonly PathGeometry Geometry=geometry;internal readonly long Bytes=bytes;internal bool Referenced=true;}
    private readonly Dictionary<ushort,RawGlyphEntry> glyphs=[];
    private readonly Queue<ushort> glyphClock=[];
    private long rawGlyphBytes;
    internal (int Count,long Bytes) RawCacheUsage {get{lock(glyphs)return(glyphs.Count,rawGlyphBytes);}}
    internal void PurgeRawGlyphCache(){lock(glyphs){glyphs.Clear();glyphClock.Clear();rawGlyphBytes=0;}}
    internal long TrimCachedGeometry(long bytesToPurge)
    {
        long freed=0;
        lock(glyphs)while(glyphs.Count>0&&freed<bytesToPurge)
        {
            ushort key=glyphClock.Dequeue();var value=glyphs[key];
            if(value.Referenced){value.Referenced=false;glyphClock.Enqueue(key);continue;}
            glyphs.Remove(key);rawGlyphBytes-=value.Bytes;freed+=value.Bytes;
        }
        lock(canonicalGate)while(canonicalOrder.First is {} node&&freed<bytesToPurge)
        {canonicalOrder.RemoveFirst();canonicalGlyphs.Remove(node.Value.Id);canonicalBytes-=node.Value.Data.Bytes;freed+=node.Value.Data.Bytes;}
        return freed;
    }
    private PairPositioning? positioning;
    public GlyphPairAdjustment GetDefaultHorizontalPairAdjustment(ushort first,ushort second)
    {
        if(first>=GlyphCount||second>=GlyphCount)throw new ArgumentOutOfRangeException(nameof(first));
        var table=Volatile.Read(ref positioning);
        if(table is null){var created=new PairPositioning(this);table=Interlocked.CompareExchange(ref positioning,created,null)??created;}
        return table.Get(first,second);
    }
    private readonly ReadOnlyMemory<byte> glyphLocations;
    private readonly bool longLocations;
    private readonly int glyphTableOffset;
    private readonly int metrics;
    private readonly UnicodeCmap unicodeCmap;
    private readonly ColorGlyphLayers? colorLayers;
    private readonly BitmapGlyphs? bitmapGlyphs;
    private ColorGlyphComposition? colorComposition;
    internal bool HasColorGlyphs=>colorLayers is not null||bitmapGlyphs is not null;
    internal GlyphBitmap? GetBitmapGlyph(ushort glyph,float ppem)=>bitmapGlyphs?.Find(glyph,ppem);
    internal ReadOnlyMemory<byte> ReadBitmapGlyph(in GlyphBitmap bitmap)=>bitmapGlyphs!.Read(bitmap);
    internal bool HasColorGlyph(ushort glyph)
        => !GetColorLayers(glyph).IsEmpty||GetBitmapGlyph(glyph,24) is not null;
    internal ColorGlyphComposition ColorComposition
    {
        get
        {
            var current=Volatile.Read(ref colorComposition);
            if(current is not null)return current;
            var parsed=new ColorGlyphComposition(this);
            return Interlocked.CompareExchange(ref colorComposition,parsed,null)??parsed;
        }
    }
    internal ReadOnlySpan<ColorGlyphLayer> GetColorLayers(ushort glyph)=>colorLayers is null?[]:colorLayers.Get(glyph);
    private readonly ReadOnlyMemory<byte> characterMap,horizontalMetrics;
    public int UnitsPerEm {get;}
    public int GlyphCount {get;}
    public short Ascender {get;}
    public short Descender {get;}
    public short LineGap {get;}
    public RectF FontBounds {get;}
    public bool TryGetTable(string tag,out ReadOnlyMemory<byte> table)
    {
        if(tables.TryGetValue(tag,out var entry))
        {
            lock(tableMemory)
            {
                if(!tableMemory.TryGetValue(tag,out table))
                {
                    table=bytes.Read(entry.Offset,entry.Length);
                    // Large explicit table exports should not permanently grow
                    // the installed-font cache. The caller owns returned memory.
                    if(table.Length<=1024*1024&&tableMemoryBytes+table.Length<=2*1024*1024)
                    {tableMemory.Add(tag,table);tableMemoryBytes+=table.Length;}
                }
            }
            return true;
        }
        table=default;return false;
    }
    internal uint[] GetTableTags()=>tables.Keys.Select(tag=>((uint)tag[0]<<24)|((uint)tag[1]<<16)|((uint)tag[2]<<8)|tag[3]).Order().ToArray();
    public Stream OpenStream()=>isCollection?new MemoryStream(FontCollection.Extract(bytes,collectionFace),false):new FontReadStream(bytes);
    internal static TrueTypeFont FromFile(string path,int faceIndex)
    {
        var file=new FileInfo(path);
        return new(FontSource.OpenInstalled(path,file.Length,file.LastWriteTimeUtc),faceIndex);
    }
    public TrueTypeFont(ReadOnlySpan<byte> data):this(data,0) { }
    public TrueTypeFont(ReadOnlySpan<byte> data,int faceIndex):this(FontCollection.Extract(data,faceIndex),0,true) { }
    // The platform font manager owns one immutable file snapshot. TTC faces use
    // its original table offsets so shared outlines are not copied per face.
    internal static TrueTypeFont FromOwnedCollection(byte[] data,int faceIndex)=>new(new FontSource(data),faceIndex);
    internal static TrueTypeFont FromInstalledSource(FontSource data,int faceIndex)=>new(data,faceIndex);
    private TrueTypeFont(byte[] data,int faceIndex,bool owned):this(new FontSource(data),faceIndex) { }
    private TrueTypeFont(FontSource data,int faceIndex)
    {
        bytes=data;int faces=bytes.FaceCount();if(faceIndex<0||faceIndex>=faces)throw new ArgumentOutOfRangeException(nameof(faceIndex));
        isCollection=U32(0)==0x74746366;collectionFace=faceIndex;
        int directory=isCollection?checked((int)U32(12+faceIndex*4)):0;
        uint magic=U32(directory);
        if(magic!=0x00010000&&magic!=0x74727565)throw new NotSupportedException("Only TrueType glyf fonts are supported; CFF/WOFF require separate parsers.");
        int count=U16(directory+4);Check(directory+12,checked(count*16));
        for(int i=0;i<count;i++)
        {
            int p=directory+12+i*16;string tag=Encoding.ASCII.GetString(bytes.Read(p,4).Span);int offset=checked((int)U32(p+8)),length=checked((int)U32(p+12));Check(offset,length);
            if(!tables.TryAdd(tag,(offset,length)))throw new InvalidDataException("Duplicate font table.");
        }
        if(tables.ContainsKey("fvar")||tables.ContainsKey("gvar"))throw new NotSupportedException("Variable fonts require variation processing.");
        if(tables.ContainsKey("SVG "))throw new NotSupportedException("SVG glyphs require their own renderer.");
        var head=Table("head",54);UnitsPerEm=U16(head.Offset+18);if(UnitsPerEm is <16 or >16384)throw new InvalidDataException("Invalid unitsPerEm.");
        FontBounds=new(S16(head.Offset+36),S16(head.Offset+38),S16(head.Offset+40),S16(head.Offset+42));
        GlyphCount=U16(Table("maxp",6).Offset+4);var hhea=Table("hhea",36);metrics=U16(hhea.Offset+34);
        if(tables.ContainsKey("COLR"))
        {
            var colr=Table("COLR",ColorGlyphLayers.HeaderBytes);var cpal=Table("CPAL",14);
            colorLayers=ColorGlyphLayers.Read(bytes,colr.Offset,colr.Length,cpal.Offset,cpal.Length,GlyphCount);
        }
        if(tables.TryGetValue("sbix",out var sbix))
            bitmapGlyphs=BitmapGlyphs.ReadSbix(bytes,sbix.Offset,sbix.Length,GlyphCount);
        if(tables.ContainsKey("CBDT")||tables.ContainsKey("CBLC"))
        {
            var cbdt=Table("CBDT",4);var cblc=Table("CBLC",8);
            var colorBitmaps=BitmapGlyphs.ReadCbdt(bytes,cbdt.Offset,cbdt.Length,cblc.Offset,cblc.Length,GlyphCount);
            bitmapGlyphs=bitmapGlyphs is null?colorBitmaps:bitmapGlyphs.Append(colorBitmaps);
        }
        if(metrics<1||metrics>GlyphCount)throw new InvalidDataException("Invalid horizontal metrics.");
        Ascender=S16(hhea.Offset+4);Descender=S16(hhea.Offset+6);LineGap=S16(hhea.Offset+8);
        var hm=Table("hmtx",checked(metrics*4+(GlyphCount-metrics)*2));horizontalMetrics=bytes.Read(hm.Offset,hm.Length);
        if(bitmapGlyphs is null||tables.ContainsKey("glyf")||tables.ContainsKey("loca"))
        {
            var glyf=Table("glyf",0);
            int format=S16(head.Offset+50);if(format is not (0 or 1))throw new InvalidDataException("Invalid loca format.");
            longLocations=format==1;glyphTableOffset=glyf.Offset;
            int locationBytes=(GlyphCount+1)*(longLocations?4:2);
            var loca=Table("loca",locationBytes);
            glyphLocations=bytes.Read(loca.Offset,locationBytes);
            int previousOffset=0;
            for(int i=0;i<=GlyphCount;i++)
            {
                int offset=GlyphOffset(i);
                if(offset<previousOffset||offset>glyf.Length)throw new InvalidDataException("Invalid glyph offsets.");
                previousOffset=offset;
            }
        }
        var cmap=Table("cmap",4);
        unicodeCmap=UnicodeCmap.Select(cmap.Length,(offset,length)=>data.Read(cmap.Offset+offset,length));
        characterMap=bytes.Read(cmap.Offset+unicodeCmap.Offset,unicodeCmap.Length);
        unicodeCmap.Validate(characterMap.Span);

    }
    private readonly ulong[] glyphMapCache=new ulong[128];
    public ushort GetGlyph(int codepoint)
    {
        if(!Rune.IsValid(codepoint))throw new ArgumentOutOfRangeException(nameof(codepoint));
        int slot=codepoint&127;ulong key=(ulong)((uint)codepoint+1)<<32,value=Volatile.Read(ref glyphMapCache[slot]);
        if((value&0xffffffff00000000UL)==key)return (ushort)value;
        ushort glyph=unicodeCmap.GetGlyph(characterMap.Span,codepoint,GlyphCount);
        Volatile.Write(ref glyphMapCache[slot],key|glyph);return glyph;
    }
    public int Advance(ushort glyph){if(glyph>=GlyphCount)throw new ArgumentOutOfRangeException(nameof(glyph));return BinaryPrimitives.ReadUInt16BigEndian(horizontalMetrics.Span[(Math.Min(glyph,metrics-1)*4)..]);}
    private int GlyphOffset(int index)=>longLocations
        ?checked((int)BinaryPrimitives.ReadUInt32BigEndian(glyphLocations.Span[(index*4)..]))
        :BinaryPrimitives.ReadUInt16BigEndian(glyphLocations.Span[(index*2)..])*2;
    private ReadOnlySpan<byte> GlyphData(ushort glyph,int maxLength=int.MaxValue)
    {
        if(glyphLocations.IsEmpty)return [];
        int start=GlyphOffset(glyph),length=GlyphOffset(glyph+1)-start;
        return bytes.Read(glyphTableOffset+start,Math.Min(length,maxLength)).Span;
    }
    public RectF GetGlyphBounds(ushort glyph)
    {
        if(glyph>=GlyphCount)throw new ArgumentOutOfRangeException(nameof(glyph));
        var header=GlyphData(glyph,10);
        if(header.IsEmpty)return default;
        if(header.Length<10)throw new InvalidDataException("Truncated glyph header.");
        return new(BinaryPrimitives.ReadInt16BigEndian(header[2..]),BinaryPrimitives.ReadInt16BigEndian(header[4..]),BinaryPrimitives.ReadInt16BigEndian(header[6..]),BinaryPrimitives.ReadInt16BigEndian(header[8..]));
    }
    public PathGeometry Outline(ushort glyph)
    {
        if(glyph>=GlyphCount)throw new ArgumentOutOfRangeException(nameof(glyph));
        PathGeometry result;
        lock(glyphs)
        {
            if(glyphs.TryGetValue(glyph,out var cached)){cached.Referenced=true;return cached.Geometry;}
            result=ReadGlyph(glyph,new HashSet<ushort>(),0);long size=(long)result.Segments.Length*28+96;
            // A long-lived Chinese font must not accumulate every outline it
            // ever sees. Second chance avoids reference relinking on hot hits.
            if(size<=4*1024*1024&&Volatile.Read(ref FontCacheRegistry.ByteLimit)>0&&Volatile.Read(ref FontCacheRegistry.CountLimit)>0)
            {
                while(glyphs.Count>=512||rawGlyphBytes+size>4*1024*1024)
                {
                    ushort candidate=glyphClock.Dequeue();var entry=glyphs[candidate];
                    if(entry.Referenced){entry.Referenced=false;glyphClock.Enqueue(candidate);continue;}
                    glyphs.Remove(candidate);rawGlyphBytes-=entry.Bytes;
                }
                glyphs.Add(glyph,new(result,size));glyphClock.Enqueue(glyph);rawGlyphBytes+=size;
            }
        }
        FontCacheRegistry.Notify(this);return result;
    }
    private PathGeometry ReadGlyph(ushort glyph,HashSet<ushort> active,int depth)
    {
        if(depth>32||!active.Add(glyph))throw new InvalidDataException("Cyclic/deep composite glyph.");
        try
        {
            var data=GlyphData(glyph);if(data.IsEmpty)return new([]);
            var reader=new GlyphReader(data);int contours=reader.Short();reader.Skip(8);
            var segments=new List<PathSegment>();
            if(contours>=0)
            {
                if(contours==0)return new([]);var ends=new int[contours];for(int i=0;i<contours;i++){ends[i]=reader.UShort();if(i>0&&ends[i]<=ends[i-1])throw new InvalidDataException("Invalid contour endpoints.");}
                int points=ends[^1]+1;reader.Skip(reader.UShort());var flags=new byte[points];
                for(int i=0;i<points;i++){byte flag=reader.Byte();flags[i]=flag;if((flag&8)!=0){int repeats=reader.Byte();if(repeats>=points-i)throw new InvalidDataException("Invalid glyph flags repeat.");while(repeats-->0)flags[++i]=flag;}}
                var coordinates=new PointF[points];int x=0,y=0;
                for(int i=0;i<points;i++){byte f=flags[i];x+=(f&2)!=0?((f&16)!=0?reader.Byte():-reader.Byte()):(f&16)!=0?0:reader.Short();coordinates[i]=new(x,0);}
                for(int i=0;i<points;i++){byte f=flags[i];y+=(f&4)!=0?((f&32)!=0?reader.Byte():-reader.Byte()):(f&32)!=0?0:reader.Short();coordinates[i]=coordinates[i] with {Y=y};}
                int first=0;
                foreach(int end in ends)
                {
                    bool On(int index)=>(flags[index]&1)!=0;
                    PointF start;int index,remaining=end-first+1;
                    if(On(first)){start=coordinates[first];index=first+1;remaining--;}
                    else if(On(end)){start=coordinates[end];index=first;remaining--;}
                    else{start=(coordinates[first]+coordinates[end])*0.5f;index=first;}
                    segments.Add(new(PathVerb.Move,start));
                    while(remaining>0)
                    {
                        int next=index>end?first:index;var point=coordinates[next];
                        if(On(next)){segments.Add(new(PathVerb.Line,point));index=next+1;remaining--;}
                        else
                        {
                            int following=next==end?first:next+1;var target=remaining==1?start:On(following)?coordinates[following]:(point+coordinates[following])*0.5f;
                            segments.Add(new(PathVerb.Quadratic,point,target));index=following;remaining--;
                            if(remaining>0&&On(following)){index=following+1;remaining--;}
                        }
                    }
                    segments.Add(new(PathVerb.Close,default));first=end+1;
                }
            }
            else
            {
                int flags;
                do
                {
                    flags=reader.UShort();ushort child=reader.UShort();if(child>=GlyphCount)throw new InvalidDataException("Invalid composite glyph index.");
                    bool words=(flags&1)!=0,xy=(flags&2)!=0;
                    int a=words?(xy?reader.Short():reader.UShort()):(xy?reader.SByte():reader.Byte());
                    int b=words?(xy?reader.Short():reader.UShort()):(xy?reader.SByte():reader.Byte());
                    if(!xy)throw new NotSupportedException("Composite point attachment is not supported yet.");
                    float xx=1,yy=1,xyScale=0,yx=0;
                    if((flags&8)!=0)xx=yy=reader.Short()/16384f;
                    else if((flags&64)!=0){xx=reader.Short()/16384f;yy=reader.Short()/16384f;}
                    else if((flags&128)!=0){xx=reader.Short()/16384f;xyScale=reader.Short()/16384f;yx=reader.Short()/16384f;yy=reader.Short()/16384f;}
                    float dx=a,dy=b;if((flags&2048)!=0){dx=xx*a+xyScale*b;dy=yx*a+yy*b;}
                    if((flags&4)!=0){dx=MathF.Round(dx);dy=MathF.Round(dy);}
                    var transform=new Matrix3(xx,xyScale,dx,yx,yy,dy,0,0,1);
                    segments.AddRange(ReadGlyph(child,active,depth+1).Transform(transform).Segments.ToArray());
                }while((flags&32)!=0);
                if((flags&256)!=0)reader.Skip(reader.UShort());
            }
            return new(segments);
        }
        finally{active.Remove(glyph);}
    }
    private (int Offset,int Length) Table(string name,int minimum){if(!tables.TryGetValue(name,out var table)||table.Length<minimum)throw new InvalidDataException("Missing/truncated font table: "+name);return table;}
    private void Check(int offset,int length)=>bytes.Check(offset,length);
    private ushort U16(int p)=>bytes.U16(p);
    private short S16(int p)=>unchecked((short)U16(p));
    private uint U32(int p)=>bytes.U32(p);
    private ref struct GlyphReader(ReadOnlySpan<byte> bytes)
    {
        private readonly ReadOnlySpan<byte> bytes=bytes;
        private int position;
        private readonly int end=bytes.Length;
        public byte Byte(){if(position>=end)throw new InvalidDataException("Truncated glyph.");return bytes[position++];}
        public sbyte SByte()=>unchecked((sbyte)Byte());
        public ushort UShort()=>(ushort)((Byte()<<8)|Byte());
        public short Short()=>unchecked((short)UShort());
        public byte[] ReadBytes(int count){if(count<0||position>end-count)throw new InvalidDataException("Truncated glyph program.");var data=bytes.Slice(position,count).ToArray();position+=count;return data;}
        public void Skip(int count){if(count<0||position>end-count)throw new InvalidDataException("Truncated glyph instructions.");position+=count;}
    }
}
