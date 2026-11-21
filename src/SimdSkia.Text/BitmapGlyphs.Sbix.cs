namespace SimdSkia.Text;

internal sealed partial class BitmapGlyphs
{
    internal static BitmapGlyphs ReadSbix(FontSource source, int offset, int length, int glyphCount)
    {
        var table = new Table(source, offset, length);
        var header = table.At(0, 8);
        if (U16(header, 0) != 1) throw new NotSupportedException("Unsupported sbix version.");
        bool overlay = (U16(header, 2) & 2) != 0;
        uint count = U32(header, 4);
        if (count > (length - 8) / 4) throw new InvalidDataException("Invalid sbix strike count.");
        var strikes = new Strike[count];
        int start = count == 0 ? length : table.RelativeOffset(8);
        if (start < 8 + (int)count * 4) throw new InvalidDataException("Invalid sbix strike offset.");
        for (int i = 0; i < strikes.Length; i++)
        {
            int end = i + 1 == strikes.Length ? length : table.RelativeOffset(12 + i * 4);
            var strike = table.Slice(start, end - start);
            strike.Check(0, 4 + (glyphCount + 1) * 4);
            int ppem = U16(strike.At(0, 4), 0);
            if (ppem == 0) throw new InvalidDataException("Invalid sbix strike size.");
            strikes[i] = new SbixStrike(strike, ppem, glyphCount, overlay);
            start = end;
        }
        return new(source, strikes);
    }

    private sealed class SbixStrike(Table table, int ppem, int glyphCount, bool overlay) : Strike(ppem, ppem)
    {
        internal override GlyphBitmap? Get(int glyph)
        {
            if ((uint)glyph >= (uint)glyphCount) return null;
            const int png = 0x706e6720, duplicate = 0x64757065;
            int x = 0, y = 0;
            for (int depth = 0; depth < 32; depth++)
            {
                var offsets = table.At(4 + glyph * 4, 8);
                int start = checked((int)U32(offsets, 0)), end = checked((int)U32(offsets, 4));
                if (start < 4 + (glyphCount + 1) * 4) throw new InvalidDataException("Invalid sbix glyph offset.");
                int length = end - start;
                table.Check(start, length);
                if (length == 0) return null;
                if (length < 8) throw new InvalidDataException("Truncated sbix glyph.");
                var record = table.At(start, 8);
                if (depth == 0) { x = S16(record, 0); y = S16(record, 2); }
                switch (U32(record, 4))
                {
                    case png: return table.Png(start + 8, length - 8, this, x, y, 0, 0, sbix: true, overlay);
                    case duplicate:
                        if (length != 10) throw new InvalidDataException("Invalid sbix duplicate glyph.");
                        glyph = U16(table.At(start + 8, 2), 0);
                        if (glyph >= glyphCount) throw new InvalidDataException("Invalid sbix duplicate glyph ID.");
                        break;
                    default: return null;
                }
            }
            throw new InvalidDataException("Cyclic or deeply nested sbix duplicate glyph.");
        }
    }
}
