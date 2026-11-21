namespace SimdSkia.Text;

internal readonly record struct GlyphCluster(ushort Glyph,int Cluster,int Codepoint);

/// <summary>Default-script OpenType ligatures used for joining color glyph sequences.</summary>
internal sealed class ColorGlyphComposition
{
    private readonly ReadOnlyMemory<byte> table;
    private readonly ReadOnlyMemory<byte> variations;
    private readonly int[][] lookups;
    private readonly int glyphCount;

    internal ColorGlyphComposition(TrueTypeFont font)
    {
        glyphCount=font.GlyphCount;
        font.TryGetTable("GSUB",out table);
        lookups=table.IsEmpty?[]:ReadLookups(new(table.Span));
        if(font.TryGetTable("cmap",out var cmap))variations=VariationTable(cmap);
    }

    private static int[][] ReadLookups(OpenTypeLayoutReader reader)
    {
        if(reader.U16(0)!=1)return [];
        int scripts=reader.U16(4),features=reader.U16(6),lookups=reader.U16(8);
        if(scripts==0||features==0||lookups==0)throw new InvalidDataException("Missing GSUB list.");
        int script=reader.Record(scripts,0x44464c54); // DFLT
        if(script==0)return [];
        int defaultLanguage=reader.U16(script);
        if(defaultLanguage==0)return [];
        int language=checked(script+defaultLanguage);
        int required=reader.U16(language+2),featureCount=reader.U16(language+4);
        int totalFeatures=reader.U16(features),totalLookups=reader.U16(lookups);
        var enabled=new SortedSet<int>();
        for(int i=-1;i<featureCount;i++)
        {
            int featureIndex=i<0?required:reader.U16(language+6+i*2);
            if(featureIndex==ushort.MaxValue)continue;
            if(featureIndex>=totalFeatures)throw new InvalidDataException("Invalid GSUB feature index.");
            int record=features+2+featureIndex*6;
            // ccmp, rlig and liga are enabled by default for emoji sequences.
            if(reader.U32(record) is not (0x63636d70 or 0x726c6967 or 0x6c696761))continue;
            int feature=checked(features+reader.U16(record+4));
            int count=reader.U16(feature+2);
            for(int j=0;j<count;j++)
            {
                int number=reader.U16(feature+4+j*2);
                if(number>=totalLookups)throw new InvalidDataException("Invalid GSUB lookup index.");
                enabled.Add(number);
            }
        }
        var output=new List<int[]>();
        foreach(int number in enabled)
        {
            int lookup=checked(lookups+reader.U16(lookups+2+number*2));
            int type=reader.U16(lookup),flags=reader.U16(lookup+2),count=reader.U16(lookup+4);
            // Features requiring GDEF-based skipping or contextual mutations
            // are outside direct ligature assembly. Keep the original glyphs.
            if(type is not (4 or 7)||flags!=0)continue;
            var subtables=new List<int>();
            for(int i=0;i<count;i++)
            {
                int at=checked(lookup+reader.U16(lookup+6+i*2));
                if(type==7)
                {
                    if(reader.U16(at)!=1||reader.U16(at+2)!=4)continue;
                    at=checked(at+(int)reader.U32(at+4));
                }
                if(reader.U16(at)==1)subtables.Add(at);
            }
            if(subtables.Count!=0)output.Add(subtables.ToArray());
        }
        return output.ToArray();
    }

    internal int Apply(Span<GlyphCluster> glyphs)
    {
        var reader=new OpenTypeLayoutReader(table.Span);
        int count=glyphs.Length;
        foreach(var lookup in lookups)
        {
            int written=0;
            for(int next=0;next<count;)
            {
                var current=glyphs[next];
                int consumed=1;
                if(next+1<count)
                    foreach(int subtable in lookup)
                    {
                        int matched=Match(reader,subtable,glyphs[next..count],out ushort replacement);
                        if(matched==0)continue;
                        current=current with {Glyph=replacement};
                        consumed=matched;
                        break;
                    }
                glyphs[written++]=current;
                next+=consumed;
            }
            count=written;
        }
        return count;
    }

    internal ushort Variant(ushort @default,int codepoint,int selector)
    {
        if(variations.IsEmpty)return @default;
        var map=variations.Span;
        var reader=new OpenTypeLayoutReader(map);
        int lower=0,upper=checked((int)reader.U32(6))-1;
        while(lower<=upper)
        {
            int mid=lower+(upper-lower)/2,at=10+mid*11;
            int variation=U24(map,at);
            if(selector<variation){upper=mid-1;continue;}
            if(selector>variation){lower=mid+1;continue;}
            int nonDefault=checked((int)reader.U32(at+7));
            if(nonDefault==0)return @default;
            if(nonDefault<10+(checked((int)reader.U32(6))*11))
                throw new InvalidDataException("Invalid cmap variation mapping offset.");
            uint count=reader.U32(nonDefault);
            if(count>(map.Length-nonDefault-4)/5)throw new InvalidDataException("Invalid cmap variation mapping count.");
            int left=0,right=(int)count-1;
            while(left<=right)
            {
                int entry=left+(right-left)/2,record=nonDefault+4+entry*5;
                int scalar=U24(map,record);
                if(codepoint<scalar)right=entry-1;
                else if(codepoint>scalar)left=entry+1;
                else
                {
                    int selected=reader.U16(record+3);
                    if(selected>=glyphCount)throw new InvalidDataException("Invalid cmap variation glyph.");
                    return (ushort)selected;
                }
            }
            return @default;
        }
        return @default;
    }

    private static ReadOnlyMemory<byte> VariationTable(ReadOnlyMemory<byte> cmap)
    {
        var reader=new OpenTypeLayoutReader(cmap.Span);
        int records=reader.U16(2);
        for(int i=0;i<records;i++)
        {
            int entry=4+i*8;
            if(reader.U16(entry)!=0||reader.U16(entry+2)!=5)continue;
            int offset=checked((int)reader.U32(entry+4));
            if(reader.U16(offset)!=14)continue;
            int length=checked((int)reader.U32(offset+2));
            reader.Check(offset,length);
            if(length<10||reader.U32(offset+6)>(length-10)/11)
                throw new InvalidDataException("Invalid cmap variation table.");
            return cmap.Slice(offset,length);
        }
        return default;
    }

    private static int U24(ReadOnlySpan<byte> data,int at)=>(data[at]<<16)|(data[at+1]<<8)|data[at+2];

    private int Match(OpenTypeLayoutReader reader,int table,ReadOnlySpan<GlyphCluster> glyphs,out ushort output)
    {
        output=0;
        int index=reader.Coverage(checked(table+reader.U16(table+2)),glyphs[0].Glyph);
        if(index<0)return 0;
        if(index>=reader.U16(table+4))throw new InvalidDataException("Invalid GSUB ligature coverage.");
        int set=checked(table+reader.U16(table+6+index*2));
        int count=reader.U16(set);
        for(int i=0;i<count;i++)
        {
            int ligature=checked(set+reader.U16(set+2+i*2));
            int components=reader.U16(ligature+2);
            if(components<2)throw new InvalidDataException("Invalid GSUB ligature component count.");
            if(components>glyphs.Length)continue;
            int part=1;
            while(part<components&&glyphs[part].Glyph==reader.U16(ligature+2+part*2))part++;
            if(part!=components)continue;
            output=(ushort)reader.U16(ligature);
            if(output>=glyphCount)throw new InvalidDataException("Invalid GSUB replacement glyph.");
            return components;
        }
        return 0;
    }
}
