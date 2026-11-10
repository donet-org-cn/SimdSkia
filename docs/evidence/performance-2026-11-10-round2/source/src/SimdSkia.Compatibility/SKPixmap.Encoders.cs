using SimdSkia.Imaging;
namespace SkiaSharp;
public partial class SKPixmap
{
    public SKData Encode(SKWebpEncoderOptions options)
    {
        ThrowIfDisposed();if(Storage.IsEmpty)return null!;
        if(!Enum.IsDefined(options.Compression))throw new ArgumentOutOfRangeException(nameof(options));
        if(info.ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Tagged WebP output requires ICC serialization; convert to sRGB first.");
        using var pixels=RetainPixels();return new(WebpCodec.Encode(pixels!,options.Compression==SKWebpEncoderCompression.Lossless,options.Quality));
    }
    public bool Encode(Stream dst,SKWebpEncoderOptions options){ArgumentNullException.ThrowIfNull(dst);using var data=Encode(options);if(data is null)return false;data.SaveTo(dst);return true;}
    public bool Encode(SKWStream dst,SKWebpEncoderOptions options){ArgumentNullException.ThrowIfNull(dst);using var data=Encode(options);return data is not null&&dst.WriteSpan(data.AsSpan());}
    public bool Encode(SKWStream dst,SKEncodedImageFormat encoder,int quality)
    {ArgumentNullException.ThrowIfNull(dst);using var data=Encode(encoder,quality);return data is not null&&dst.WriteSpan(data.AsSpan());}
    public SKData Encode(SKJpegEncoderOptions options)
    {
        ThrowIfDisposed();if(Storage.IsEmpty)return null!;
        if(options.Downsample==SKJpegEncoderDownsample.Downsample422)throw new NotSupportedException("The managed JPEG dependency does not yet encode 4:2:2. Choose 4:2:0 or 4:4:4 explicitly.");
        if(!Enum.IsDefined(options.Downsample)||!Enum.IsDefined(options.AlphaOption))throw new ArgumentOutOfRangeException(nameof(options));
        if(info.ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Tagged JPEG output requires ICC serialization; convert to sRGB first.");
        using var pixels=RetainPixels();return new(JpegCodec.Encode(pixels!,options.Quality,options.Downsample==SKJpegEncoderDownsample.Downsample420,options.AlphaOption==SKJpegEncoderAlphaOption.BlendOnBlack));
    }
    public SKData Encode(SKPngEncoderOptions options)
    {
        ThrowIfDisposed();if(Storage.IsEmpty)return null!;
        if(info.ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Tagged PNG output requires ICC serialization; convert to sRGB first.");
        using var pixels=RetainPixels();return new(PngCodec.Encode(pixels!,(int)options.FilterFlags,options.ZLibLevel));
    }
    public bool Encode(Stream dst,SKJpegEncoderOptions options){ArgumentNullException.ThrowIfNull(dst);using var data=Encode(options);if(data is null)return false;data.SaveTo(dst);return true;}
    public bool Encode(SKWStream dst,SKJpegEncoderOptions options){ArgumentNullException.ThrowIfNull(dst);using var data=Encode(options);return data is not null&&dst.WriteSpan(data.AsSpan());}
    public bool Encode(Stream dst,SKPngEncoderOptions options){ArgumentNullException.ThrowIfNull(dst);using var data=Encode(options);if(data is null)return false;data.SaveTo(dst);return true;}
    public bool Encode(SKWStream dst,SKPngEncoderOptions options){ArgumentNullException.ThrowIfNull(dst);using var data=Encode(options);return data is not null&&dst.WriteSpan(data.AsSpan());}
}
