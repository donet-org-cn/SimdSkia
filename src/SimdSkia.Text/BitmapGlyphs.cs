using System.Buffers.Binary;

namespace SimdSkia.Text;

internal enum GlyphBitmapFormat { Png, ApplePng, PremultipliedBgra }

internal readonly record struct GlyphBitmap(int Offset, int Length, int PpemX, int PpemY,
    int X, int Top, int Width, int Height, GlyphBitmapFormat Format, bool UseOutlineOrigin, bool OverlayOutline)
{
    internal RectF Bounds(float size, RectF outlineOrigin)
    {
        float left = X * (size / PpemX), top = -Top * (size / PpemY);
        if (UseOutlineOrigin) { left += outlineOrigin.Left; top -= outlineOrigin.Top; }
        return new(left, top, left + Width * (size / PpemX), top + Height * (size / PpemY));
    }
}

/// <summary>Indexes color strikes without loading the encoded image payloads.</summary>
internal sealed partial class BitmapGlyphs
{
    private const int MaxImageSide = 1024;
    private const int MaxImageBytes = 8 * 1024 * 1024;
    private readonly Strike[] strikes;
    private readonly FontSource source;

    private abstract class Strike(int ppemX, int ppemY)
    {
        internal int PpemX { get; } = ppemX;
        internal int PpemY { get; } = ppemY;
        internal abstract GlyphBitmap? Get(int glyph);
    }

    private BitmapGlyphs(FontSource source, Strike[] strikes)
    {
        this.source = source;
        this.strikes = strikes;
    }

    internal GlyphBitmap? Find(ushort glyph, float ppem)
    {
        GlyphBitmap? best = null;
        float distance = float.PositiveInfinity;
        foreach (var strike in strikes)
        {
            float difference = MathF.Abs(strike.PpemY - ppem);
            if (best is { } current && (difference > distance ||
                difference == distance && strike.PpemY <= current.PpemY)) continue;
            if (strike.Get(glyph) is not { } candidate) continue;
            best = candidate;
            distance = difference;
            if (distance == 0) break;
        }
        return best;
    }

    internal ReadOnlyMemory<byte> Read(in GlyphBitmap bitmap) => source.Read(bitmap.Offset, bitmap.Length);

    internal BitmapGlyphs Append(BitmapGlyphs other) => new(source, [..strikes, ..other.strikes]);

    private readonly record struct Table(FontSource Source, int Offset, int Length)
    {
        internal void Check(int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > Length - count)
                throw new InvalidDataException("Bitmap glyph data exceeds its font table.");
        }

        internal ReadOnlySpan<byte> At(int offset, int count)
        {
            Check(offset, count);
            return Source.Read(Offset + offset, count).Span;
        }

        internal Table Slice(int offset, int count)
        {
            Check(offset, count);
            return new(Source, Offset + offset, count);
        }

        internal int RelativeOffset(int offset) => checked((int)U32(At(offset, 4), 0));

        internal GlyphBitmap? Png(int offset, int length, Strike strike, int x, int y,
            int expectedWidth, int expectedHeight, bool sbix = false, bool overlay = false)
        {
            if (length is < 33 or > MaxImageBytes) return null;
            Check(offset, length);
            var prefix = At(offset, Math.Min(length, 64));
            ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
            if (!prefix[..8].SequenceEqual(signature)) return null;
            int ihdr = 8;
            var format = GlyphBitmapFormat.Png;
            if (prefix[12..16].SequenceEqual("CgBI"u8))
            {
                uint size = U32(prefix, 8);
                if (!sbix || prefix.Length < 45 || size > prefix.Length - 45) return null;
                ihdr += 12 + (int)size;
                format = GlyphBitmapFormat.ApplePng;
            }
            if (U32(prefix, ihdr) != 13 || !prefix.Slice(ihdr + 4, 4).SequenceEqual("IHDR"u8)) return null;
            uint width = U32(prefix, ihdr + 8), height = U32(prefix, ihdr + 12);
            if (width is 0 or > MaxImageSide || height is 0 or > MaxImageSide) return null;
            if (!sbix && (width != expectedWidth || height != expectedHeight))
                throw new InvalidDataException("PNG glyph dimensions do not match CBDT metrics.");
            return new(Offset + offset, length, strike.PpemX, strike.PpemY, x, sbix ? y + (int)height : y,
                (int)width, (int)height, format, sbix, overlay);
        }
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
    private static short S16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt16BigEndian(bytes[offset..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);
}
