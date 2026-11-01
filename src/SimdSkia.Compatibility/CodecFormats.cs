using SimdSkia.Imaging;
namespace SkiaSharp;

internal static class CodecFormats
{
    internal static ImageFileFormat ToEngine(SKEncodedImageFormat format)=>format switch
    {
        SKEncodedImageFormat.Png=>ImageFileFormat.Png,SKEncodedImageFormat.Jpeg=>ImageFileFormat.Jpeg,
        SKEncodedImageFormat.Gif=>ImageFileFormat.Gif,SKEncodedImageFormat.Bmp=>ImageFileFormat.Bmp,
        SKEncodedImageFormat.Ico=>ImageFileFormat.Ico,SKEncodedImageFormat.Webp=>ImageFileFormat.Webp,
        _=>throw new NotSupportedException($"Encoding {format} is not implemented by the selected codec package.")
    };
    internal static SKEncodedImageFormat ToSkia(ImageFileFormat format)=>format switch
    {
        ImageFileFormat.Png=>SKEncodedImageFormat.Png,ImageFileFormat.Jpeg=>SKEncodedImageFormat.Jpeg,
        ImageFileFormat.Gif=>SKEncodedImageFormat.Gif,ImageFileFormat.Bmp=>SKEncodedImageFormat.Bmp,
        ImageFileFormat.Ico=>SKEncodedImageFormat.Ico,ImageFileFormat.Webp=>SKEncodedImageFormat.Webp,
        _=>throw new NotSupportedException($"{format} has no corresponding SKEncodedImageFormat in the pinned SkiaSharp baseline; use SimdSkia.Imaging.ImageCodec.")
    };
}
