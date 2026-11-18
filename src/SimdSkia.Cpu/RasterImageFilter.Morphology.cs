using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SimdSkia.Cpu;

public sealed partial class RasterImageFilter
{
    public static RasterImageFilter Morphology(float radiusX, float radiusY, bool dilate, RasterImageFilter? input = null, RectF? crop = null)
    {
        ValidateSigma(radiusX);
        ValidateSigma(radiusY);
        return new((source, matrix, clip) =>
        {
            using var image = Input(input, source, matrix, clip);
            float x = MathF.Floor(MathF.Abs(matrix.M00) * radiusX + MathF.Abs(matrix.M01) * radiusY + .5f);
            float y = MathF.Floor(MathF.Abs(matrix.M10) * radiusX + MathF.Abs(matrix.M11) * radiusY + .5f);
            if (x > 1024 || y > 1024)
                throw new NotSupportedException("Morphology radius exceeds 1024 device pixels.");
            int rx = (int)x, ry = (int)y;
            var sourceBounds = image.Bounds;
            var bounds = Crop(dilate ? Inflate(sourceBounds, rx, ry) : sourceBounds, crop, matrix);
            var result = FilterFrame.Rent(bounds);
            try
            {
                if (ry == 0)
                    MorphologyPass(image, result, rx, dilate, vertical: false);
                else if (rx == 0 && image.Pixels?.Format != PixelFormat.Alpha8)
                    MorphologyPass(image, result, ry, dilate, vertical: true);
                else if (result.Pixels is not null)
                {
                    // The vertical pass supplies transparent samples outside this frame.
                    // Horizontal processing only needs rows with source pixels.
                    var rows = new RectF(bounds.Left, MathF.Max(bounds.Top - ry, sourceBounds.Top),
                        bounds.Right, MathF.Min(bounds.Bottom + ry, sourceBounds.Bottom));
                    using var horizontal = FilterFrame.Rent(rows);
                    MorphologyPass(image, horizontal, rx, dilate, vertical: false);
                    MorphologyPass(horizontal, result, ry, dilate, vertical: true);
                }
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }, input);
    }

    private static void MorphologyPass(FilterFrame source, FilterFrame destination, int radius, bool dilate, bool vertical)
    {
        if (source.Pixels is not { } input || destination.Pixels is not { } output)
            return;
        if (input.Format == PixelFormat.Alpha8)
        {
            for (int y = 0; y < output.Height; y++)
            {
                var row = output.GetRowSpan(y);
                for (int x = 0; x < output.Width; x++)
                {
                    var value = dilate ? Vector4.Zero : Vector4.One;
                    for (int k = -radius; k <= radius; k++)
                    {
                        var sample = Read(source, destination.Left + x + (vertical ? 0 : k), destination.Top + y + (vertical ? k : 0));
                        value = dilate ? Vector4.Max(value, sample) : Vector4.Min(value, sample);
                    }
                    Write(row, x, value);
                }
            }
            return;
        }

        int inputLength = vertical ? input.Height : input.Width;
        int outputLength = vertical ? output.Height : output.Width;
        int inputLines = vertical ? input.Width : input.Height;
        int outputLines = vertical ? output.Width : output.Height;
        int inputStep = vertical ? input.RowBytes : 4;
        int outputStep = vertical ? output.RowBytes : 4;
        int inputLineStep = vertical ? 4 : input.RowBytes;
        int outputLineStep = vertical ? 4 : output.RowBytes;
        long firstSample = vertical ? (long)destination.Top - source.Top - radius : (long)destination.Left - source.Left - radius;
        long firstLine = vertical ? (long)destination.Left - source.Left : (long)destination.Top - source.Top;
        bool swapRedBlue = input.Format == PixelFormat.Rgba8888;
        var alpha = input.Alpha;
        bool simd = CpuOptions.EnableSimd && Vector128.IsHardwareAccelerated;
        ReadOnlySpan<byte> sourcePixels = input.GetPixelSpan();
        Span<byte> targetPixels = output.GetPixelSpan();

        // Every window spans the suffix of one block and, except when aligned,
        // the prefix of its neighbor. Cache one block and extend the prefix as we write.
        int window = 2 * radius + 1;
        var rented = window > 256 ? ArrayPool<uint>.Shared.Rent(window) : null;
        Span<uint> suffix = rented is null ? stackalloc uint[window] : rented.AsSpan(0, window);
        try
        {
            for (int line = 0; line < outputLines; line++)
            {
                long sourceLine = firstLine + line;
                if ((ulong)sourceLine >= (uint)inputLines)
                    continue;
                var from = sourcePixels.Slice((int)sourceLine * inputLineStep);
                var to = targetPixels.Slice(line * outputLineStep);
                for (int offset = 0; offset < outputLength; offset += window)
                {
                    long start = firstSample + offset;
                    long next = start + window;
                    uint running = MorphologySample(from, next - 1, inputLength, inputStep, swapRedBlue, alpha);
                    suffix[window - 1] = running;
                    for (int i = window - 2; i >= 0; i--)
                    {
                        running = CombineExtrema(running, MorphologySample(from, start + i, inputLength, inputStep, swapRedBlue, alpha), dilate, simd);
                        suffix[i] = running;
                    }

                    int count = Math.Min(window, outputLength - offset);
                    BinaryPrimitives.WriteUInt32LittleEndian(to[(offset * outputStep)..], running);
                    running = dilate ? 0 : uint.MaxValue;
                    for (int i = 1; i < count; i++)
                    {
                        running = CombineExtrema(running, MorphologySample(from, next + i - 1, inputLength, inputStep, swapRedBlue, alpha), dilate, simd);
                        BinaryPrimitives.WriteUInt32LittleEndian(to[((offset + i) * outputStep)..],
                            CombineExtrema(suffix[i], running, dilate, simd));
                    }
                }
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<uint>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MorphologySample(ReadOnlySpan<byte> pixels, long index, int length, int step, bool swapRedBlue, AlphaFormat alpha)
    {
        if ((ulong)index >= (uint)length)
            return 0;
        uint bgra = BinaryPrimitives.ReadUInt32LittleEndian(pixels[((int)index * step)..]);
        if (swapRedBlue)
            bgra = (bgra & 0xff00ff00u) | ((bgra & 255u) << 16) | ((bgra >> 16) & 255u);
        if (alpha == AlphaFormat.Opaque)
            return bgra | 0xff000000u;
        if (alpha == AlphaFormat.Premultiplied)
            return bgra;
        // The monotone quantization commutes with each channel's min/max.
        byte a = (byte)(bgra >> 24);
        return (bgra & 0xff000000u) | PixelBuffer.Multiply((byte)bgra, a)
            | ((uint)PixelBuffer.Multiply((byte)(bgra >> 8), a) << 8)
            | ((uint)PixelBuffer.Multiply((byte)(bgra >> 16), a) << 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CombineExtrema(uint a, uint b, bool dilate, bool simd)
    {
        if (simd)
        {
            var left = Vector128.CreateScalarUnsafe(a).AsByte();
            var right = Vector128.CreateScalarUnsafe(b).AsByte();
            return (dilate ? Vector128.Max(left, right) : Vector128.Min(left, right)).AsUInt32().ToScalar();
        }
        return (uint)ExtremaByte((byte)a, (byte)b, dilate)
            | ((uint)ExtremaByte((byte)(a >> 8), (byte)(b >> 8), dilate) << 8)
            | ((uint)ExtremaByte((byte)(a >> 16), (byte)(b >> 16), dilate) << 16)
            | ((uint)ExtremaByte((byte)(a >> 24), (byte)(b >> 24), dilate) << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ExtremaByte(byte a, byte b, bool dilate) => dilate ? Math.Max(a, b) : Math.Min(a, b);
}
