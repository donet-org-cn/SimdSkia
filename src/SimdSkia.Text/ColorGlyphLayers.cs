using System.Buffers.Binary;

namespace SimdSkia.Text;

internal readonly record struct ColorGlyphLayer(ushort Glyph,Rgba32? Color);

// COLR v0 layers, including the legacy records of a COLR v1 table. Read only
// those ranges and palette zero; v1 paint graphs can be several megabytes.
internal sealed class ColorGlyphLayers(ReadOnlyMemory<byte> glyphRecords,ColorGlyphLayer[] layers)
{
    internal const int HeaderBytes=14;
    internal static bool HasLegacyRecords(ReadOnlySpan<byte> header)
        => header.Length>=HeaderBytes&&U16(header,0)<=1&&U16(header,2)>0&&U16(header,12)>0;

    internal static ColorGlyphLayers Read(FontSource source,int colrOffset,int colrSize,int cpalOffset,int cpalSize,int glyphCount)
    {
        var header=source.Read(colrOffset,HeaderBytes).Span;
        if(!HasLegacyRecords(header))throw new NotSupportedException("COLR contains no supported legacy layers.");
        int bases=U16(header,2), count=U16(header,12);
        int headerSize=U16(header,0)==1?34:HeaderBytes;
        int baseOffset=Range(colrOffset,colrSize,U32(header,4),bases*6,headerSize);
        int layerOffset=Range(colrOffset,colrSize,U32(header,8),count*4,headerSize);
        var glyphRecords=source.Read(baseOffset,bases*6);
        var records=source.Read(layerOffset,count*4).Span;

        var palette=source.Read(cpalOffset,14).Span;
        int entries=U16(palette,2), palettes=U16(palette,4), colors=U16(palette,6), first=U16(palette,12);
        int paletteHeader=12+palettes*2+(U16(palette,0)==1?12:0);
        if(U16(palette,0)>1||entries==0||palettes==0||first+entries>colors)
            throw new InvalidDataException("Invalid CPAL palette.");
        int colorsOffset=Range(cpalOffset,cpalSize,U32(palette,8),colors*4,paletteHeader);
        var rgba=source.Read(colorsOffset+first*4,entries*4).Span;
        var layers=new ColorGlyphLayer[count];
        for(int i=0;i<count;i++)
        {
            ushort glyph=U16(records,i*4),index=U16(records,i*4+2);
            if(glyph>=glyphCount||(index!=ushort.MaxValue&&index>=entries))
                throw new InvalidDataException("Invalid COLR layer glyph or palette index.");
            int at=index*4;
            layers[i]=new(glyph,index==ushort.MaxValue?null:new Rgba32(rgba[at+2],rgba[at+1],rgba[at],rgba[at+3]));
        }
        int previous=-1;
        for(int i=0;i<bases;i++)
        {
            var data=glyphRecords.Span[(i*6)..];
            int glyph=U16(data,0),start=U16(data,2),length=U16(data,4);
            if(glyph<=previous||glyph>=glyphCount||length==0||start+length>count)
                throw new InvalidDataException("Invalid COLR base glyph record.");
            previous=glyph;
        }
        return new(glyphRecords,layers);
    }

    internal ReadOnlySpan<ColorGlyphLayer> Get(ushort glyph)
    {
        var records=glyphRecords.Span;
        int lower=0,upper=records.Length/6-1;
        while(lower<=upper)
        {
            int mid=lower+(upper-lower)/2,at=mid*6;
            int candidate=U16(records,at);
            if(glyph<candidate)upper=mid-1;
            else if(glyph>candidate)lower=mid+1;
            else return layers.AsSpan(U16(records,at+2),U16(records,at+4));
        }
        return [];
    }

    private static int Range(int tableOffset,int size,uint at,int bytes,int minimum)
    {
        if(size<minimum||at<minimum||at>size-bytes||size<bytes)
            throw new InvalidDataException("Color glyph records exceed their table.");
        return checked(tableOffset+(int)at);
    }
    private static ushort U16(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadUInt16BigEndian(data[at..]);
    private static uint U32(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadUInt32BigEndian(data[at..]);
}
