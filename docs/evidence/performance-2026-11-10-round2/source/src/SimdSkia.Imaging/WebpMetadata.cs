using System.Buffers.Binary;

namespace SimdSkia.Imaging;

internal readonly record struct WebpFrameMetadata(int X,int Y,int Width,int Height,int Duration,bool HasAlpha,bool Blend,bool DisposeToBackground);

/// <summary>RIFF/ANMF metadata only. Sdcb performs every pixel and animation decode.</summary>
internal static class WebpMetadata
{
    private static int U24(ReadOnlySpan<byte> data)=>data[0]|data[1]<<8|data[2]<<16;
    internal static WebpFrameMetadata[] ReadFrames(ReadOnlySpan<byte> data)
    {
        if(data.Length<12||!data[..4].SequenceEqual("RIFF"u8)||!data.Slice(8,4).SequenceEqual("WEBP"u8))throw new InvalidDataException("Invalid WebP RIFF header.");
        long declared=BinaryPrimitives.ReadUInt32LittleEndian(data[4..])+8L;
        if(declared!=data.Length)throw new InvalidDataException("WebP RIFF length differs from input.");
        var frames=new List<WebpFrameMetadata>();int at=12;
        while(at<data.Length)
        {
            if(data.Length-at<8)throw new InvalidDataException("Truncated WebP chunk.");
            uint length=BinaryPrimitives.ReadUInt32LittleEndian(data[(at+4)..]);
            if(length>(uint)(data.Length-at-8))throw new InvalidDataException("Truncated WebP chunk payload.");
            var chunk=data.Slice(at+8,(int)length);
            if(data.Slice(at,4).SequenceEqual("ANMF"u8))
            {
                if(chunk.Length<16||(chunk[15]&252)!=0)throw new InvalidDataException("Invalid WebP animation frame header.");
                bool alpha=false;int p=16;
                while(p<chunk.Length)
                {
                    if(chunk.Length-p<8)throw new InvalidDataException("Truncated WebP frame chunk.");
                    uint size=BinaryPrimitives.ReadUInt32LittleEndian(chunk[(p+4)..]);
                    if(size>(uint)(chunk.Length-p-8))throw new InvalidDataException("Truncated WebP frame payload.");
                    var kind=chunk.Slice(p,4);
                    if(kind.SequenceEqual("ALPH"u8))alpha=true;
                    if(kind.SequenceEqual("VP8L"u8))
                    {
                        if(size<5||chunk[p+8]!=0x2f)throw new InvalidDataException("Invalid lossless WebP frame header.");
                        alpha|=(BinaryPrimitives.ReadUInt32LittleEndian(chunk[(p+9)..])&(1u<<28))!=0;
                    }
                    p=checked(p+8+(int)size+((int)size&1));
                }
                if(p!=chunk.Length)throw new InvalidDataException("Invalid WebP frame padding.");
                frames.Add(new(U24(chunk)*2,U24(chunk[3..])*2,U24(chunk[6..])+1,U24(chunk[9..])+1,U24(chunk[12..]),alpha,(chunk[15]&2)==0,(chunk[15]&1)!=0));
                if(frames.Count>100000)throw new NotSupportedException("WebP frame budget exceeded.");
            }
            at=checked(at+8+(int)length+((int)length&1));
        }
        if(at!=data.Length)throw new InvalidDataException("Invalid WebP RIFF padding.");
        return frames.ToArray();
    }
}
