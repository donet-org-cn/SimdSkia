using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;

namespace SimdSkia.Text;

// Only installed fonts use file pages. Caller-provided spans/streams still own
// an immutable byte snapshot. Returned memories always own managed storage and
// remain valid independently of the file and the font's lifetime.
internal sealed class FontSource
{
    private const int PageSize=16384,MaximumPages=128;
    private readonly byte[]? bytes;
    private readonly SafeFileHandle? handle;
    private readonly DateTime writeTime;
    private readonly Dictionary<int,byte[]> pages=[];
    private readonly Queue<int> order=[];
    internal int Length {get;}
    internal FontSource(byte[] bytes){this.bytes=bytes;Length=bytes.Length;}
    private FontSource(SafeFileHandle handle,int length,DateTime writeTime){this.handle=handle;Length=length;this.writeTime=writeTime;}
    internal static FontSource OpenInstalled(string path,long expectedLength,DateTime expectedWrite)
    {
        var handle=File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete,FileOptions.RandomAccess);
        try
        {
            long length=RandomAccess.GetLength(handle);
            if(length!=expectedLength||File.GetLastWriteTimeUtc(handle)!=expectedWrite)
                throw new IOException("Installed font changed between discovery and use.");
            return new(handle,checked((int)length),expectedWrite);
        }
        catch {handle.Dispose();throw;}
    }
    internal void Check(int offset,int count)
    {if(offset<0||count<0||offset>Length-count)throw new InvalidDataException("Font table outside file.");}
    internal ReadOnlyMemory<byte> Read(int offset,int count)
    {
        Check(offset,count);
        if(bytes is not null)return bytes.AsMemory(offset,count);
        if(count==0)return ReadOnlyMemory<byte>.Empty;
        if(count<=PageSize-offset%PageSize)return Page(offset/PageSize).AsMemory(offset%PageSize,count);
        var result=new byte[count];ReadExactly(result,offset);return result;
    }
    internal void CopyTo(int offset,Span<byte> destination)
    {
        Check(offset,destination.Length);
        if(bytes is not null)bytes.AsSpan(offset,destination.Length).CopyTo(destination);
        else ReadExactly(destination,offset);
    }
    private byte[] Page(int index)
    {
        lock(pages)
        {
            if(pages.TryGetValue(index,out var data))return data;
            int offset=index*PageSize;data=new byte[Math.Min(PageSize,Length-offset)];ReadExactly(data,offset);
            if(pages.Count==MaximumPages)pages.Remove(order.Dequeue());
            pages.Add(index,data);order.Enqueue(index);return data;
        }
    }
    private void ReadExactly(Span<byte> destination,int offset)
    {
        VerifyFile();
        // RandomAccess has no shared stream position; concurrent face/table
        // readers cannot corrupt each other's offsets. SafeFileHandle owns close.
        while(!destination.IsEmpty)
        {
            int read=RandomAccess.Read(handle!,destination,offset);
            if(read==0)throw new InvalidDataException("Installed font was truncated.");
            offset+=read;destination=destination[read..];
        }
        VerifyFile();
    }
    private void VerifyFile()
    {
        // Windows denies in-place writes while this read handle is alive. On
        // Unix a non-cooperating writer can bypass sharing; refuse changed data.
        if(!OperatingSystem.IsWindows()&&(RandomAccess.GetLength(handle!)!=Length||File.GetLastWriteTimeUtc(handle!)!=writeTime))
            throw new IOException("Installed font changed while in use.");
    }
    internal ushort U16(int offset)=>BinaryPrimitives.ReadUInt16BigEndian(Read(offset,2).Span);
    internal uint U32(int offset)=>BinaryPrimitives.ReadUInt32BigEndian(Read(offset,4).Span);
    internal int FaceCount()
    {
        Check(0,12);if(U32(0)!=0x74746366)return 1;
        uint version=U32(4),count=U32(8);
        if(version is not (0x10000 or 0x20000))throw new NotSupportedException("Unsupported TTC version.");
        if(count is 0 or >4096||12+count*4+(version==0x20000?12:0)>Length)throw new InvalidDataException("Invalid TTC face count/header.");
        for(int i=0;i<count;i++)if(U32(12+i*4)>Length-12)throw new InvalidDataException("Invalid TTC face offset.");
        if(version==0x20000)
        {
            int p=12+(int)count*4;uint tag=U32(p),length=U32(p+4),offset=U32(p+8);
            if(tag==0?(length!=0||offset!=0):(tag!=0x44534947||offset>Length||length>Length-offset))throw new InvalidDataException("Invalid TTC signature directory.");
        }
        return (int)count;
    }
}
