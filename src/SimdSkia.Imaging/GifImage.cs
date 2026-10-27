using System.Buffers.Binary;

namespace SimdSkia.Imaging;

public sealed record GifFrame(int Left,int Top,int Width,int Height,int Duration,int Disposal,int TransparentIndex,bool Interlaced,byte[] Palette,byte[] Compressed,int CodeSize);

/// <summary>GIF87a/89a LZW decoder. Two compositing buffers suffice regardless of frame count.</summary>
public sealed class GifImage : IDisposable
{
    private readonly List<GifFrame> frames=[];
    private PixelBuffer? canvas,restore;
    private int current=-1;
    private readonly byte[] indexBuffer;
    private bool disposed;
    public int Width {get;}
    public int Height {get;}
    public int RepetitionCount {get;private set;}
    public IReadOnlyList<GifFrame> Frames=>frames;
    public GifImage(ReadOnlySpan<byte> encoded)
    {
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
        indexBuffer=new byte[frames.Max(f=>checked(f.Width*f.Height))];
    }
    public PixelBuffer DecodeFrame(int index)=>DecodeFrameCore(index).Clone();
    internal PixelBuffer DecodeFrameCore(int index)
    {
        ObjectDisposedException.ThrowIf(disposed,this);if((uint)index>=(uint)frames.Count)throw new ArgumentOutOfRangeException(nameof(index));
        if(canvas is null||index<current){canvas?.Dispose();restore?.Dispose();restore=null;canvas=new(Width,Height,PixelFormat.Rgba8888,AlphaFormat.Straight);current=-1;}
        try
        {
            for(int i=current+1;i<=index;i++)
            {
                if(i>0)
                {
                    var prior=frames[i-1];
                    if(prior.Disposal==2){for(int y=prior.Top;y<Math.Min(Height,prior.Top+prior.Height);y++)if(prior.Left<Width)canvas.GetRowSpan(y).Slice(prior.Left*4,Math.Min(prior.Width,Width-prior.Left)*4).Clear();}
                    else if(prior.Disposal==3&&restore is not null)restore.GetPixelSpan().CopyTo(canvas.GetPixelSpan());
                }
                restore?.Dispose();restore=null;var frame=frames[i];if(frame.Disposal==3)restore=canvas.Clone();
                DecodeLzw(frame,indexBuffer.AsSpan(0,frame.Width*frame.Height));int sourceRow=0;
                void Row(int y)
                {
                    if(y+frame.Top<Height)
                    {
                        var row=canvas.GetRowSpan(y+frame.Top);
                        for(int x=0;x<frame.Width&&x+frame.Left<Width;x++)
                        {
                            int color=indexBuffer[sourceRow*frame.Width+x];if(color==frame.TransparentIndex)continue;
                            if(color>=frame.Palette.Length/3)throw new InvalidDataException("GIF color index exceeds its palette.");
                            var pixel=row.Slice((x+frame.Left)*4,4);frame.Palette.AsSpan(color*3,3).CopyTo(pixel);pixel[3]=255;
                        }
                    }sourceRow++;
                }
                if(frame.Interlaced){for(int pass=0;pass<4;pass++){int start=pass switch{0=>0,1=>4,2=>2,_=>1},step=pass<2?8:pass==2?4:2;for(int y=start;y<frame.Height;y+=step)Row(y);}}
                else for(int y=0;y<frame.Height;y++)Row(y);
                current=i;
            }
            return canvas;
        }
        catch{canvas.Dispose();canvas=null;restore?.Dispose();restore=null;current=-1;throw;}
    }
    private static void DecodeLzw(GifFrame frame,Span<byte> result)
    {
        int clear=1<<frame.CodeSize,end=clear+1,next=end+1,bits=frame.CodeSize+1,old=-1,first=0,position=0,output=0;
        Span<ushort> prefix=stackalloc ushort[4096];Span<byte> suffix=stackalloc byte[4096];Span<byte> stack=stackalloc byte[4097];
        for(int i=0;i<clear;i++)suffix[i]=(byte)i;
        while(true)
        {
            if((long)position+bits>(long)frame.Compressed.Length*8)throw new InvalidDataException("Truncated GIF LZW data.");
            int code=0;for(int b=0;b<bits;b++)code|=((frame.Compressed[(position+b)>>3]>>((position+b)&7))&1)<<b;position+=bits;
            if(code==clear){next=end+1;bits=frame.CodeSize+1;old=-1;continue;}
            if(code==end){if(output!=result.Length)throw new InvalidDataException("Incomplete GIF frame.");return;}
            if(output==result.Length)throw new InvalidDataException("Excess GIF frame pixels.");
            int input=code,top=0;
            if(code==next&&old>=0){stack[top++]=(byte)first;code=old;}
            else if(code>=next)throw new InvalidDataException("Invalid GIF LZW dictionary reference.");
            while(code>=clear){if(code<end+1||code>=next||top>=4096)throw new InvalidDataException("Cyclic GIF LZW dictionary.");stack[top++]=suffix[code];code=prefix[code];}
            first=code;stack[top++]=(byte)first;
            if(top>result.Length-output)throw new InvalidDataException("GIF LZW output exceeds frame.");
            while(top>0)result[output++]=stack[--top];
            if(old>=0&&next<4096){prefix[next]=(ushort)old;suffix[next]=(byte)first;next++;if(next==(1<<bits)&&bits<12)bits++;}
            old=input;
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;canvas?.Dispose();restore?.Dispose();canvas=null;restore=null;frames.Clear();}
    private ref struct Reader(ReadOnlySpan<byte> data)
    {
        private ReadOnlySpan<byte> remaining=data;
        public ReadOnlySpan<byte> Take(int n){if(n<0||n>remaining.Length)throw new InvalidDataException("Truncated GIF stream.");var value=remaining[..n];remaining=remaining[n..];return value;}
        public int Byte()=>Take(1)[0];
        public int U16()=>BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
        public byte[] Blocks(){using var output=new MemoryStream();while(true){int n=Byte();if(n==0)return output.ToArray();if(output.Length+n>256*1024*1024)throw new NotSupportedException("GIF block data budget exceeded.");output.Write(Take(n));}}
    }
}
