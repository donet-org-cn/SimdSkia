namespace SimdSkia.Text;

internal sealed partial class BitmapGlyphs
{
    internal static BitmapGlyphs ReadCbdt(FontSource source, int dataOffset, int dataLength,
        int locationsOffset, int locationsLength, int glyphCount)
    {
        var locations = new Table(source, locationsOffset, locationsLength);
        var data = new Table(source, dataOffset, dataLength);
        var header = locations.At(0, 8);
        if (U32(header, 0) != 0x30000 || U32(data.At(0, 4), 0) != 0x30000)
            throw new NotSupportedException("Unsupported CBDT/CBLC version.");
        uint count = U32(header, 4);
        const int sizeRecord = 48;
        if (count > (locationsLength - 8) / sizeRecord) throw new InvalidDataException("Invalid CBLC strike count.");
        int headerLength = 8 + (int)count * sizeRecord;
        var strikes = new List<Strike>();
        for (int i = 0; i < count; i++)
        {
            var strike = locations.At(8 + i * sizeRecord, sizeRecord);
            int offset = checked((int)U32(strike, 0));
            int length = checked((int)U32(strike, 4));
            int indexes = checked((int)U32(strike, 8));
            if (offset < headerLength || indexes > length / 8)
                throw new InvalidDataException("Invalid CBLC index table.");
            if (strike[44] == 0 || strike[45] == 0 || U16(strike, 40) > U16(strike, 42) || U16(strike, 42) >= glyphCount)
                throw new InvalidDataException("Invalid CBLC strike dimensions or glyph range.");
            if ((strike[47] & 1) == 0) continue;
            strikes.Add(new CbdtStrike(data, locations.Slice(offset, length), indexes, strike[44], strike[45], strike[46]));
        }
        return new(source, strikes.ToArray());
    }

    private readonly record struct Metrics(int Width, int Height, int X, int Y)
    {
        internal static Metrics Read(ReadOnlySpan<byte> bytes) => new(bytes[1], bytes[0], (sbyte)bytes[2], (sbyte)bytes[3]);
    }

    private sealed class CbdtStrike : Strike
    {
        private readonly record struct Index(int First, int Last, int Offset);
        private readonly Table data, table;
        private readonly Index[] indexes;
        private readonly byte bitDepth;

        internal CbdtStrike(Table data, Table table, int count, int ppemX, int ppemY, byte bitDepth) : base(ppemX, ppemY)
        {
            this.data = data;
            this.table = table;
            this.bitDepth = bitDepth;
            indexes = new Index[count];
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                var record = table.At(i * 8, 8);
                int first = U16(record, 0), last = U16(record, 2), offset = checked((int)U32(record, 4));
                if (first <= previous || last < first || offset < count * 8 || offset > table.Length - 8)
                    throw new InvalidDataException("Invalid CBLC glyph index range.");
                indexes[i] = new(first, last, offset);
                previous = last;
            }
        }

        internal override GlyphBitmap? Get(int glyph)
        {
            int lower = 0, upper = indexes.Length - 1;
            while (lower <= upper)
            {
                int mid = lower + (upper - lower) / 2;
                var index = indexes[mid];
                if (glyph < index.First) upper = mid - 1;
                else if (glyph > index.Last) lower = mid + 1;
                else return GetBitmap(index, glyph);
            }
            return null;
        }

        private GlyphBitmap? GetBitmap(Index index, int glyph)
        {
            var header = table.At(index.Offset, 8);
            int indexFormat = U16(header, 0), imageFormat = U16(header, 2);
            int dataStart = checked((int)U32(header, 4));
            if (dataStart < 4) throw new InvalidDataException("Invalid CBDT image base.");
            int offsetArray = index.Offset + 8;
            int slot = glyph - index.First;
            int start, end;
            Metrics? shared = null;
            switch (indexFormat)
            {
                case 1:
                    var wide = table.At(checked(offsetArray + slot * 4), 8);
                    start = checked((int)U32(wide, 0));
                    end = checked((int)U32(wide, 4));
                    break;
                case 3:
                    var narrow = table.At(checked(offsetArray + slot * 2), 4);
                    start = U16(narrow, 0);
                    end = U16(narrow, 2);
                    break;
                case 4:
                    int sparseCount = table.RelativeOffset(offsetArray);
                    var pairs = table.At(offsetArray + 4, checked((sparseCount + 1) * 4));
                    int pair = FindGlyph(pairs, sparseCount, 4, glyph);
                    if (pair < 0) return null;
                    start = U16(pairs, pair * 4 + 2);
                    end = U16(pairs, pair * 4 + 6);
                    break;
                case 2:
                case 5:
                    int imageSize = table.RelativeOffset(offsetArray);
                    shared = Metrics.Read(table.At(offsetArray + 4, 8));
                    if (indexFormat == 5)
                    {
                        int glyphCount = table.RelativeOffset(offsetArray + 12);
                        var ids = table.At(offsetArray + 16, checked(glyphCount * 2));
                        slot = FindGlyph(ids, glyphCount, 2, glyph);
                        if (slot < 0) return null;
                    }
                    start = checked(slot * imageSize);
                    end = checked(start + imageSize);
                    break;
                default: return null;
            }
            int position = checked(dataStart + start), length = end - start;
            data.Check(position, length);
            if (length == 0) return null;
            var image = data.Slice(position, length);
            return ReadBitmap(image, imageFormat, shared);
        }

        private GlyphBitmap? ReadBitmap(Table image, int imageFormat, Metrics? shared)
        {
            int header = imageFormat switch { 1 or 2 or 17 => 5, 6 or 7 or 18 => 8, 5 or 19 => 0, _ => -1 };
            if (header < 0 || (header == 0 && shared is null)) return null;
            var metrics = header == 0 ? shared!.Value : Metrics.Read(image.At(0, header));
            if (imageFormat >= 17)
            {
                int length = image.RelativeOffset(header);
                return image.Png(header + 4, length, this, metrics.X, metrics.Y, metrics.Width, metrics.Height);
            }
            if (bitDepth != 32 || metrics.Width == 0 || metrics.Height == 0) return null;
            int imageLength = metrics.Width * metrics.Height * 4;
            image.Check(header, imageLength);
            return new(image.Offset + header, imageLength, PpemX, PpemY, metrics.X, metrics.Y,
                metrics.Width, metrics.Height, GlyphBitmapFormat.PremultipliedBgra, false, false);
        }

        private static int FindGlyph(ReadOnlySpan<byte> records, int count, int stride, int glyph)
        {
            int lower = 0, upper = count - 1;
            while (lower <= upper)
            {
                int mid = lower + (upper - lower) / 2;
                int id = U16(records, mid * stride);
                if (glyph < id) upper = mid - 1;
                else if (glyph > id) lower = mid + 1;
                else return mid;
            }
            return -1;
        }
    }
}
