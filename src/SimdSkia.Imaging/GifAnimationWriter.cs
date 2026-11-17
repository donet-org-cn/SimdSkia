using System.Buffers.Binary;
using Sdcb.SimdCodecs.Gif;

namespace SimdSkia.Imaging;

/// <summary>Controls what stays on the GIF canvas after a frame's delay elapses.</summary>
public enum GifFrameDisposal
{
    Keep = 1,
    RestoreBackground = 2,
    RestorePrevious = 3
}

/// <summary>
/// Encodes full-canvas frames directly to a writable stream, with one palette per
/// frame and GIF's one-bit transparency (alpha below 128 is transparent).
/// The writer does not retain the source pixels or close the stream.
/// </summary>
public sealed class GifAnimationWriter : IDisposable
{
    private readonly Stream output;
    private readonly int width, height, loopCount;
    private int frameCount;
    private bool complete, faulted, disposed;

    /// <param name="output">Writable destination; seeking is not required.</param>
    /// <param name="width">Canvas width in pixels.</param>
    /// <param name="height">Canvas height in pixels.</param>
    /// <param name="loopCount">Total plays, including the first; 0 repeats forever.</param>
    public GifAnimationWriter(Stream output, int width, int height, int loopCount = 0)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("The GIF destination must be writable.", nameof(output));
        if (width is < 1 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 1 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(height));
        if ((long)width * height > 16 * 1024 * 1024) throw new NotSupportedException("GIF dimensions exceed the 16 megapixel budget.");
        if (loopCount is < 0 or > ushort.MaxValue + 1) throw new ArgumentOutOfRangeException(nameof(loopCount));
        this.output = output;
        this.width = width;
        this.height = height;
        this.loopCount = loopCount;
    }

    /// <summary>
    /// Writes one frame. The delay is rounded to the nearest 10 ms, up to 655.35 s.
    /// Transparent pixels show the canvas left by the previous frame's disposal.
    /// </summary>
    public void AddFrame(PixelBuffer pixels, TimeSpan delay, GifFrameDisposal disposal = GifFrameDisposal.RestoreBackground)
    {
        CheckState();
        if (complete) throw new InvalidOperationException("The GIF is already complete.");
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Width != width || pixels.Height != height) throw new ArgumentException("The frame must match the canvas dimensions.", nameof(pixels));
        if (delay.Ticks < 0 || delay.Ticks > (long)ushort.MaxValue * TicksPerDelay) throw new ArgumentOutOfRangeException(nameof(delay));
        if (disposal is < GifFrameDisposal.Keep or > GifFrameDisposal.RestorePrevious) throw new ArgumentOutOfRangeException(nameof(disposal));
        if (frameCount >= 100000) throw new NotSupportedException("GIF frame budget exceeded.");

        using var source = new EncodingPixels(pixels, true);
        byte[] still = GifEncoder.Encode(source.Span, width, height, 4);
        // The still encoder supplies a global palette and an LZW image block.
        // Move the palette into the image descriptor so each frame has its own.
        int tableFlags = still[10];
        int tableBytes = 3 << ((tableFlags & 7) + 1);
        int image = 13 + tableBytes;
        bool transparent = still[image] == 0x21 && still[image + 1] == 0xF9 && (still[image + 3] & 1) != 0;
        byte transparentIndex = transparent ? still[image + 6] : (byte)0;
        if (still[image] == 0x21 && still[image + 1] == 0xF9) image += 8;
        if ((tableFlags & 0x80) == 0 || still[image] != 0x2C || (still[image + 9] & 0x80) != 0 || still[^1] != 0x3B)
            throw new InvalidDataException("Unexpected frame layout from the GIF codec.");

        try
        {
            if (frameCount == 0) WriteHeader();
            int centiseconds = (int)((delay.Ticks + TicksPerDelay / 2) / TicksPerDelay);
            output.Write([0x21, 0xF9, 4, (byte)(((int)disposal << 2) | (transparent ? 1 : 0)),
                (byte)centiseconds, (byte)(centiseconds >> 8), transparentIndex, 0]);
            still[image + 9] = (byte)(still[image + 9] | 0x80 | (tableFlags & 7));
            output.Write(still.AsSpan(image, 10));
            output.Write(still.AsSpan(13, tableBytes));
            output.Write(still.AsSpan(image + 10, still.Length - image - 11));
            frameCount++;
        }
        catch { faulted = true; throw; }
    }

    private const int TicksPerDelay = 10 * (int)TimeSpan.TicksPerMillisecond;

    private void WriteHeader()
    {
        Span<byte> header = stackalloc byte[13];
        "GIF89a"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..], (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(header[8..], (ushort)height);
        // No global palette or opaque background color.
        header[10] = 0x70;
        header[11] = header[12] = 0;
        output.Write(header);
        if (loopCount != 1)
        {
            int repeats = loopCount == 0 ? 0 : loopCount - 1;
            output.Write([0x21, 0xFF, 11, .. "NETSCAPE2.0"u8, 3, 1, (byte)repeats, (byte)(repeats >> 8), 0]);
        }
    }

    /// <summary>Finishes the GIF. At least one frame must have been written.</summary>
    public void Complete()
    {
        CheckState();
        if (complete) return;
        if (frameCount == 0) throw new InvalidOperationException("The GIF must have at least one frame.");
        try { output.WriteByte(0x3B); complete = true; }
        catch { faulted = true; throw; }
    }

    private void CheckState()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted) throw new InvalidOperationException("Writing to the GIF destination failed.");
    }

    /// <summary>Finishes a nonempty GIF if needed, leaving the destination open.</summary>
    public void Dispose()
    {
        if (disposed) return;
        try { if (frameCount != 0 && !faulted) Complete(); }
        finally { disposed = true; }
    }
}
