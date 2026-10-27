using SimdSkia;
using SimdSkia.Imaging;

namespace SkiaSharp;

public class SKCodec : SKObject
{
    private GifImage? gif;
    private PixelBuffer? still;
    private Stream? ownedStream;
    private readonly SKEncodedImageFormat format;
    private readonly SKImageInfo info;
    private readonly SKCodecFrameInfo[] frames;
    private SKCodec(ReadOnlySpan<byte> bytes)
    {
        if(bytes.StartsWith("GIF87a"u8)||bytes.StartsWith("GIF89a"u8))
        {
            gif=new(bytes);format=SKEncodedImageFormat.Gif;var first=gif.Frames[0];
            info=new(gif.Width,gif.Height,SKColorType.Bgra8888,first.TransparentIndex<0&&first.Left==0&&first.Top==0&&first.Width>=gif.Width&&first.Height>=gif.Height?SKAlphaType.Opaque:SKAlphaType.Unpremul);
            frames=new SKCodecFrameInfo[gif.Frames.Count];
            for(int i=0;i<frames.Length;i++)
            {
                var f=gif.Frames[i];bool full=f.Left==0&&f.Top==0&&f.Width>=info.Width&&f.Height>=info.Height;
                int required=i==0||full&&f.TransparentIndex<0?-1:i-1;
                while(required>=0&&gif.Frames[required].Disposal==3)required=frames[required].RequiredFrame;
                frames[i]=new(){RequiredFrame=required,Duration=f.Duration,FullyRecieved=true,HasAlphaWithinBounds=f.TransparentIndex>=0,
                    AlphaType=full&&f.TransparentIndex<0?SKAlphaType.Opaque:SKAlphaType.Unpremul,DisposalMethod=(SKCodecAnimationDisposalMethod)f.Disposal,
                    Blend=SKCodecAnimationBlend.SrcOver,FrameRect=new(f.Left,f.Top,Math.Min(info.Width,f.Left+f.Width),Math.Min(info.Height,f.Top+f.Height))};
            }
        }
        else
        {
            still=ImageCodec.Decode(bytes);format=bytes.Length>1&&bytes[0]==255?SKEncodedImageFormat.Jpeg:SKEncodedImageFormat.Png;
            bool opaque=true;var span=still.GetPixelSpan();for(int i=3;i<span.Length;i+=4)if(span[i]!=255){opaque=false;break;}
            info=new(still.Width,still.Height,SKColorType.Bgra8888,opaque?SKAlphaType.Opaque:SKAlphaType.Unpremul);frames=[];
        }
    }
    public SKImageInfo Info {get{ThrowIfDisposed();return info;}}
    public SKEncodedImageFormat EncodedFormat {get{ThrowIfDisposed();return format;}}
    public int FrameCount {get{ThrowIfDisposed();return frames.Length;}}
    public int RepetitionCount {get{ThrowIfDisposed();return gif?.RepetitionCount??0;}}
    public SKCodecFrameInfo[] FrameInfo {get{ThrowIfDisposed();return (SKCodecFrameInfo[])frames.Clone();}}
    public bool GetFrameInfo(int index,out SKCodecFrameInfo frameInfo){ThrowIfDisposed();frameInfo=default;if((uint)index>=(uint)frames.Length)return false;frameInfo=frames[index];return true;}
    public byte[] Pixels {get{var result=GetPixels(out var pixels);if(result!=SKCodecResult.Success)throw new InvalidDataException(result.ToString());return pixels;}}
    public static SKCodec? Create(SKData data){ArgumentNullException.ThrowIfNull(data);return CreateBytes(data.AsSpan(),out _);}
    public static SKCodec? Create(string filename)=>Create(filename,out _);
    public static SKCodec? Create(string filename,out SKCodecResult result)
    {
        ArgumentNullException.ThrowIfNull(filename);
        try{return Create(File.OpenRead(filename),out result);}catch(IOException){result=SKCodecResult.InternalError;return null;}
    }
    public static SKCodec? Create(Stream stream)=>Create(stream,out _);
    public static SKCodec? Create(Stream stream,out SKCodecResult result)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var copy=new MemoryStream();byte[] buffer=new byte[16384];int read;
            while((read=stream.Read(buffer))>0){if(copy.Length+read>256*1024*1024)throw new NotSupportedException("Encoded image exceeds the 256 MiB input budget.");copy.Write(buffer.AsSpan(0,read));}
            var codec=CreateBytes(copy.GetBuffer().AsSpan(0,(int)copy.Length),out result);
            if(codec is null)stream.Dispose();else codec.ownedStream=stream;return codec;
        }
        catch{stream.Dispose();throw;}
    }
    private static SKCodec? CreateBytes(ReadOnlySpan<byte> bytes,out SKCodecResult result)
    {
        try{var codec=new SKCodec(bytes);result=SKCodecResult.Success;return codec;}
        catch(NotSupportedException){result=SKCodecResult.Unimplemented;return null;}
        catch(Exception e)when(e is InvalidDataException or OverflowException or ArgumentException){result=SKCodecResult.InvalidInput;return null;}
    }
    public SKCodecResult GetPixels(out byte[] pixels)=>GetPixels(Info,out pixels);
    public SKCodecResult GetPixels(SKImageInfo info,out byte[] pixels){pixels=new byte[info.BytesSize];return GetPixels(info,pixels);}
    public unsafe SKCodecResult GetPixels(SKImageInfo info,byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);ThrowIfDisposed();if(pixels.Length<info.BytesSize)return SKCodecResult.InvalidParameters;
        fixed(byte* p=pixels)return GetPixels(info,(IntPtr)p,info.RowBytes,SKCodecOptions.Default);
    }
    public SKCodecResult GetPixels(SKImageInfo info,IntPtr pixels)=>GetPixels(info,pixels,info.RowBytes,SKCodecOptions.Default);
    public SKCodecResult GetPixels(SKImageInfo info,IntPtr pixels,SKCodecOptions options)=>GetPixels(info,pixels,info.RowBytes,options);
    public unsafe SKCodecResult GetPixels(SKImageInfo info,IntPtr pixels,int rowBytes,SKCodecOptions options)
    {
        ThrowIfDisposed();if(pixels==IntPtr.Zero)throw new ArgumentNullException(nameof(pixels));
        if(info.ColorType is not (SKColorType.Bgra8888 or SKColorType.Rgba8888)||info.AlphaType is not (SKAlphaType.Opaque or SKAlphaType.Premul or SKAlphaType.Unpremul))return SKCodecResult.InvalidConversion;
        if(info.ColorSpace is not null&&!info.ColorSpace.IsSrgb)return SKCodecResult.InvalidConversion;
        if(info.Width!=this.info.Width||info.Height!=this.info.Height)return SKCodecResult.InvalidScale;
        if(rowBytes<info.RowBytes||options.FrameIndex<0||options.PriorFrame< -1||options.PriorFrame>=options.FrameIndex)return SKCodecResult.InvalidParameters;
        if(options.FrameIndex>=Math.Max(1,FrameCount))return SKCodecResult.IncompleteInput;
        if(options.HasSubset)return SKCodecResult.Unimplemented;
        try
        {
            var source=gif is null?still!:gif.DecodeFrameCore(options.FrameIndex);
            int length=checked(rowBytes*(info.Height-1)+info.RowBytes);var target=new Span<byte>((void*)pixels,length);
            for(int y=0;y<info.Height;y++)
            {
                var input=source.GetRowSpan(y);var output=target.Slice(y*rowBytes,info.RowBytes);
                if(info.AlphaType==SKAlphaType.Unpremul&&info.ColorType==SKColorType.Rgba8888){input[..info.RowBytes].CopyTo(output);continue;}
                for(int x=0;x<info.Width;x++)
                {
                    var p=input.Slice(x*4,4);byte a=p[3],r=p[0],g=p[1],b=p[2];
                    if(info.AlphaType==SKAlphaType.Opaque&&a!=255)return SKCodecResult.InvalidConversion;
                    if(info.AlphaType==SKAlphaType.Premul){r=PixelBuffer.Multiply(r,a);g=PixelBuffer.Multiply(g,a);b=PixelBuffer.Multiply(b,a);}
                    int n=x*4;output[n]=info.ColorType==SKColorType.Rgba8888?r:b;output[n+1]=g;output[n+2]=info.ColorType==SKColorType.Rgba8888?b:r;output[n+3]=a;
                }
            }
            return SKCodecResult.Success;
        }
        catch(InvalidDataException){return SKCodecResult.ErrorInInput;}
    }
    protected override void Dispose(bool disposing){gif?.Dispose();gif=null;still?.Dispose();still=null;ownedStream?.Dispose();ownedStream=null;}
}
