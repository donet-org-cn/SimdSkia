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
    public GifImage(ReadOnlySpan<byte> encoded) : this(encoded, includeFrameData: true) { }

    // SKCodec and still-image decoding use the frame geometry and timing.
    // The upstream reader already owns the encoded data it needs to composite.
    internal static GifImage OpenForCodec(ReadOnlySpan<byte> encoded) => new(encoded, includeFrameData: false);

    private GifImage(ReadOnlySpan<byte> encoded, bool includeFrameData)
    {
        CodecBackend.ValidateInput(encoded);
        var reader=new Reader(encoded);
        var signature=reader.Take(6);if(!signature.SequenceEqual("GIF87a"u8)&&!signature.SequenceEqual("GIF89a"u8))throw new InvalidDataException("Invalid GIF header.");
        Width=reader.U16();Height=reader.U16();
        if(Width==0||Height==0||(long)Width*Height>16*1024*1024)throw new NotSupportedException("GIF dimensions exceed the 16 megapixel decode budget.");
        int packed=reader.Byte();reader.Take(2);
        var globalTable = (packed & 128) != 0 ? reader.Take(3 << ((packed & 7) + 1)) : ReadOnlySpan<byte>.Empty;
        byte[] palette = includeFrameData ? globalTable.ToArray() : [];
        Span<byte> extensionPrefix = stackalloc byte[3];
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
                    var app = reader.Take(reader.Byte());
                    int length = reader.ReadBlocks(extensionPrefix);
                    if ((app.SequenceEqual("NETSCAPE2.0"u8) || app.SequenceEqual("ANIMEXTS1.0"u8)) &&
                        length >= extensionPrefix.Length && extensionPrefix[0] == 1)
                    {
                        int loops = BinaryPrimitives.ReadUInt16LittleEndian(extensionPrefix[1..]);
                        RepetitionCount = loops == 0 ? -1 : loops;
                    }
                }
                else if(label==1)throw new NotSupportedException("GIF plain-text rendering extensions are not supported.");
                else reader.ReadBlocks();
                continue;
            }
            if(marker!=0x2c)throw new InvalidDataException("Invalid GIF block marker.");
            int x=reader.U16(),y=reader.U16(),w=reader.U16(),h=reader.U16();int flags=reader.Byte();
            if(w==0||h==0||(long)w*h>16*1024*1024)throw new InvalidDataException("Invalid GIF frame dimensions.");
            bool hasLocalTable = (flags & 128) != 0;
            var colorTable = hasLocalTable ? reader.Take(3 << ((flags & 7) + 1)) : globalTable;
            if (colorTable.IsEmpty) throw new InvalidDataException("GIF frame has no color table.");
            int size=reader.Byte();if(size is <2 or >8)throw new InvalidDataException("Invalid GIF LZW minimum code size.");
            byte[] colors = [], compressed = [];
            if (includeFrameData)
            {
                colors = hasLocalTable ? colorTable.ToArray() : palette;
                compressed = reader.CopyBlocks();
            }
            else reader.ReadBlocks();
            frames.Add(new(x,y,w,h,duration,disposal,transparent,(flags&64)!=0,colors,compressed,size));
            if(frames.Count>100000)throw new NotSupportedException("GIF frame budget exceeded.");
            duration=0;disposal=1;transparent=-1;
        }
        if(frames.Count==0)throw new InvalidDataException("GIF contains no image frames.");
        if(frames.Count==1)RepetitionCount=-1;
        if(!Sdcb.SimdCodecs.ImageCodecs.TryOpenFrames(encoded.ToArray(),out var frameReader))throw new InvalidDataException("Sdcb rejected GIF frames.");
        this.reader=frameReader;
        if(frameReader.FrameCount!=frames.Count){frameReader.Dispose();throw new InvalidDataException("GIF frame count differs from metadata.");}
    }
    public PixelBuffer DecodeFrame(int index)
    {
        CheckFrame(index);
        var output = PixelBuffer.RentRgbaExact(Width, Height);
        try
        {
            DecodeInto(index, output);
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    internal PixelBuffer DecodeFrameCore(int index)
    {
        CheckFrame(index);
        canvas ??= PixelBuffer.RentRgba(Width, Height);
        DecodeInto(index, canvas);
        return canvas;
    }

    private void CheckFrame(int index)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if ((uint)index >= (uint)frames.Count) throw new ArgumentOutOfRangeException(nameof(index));
    }

    private void DecodeInto(int index, PixelBuffer output)
    {
        if (!reader!.TryDecodeFrameInto(index, output.GetPixelSpan(), output.RowBytes, Sdcb.SimdCodecs.Abstractions.PixelFormat.Rgba32))
            throw new InvalidDataException("GIF frame decode failed.");
    }

    public void Dispose(){if(disposed)return;disposed=true;reader?.Dispose();reader=null;canvas?.Dispose();canvas=null;frames.Clear();}
    private ref struct Reader(ReadOnlySpan<byte> data)
    {
        private ReadOnlySpan<byte> remaining=data;
        public ReadOnlySpan<byte> Take(int n){if(n<0||n>remaining.Length)throw new InvalidDataException("Truncated GIF stream.");var value=remaining[..n];remaining=remaining[n..];return value;}
        public int Byte()=>Take(1)[0];
        public int U16()=>BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
        public int ReadBlocks(scoped Span<byte> prefix = default)
        {
            int length = 0;
            while (true)
            {
                int size = Byte();
                if (size == 0) return length;
                var block = Take(size);
                if (length < prefix.Length)
                    block[..Math.Min(size, prefix.Length - length)].CopyTo(prefix[length..]);
                length += size;
            }
        }

        public byte[] CopyBlocks()
        {
            var start = this;
            int length = ReadBlocks();
            if (length == 0) return [];
            var result = new byte[length];
            start.ReadBlocks(result);
            return result;
        }
    }
}
