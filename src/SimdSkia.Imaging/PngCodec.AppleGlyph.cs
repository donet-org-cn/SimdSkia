using System.Buffers;
using System.Buffers.Binary;

namespace SimdSkia.Imaging;

public static partial class PngCodec
{
    // A subset of sbix PNGs use Apple's CgBI chunk, raw DEFLATE and BGR(A).
    // Keep this variant restricted to font bitmaps with validated dimensions.
    internal static PixelBuffer DecodeAppleGlyph(ReadOnlySpan<byte> png, int width, int height)
    {
        if (png.Length < 45 || !png[..8].SequenceEqual(Signature) || !png[12..16].SequenceEqual("CgBI"u8) ||
            width is < 1 or > 1024 || height is < 1 or > 1024)
            throw new InvalidDataException("Invalid CgBI glyph image.");

        byte[] input = ArrayPool<byte>.Shared.Rent(png.Length);
        try
        {
            int position = 8, count = 0, channels = 0;
            bool hasData = false, dataEnded = false, ended = false;
            while (position <= png.Length - 12)
            {
                int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png[position..]));
                if (length > png.Length - position - 12) throw new InvalidDataException("Truncated CgBI chunk.");
                var chunk = png.Slice(position + 4, length + 4);
                var name = chunk[..4];
                var data = chunk[4..];
                if (Crc(chunk) != BinaryPrimitives.ReadUInt32BigEndian(png[(position + length + 8)..]))
                    throw new InvalidDataException("CgBI chunk checksum mismatch.");
                if (hasData && !name.SequenceEqual("IDAT"u8)) dataEnded = true;

                if (name.SequenceEqual("IHDR"u8))
                {
                    if (channels != 0 || length != 13 || BinaryPrimitives.ReadInt32BigEndian(data) != width ||
                        BinaryPrimitives.ReadInt32BigEndian(data[4..]) != height ||
                        data[8] != 8 || data[9] is not (2 or 6) || data[10] != 0 || data[11] != 0 || data[12] != 0)
                        throw new NotSupportedException("Unsupported CgBI glyph pixel layout.");
                    channels = data[9] == 6 ? 4 : 3;
                }
                else if (name.SequenceEqual("IDAT"u8))
                {
                    if (channels == 0 || dataEnded) throw new InvalidDataException("Invalid CgBI image data order.");
                    data.CopyTo(input.AsSpan(count));
                    count += length;
                    hasData = true;
                }
                else if (name.SequenceEqual("IEND"u8))
                {
                    ended = length == 0 && count != 0 && position + 12 == png.Length;
                    break;
                }
                else if (!(position == 8 && name.SequenceEqual("CgBI"u8)) && (name[0] & 32) == 0)
                    throw new NotSupportedException("Unknown critical CgBI chunk.");
                position += length + 12;
            }
            if (!ended) throw new InvalidDataException("Incomplete CgBI image.");

            int stride = width * channels + 1;
            byte[] filtered = ArrayPool<byte>.Shared.Rent(stride * height);
            try
            {
                var rows = filtered.AsSpan(0, stride * height);
                ManagedZlib.DecodeRaw(input.AsSpan(0, count), rows);
                return ReadAppleRows(rows, width, height, channels, stride);
            }
            finally { ArrayPool<byte>.Shared.Return(filtered); }
        }
        finally { ArrayPool<byte>.Shared.Return(input); }
    }

    private static PixelBuffer ReadAppleRows(Span<byte> rows, int width, int height, int channels, int stride)
    {
        var output = PixelBuffer.RentForOverwrite(width, height, PixelFormat.Bgra8888);
        try
        {
            Span<byte> previous = [];
            for (int y = 0; y < height; y++)
            {
                int filter = rows[y * stride];
                if (filter > 4) throw new InvalidDataException("Invalid CgBI row filter.");
                var row = rows.Slice(y * stride + 1, stride - 1);
                PngValidation.Unfilter(row, previous, channels, filter);
                var pixels = output.GetRowSpan(y);
                if (channels == 4) row.CopyTo(pixels);
                else for (int x = 0; x < width; x++)
                {
                    row.Slice(x * 3, 3).CopyTo(pixels[(x * 4)..]);
                    pixels[x * 4 + 3] = 255;
                }
                previous = row;
            }
            return output;
        }
        catch { output.Dispose(); throw; }
    }
}
