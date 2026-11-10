using SimdSkia;
using System.Buffers;
namespace SkiaSharp;
public partial class SKCodec
{
    private readonly SKEncodedOrigin encodedOrigin=SKEncodedOrigin.TopLeft;
    private readonly SKCodecScanlineOrder scanlineOrder;
    private SKStream? ownedSkStream;
    private PixelBuffer? scanPixels;
    private SKImageInfo scanInfo;
    private int nextScanline=-1;
    private sealed record IncrementalState(SKImageInfo Info,IntPtr Address,int Stride,SKCodecOptions Options){internal bool Completed;}
    private IncrementalState? incremental;
    public static int MinBufferedBytesNeeded=>32;
    public SKEncodedOrigin EncodedOrigin {get{ThrowIfDisposed();return encodedOrigin;}}
    public SKCodecScanlineOrder ScanlineOrder {get{ThrowIfDisposed();return scanlineOrder;}}
    public int NextScanline {get{ThrowIfDisposed();return GetOutputScanline(nextScanline);}}
    public int GetOutputScanline(int inputScanline){ThrowIfDisposed();return scanlineOrder==SKCodecScanlineOrder.BottomUp?info.Height-inputScanline-1:inputScanline;}
    public SKSizeI GetScaledDimensions(float desiredScale)
    {
        ThrowIfDisposed();if(!float.IsFinite(desiredScale)||desiredScale<=0)throw new ArgumentOutOfRangeException(nameof(desiredScale));
        if(format==SKEncodedImageFormat.Jpeg){int n=(int)Math.Clamp(MathF.Floor(desiredScale*8+.5f),1,8);return new((info.Width*n+7)/8,(info.Height*n+7)/8);}
        if(format is SKEncodedImageFormat.Webp or SKEncodedImageFormat.Gif)return new((int)Math.Clamp(MathF.Floor(info.Width*desiredScale+.5f),1,info.Width),(int)Math.Clamp(MathF.Floor(info.Height*desiredScale+.5f),1,info.Height));
        return info.Size;
    }
    public bool GetValidSubset(ref SKRectI desiredSubset)
    {
        ThrowIfDisposed();var r=desiredSubset;if(format!=SKEncodedImageFormat.Webp||r.Left<0||r.Top<0||r.Left>=r.Right||r.Top>=r.Bottom||r.Right>info.Width||r.Bottom>info.Height)return false;
        desiredSubset=new(r.Left&~1,r.Top&~1,r.Right,r.Bottom);return true;
    }
    public static SKCodec? Create(SKStream stream)=>Create(stream,out _);
    public static SKCodec? Create(SKStream stream,out SKCodecResult result)
    {
        ArgumentNullException.ThrowIfNull(stream);stream.ThrowIfDisposed();byte[] rented=ArrayPool<byte>.Shared.Rent(16384);
        try
        {
            using var bytes=new MemoryStream();int n;while((n=stream.ReadSpan(rented))!=0){if(bytes.Length+n>256*1024*1024)throw new NotSupportedException("Encoded image exceeds the 256 MiB budget.");bytes.Write(rented.AsSpan(0,n));}
            var codec=CreateBytes(bytes.GetBuffer().AsSpan(0,(int)bytes.Length),out result);if(codec is null)stream.Dispose();else codec.ownedSkStream=stream;return codec;
        }
        catch{stream.Dispose();throw;}
        finally{ArrayPool<byte>.Shared.Return(rented);}
    }
    private void CancelDecode(){scanPixels?.Dispose();scanPixels=null;nextScanline=-1;incremental=null;}
    private SKCodecResult ValidateDecode(SKImageInfo target,int stride,SKCodecOptions options)
    {
        if(target.ColorType is not (SKColorType.Bgra8888 or SKColorType.Rgba8888)||target.AlphaType is not (SKAlphaType.Premul or SKAlphaType.Unpremul or SKAlphaType.Opaque)||target.ColorSpace is {} space&&!space.IsSrgb)return SKCodecResult.InvalidConversion;
        if(target.Width!=info.Width||target.Height!=info.Height)return SKCodecResult.InvalidScale;
        if(stride<target.RowBytes||options.FrameIndex<0||options.PriorFrame< -1||options.PriorFrame>=options.FrameIndex)return SKCodecResult.InvalidParameters;
        if(options.FrameIndex>=Math.Max(1,FrameCount))return SKCodecResult.IncompleteInput;
        if(options.HasSubset)return SKCodecResult.Unimplemented;
        if(target.AlphaType==SKAlphaType.Opaque&&info.AlphaType!=SKAlphaType.Opaque)return SKCodecResult.InvalidConversion;
        return SKCodecResult.Success;
    }
    public SKCodecResult StartScanlineDecode(SKImageInfo info)=>StartScanlineDecode(info,SKCodecOptions.Default);
    public SKCodecResult StartScanlineDecode(SKImageInfo info,SKCodecOptions options)
    {
        ThrowIfDisposed();CancelDecode();var result=ValidateDecode(info,info.RowBytes,options);if(result!=SKCodecResult.Success)return result;
        if(format is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Bmp))return SKCodecResult.Unimplemented;
        using var frame=sequence?.DecodeFrame(options.FrameIndex);var source=frame??(gif is null?still!:gif.DecodeFrameCore(options.FrameIndex));scanPixels=source.Share();scanInfo=SKColorSpace.RetainInfo(info);nextScanline=0;return SKCodecResult.Success;
    }
    public unsafe int GetScanlines(IntPtr dst,int countLines,int rowBytes)
    {
        ThrowIfDisposed();if(dst==IntPtr.Zero)throw new ArgumentNullException(nameof(dst));if(countLines<0||rowBytes<scanInfo.RowBytes)return 0;if(scanPixels is null)return 0;
        int count=Math.Min(countLines,info.Height-nextScanline);if(count<=0)return 0;
        var destination=new Span<byte>((void*)dst,checked((count-1)*rowBytes+scanInfo.RowBytes));var sourceInfo=new SKImageInfo(scanPixels.Width,scanPixels.Height,scanPixels.Format==PixelFormat.Rgba8888?SKColorType.Rgba8888:SKColorType.Bgra8888,this.info.AlphaType==SKAlphaType.Opaque?SKAlphaType.Opaque:scanPixels.Alpha switch{AlphaFormat.Straight=>SKAlphaType.Unpremul,AlphaFormat.Premultiplied=>SKAlphaType.Premul,_=>SKAlphaType.Opaque});
        if(scanlineOrder==SKCodecScanlineOrder.TopDown){PixelOperations.Read(scanPixels.GetPixelSpan(),sourceInfo,scanPixels.RowBytes,destination,scanInfo.WithSize(scanInfo.Width,count),rowBytes,0,nextScanline);nextScanline+=count;return count;}
        int firstSourceY=GetOutputScanline(nextScanline+count-1);
        PixelOperations.Read(scanPixels.GetPixelSpan(),sourceInfo,scanPixels.RowBytes,destination,scanInfo.WithSize(scanInfo.Width,count),rowBytes,0,firstSourceY);nextScanline+=count;
        return count;
    }
    public bool SkipScanlines(int countLines)
    {ThrowIfDisposed();if(scanPixels is null||countLines<0||(long)nextScanline+countLines>info.Height)return false;nextScanline+=countLines;return true;}
    public SKCodecResult StartIncrementalDecode(SKImageInfo info,IntPtr pixels,int rowBytes)=>StartIncrementalDecode(info,pixels,rowBytes,SKCodecOptions.Default);
    public SKCodecResult StartIncrementalDecode(SKImageInfo info,IntPtr pixels,int rowBytes,SKCodecOptions options)
    {
        ThrowIfDisposed();if(pixels==IntPtr.Zero)throw new ArgumentNullException(nameof(pixels));CancelDecode();var result=ValidateDecode(info,rowBytes,options);if(result!=SKCodecResult.Success)return result;
        if(format is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Gif))return SKCodecResult.Unimplemented;
        incremental=new(SKColorSpace.RetainInfo(info),pixels,rowBytes,options);return SKCodecResult.Success;
    }
    public SKCodecResult IncrementalDecode()=>IncrementalDecode(out _);
    public SKCodecResult IncrementalDecode(out int rowsDecoded)
    {
        ThrowIfDisposed();rowsDecoded=0;var state=incremental;if(state is null)return SKCodecResult.InternalError;
        if(state.Completed)return SKCodecResult.Success;
        var result=GetPixelsCore(state.Info,state.Address,state.Stride,state.Options);if(result==SKCodecResult.Success){state.Completed=true;rowsDecoded=state.Info.Height;if(format==SKEncodedImageFormat.Gif)incremental=null;}return result;
    }
}
