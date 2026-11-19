namespace SimdSkia;

/// <summary>
/// Shared decoder storage. A bounded process-wide pool avoids retaining one huge array
/// per thread; the final independent PixelBuffer lifetime returns its storage.
/// </summary>
internal sealed class PixelStorage
{
    private const int Budget=32*1024*1024;
    private static readonly object Gate=new();
    private static readonly List<byte[]> Available=[];
    private static long pooledBytes;
    private int references=1;
    private readonly int deferredLength;
    private byte[]? bytes;
    internal bool IsAllocated => Volatile.Read(ref bytes) is not null;
    public byte[] Bytes
    {
        get
        {
            if (Volatile.Read(ref bytes) is { } allocated)
                return allocated;
            lock (this)
            {
                if (bytes is { } ready)
                    return ready;
                var buffer = RentArray(deferredLength, exactSize: false);
                buffer.AsSpan(0, deferredLength).Clear();
                Volatile.Write(ref bytes, buffer);
                return buffer;
            }
        }
    }
    private PixelStorage(byte[] bytes)=>this.bytes=bytes;
    private PixelStorage(int length)=>deferredLength=length;
    internal static PixelStorage Deferred(int length)=>new(length);

    public static PixelStorage Rent(int length, bool exactSize) => new(RentArray(length, exactSize));
    private static byte[] RentArray(int length, bool exactSize)
    {
        long maximum = exactSize ? length : Math.Max(4096L, (long)length * 2);
        lock(Gate)
        {
            int best=-1;
            for(int i=0;i<Available.Count;i++)
                if(Available[i].Length>=length&&Available[i].Length<=maximum
                    &&(best<0||Available[i].Length<Available[best].Length))best=i;
            if(best>=0)
            {
                byte[] bytes=Available[best];Available.RemoveAt(best);pooledBytes-=bytes.Length;
                return bytes;
            }
        }
        return GC.AllocateUninitializedArray<byte>(length);
    }

    public void Retain()=>Interlocked.Increment(ref references);
    public void Release()
    {
        if (Interlocked.Decrement(ref references) != 0 || Volatile.Read(ref bytes) is not { } buffer || buffer.Length > Budget)
            return;
        lock(Gate)
        {
            while(Available.Count>=4||pooledBytes+buffer.Length>Budget)
            {pooledBytes-=Available[0].Length;Available.RemoveAt(0);}
            Available.Add(buffer);pooledBytes+=buffer.Length;
        }
    }
}
