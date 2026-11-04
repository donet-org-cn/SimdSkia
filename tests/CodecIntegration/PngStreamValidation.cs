using System.Buffers.Binary;
using SimdSkia;
using SimdSkia.Imaging;

internal static class PngStreamValidation
{
    internal static bool Run()
    {
        using var source=new PixelBuffer(23,17,PixelFormat.Rgba8888,AlphaFormat.Straight);
        for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)
            source.WritePixel(x,y,new((byte)(x*9),(byte)(y*13),(byte)(x+y),(byte)((x+y)%3*127)));
        var png=PngCodec.Encode(source);
        foreach(int size in new[]{1,2,7,4096})
        {
            using var stream=new MemoryStream();stream.Write(png.AsSpan(0,8));
            for(int at=8;at<png.Length;)
            {
                int length=(int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at));
                var type=png.AsSpan(at+4,4);var payload=png.AsSpan(at+8,length);
                if(type.SequenceEqual("IDAT"u8))
                {
                    Chunk(stream,type,[]);
                    for(int offset=0;offset<payload.Length;offset+=size)
                    {Chunk(stream,type,payload.Slice(offset,Math.Min(size,payload.Length-offset)));Chunk(stream,type,[]);}
                }
                else Chunk(stream,type,payload);
                at+=length+12;
            }
            var fragmented=stream.ToArray();var padded=new byte[fragmented.Length+47];fragmented.CopyTo(padded,19);
            using var cancel=new CancellationTokenSource();
            var collector=Task.Run(()=>{for(int i=0;i<32&&!cancel.IsCancellationRequested;i++){GC.Collect();Thread.Yield();}});
            try
            {
                for(int i=0;i<5;i++)
                {
                    using var decoded=PngCodec.Decode(padded.AsSpan(19,fragmented.Length));
                    if(!decoded.GetPixelSpan().SequenceEqual(source.GetPixelSpan()))throw new InvalidDataException("Fragmented PNG pixel mismatch.");
                }
            }
            finally{cancel.Cancel();collector.GetAwaiter().GetResult();}
        }
        return true;
    }
    private static void Chunk(Stream target,ReadOnlySpan<byte> type,ReadOnlySpan<byte> payload)
    {
        Span<byte> word=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(word,payload.Length);target.Write(word);target.Write(type);target.Write(payload);
        uint crc=uint.MaxValue;
        foreach(byte value in type){crc^=value;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
        foreach(byte value in payload){crc^=value;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
        BinaryPrimitives.WriteUInt32BigEndian(word,~crc);target.Write(word);
    }
}
