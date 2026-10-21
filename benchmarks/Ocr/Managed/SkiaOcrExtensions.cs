using SkiaSharp;
using Qcxt.OCR;
namespace Qcxt.OCR.SkiaSharp;
public static class SkiaOcrExtensions
{
    public static PaddleOcrResult RunBitmap(this PaddleOcrAll ocr, SKBitmap bitmap, bool returnCtcAlignment = false)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.ByteCount == 0) throw new ArgumentException("Bitmap has no pixels.", nameof(bitmap));
        // SkiaSharp 各平台解码器最常见 Bgra8888（Windows、Linux）和 Rgba8888（macOS、iOS），
        // 推理层在采样时按 ImagePixelFormat 直接调整通道顺序，行步幅原样传递，不产生中间拷贝。
        ImagePixelFormat format = bitmap.ColorType switch
        {
            SKColorType.Bgra8888 => ImagePixelFormat.Bgra32,
            SKColorType.Rgba8888 => ImagePixelFormat.Rgba32,
            _ => throw new ArgumentException($"Unsupported bitmap color type {bitmap.ColorType}; expected Bgra8888 or Rgba8888.", nameof(bitmap)),
        };
        return ocr.Run(bitmap.GetPixelSpan(), bitmap.Width, bitmap.Height, returnCtcAlignment, bitmap.RowBytes, format);
    }
}
