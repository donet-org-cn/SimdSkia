using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
namespace SimdSkia.Imaging;
public static partial class PngCodec
{
    /// <summary>Uses Sdcb for the image/container encoding, then honors explicit PNG filter flags and a numeric zlib level.</summary>
    public static byte[] Encode(PixelBuffer pixels,int filterFlags,int zlibLevel)
    {
        if((filterFlags&~248)!=0||filterFlags<0)throw new ArgumentOutOfRangeException(nameof(filterFlags));
        if(zlibLevel is <0 or >9)throw new ArgumentOutOfRangeException(nameof(zlibLevel));
        if(filterFlags==0)filterFlags=248; // libpng's zero mask retains automatic filter selection.
        if(filterFlags==248&&zlibLevel==6)return Encode(pixels);
        byte[] png=Encode(pixels);return Refilter(png,filterFlags,zlibLevel);
    }
    private static byte[] Refilter(ReadOnlySpan<byte> png,int flags,int level)
    {
        int width=BinaryPrimitives.ReadInt32BigEndian(png[16..]),height=BinaryPrimitives.ReadInt32BigEndian(png[20..]),depth=png[24],type=png[25];
        if(png[28]!=0)throw new NotSupportedException("Explicit PNG refiltering requires noninterlaced package output.");
        int channels=type switch{0 or 3=>1,2=>3,4=>2,6=>4,_=>throw new InvalidDataException("Invalid package PNG type.")};int bytesPerPixel=Math.Max(1,(channels*depth+7)/8),rowBytes=checked((width*channels*depth+7)/8);
        if((long)rowBytes*height>CodecBackend.InputLimit)throw new NotSupportedException("PNG filter input exceeds 256 MiB.");
        using var encoded=new MemoryStream();using var prefix=new MemoryStream();using var suffix=new MemoryStream();prefix.Write(png[..8]);bool idat=false;
        for(int at=8;at<png.Length;)
        {
            int n=BinaryPrimitives.ReadInt32BigEndian(png[at..]);var chunk=png.Slice(at,n+12);
            if(chunk.Slice(4,4).SequenceEqual("IDAT"u8)){encoded.Write(chunk.Slice(8,n));idat=true;}
            else (idat?suffix:prefix).Write(chunk);
            at+=n+12;
        }
        encoded.Position=0;using var inflater=new ZLibStream(encoded,CompressionMode.Decompress);using var compressed=new MemoryStream();
        byte[] storage=ArrayPool<byte>.Shared.Rent(checked(rowBytes*4+1));
        try
        {
            var previous=storage.AsSpan(0,rowBytes);previous.Clear();var row=storage.AsSpan(rowBytes,rowBytes);var candidate=storage.AsSpan(rowBytes*2,rowBytes+1);var best=storage.AsSpan(rowBytes*3+1,rowBytes);
            using(var deflater=new ZLibStream(compressed,new ZLibCompressionOptions{CompressionLevel=level},true))
            for(int y=0;y<height;y++)
            {
                int filter=inflater.ReadByte();if((uint)filter>4)throw new InvalidDataException("Invalid package PNG scanline.");inflater.ReadExactly(row);
                for(int x=0;x<rowBytes;x++){int left=x>=bytesPerPixel?row[x-bytesPerPixel]:0,up=previous[x],upperLeft=x>=bytesPerPixel?previous[x-bytesPerPixel]:0;row[x]=unchecked((byte)(row[x]+Predict(filter,left,up,upperLeft)));}
                int selected=0;long bestScore=long.MaxValue;
                for(int f=0;f<5;f++)
                {
                    if(flags==0?f!=0:(flags&(8<<f))==0)continue;long score=0;
                    for(int x=0;x<rowBytes;x++){int left=x>=bytesPerPixel?row[x-bytesPerPixel]:0,up=previous[x],upperLeft=x>=bytesPerPixel?previous[x-bytesPerPixel]:0;byte value=unchecked((byte)(row[x]-Predict(f,left,up,upperLeft)));candidate[x+1]=value;score+=Math.Abs((int)unchecked((sbyte)value));}
                    if(score<bestScore){bestScore=score;selected=f;candidate[1..].CopyTo(best);}
                }
                deflater.WriteByte((byte)selected);deflater.Write(best);row.CopyTo(previous);
            }
            if(inflater.ReadByte()!=-1)throw new InvalidDataException("Extra package PNG scanlines.");
            using var result=new MemoryStream();prefix.Position=0;prefix.CopyTo(result);WriteIdat(result,compressed.GetBuffer().AsSpan(0,(int)compressed.Length));suffix.Position=0;suffix.CopyTo(result);return result.ToArray();
        }
        finally{ArrayPool<byte>.Shared.Return(storage);}
    }
    private static int Predict(int filter,int a,int b,int c)
    {
        if(filter==0)return 0;if(filter==1)return a;if(filter==2)return b;if(filter==3)return(a+b)/2;
        int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;
    }
    private static void WriteIdat(Stream stream,ReadOnlySpan<byte> data)
    {
        Span<byte> bytes=stackalloc byte[8];BinaryPrimitives.WriteInt32BigEndian(bytes,data.Length);"IDAT"u8.CopyTo(bytes[4..]);stream.Write(bytes);stream.Write(data);
        uint crc=0xffffffff;foreach(byte b in "IDAT"u8)crc=Step(crc,b);foreach(byte b in data)crc=Step(crc,b);BinaryPrimitives.WriteUInt32BigEndian(bytes,~crc);stream.Write(bytes[..4]);
        static uint Step(uint c,byte b){c^=b;for(int k=0;k<8;k++)c=(c&1)!=0?(c>>1)^0xedb88320:c>>1;return c;}
    }
}
