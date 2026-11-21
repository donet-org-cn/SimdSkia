using System.Buffers.Binary;

namespace SimdSkia.Text;

internal readonly ref struct OpenTypeLayoutReader
{
    private readonly ReadOnlySpan<byte> data;
    internal OpenTypeLayoutReader(ReadOnlySpan<byte> data)=>this.data=data;

    internal void Check(int offset,int length)
    {
        if(offset<0||length<0||offset>data.Length-length)
            throw new InvalidDataException("OpenType layout offset exceeds its table.");
    }

    internal int U16(int offset){Check(offset,2);return BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);}
    internal int S16(int offset){Check(offset,2);return BinaryPrimitives.ReadInt16BigEndian(data[offset..]);}
    internal uint U32(int offset){Check(offset,4);return BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);}
    internal int Record(int list,uint tag)
    {
        int count=U16(list);
        for(int i=0;i<count;i++)
        {
            int p=checked(list+2+i*6);
            if(U32(p)==tag)return checked(list+U16(p+4));
        }
        return 0;
    }

    internal int Coverage(int table,ushort glyph)
    {
        int format=U16(table),count=U16(table+2),lower=0,upper=count-1;
        if(format==1)
        {
            while(lower<=upper)
            {
                int mid=lower+(upper-lower)/2,value=U16(table+4+mid*2);
                if(value<glyph)lower=mid+1;
                else if(value>glyph)upper=mid-1;
                else return mid;
            }
            return -1;
        }
        if(format!=2)throw new NotSupportedException("Unknown OpenType coverage format.");
        while(lower<=upper)
        {
            int mid=lower+(upper-lower)/2,at=table+4+mid*6;
            int start=U16(at),end=U16(at+2);
            if(glyph<start)upper=mid-1;
            else if(glyph>end)lower=mid+1;
            else return U16(at+4)+glyph-start;
        }
        return -1;
    }
}
