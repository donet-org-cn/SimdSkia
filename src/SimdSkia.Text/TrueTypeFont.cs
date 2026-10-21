using System.Buffers.Binary;
using System.Text;

namespace SimdSkia.Text;

/// <summary>Managed, unhinted TrueType glyf outlines; cmap 4/12, simple and XY composite glyphs.</summary>
public sealed class TrueTypeFont
{
    private readonly byte[] bytes;
    private readonly Dictionary<string,(int Offset,int Length)> tables=[];
    private readonly Dictionary<ushort,PathGeometry> glyphs=[];
    private readonly int[] offsets;
    private readonly int cmapOffset,cmapLength,cmapFormat,metrics;
    public int UnitsPerEm {get;}
    public int GlyphCount {get;}
    public short Ascender {get;}
    public short Descender {get;}
    public short LineGap {get;}
    public RectF FontBounds {get;}
    public TrueTypeFont(ReadOnlySpan<byte> data)
    {
        bytes=data.ToArray();if(bytes.Length<12)throw new InvalidDataException("Truncated font.");
        uint magic=U32(0);if(magic==0x74746366)throw new NotSupportedException("TrueType collections are not supported yet.");
        if(magic!=0x00010000&&magic!=0x74727565)throw new NotSupportedException("Only TrueType glyf fonts are supported; CFF/WOFF require separate parsers.");
        int count=U16(4);Check(12,checked(count*16));
        for(int i=0;i<count;i++)
        {
            int p=12+i*16;string tag=Encoding.ASCII.GetString(bytes,p,4);int offset=checked((int)U32(p+8)),length=checked((int)U32(p+12));Check(offset,length);
            if(!tables.TryAdd(tag,(offset,length)))throw new InvalidDataException("Duplicate font table.");
        }
        if(tables.ContainsKey("fvar")||tables.ContainsKey("gvar"))throw new NotSupportedException("Variable fonts require variation processing.");
        if(new[]{"COLR","CPAL","CBDT","CBLC","sbix","SVG "}.Any(tables.ContainsKey))throw new NotSupportedException("Color/bitmap/SVG font rendering requires its own color glyph pipeline.");
        var head=Table("head",54);UnitsPerEm=U16(head.Offset+18);if(UnitsPerEm is <16 or >16384)throw new InvalidDataException("Invalid unitsPerEm.");
        FontBounds=new(S16(head.Offset+36),S16(head.Offset+38),S16(head.Offset+40),S16(head.Offset+42));
        GlyphCount=U16(Table("maxp",6).Offset+4);var hhea=Table("hhea",36);metrics=U16(hhea.Offset+34);
        if(metrics<1||metrics>GlyphCount)throw new InvalidDataException("Invalid horizontal metrics.");
        Ascender=S16(hhea.Offset+4);Descender=S16(hhea.Offset+6);LineGap=S16(hhea.Offset+8);
        _=Table("hmtx",checked(metrics*4+(GlyphCount-metrics)*2));var glyf=Table("glyf",0);
        int format=S16(head.Offset+50);if(format is not (0 or 1))throw new InvalidDataException("Invalid loca format.");
        var loca=Table("loca",checked((GlyphCount+1)*(format==0?2:4)));offsets=new int[GlyphCount+1];
        for(int i=0;i<offsets.Length;i++){int offset=format==0?U16(loca.Offset+i*2)*2:checked((int)U32(loca.Offset+i*4));if(offset>glyf.Length||(i>0&&offset<offsets[i-1]))throw new InvalidDataException("Invalid glyph offsets.");offsets[i]=offset;}
        var cmap=Table("cmap",4);int records=U16(cmap.Offset+2);if(4+records*8>cmap.Length)throw new InvalidDataException("Truncated cmap records.");
        int selected=-1,best=0,lengthSelected=0,formatSelected=0;
        for(int i=0;i<records;i++)
        {
            int record=cmap.Offset+4+i*8;int platform=U16(record),encoding=U16(record+2);int offset=checked((int)U32(record+4));
            if(offset<0||offset+2>cmap.Length)throw new InvalidDataException("Invalid cmap subtable offset.");
            int position=cmap.Offset+offset;int f=U16(position);int priority=f==12?2:f==4?1:0;
            if(priority>best&&(platform==0||(platform==3&&encoding is 1 or 10)))
            {
                int length=f==12?checked((int)U32(position+4)):U16(position+2);
                if(length< (f==12?16:16)||length>cmap.Length-offset)throw new InvalidDataException("Invalid cmap subtable length.");
                selected=position;best=priority;lengthSelected=length;formatSelected=f;
            }
        }
        if(selected<0)throw new NotSupportedException("Font has no supported Unicode cmap 4/12.");
        cmapOffset=selected;cmapLength=lengthSelected;cmapFormat=formatSelected;
    }
    public ushort GetGlyph(int codepoint)
    {
        if(!Rune.IsValid(codepoint))throw new ArgumentOutOfRangeException(nameof(codepoint));
        uint glyph=0;
        if(cmapFormat==12)
        {
            uint count=U32(cmapOffset+12);if(count>(uint)((cmapLength-16)/12))throw new InvalidDataException("Invalid cmap groups.");
            int lo=0,hi=(int)count-1;
            while(lo<=hi){int mid=lo+(hi-lo)/2,p=cmapOffset+16+mid*12;uint start=U32(p),end=U32(p+4);if((uint)codepoint<start)hi=mid-1;else if((uint)codepoint>end)lo=mid+1;else{glyph=U32(p+8)+(uint)codepoint-start;break;}}
        }
        else if(codepoint<=65535)
        {
            int count=U16(cmapOffset+6)/2;int endBase=cmapOffset+14,startBase=endBase+count*2+2,deltaBase=startBase+count*2,rangeBase=deltaBase+count*2;
            if(count==0||rangeBase+count*2>cmapOffset+cmapLength)throw new InvalidDataException("Invalid cmap segments.");
            for(int i=0;i<count;i++)
            {
                int end=U16(endBase+i*2);if(codepoint>end)continue;
                int start=U16(startBase+i*2);if(codepoint<start)break;
                int delta=S16(deltaBase+i*2),range=U16(rangeBase+i*2);
                if(range==0)glyph=(uint)((codepoint+delta)&65535);
                else{int p=rangeBase+i*2+range+(codepoint-start)*2;if(p<cmapOffset||p+2>cmapOffset+cmapLength)throw new InvalidDataException("Invalid cmap glyph offset.");glyph=U16(p);if(glyph!=0)glyph=(uint)((glyph+delta)&65535);}break;
            }
        }
        if(glyph>=GlyphCount)throw new InvalidDataException("cmap references invalid glyph.");return (ushort)glyph;
    }
    public int Advance(ushort glyph){if(glyph>=GlyphCount)throw new ArgumentOutOfRangeException(nameof(glyph));return U16(Table("hmtx",0).Offset+Math.Min(glyph,metrics-1)*4);}
    public PathGeometry Outline(ushort glyph)
    {
        if(glyph>=GlyphCount)throw new ArgumentOutOfRangeException(nameof(glyph));
        lock(glyphs)
        {
            if(glyphs.TryGetValue(glyph,out var cached))return cached;
            var result=ReadGlyph(glyph,new HashSet<ushort>(),0);glyphs.Add(glyph,result);return result;
        }
    }
    private PathGeometry ReadGlyph(ushort glyph,HashSet<ushort> active,int depth)
    {
        if(depth>32||!active.Add(glyph))throw new InvalidDataException("Cyclic/deep composite glyph.");
        try
        {
            int size=offsets[glyph+1]-offsets[glyph];if(size==0)return new([]);
            var reader=new GlyphReader(bytes,Table("glyf",0).Offset+offsets[glyph],size);int contours=reader.Short();reader.Skip(8);
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
    private void Check(int offset,int length){if(offset<0||length<0||offset>bytes.Length-length)throw new InvalidDataException("Font table outside file.");}
    private ushort U16(int p){Check(p,2);return BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p,2));}
    private short S16(int p)=>unchecked((short)U16(p));
    private uint U32(int p){Check(p,4);return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(p,4));}
    private sealed class GlyphReader(byte[] bytes,int offset,int size)
    {
        private int position=offset;
        private readonly int end=checked(offset+size);
        public byte Byte(){if(position>=end)throw new InvalidDataException("Truncated glyph.");return bytes[position++];}
        public sbyte SByte()=>unchecked((sbyte)Byte());
        public ushort UShort()=>(ushort)((Byte()<<8)|Byte());
        public short Short()=>unchecked((short)UShort());
        public void Skip(int count){if(count<0||position>end-count)throw new InvalidDataException("Truncated glyph instructions.");position+=count;}
    }
}
