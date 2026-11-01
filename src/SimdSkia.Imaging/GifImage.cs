using System.Buffers.Binary;

namespace SimdSkia.Imaging;

public sealed record GifFrame(int Left,int Top,int Width,int Height,int Duration,int Disposal,int TransparentIndex,bool Interlaced,byte[] Palette,byte[] Compressed,int CodeSize);

/// <summary>GIF metadata/lifetime adapter; Sdcb.SimdCodecs performs all LZW and compositing.</summary>
public sealed class GifImage : IDisposable
{
    private readonly List<GifFrame> frames=[];
    private PixelBuffer? canvas;
    private Sdcb.SimdCodecs.Abstractions.IImageFrameReader? reader;
    private bool disposed;
    public int Width {get;}
    public int Height {get;}
    public int RepetitionCount {get;private set;}
    public IReadOnlyList<GifFrame> Frames=>frames;
    public GifImage(ReadOnlySpan<byte> encoded)
    {
        CodecBackend.ValidateInput(encoded);
        var reader=new Reader(encoded);
        var signature=reader.Take(6);if(!signature.SequenceEqual("GIF87a"u8)&&!signature.SequenceEqual("GIF89a"u8))throw new InvalidDataException("Invalid GIF header.");
        Width=reader.U16();Height=reader.U16();
        if(Width==0||Height==0||(long)Width*Height>16*1024*1024)throw new NotSupportedException("GIF dimensions exceed the 16 megapixel decode budget.");
        int packed=reader.Byte();reader.Take(2);byte[] palette=(packed&128)!=0?reader.Take(3*(1<<((packed&7)+1))).ToArray():[];
        int duration=0,disposal=1,transparent=-1;
        while(true)
        {
            int marker=reader.Byte();if(marker==0x3b)break;
            if(marker==0x21)
            {
                int label=reader.Byte();
                if(label==0xf9)
                {
                    if(reader.Byte()!=4)throw new InvalidDataException("Invalid GIF control block.");
                    int control=reader.Byte();duration=reader.U16()*10;int index=reader.Byte();transparent=(control&1)!=0?index:-1;disposal=(control>>2)&7;
                    if(disposal==0)disposal=1;if(disposal>3)throw new NotSupportedException("Reserved GIF disposal method.");
                    if(reader.Byte()!=0)throw new InvalidDataException("Unterminated GIF control block.");
                }
                else if(label==0xff)
                {
                    var app=reader.Take(reader.Byte());var data=reader.Blocks();
                    if((app.SequenceEqual("NETSCAPE2.0"u8)||app.SequenceEqual("ANIMEXTS1.0"u8))&&data.Length>=3&&data[0]==1){int loops=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(1));RepetitionCount=loops==0?-1:loops;}
                }
                else if(label==1)throw new NotSupportedException("GIF plain-text rendering extensions are not supported.");
                else reader.Blocks();
                continue;
            }
            if(marker!=0x2c)throw new InvalidDataException("Invalid GIF block marker.");
            int x=reader.U16(),y=reader.U16(),w=reader.U16(),h=reader.U16();int flags=reader.Byte();
            if(w==0||h==0||(long)w*h>16*1024*1024)throw new InvalidDataException("Invalid GIF frame dimensions.");
            var colors=(flags&128)!=0?reader.Take(3*(1<<((flags&7)+1))).ToArray():palette;
            if(colors.Length==0)throw new InvalidDataException("GIF frame has no color table.");
            int size=reader.Byte();if(size is <2 or >8)throw new InvalidDataException("Invalid GIF LZW minimum code size.");
            frames.Add(new(x,y,w,h,duration,disposal,transparent,(flags&64)!=0,colors,reader.Blocks(),size));
            if(frames.Count>100000)throw new NotSupportedException("GIF frame budget exceeded.");
            duration=0;disposal=1;transparent=-1;
        }
        if(frames.Count==0)throw new InvalidDataException("GIF contains no image frames.");
        if(frames.Count==1)RepetitionCount=-1;
        if(!Sdcb.SimdCodecs.SimdCodecs.TryOpenFrames(encoded.ToArray(),out var frameReader))throw new InvalidDataException("Sdcb rejected GIF frames.");
        this.reader=frameReader;
        if(frameReader.FrameCount!=frames.Count){frameReader.Dispose();throw new InvalidDataException("GIF frame count differs from metadata.");}
    }
    public PixelBuffer DecodeFrame(int index)=>DecodeFrameCore(index).Clone();
    internal PixelBuffer DecodeFrameCore(int index)
    {
        ObjectDisposedException.ThrowIf(disposed,this);if((uint)index>=(uint)frames.Count)throw new ArgumentOutOfRangeException(nameof(index));
        canvas??=PixelBuffer.RentRgba(Width,Height);
        if(!reader!.TryDecodeFrameInto(index,canvas.GetPixelSpan(),canvas.RowBytes,Sdcb.SimdCodecs.Abstractions.ImagePixelFormat.Rgba32))
            throw new InvalidDataException("GIF frame decode failed.");
        return canvas;
    }
    public void Dispose(){if(disposed)return;disposed=true;reader?.Dispose();reader=null;canvas?.Dispose();canvas=null;frames.Clear();}
    private ref struct Reader(ReadOnlySpan<byte> data)
    {
        private ReadOnlySpan<byte> remaining=data;
        public ReadOnlySpan<byte> Take(int n){if(n<0||n>remaining.Length)throw new InvalidDataException("Truncated GIF stream.");var value=remaining[..n];remaining=remaining[n..];return value;}
        public int Byte()=>Take(1)[0];
        public int U16()=>BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
        public byte[] Blocks(){using var output=new MemoryStream();while(true){int n=Byte();if(n==0)return output.ToArray();if(output.Length+n>256*1024*1024)throw new NotSupportedException("GIF block data budget exceeded.");output.Write(Take(n));}}
    }
}
