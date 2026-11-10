using System.Buffers.Binary;
namespace SimdSkia.Imaging;
/// <summary>Container EXIF orientation without rotating the encoded raster or depending on a platform image library.</summary>
public static class EncodedMetadata
{
    public static int ReadOrientation(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length>=4&&bytes[0]==255&&bytes[1]==216)
        {
            for(int p=2;p<bytes.Length;)
            {
                if(bytes[p++]!=255)continue;while(p<bytes.Length&&bytes[p]==255)p++;if(p>=bytes.Length)break;int marker=bytes[p++];if(marker is 0xd9 or 0xda)break;if(marker is 0xd8 or 0x01 or >=0xd0 and <=0xd7)continue;
                if(p>bytes.Length-2)break;int n=BinaryPrimitives.ReadUInt16BigEndian(bytes[p..]);if(n<2||n>bytes.Length-p)break;var payload=bytes.Slice(p+2,n-2);
                if(marker==0xe1&&payload.StartsWith("Exif\0\0"u8))return Tiff(payload[6..]);p+=n;
            }
        }
        else if(bytes.Length>=8&&BinaryPrimitives.ReadUInt64BigEndian(bytes)==0x89504e470d0a1a0aul)
        {
            for(int p=8;p<=bytes.Length-12;)
            {uint n=BinaryPrimitives.ReadUInt32BigEndian(bytes[p..]);if(n>bytes.Length-p-12)break;if(bytes.Slice(p+4,4).SequenceEqual("eXIf"u8))return Tiff(bytes.Slice(p+8,(int)n));p+=checked((int)n+12);}
        }
        else if(bytes.Length>=12&&bytes[..4].SequenceEqual("RIFF"u8)&&bytes.Slice(8,4).SequenceEqual("WEBP"u8))
        {
            for(int p=12;p<=bytes.Length-8;)
            {uint n=BinaryPrimitives.ReadUInt32LittleEndian(bytes[(p+4)..]);if(n>bytes.Length-p-8)break;if(bytes.Slice(p,4).SequenceEqual("EXIF"u8)){var exif=bytes.Slice(p+8,(int)n);return Tiff(exif.StartsWith("Exif\0\0"u8)?exif[6..]:exif);}p+=checked((int)n+8+((int)n&1));}
        }
        return 1;
    }
    private static int Tiff(ReadOnlySpan<byte> data)
    {
        if(data.Length<8)return 1;bool little=data[..2].SequenceEqual("II"u8);if(!little&&!data[..2].SequenceEqual("MM"u8))return 1;
        if(U16(data[2..],little)!=42)return 1;uint offset=U32(data[4..],little);if(offset>data.Length-2)return 1;int p=(int)offset,count=U16(data[p..],little);p+=2;
        if(count>(data.Length-p)/12)return 1;
        for(int i=0;i<count;i++,p+=12)if(U16(data[p..],little)==0x112&&U16(data[(p+2)..],little)==3&&U32(data[(p+4)..],little)==1){int value=U16(data[(p+8)..],little);return value is >=1 and <=8?value:1;}
        return 1;
    }
    private static ushort U16(ReadOnlySpan<byte> b,bool little)=>little?BinaryPrimitives.ReadUInt16LittleEndian(b):BinaryPrimitives.ReadUInt16BigEndian(b);
    private static uint U32(ReadOnlySpan<byte> b,bool little)=>little?BinaryPrimitives.ReadUInt32LittleEndian(b):BinaryPrimitives.ReadUInt32BigEndian(b);
}
