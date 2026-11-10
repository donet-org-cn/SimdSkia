using SimdSkia;
using SimdSkia.Imaging;
namespace SkiaSharp;
public partial class SKImage
{
    public uint UniqueId{get{ThrowIfDisposed();return id;}}
    public bool IsTextureBacked{get{ThrowIfDisposed();return gpuImage is not null;}}
    public bool IsLazyGenerated{get{ThrowIfDisposed();return generated is not null&&gpuImage is null;}}
    public SKData EncodedData{get{ThrowIfDisposed();return encoded?.Subset(0,(ulong)encoded.Size)!;}}
    public SKData Encode(){ThrowIfDisposed();return EncodedData??Encode(SKEncodedImageFormat.Png,100);}
    public static SKImage Create(SKImageInfo info)
    {using var bitmap=new SKBitmap(info);return new(info,bitmap.Buffer.Share());}
    public static SKImage FromPixelCopy(SKImageInfo info,byte[] pixels)=>FromPixelCopy(info,pixels,info.RowBytes);
    public static SKImage FromPixelCopy(SKImageInfo info,byte[] pixels,int rowBytes){ArgumentNullException.ThrowIfNull(pixels);return FromPixelCopy(info,pixels.AsSpan(),rowBytes);}
    public static SKImage FromPixelCopy(SKImageInfo info,ReadOnlySpan<byte> pixels)=>FromPixelCopy(info,pixels,info.RowBytes);
    public static SKImage FromPixelCopy(SKImageInfo info,ReadOnlySpan<byte> pixels,int rowBytes)
    {PixelOperations.Validate(info,rowBytes);if(info.IsEmpty||pixels.Length<PixelOperations.Length(info,rowBytes))return null!;using var bitmap=new SKBitmap(info,rowBytes);pixels[..PixelOperations.Length(info,rowBytes)].CopyTo(bitmap.GetPixelSpan());return new(info,bitmap.Buffer.Share());}
    public static SKImage FromPixelCopy(SKImageInfo info,IntPtr pixels)=>FromPixelCopy(info,pixels,info.RowBytes);
    public static SKImage FromPixelCopy(SKImageInfo info,IntPtr pixels,int rowBytes)
    {if(pixels==IntPtr.Zero)throw new ArgumentNullException(nameof(pixels));return FromPixelCopy(info,PixelOperations.Wrap(info,pixels,rowBytes),rowBytes);}
    public static SKImage FromPixelCopy(SKPixmap pixmap){ArgumentNullException.ThrowIfNull(pixmap);return FromPixelCopy(pixmap.Info,pixmap.Storage,pixmap.RowBytes);}
    public static SKImage FromPixelCopy(SKImageInfo info,SKStream pixels)=>FromPixelCopy(info,pixels,info.RowBytes);
    public static SKImage FromPixelCopy(SKImageInfo info,SKStream pixels,int rowBytes){ArgumentNullException.ThrowIfNull(pixels);using var data=SKData.Create(pixels);return FromPixels(info,data,rowBytes);}
    public static SKImage FromPixelCopy(SKImageInfo info,Stream pixels)=>FromPixelCopy(info,pixels,info.RowBytes);
    public static SKImage FromPixelCopy(SKImageInfo info,Stream pixels,int rowBytes){ArgumentNullException.ThrowIfNull(pixels);using var data=SKData.Create(pixels);return FromPixels(info,data,rowBytes);}
    public static SKImage FromPixels(SKImageInfo info,SKData data)=>FromPixels(info,data,info.RowBytes);
    public static SKImage FromPixels(SKImageInfo info,SKData data,int rowBytes)
    {
        ArgumentNullException.ThrowIfNull(data);PixelOperations.Validate(info,rowBytes);int length=PixelOperations.Length(info,rowBytes);if(length==0||data.Size<length)return null!;var held=data.Subset(0,(ulong)data.Size);
        try{return new(info,PixelOperations.External(info,held.Data,rowBytes,held.Dispose));}catch{held.Dispose();throw;}
    }
    public static SKImage FromPixels(SKImageInfo info,IntPtr pixels)=>FromPixels(info,pixels,info.RowBytes);
    public static SKImage FromPixels(SKImageInfo info,IntPtr pixels,int rowBytes)
    {PixelOperations.Validate(info,rowBytes);return pixels==IntPtr.Zero||info.IsEmpty?null!:new(info,PixelOperations.External(info,pixels,rowBytes));}
    public static SKImage FromPixels(SKPixmap pixmap)=>FromPixels(pixmap,null,null!);
    public static SKImage FromPixels(SKPixmap pixmap,SKImageRasterReleaseDelegate? releaseProc)=>FromPixels(pixmap,releaseProc,null!);
    public static SKImage FromPixels(SKPixmap pixmap,SKImageRasterReleaseDelegate? releaseProc,object releaseContext)
    {ArgumentNullException.ThrowIfNull(pixmap);var address=pixmap.GetPixels();var held=pixmap.RetainPixels(releaseProc is null?null:()=>releaseProc(address,releaseContext));return held is null?null!:new(pixmap.Info,held);}
    public static SKImage FromEncodedData(byte[] data){ArgumentNullException.ThrowIfNull(data);return FromEncodedData(data.AsSpan());}
    public static SKImage FromEncodedData(ReadOnlySpan<byte> data){using var held=SKData.CreateCopy(data);return FromEncodedData(held);}
    public static SKImage FromEncodedData(SKData data)
    {
        ArgumentNullException.ThrowIfNull(data);if(data.IsEmpty||ImageCodec.DetectFormat(data.AsSpan())==ImageFileFormat.Unknown)return null!;
        (SKImageInfo Info,PixelBuffer Pixels) raster;try{raster=DecodedRaster.Read(data.AsSpan());}catch(InvalidDataException){return null!;}catch(EndOfStreamException){return null!;}
        try{return new(raster.Info,raster.Pixels){encoded=data.Subset(0,(ulong)data.Size)};}catch{raster.Pixels.Dispose();throw;}
    }
    public static SKImage FromEncodedData(SKData data,SKRectI subset){using var image=FromEncodedData(data);return image?.Subset(subset)!;}
    public static SKImage FromEncodedData(SKStream data){ArgumentNullException.ThrowIfNull(data);using var held=SKData.Create(data);return FromEncodedData(held);}
    public static SKImage FromEncodedData(Stream data){ArgumentNullException.ThrowIfNull(data);using var held=SKData.Create(data);return FromEncodedData(held);}
    public static SKImage FromEncodedData(string filename){ArgumentNullException.ThrowIfNull(filename);using var stream=File.OpenRead(filename);return FromEncodedData(stream);}
    public SKImage Subset(SKRectI subset)
    {
        ThrowIfDisposed();if(subset.Left>=subset.Right||subset.Top>=subset.Bottom||subset.Left<0||subset.Top<0||subset.Right>Width||subset.Bottom>Height)return null!;
        var next=info.WithSize(subset.Width,subset.Height);using var bitmap=new SKBitmap(next);PixelOperations.Read(Buffer.GetPixelSpan(),info,Buffer.RowBytes,bitmap.GetPixelSpan(),next,bitmap.RowBytes,subset.Left,subset.Top);return new(next,bitmap.Buffer.Share());
    }
    public SKImage ToRasterImage()=>ToRasterImage(false);
    public SKImage ToRasterImage(bool ensurePixelData)
    {ThrowIfDisposed();if(gpuImage is null&&(!ensurePixelData||generated is null))return this;var image=new SKImage(info,gpuImage is not null?Buffer.Clone():Buffer.Share(),id);image.encoded=encoded?.Subset(0,(ulong)encoded.Size);return image;}
}
