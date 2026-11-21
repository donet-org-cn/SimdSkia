using System.Numerics;

namespace SimdSkia.Cpu;

/// <summary>Reuses Gaussian alpha coverage for the same layer at a different pixel origin.</summary>
internal sealed class DropShadowBlurCache
{
    internal const int MaximumPlaneBytes = 2 * 1024 * 1024;
    private const int Budget = 8 * 1024 * 1024;
    private const int MaxEntries = 4;
    private readonly object gate = new();
    private readonly List<Entry> entries = new(MaxEntries);
    private int retainedBytes;

    private readonly record struct Key(int Width, int Height, RectF Output, float SigmaX, float SigmaY);

    private sealed class Entry(int capacity)
    {
        internal Key Key;
        internal readonly byte[] Input = GC.AllocateUninitializedArray<byte>(capacity);
        internal readonly byte[] Output = GC.AllocateUninitializedArray<byte>(capacity);
    }

    // The returned coverage includes the input halo and is positioned at (left, top).
    // The old filter is used for empty or oversized regions.
    internal PixelBuffer? Blur(PixelBuffer source, float sigmaX, float sigmaY, RectF? region,
        out int left, out int top)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.ThrowIfDisposed();
        int radiusX = ManagedFilters.Kernel(sigmaX).Length / 2;
        int radiusY = ManagedFilters.Kernel(sigmaY).Length / 2;
        var bounds = region ?? new RectF(0, 0, source.Width, source.Height);
        if (!bounds.IsFinite) throw new ArgumentException("Filter bounds must be finite.", nameof(region));
        int outputLeft = (int)Math.Clamp(MathF.Floor(bounds.Left), 0, source.Width);
        int outputTop = (int)Math.Clamp(MathF.Floor(bounds.Top), 0, source.Height);
        int outputRight = (int)Math.Clamp(MathF.Ceiling(bounds.Right), 0, source.Width);
        int outputBottom = (int)Math.Clamp(MathF.Ceiling(bounds.Bottom), 0, source.Height);
        left = Math.Max(0, outputLeft - radiusX);
        top = Math.Max(0, outputTop - radiusY);
        int width = (int)Math.Min(source.Width, (long)outputRight + radiusX) - left;
        int height = (int)Math.Min(source.Height, (long)outputBottom + radiusY) - top;
        if (outputRight <= outputLeft || outputBottom <= outputTop || (long)width * height > MaximumPlaneBytes)
        {
            left = top = 0;
            return null;
        }

        var output = new RectF(outputLeft-left, outputTop-top, outputRight-left, outputBottom-top);
        var key = new Key(width, height, output, sigmaX, sigmaY);
        if (TryRead(key, source, left, top) is { } cached) return cached;

        // Read the cropped input directly from the layer. A third alpha
        // work buffer can displace large RGBA layers from the pixel pool.
        var blurred = ManagedFilters.BlurShadowAlpha(source, sigmaX, sigmaY, output, left, top, width, height);
        try
        {
            Remember(key, source, left, top, blurred.GetPixelSpan());
            return blurred;
        }
        catch { blurred.Dispose(); throw; }
    }

    private static void ExtractAlpha(PixelBuffer source, int left, int top, int width, int height, Span<byte> alpha)
    {
        if (source.Alpha == AlphaFormat.Opaque)
        {
            alpha.Fill(255);
            return;
        }
        for (int y = 0; y < height; y++)
        {
            var row = source.GetRowSpan(top+y);
            var target = alpha.Slice(y*width, width);
            if (source.Format == PixelFormat.Alpha8) row.Slice(left, width).CopyTo(target);
            else for (int x = 0; x < width; x++) target[x] = row[(left+x)*4+3];
        }
    }

    private PixelBuffer? TryRead(Key key, PixelBuffer source, int left, int top)
    {
        lock (gate)
        {
            for (int i = entries.Count-1; i >= 0; i--)
            {
                var entry = entries[i];
                if (!Matches(entry, key, source, left, top)) continue;
                var result = PixelBuffer.RentForOverwrite(key.Width, key.Height, PixelFormat.Alpha8);
                entry.Output.AsSpan(0, key.Width*key.Height).CopyTo(result.GetPixelSpan());
                if (i != entries.Count-1)
                {
                    entries.RemoveAt(i);
                    entries.Add(entry);
                }
                return result;
            }
            return null;
        }
    }

    private static bool Matches(Entry entry, Key key, PixelBuffer source, int left, int top)
    {
        if (entry.Key != key) return false;
        if (source.Alpha == AlphaFormat.Opaque)
            return entry.Input.AsSpan(0, key.Width*key.Height).IndexOfAnyExcept((byte)255) < 0;
        for (int y = 0; y < key.Height; y++)
        {
            var actual = source.GetRowSpan(top+y);
            var expected = entry.Input.AsSpan(y*key.Width, key.Width);
            if (source.Format == PixelFormat.Alpha8)
            {
                if (!actual.Slice(left, key.Width).SequenceEqual(expected)) return false;
            }
            else for (int x = 0; x < expected.Length; x++)
                if (actual[(left+x)*4+3] != expected[x]) return false;
        }
        return true;
    }

    private void Remember(Key key, PixelBuffer source, int left, int top, ReadOnlySpan<byte> output)
    {
        lock (gate)
        {
            int capacity = (int)BitOperations.RoundUpToPowerOf2((uint)output.Length);
            Entry? entry = null;
            while (entries.Count >= MaxEntries || retainedBytes+capacity*2 > Budget)
            {
                var oldest = entries[0];
                entries.RemoveAt(0);
                retainedBytes -= oldest.Input.Length*2;
                if (oldest.Input.Length == capacity) entry ??= oldest;
            }
            entry ??= new(capacity);
            ExtractAlpha(source, left, top, key.Width, key.Height, entry.Input.AsSpan(0, output.Length));
            entry.Key = key;
            output.CopyTo(entry.Output);
            entries.Add(entry);
            retainedBytes += capacity*2;
        }
    }
}
