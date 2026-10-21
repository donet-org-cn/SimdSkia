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
    public byte[] Bytes {get;}
    private PixelStorage(byte[] bytes)=>Bytes=bytes;

    public static PixelStorage Rent(int length)
    {
        lock(Gate)
        {
            int best=-1;
            for(int i=0;i<Available.Count;i++)
                if(Available[i].Length>=length&&(long)Available[i].Length<=Math.Max(4096L,(long)length*2)
                    &&(best<0||Available[i].Length<Available[best].Length))best=i;
            if(best>=0)
            {
                byte[] bytes=Available[best];Available.RemoveAt(best);pooledBytes-=bytes.Length;
                return new(bytes);
            }
        }
        return new(GC.AllocateUninitializedArray<byte>(length));
    }

    public void Retain()=>Interlocked.Increment(ref references);
    public void Release()
    {
        if(Interlocked.Decrement(ref references)!=0||Bytes.Length>Budget)return;
        lock(Gate)
        {
            while(Available.Count>=4||pooledBytes+Bytes.Length>Budget)
            {pooledBytes-=Available[0].Length;Available.RemoveAt(0);}
            Available.Add(Bytes);pooledBytes+=Bytes.Length;
        }
    }
}
