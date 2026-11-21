using System.Buffers.Binary;

namespace SimdSkia.Text;

// Shares subtable selection and glyph lookup between full font loading and
// installed font discovery. Offsets are relative to the containing cmap table.
internal readonly record struct UnicodeCmap(int Offset, int Length, int Format)
{
    internal static UnicodeCmap Select(int length, Func<int, int, ReadOnlyMemory<byte>> read)
    {
        if (length < 4) throw new InvalidDataException("Truncated cmap.");
        int records = U16(read(0, 4).Span, 2);
        if (records > (length-4)/8) throw new InvalidDataException("Truncated cmap records.");
        var directory = read(4, records*8).Span;
        UnicodeCmap selected = default;
        for (int i=0; i<records; i++)
        {
            int record=i*8, platform=U16(directory,record), encoding=U16(directory,record+2);
            uint offset=U32(directory,record+4);
            if (offset>length-2) throw new InvalidDataException("Invalid cmap subtable offset.");
            if (platform!=0 && (platform!=3 || encoding is not (1 or 10))) continue;
            int position=(int)offset;
            int format=U16(read(position,2).Span,0);
            if (format is not (4 or 12) || format<=selected.Format) continue;
            int headerLength=format==12 ? 8 : 4;
            if (position>length-headerLength) throw new InvalidDataException("Truncated cmap subtable header.");
            var header=read(position,headerLength).Span;
            uint size=format==12 ? U32(header,4) : U16(header,2);
            if (size<16 || size>length-position) throw new InvalidDataException("Invalid cmap subtable length.");
            selected=new(position,(int)size,format);
        }
        if (selected.Format==0) throw new NotSupportedException("Font has no supported Unicode cmap 4/12.");
        return selected;
    }

    internal void Validate(ReadOnlySpan<byte> map)
    {
        if (Format==12)
        {
            int count=Groups(map);
            uint previous=0;
            for (int i=0; i<count; i++)
            {
                int at=16+i*12;
                uint start=U32(map,at), end=U32(map,at+4);
                if (end>0x10ffff || start>end || (i!=0 && start<=previous))
                    throw new InvalidDataException("cmap groups must be sorted and disjoint Unicode ranges.");
                previous=end;
            }
            return;
        }

        int segments=Segments(map), last=-1;
        for (int i=0; i<segments; i++)
        {
            int end=U16(map,14+i*2), start=U16(map,16+segments*2+i*2);
            if (end<=last || start>end || start<=last)
                throw new InvalidDataException("cmap segments must be sorted and disjoint.");
            last=end;
        }
    }

    internal ushort GetGlyph(ReadOnlySpan<byte> map, int codepoint, int glyphCount)
    {
        uint glyph=0;
        if (Format==12)
        {
            int lo=0, hi=Groups(map)-1;
            while (lo<=hi)
            {
                int mid=lo+(hi-lo)/2, at=16+mid*12;
                uint start=U32(map,at), end=U32(map,at+4);
                if ((uint)codepoint<start) hi=mid-1;
                else if ((uint)codepoint>end) lo=mid+1;
                else
                {
                    ulong mapped=(ulong)U32(map,at+8)+(uint)codepoint-start;
                    if (mapped>ushort.MaxValue) throw new InvalidDataException("cmap references invalid glyph.");
                    glyph=(uint)mapped;
                    break;
                }
            }
        }
        else if (codepoint<=65535)
        {
            int count=Segments(map), ends=14, starts=16+count*2, deltas=starts+count*2, ranges=deltas+count*2;
            int lo=0, hi=count-1;
            while (lo<hi)
            {
                int mid=(lo+hi)/2;
                if (U16(map,ends+mid*2)<codepoint) lo=mid+1;
                else hi=mid;
            }
            int start=U16(map,starts+lo*2), end=U16(map,ends+lo*2);
            if (codepoint>=start && codepoint<=end)
            {
                int delta=unchecked((short)U16(map,deltas+lo*2)), at=ranges+lo*2;
                int range=U16(map,at);
                if (range==0) glyph=(uint)((codepoint+delta)&65535);
                else
                {
                    int offset=at+range+(codepoint-start)*2;
                    if (offset>map.Length-2) throw new InvalidDataException("Invalid cmap glyph offset.");
                    glyph=U16(map,offset);
                    if (glyph!=0) glyph=(uint)((glyph+delta)&65535);
                }
            }
        }
        if (glyph>=glyphCount) throw new InvalidDataException("cmap references invalid glyph.");
        return (ushort)glyph;
    }

    // One bit per 256 codepoints. This conservative index avoids opening most
    // files without retaining every installed cmap or opening glyf/loca tables.
    internal ulong[] GetCoveragePages(ReadOnlySpan<byte> map)
    {
        int count=Format==12 ? Groups(map) : Segments(map);
        var pages=new ulong[Format==12 ? 68 : 4];
        for (int i=0; i<count; i++)
        {
            int start=Format==12 ? (int)U32(map,16+i*12) : U16(map,16+count*2+i*2);
            int end=Format==12 ? (int)U32(map,20+i*12) : U16(map,14+i*2);
            for (int page=start>>8; page<=end>>8; page++) pages[page>>6]|=1UL<<(page&63);
        }
        return pages;
    }

    private static int Segments(ReadOnlySpan<byte> map)
    {
        int count=U16(map,6)/2;
        if (count==0 || 16+count*8>map.Length) throw new InvalidDataException("Invalid cmap segments.");
        return count;
    }

    private static int Groups(ReadOnlySpan<byte> map)
    {
        uint count=U32(map,12);
        if (count>(uint)((map.Length-16)/12)) throw new InvalidDataException("Invalid cmap groups.");
        return (int)count;
    }

    private static ushort U16(ReadOnlySpan<byte> map,int at)=>BinaryPrimitives.ReadUInt16BigEndian(map[at..]);
    private static uint U32(ReadOnlySpan<byte> map,int at)=>BinaryPrimitives.ReadUInt32BigEndian(map[at..]);
}
