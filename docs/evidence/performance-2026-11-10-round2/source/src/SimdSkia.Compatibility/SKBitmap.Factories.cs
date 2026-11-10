namespace SkiaSharp;
public partial class SKBitmap
{
    public SKBitmap(SKImageInfo info,SKBitmapAllocFlags flags)
    {if(!TryAllocPixels(info,flags))throw new Exception("Unable to allocate pixels for the bitmap.");}
    public bool TryAllocPixels(SKImageInfo info,SKBitmapAllocFlags flags)
    {if(!Enum.IsDefined(flags))throw new ArgumentOutOfRangeException(nameof(flags));return TryAllocPixels(info);}
    public bool ExtractSubset(SKBitmap destination,SKRectI subset)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(destination);destination.ThrowIfDisposed();using var source=PeekPixels();if(source is null)return false;using var part=source.ExtractSubset(subset);if(part is null)return false;var lease=part.RetainPixels();if(lease is null)return false;destination.ReplacePixels(part.Info,lease);return true;}
    public static SKBitmap Decode(SKCodec codec){ArgumentNullException.ThrowIfNull(codec);var info=codec.Info;if(info.AlphaType==SKAlphaType.Unpremul)info.AlphaType=SKAlphaType.Premul;return Decode(codec,info);}
    public static SKBitmap Decode(SKCodec codec,SKImageInfo bitmapInfo)
    {ArgumentNullException.ThrowIfNull(codec);var bitmap=new SKBitmap(bitmapInfo);try{var result=codec.GetPixels(bitmapInfo,bitmap.GetPixels(),bitmap.RowBytes,SKCodecOptions.Default);if(result is SKCodecResult.Success or SKCodecResult.IncompleteInput)return bitmap;bitmap.Dispose();return null!;}catch{bitmap.Dispose();throw;}}
    public static SKBitmap Decode(SKStream stream){using var data=SKData.Create(stream);return Decode(data);}
    public static SKBitmap Decode(SKStream stream,SKImageInfo bitmapInfo){using var data=SKData.Create(stream);return Decode(data,bitmapInfo);}
    public static SKBitmap Decode(SKData data,SKImageInfo bitmapInfo){ArgumentNullException.ThrowIfNull(data);return Decode(data.AsSpan(),bitmapInfo);}
    public static SKBitmap Decode(byte[] buffer,SKImageInfo bitmapInfo){ArgumentNullException.ThrowIfNull(buffer);return Decode(buffer.AsSpan(),bitmapInfo);}
    public static SKBitmap Decode(ReadOnlySpan<byte> buffer,SKImageInfo bitmapInfo)
    {using var codec=SKCodec.Create(buffer);return codec is null?null!:codec.TakeRgbaBitmap(bitmapInfo)??Decode(codec,bitmapInfo);}
    public static SKBitmap Decode(string filename,SKImageInfo bitmapInfo){ArgumentNullException.ThrowIfNull(filename);using var stream=File.OpenRead(filename);return Decode(stream,bitmapInfo);}
    public static SKBitmap Decode(Stream stream,SKImageInfo bitmapInfo){ArgumentNullException.ThrowIfNull(stream);using var codec=SKCodec.Create(stream);return codec is null?null!:Decode(codec,bitmapInfo);}
    public static SKImageInfo DecodeBounds(byte[] buffer){ArgumentNullException.ThrowIfNull(buffer);return DecodeBounds(buffer.AsSpan());}
    public static SKImageInfo DecodeBounds(ReadOnlySpan<byte> buffer){using var data=SKData.CreateCopy(buffer);return DecodeBounds(data);}
    public static SKImageInfo DecodeBounds(SKData data){ArgumentNullException.ThrowIfNull(data);using var codec=SKCodec.Create(data);return codec?.Info??default;}
    public static SKImageInfo DecodeBounds(string filename){ArgumentNullException.ThrowIfNull(filename);using var stream=File.OpenRead(filename);return DecodeBounds(stream);}
    public static SKImageInfo DecodeBounds(Stream stream){ArgumentNullException.ThrowIfNull(stream);using var codec=SKCodec.Create(stream);return codec?.Info??default;}
    public static SKImageInfo DecodeBounds(SKStream stream){ArgumentNullException.ThrowIfNull(stream);using var data=SKData.Create(stream);return DecodeBounds(data);}
    public bool Encode(SKWStream dst,SKEncodedImageFormat format,int quality){ArgumentNullException.ThrowIfNull(dst);using var data=Encode(format,quality);return dst.WriteSpan(data.AsSpan());}
}
