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
    private static unsafe byte[] Refilter(ReadOnlySpan<byte> png,int flags,int level)
    {
        int width=BinaryPrimitives.ReadInt32BigEndian(png[16..]),height=BinaryPrimitives.ReadInt32BigEndian(png[20..]),depth=png[24],type=png[25];
        if(png[28]!=0)throw new NotSupportedException("Explicit PNG refiltering requires noninterlaced package output.");
        int channels=type switch{0 or 3=>1,2=>3,4=>2,6=>4,_=>throw new InvalidDataException("Invalid package PNG type.")};int bytesPerPixel=Math.Max(1,(channels*depth+7)/8),rowBytes=checked((width*channels*depth+7)/8);
        if((long)rowBytes*height>CodecBackend.InputLimit)throw new NotSupportedException("PNG filter input exceeds 256 MiB.");
        int first = -1, end = 0, count = 0, compressedLength = 0;
        for(int at=8;at<png.Length;)
        {
            int n=BinaryPrimitives.ReadInt32BigEndian(png[at..]);var chunk=png.Slice(at,n+12);
            if(chunk.Slice(4,4).SequenceEqual("IDAT"u8))
            {
                if (count == 0) first = at;
                else if (at != end) throw new InvalidDataException("Non-consecutive package IDAT chunks.");
                count++;
                compressedLength += n;
                end = at + n + 12;
            }
            at+=n+12;
        }
        if (count == 0) throw new InvalidDataException("Missing package IDAT.");
        using var compressed = new MemoryStream();
        fixed (byte* data = png)
        {
            using var input = new PngValidation.IdatStream(data, png.Length, first, count, compressedLength);
            using var inflater = new ZLibStream(input, CompressionMode.Decompress);
            using var deflater = new ZLibStream(compressed, new ZLibCompressionOptions { CompressionLevel = level }, true);
            WriteFilteredRows(inflater, deflater, height, rowBytes, bytesPerPixel, flags);
        }
        int length = checked((int)compressed.Length);
        var result = GC.AllocateUninitializedArray<byte>(checked(first + 12 + length + png.Length - end));
        png[..first].CopyTo(result);
        var idat = result.AsSpan(first, length + 12);
        BinaryPrimitives.WriteInt32BigEndian(idat, length);
        "IDAT"u8.CopyTo(idat[4..]);
        compressed.GetBuffer().AsSpan(0, length).CopyTo(idat[8..]);
        BinaryPrimitives.WriteUInt32BigEndian(idat[(8 + length)..], Crc(idat.Slice(4, length + 4)));
        png[end..].CopyTo(result.AsSpan(first + idat.Length));
        return result;
    }

    private static void WriteFilteredRows(Stream inflater, Stream deflater, int height, int rowBytes, int bytesPerPixel, int flags)
    {
        byte[] storage=ArrayPool<byte>.Shared.Rent(checked(rowBytes*4));
        try
        {
            var previous=storage.AsSpan(0,rowBytes);previous.Clear();var row=storage.AsSpan(rowBytes,rowBytes);
            var candidate=storage.AsSpan(rowBytes*2,rowBytes);var best=storage.AsSpan(rowBytes*3,rowBytes);
            for(int y=0;y<height;y++)
            {
                int filter=inflater.ReadByte();if((uint)filter>4)throw new InvalidDataException("Invalid package PNG scanline.");inflater.ReadExactly(row);
                if (filter != 0)
                    for(int x=0;x<rowBytes;x++){int left=x>=bytesPerPixel?row[x-bytesPerPixel]:0,up=previous[x],upperLeft=x>=bytesPerPixel?previous[x-bytesPerPixel]:0;row[x]=unchecked((byte)(row[x]+Predict(filter,left,up,upperLeft)));}
                int selected=0;long bestScore=long.MaxValue;
                if ((flags & (flags - 1)) == 0)
                {
                    selected = System.Numerics.BitOperations.TrailingZeroCount((uint)flags) - 3;
                    if (selected != 0) FilterRow(row, previous, best, bytesPerPixel, selected);
                }
                else for(int f=0;f<5;f++)
                {
                    if ((flags & (8 << f)) == 0) continue;
                    if (f != 0) FilterRow(row, previous, candidate, bytesPerPixel, f);
                    long score = 0;
                    foreach (byte value in f == 0 ? row : candidate) score += Math.Abs((int)unchecked((sbyte)value));
                    if(score<bestScore){bestScore=score;selected=f;var swap=candidate;candidate=best;best=swap;}
                }
                deflater.WriteByte((byte)selected);deflater.Write(selected == 0 ? row : best);
                var last=previous;previous=row;row=last;
            }
            if(inflater.ReadByte()!=-1)throw new InvalidDataException("Extra package PNG scanlines.");
        }
        finally{ArrayPool<byte>.Shared.Return(storage);}
    }
    private static void FilterRow(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, Span<byte> output, int step, int filter)
    {
        for (int x = 0; x < row.Length; x++)
        {
            int left = x >= step ? row[x-step] : 0, upperLeft = x >= step ? previous[x-step] : 0;
            output[x] = unchecked((byte)(row[x] - Predict(filter, left, previous[x], upperLeft)));
        }
    }
    private static int Predict(int filter,int a,int b,int c)
    {
        if(filter==0)return 0;if(filter==1)return a;if(filter==2)return b;if(filter==3)return(a+b)/2;
        int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;
    }
}
