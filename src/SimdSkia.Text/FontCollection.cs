using System.Buffers.Binary;

namespace SimdSkia.Text;

/// <summary>Validated TTC v1/v2 face extraction into standalone, checksummed sfnt storage.</summary>
public static class FontCollection
{
    private static uint U32(ReadOnlySpan<byte> data,int at)
    {if(at<0||at>data.Length-4)throw new InvalidDataException("Truncated TTC header.");return BinaryPrimitives.ReadUInt32BigEndian(data[at..]);}
    public static int Count(ReadOnlySpan<byte> data)
    {
        if(data.Length<12)throw new InvalidDataException("Truncated font.");
        if(U32(data,0)!=0x74746366)return 1;
        uint version=U32(data,4),count=U32(data,8);
        if(version is not (0x00010000 or 0x00020000))throw new NotSupportedException("Unsupported TTC version.");
        if(count is 0 or >4096||12+count*4+(version==0x20000?12:0)>data.Length)throw new InvalidDataException("Invalid TTC face count/header.");
        for(int i=0;i<count;i++){uint offset=U32(data,12+i*4);if(offset>data.Length-12)throw new InvalidDataException("Invalid TTC face offset.");}
        if(version==0x20000)
        {
            int p=12+(int)count*4;uint tag=U32(data,p),length=U32(data,p+4),offset=U32(data,p+8);
            if(tag==0?(length!=0||offset!=0):(tag!=0x44534947||offset>data.Length||length>data.Length-offset))throw new InvalidDataException("Invalid TTC signature directory.");
        }
        return(int)count;
    }
    internal static byte[] Extract(ReadOnlySpan<byte> data,int index)
    {
        int faces=Count(data);if(index<0||index>=faces)throw new ArgumentOutOfRangeException(nameof(index));
        if(U32(data,0)!=0x74746366)return data.ToArray();
        int start=checked((int)U32(data,12+index*4));int count=BinaryPrimitives.ReadUInt16BigEndian(data[(start+4)..]);
        if(count==0||start>data.Length-12-count*16)throw new InvalidDataException("Truncated TTC face directory.");
        int cursor=12+count*16;
        for(int i=0;i<count;i++)
        {
            int entry=start+12+i*16;uint offset=U32(data,entry+8),length=U32(data,entry+12);
            if(offset>data.Length||length>data.Length-offset)throw new InvalidDataException("TTC table outside collection.");
            cursor=checked(cursor+((checked((int)length)+3)&~3));
            if(cursor>64*1024*1024)throw new NotSupportedException("A TTC face exceeds the 64 MiB extraction budget.");
        }
        var result=new byte[cursor];data.Slice(start,12+count*16).CopyTo(result);cursor=12+count*16;int head=-1;
        for(int i=0;i<count;i++)
        {
            int entry=12+i*16,offset=(int)U32(result,entry+8),length=(int)U32(result,entry+12);
            data.Slice(offset,length).CopyTo(result.AsSpan(cursor));BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(entry+8),cursor);
            if(U32(result,entry)==0x68656164){if(length<12)throw new InvalidDataException("Truncated TTC head table.");head=cursor;result.AsSpan(cursor+8,4).Clear();}
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(entry+4),Checksum(result.AsSpan(cursor,(length+3)&~3)));cursor+=(length+3)&~3;
        }
        if(head<0)throw new InvalidDataException("TTC face is missing head.");
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(head+8),unchecked(0xb1b0afba-Checksum(result)));return result;
    }
    private static uint Checksum(ReadOnlySpan<byte> bytes)
    {uint sum=0;for(int i=0;i<bytes.Length;i+=4)sum=unchecked(sum+BinaryPrimitives.ReadUInt32BigEndian(bytes[i..]));return sum;}

    // Export only the requested face, without materializing the entire TTC.
    internal static byte[] Extract(FontSource source,int index)
    {
        int faces=source.FaceCount();if(index<0||index>=faces)throw new ArgumentOutOfRangeException(nameof(index));
        if(source.U32(0)!=0x74746366)return source.Read(0,source.Length).ToArray();
        int start=checked((int)source.U32(12+index*4));int count=source.U16(start+4);
        if(count==0)throw new InvalidDataException("Truncated TTC face directory.");
        source.Check(start,12+count*16);int cursor=12+count*16;
        for(int i=0;i<count;i++)
        {
            int entry=start+12+i*16,offset=checked((int)source.U32(entry+8)),length=checked((int)source.U32(entry+12));source.Check(offset,length);
            cursor=checked(cursor+((checked(length+3))&~3));
            if(cursor>64*1024*1024)throw new NotSupportedException("A TTC face exceeds the 64 MiB extraction budget.");
        }
        var result=new byte[cursor];source.Read(start,12+count*16).Span.CopyTo(result);cursor=12+count*16;int head=-1;
        for(int i=0;i<count;i++)
        {
            int entry=12+i*16,offset=(int)U32(result,entry+8),length=(int)U32(result,entry+12);
            // Bounded page copies avoid a second large glyf allocation.
            for(int done=0;done<length;)
            {
                int size=Math.Min(16384-(offset+done)%16384,length-done);
                source.Read(offset+done,size).Span.CopyTo(result.AsSpan(cursor+done,size));done+=size;
            }
            BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(entry+8),cursor);
            if(U32(result,entry)==0x68656164){if(length<12)throw new InvalidDataException("Truncated TTC head table.");head=cursor;result.AsSpan(cursor+8,4).Clear();}
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(entry+4),Checksum(result.AsSpan(cursor,(length+3)&~3)));cursor+=(length+3)&~3;
        }
        if(head<0)throw new InvalidDataException("TTC face is missing head.");
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(head+8),unchecked(0xb1b0afba-Checksum(result)));return result;
    }
}
