using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;

namespace SimdSkia.Imaging;

/// <summary>Strict input validation, not a pixel decoder. BCL inflate is explicitly permitted.</summary>
internal static class PngValidation
{
    internal static unsafe void ValidateRows(ReadOnlySpan<byte> png,int firstIdat,int compressedLength,int idatCount,int width,int height,int depth,int type,int interlace,int paletteCount)
    {
        int channels=type==2?3:type==4?2:type==6?4:1;
        ReadOnlySpan<(int X,int Y,int Dx,int Dy)> passes=interlace==0?[(0,0,1,1)]:[(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)];
        long total=0;
        foreach(var p in passes)
        {
            int w=(Math.Max(0,width-p.X)+p.Dx-1)/p.Dx,h=(Math.Max(0,height-p.Y)+p.Dy-1)/p.Dy;
            if(w>0&&h>0)total+=((long)(w*channels*depth+7)/8+1)*h;
        }
        if(total>CodecBackend.InputLimit)throw new NotSupportedException("PNG inflation exceeds the 256 MiB budget.");
        int maxRow=checked((width*channels*depth+7)/8);
        var rows=ArrayPool<byte>.Shared.Rent(checked(maxRow*2));
        try
        {
            // The read-only stream cannot escape this fixed scope. It reads the
            // already checked IDAT chunks in place, avoiding an image-sized
            // compressed copy and its retained ArrayPool bucket.
            fixed(byte* encoded=png)
            {
            using var input=new IdatStream(encoded,png.Length,firstIdat,idatCount,compressedLength);
            using var inflater=new ZLibStream(input,CompressionMode.Decompress);
            foreach(var p in passes)
            {
                int w=(Math.Max(0,width-p.X)+p.Dx-1)/p.Dx,h=(Math.Max(0,height-p.Y)+p.Dy-1)/p.Dy;
                if(w==0||h==0)continue;
                int row=(w*channels*depth+7)/8;var previous=rows.AsSpan(0,row);var line=rows.AsSpan(maxRow,row);previous.Clear();
                for(int y=0;y<h;y++)
                {
                    int filter=inflater.ReadByte();if(filter is <0 or >4)throw new InvalidDataException("Invalid PNG row filter.");
                    try{inflater.ReadExactly(line);}catch(EndOfStreamException e){throw new InvalidDataException("Truncated PNG row.",e);}
                    if(type!=3)continue;
                    // Palette bounds are a semantic input check. Reconstruct only
                    // this indexed row (PNG byte distance is one for all index depths).
                    for(int i=0;i<line.Length;i++)
                    {
                        int a=i==0?0:line[i-1],b=previous[i],c=i==0?0:previous[i-1];
                        int predictor=filter switch{0=>0,1=>a,2=>b,3=>(a+b)/2,_=>Paeth(a,b,c)};
                        line[i]=unchecked((byte)(line[i]+predictor));
                    }
                    for(int x=0;x<w;x++)if(((line[x*depth/8]>>(8-depth-x*depth%8))&((1<<depth)-1))>=paletteCount)throw new InvalidDataException("PNG palette index outside its table.");
                    var swap=previous;previous=line;line=swap;
                }
            }
            if(inflater.ReadByte()!=-1)throw new InvalidDataException("Excess PNG inflated pixels.");
            }
        }
        finally{ArrayPool<byte>.Shared.Return(rows);}
    }
    private sealed unsafe class IdatStream(byte* encoded,int length,int chunk,int chunks,int compressedLength):Stream
    {
        private int position=chunk,remainingChunks=chunks,remaining,consumed;
        public override bool CanRead=>true;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>compressedLength;
        public override long Position{get=>consumed;set=>throw new NotSupportedException();}
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer)
        {
            int count=0;
            while(!buffer.IsEmpty)
            {
                if(remaining==0)
                {
                    if(remainingChunks==0)break;
                    if(position<0||position>length-12)throw new InvalidDataException("Truncated IDAT header.");
                    var header=new ReadOnlySpan<byte>(encoded+position,8);
                    if(!header[4..].SequenceEqual("IDAT"u8))throw new InvalidDataException("Non-consecutive IDAT.");
                    uint size=BinaryPrimitives.ReadUInt32BigEndian(header);
                    if(size>(uint)(length-position-12))throw new InvalidDataException("Truncated IDAT payload.");
                    remaining=(int)size;remainingChunks--;position+=8;
                    if(remaining==0){position+=4;continue;}
                }
                int take=Math.Min(buffer.Length,remaining);
                new ReadOnlySpan<byte>(encoded+position,take).CopyTo(buffer);
                position+=take;remaining-=take;consumed+=take;count+=take;buffer=buffer[take..];
                if(remaining==0)position+=4;
            }
            return count;
        }
        public override void Flush() { }
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
    private static int Paeth(int a,int b,int c){int p=a+b-c,da=Math.Abs(p-a),db=Math.Abs(p-b),dc=Math.Abs(p-c);return da<=db&&da<=dc?a:db<=dc?b:c;}
}
