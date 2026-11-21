using System.Buffers.Binary;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using SimdSkia;
using SimdSkia.Avalonia;
using SimdSkia.Imaging;
using SimdSkia.Text;
using DrawingContext = SimdSkia.Avalonia.DrawingContext;
using GlyphRun = SimdSkia.Avalonia.GlyphRun;
using FontManager = SimdSkia.Avalonia.FontManager;
using PlatformTypeface = SimdSkia.Avalonia.PlatformTypeface;
using TextShaper = SimdSkia.Avalonia.TextShaper;

internal static class BitmapFontValidation
{
    private const int Octopus = 0x1f419;
    private const string Family = "Fixture Emoji";

    internal static int Run(string temporary, string primary)
    {
        using var sample = new PixelBuffer(2, 2, PixelFormat.Rgba8888, AlphaFormat.Straight);
        new byte[] { 255, 0, 0, 128, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 }.CopyTo(sample.GetPixelSpan());
        byte[] png = PngCodec.Encode(sample);
        byte[] apple = ApplePng();
        byte[] sbix = Sbix(png, apple);
        string sbixPath = Path.Combine(temporary, "fixture-sbix.ttf");
        File.WriteAllBytes(sbixPath, Font([("sbix", sbix),("GSUB", Ligatures())]));
        string cbdtPath = Path.Combine(temporary, "fixture-cbdt.ttf");
        File.WriteAllBytes(cbdtPath, Font(Cbdt(png)));

        var portable = UnixSystemFonts.DiscoverWithFallbacks([temporary], new FontData(File.ReadAllBytes(primary)).Family);
        Require(portable.Fallbacks.Contains(sbixPath) && portable.Fallbacks.Contains(cbdtPath), "Bitmap-only fonts must be discoverable.");
        foreach (var path in new[] { sbixPath, cbdtPath })
        {
            var manager = new FontManager([primary], supportedFacesOnly: true, installedFallbackFiles: [path]);
            using var selected = FindFace(manager, manager.GetDefaultFontFamilyName());
            Require(selected.FamilyName == Family && manager.LoadedFaceCount == 2, "Emoji must resolve through lazy installed fallback.");
            var face = new GlyphTypeface(selected, FontSimulations.None);
            var font = selected.Data.Font;
            ushort glyph = font.GetGlyph(Octopus);
            Require(glyph == 2 && font.Outline(glyph).Segments.IsEmpty, "A bitmap-only glyph has no outline.");
            var strike = font.GetBitmapGlyph(glyph, 16);
            Require(strike is { Width: 2, Height: 2, PpemX: 16, Top: 1, X: 3 }, "Bitmap origin or strike size is incorrect.");
            using var target = Render(face, glyph, 96);
            Require(Pixel(target, 7, 7).SequenceEqual(new byte[] { 0, 0, 128, 128 }), "Bitmap must preserve premultiplied red.");
            Require(Pixel(target, 8, 7).SequenceEqual(new byte[] { 0, 255, 0, 255 }), "Bitmap must preserve the adjacent green pixel.");
            Require(Pixel(target, 6, 7)[3] == 0 && Pixel(target, 7, 6)[3] == 0, "Bitmap must honor its horizontal and vertical bearings.");
            if (path == sbixPath)
            {
                using var shaped=new TextShaper().ShapeText("🐙️‍🐙".AsMemory(),new(face,16));
                Require(shaped.Length==1&&shaped[0].GlyphIndex==2&&shaped[0].GlyphCluster==0,
                    "Emoji selectors, the joiner and ligature must preserve the source UTF-16 cluster.");
                Require(font.GetBitmapGlyph(glyph, 24) is { PpemX: 32, Format: GlyphBitmapFormat.ApplePng }, "Choose the higher-resolution strike on a tie.");
                using var scaled = Render(face, glyph, 192);
                Require(Pixel(scaled, 11, 15).SequenceEqual(new byte[] { 64, 20,100,128 }), "CgBI must preserve premultiplied BGRA.");
                Require(Pixel(scaled, 11, 16).SequenceEqual(new byte[] { 66, 23,104,133 }), "CgBI row filters must reconstruct the prior scanline.");
            }
        }

        var malformed = (byte[])sbix.Clone();
        Write16(malformed, 16 + 20 + 8 + png.Length + 8, 2);
        var broken = new TrueTypeFont(Font([("sbix", malformed)]));
        try
        {
            _ = broken.GetBitmapGlyph(2, 16);
            throw new InvalidOperationException("A cyclic sbix alias was accepted.");
        }
        catch (InvalidDataException) { }
        return 19;
    }

    internal static void CheckInstalled(string path)
    {
        var primary = Path.GetFullPath("baseline/fonts/ZCOOLXiaoWei-Regular.ttf");
        var manager = new FontManager([primary], supportedFacesOnly: true, installedFallbackFiles: [Path.GetFullPath(path)]);
        using var selected = FindFace(manager, manager.GetDefaultFontFamilyName());
        var face = new GlyphTypeface(selected, FontSimulations.None);
        var font = selected.Data.Font;
        ushort glyph = font.GetGlyph(Octopus);
        var bitmap = font.GetBitmapGlyph(glyph, 16);
        Require(bitmap is not null, "The installed face must provide a supported octopus bitmap.");
        using var target = Render(face, glyph, 192);
        int color = 0;
        var data = target.GetPixelSpan();
        for (int i = 0; i < data.Length; i += 4)
            if (data[i + 3] > 0 && (data[i] != data[i + 1] || data[i] != data[i + 2])) color++;
        Require(color > 80, "The real installed octopus bitmap did not produce colored pixels.");
        Console.WriteLine($"PASS {selected.FamilyName}: glyph {glyph}, strike {bitmap!.Value.PpemY} ppem, {color} colored pixels.");
        if(selected.FamilyName=="Noto Color Emoji")
        {
            foreach(string text in new[]{"👩‍💻","🇺🇸","👍🏽","❤️‍🔥","1️⃣"})
            {
                using var shaped=new TextShaper().ShapeText(text.AsMemory(),new(face,16));
                Require(shaped.Length==1&&shaped[0].GlyphCluster==0&&font.GetBitmapGlyph(shaped[0].GlyphIndex,32) is not null,
                    $"Emoji sequence {text} failed to resolve to one drawable ligature.");
            }
            Console.WriteLine("PASS Noto emoji ZWJ, flag, skin tone, selector and keycap sequences.");
        }
    }

    private static PlatformTypeface FindFace(FontManager manager, string family)
    {
        if (!manager.TryMatchCharacter(Octopus, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, family, null, out var face))
            throw new InvalidOperationException("No bitmap emoji fallback was resolved.");
        return (PlatformTypeface)face;
    }

    private static PixelBuffer Render(GlyphTypeface face, ushort glyph, int dpi)
    {
        var target = new PixelBuffer(96, 96, PixelFormat.Bgra8888, AlphaFormat.Premultiplied);
        try
        {
            using var context = new DrawingContext(target, new Vector(dpi, dpi));
            using var run = new GlyphRun(face, 16, [new GlyphInfo(glyph, 0, 16)], new Point(4, 8));
            Require(run.Bounds.Width > 0 && run.Bounds.Height > 0, "A bitmap glyph must contribute to text ink bounds.");
            context.DrawGlyphRun(new SolidColorBrush(Colors.Black), run);
            return target;
        }
        catch { target.Dispose(); throw; }
    }

    private static Span<byte> Pixel(PixelBuffer target, int x, int y) => target.GetRowSpan(y).Slice(x * 4, 4);

    private static byte[] Font((string Tag, byte[] Data)[] bitmaps)
    {
        var head = new byte[54]; Write16(head, 18, 1000);
        var hhea = new byte[36]; Write16(hhea, 4, 800); Write16(hhea, 6, unchecked((ushort)-200)); Write16(hhea, 34, 1);
        var maxp = new byte[6]; Write32(maxp, 0, 0x5000); Write16(maxp, 4, 3);
        var hmtx = new byte[8]; Write16(hmtx, 0, 1000);
        var cmap = new byte[52]; Write16(cmap, 2, 1); Write16(cmap, 4, 3); Write16(cmap, 6, 10);
        Write32(cmap, 8, 12); Write16(cmap, 12, 12); Write32(cmap, 16, 40); Write32(cmap, 24, 2);
        Write32(cmap, 28, 0x200d); Write32(cmap, 32, 0x200d); Write32(cmap, 36, 1);
        Write32(cmap, 40, Octopus); Write32(cmap, 44, Octopus); Write32(cmap, 48, 2);
        byte[] family = Encoding.BigEndianUnicode.GetBytes(Family);
        var name = new byte[18 + family.Length]; Write16(name, 2, 1); Write16(name, 4, 18);
        Write16(name, 6, 3); Write16(name, 8, 1); Write16(name, 10, 0x409); Write16(name, 12, 1);
        Write16(name, 14, family.Length); family.CopyTo(name, 18);
        (string Tag, byte[] Data)[] tables = [("head", head), ("hhea", hhea), ("maxp", maxp),
            ("hmtx", hmtx), ("name", name), ("cmap", cmap), .. bitmaps];
        int offset = 12 + 16 * tables.Length;
        var result = new byte[offset + tables.Sum(table => (table.Data.Length + 3) & ~3)];
        Write32(result, 0, 0x10000); Write16(result, 4, tables.Length);
        for (int i = 0; i < tables.Length; i++)
        {
            var table = tables[i];
            int record = 12 + i * 16;
            Encoding.ASCII.GetBytes(table.Tag).CopyTo(result, record);
            Write32(result, record + 8, offset);
            Write32(result, record + 12, table.Data.Length);
            table.Data.CopyTo(result, offset);
            offset += (table.Data.Length + 3) & ~3;
        }
        return result;
    }

    private static byte[] Ligatures()
    {
        var data=new byte[82];
        Write16(data,0,1); Write16(data,4,10); Write16(data,6,30); Write16(data,8,44);
        Write16(data,10,1); "DFLT"u8.CopyTo(data.AsSpan(12)); Write16(data,16,8);
        Write16(data,18,4); Write16(data,24,65535); Write16(data,26,1);
        Write16(data,30,1); "ccmp"u8.CopyTo(data.AsSpan(32)); Write16(data,36,8); Write16(data,40,1);
        Write16(data,44,1); Write16(data,46,4); Write16(data,48,4); Write16(data,52,1); Write16(data,54,8);
        Write16(data,56,1); Write16(data,58,8); Write16(data,60,1); Write16(data,62,14);
        Write16(data,64,1); Write16(data,66,1); Write16(data,68,2);
        Write16(data,70,1); Write16(data,72,4);
        Write16(data,74,2); Write16(data,76,3); Write16(data,78,1); Write16(data,80,2);
        return data;
    }

    private static (string Tag, byte[] Data)[] Cbdt(byte[] png)
    {
        var data = new byte[13 + png.Length];
        Write32(data, 0, 0x30000);
        new byte[] { 2, 2, 3, 1, 16 }.CopyTo(data, 4);
        Write32(data, 9, png.Length); png.CopyTo(data, 13);
        var locations = new byte[80];
        Write32(locations, 0, 0x30000); Write32(locations, 4, 1);
        Write32(locations, 8, 56); Write32(locations, 12, 24); Write32(locations, 16, 1);
        Write16(locations, 48, 2); Write16(locations, 50, 2);
        new byte[] { 16, 16, 32, 1 }.CopyTo(locations, 52);
        Write16(locations, 56, 2); Write16(locations, 58, 2); Write32(locations, 60, 8);
        Write16(locations, 64, 1); Write16(locations, 66, 17); Write32(locations, 68, 4);
        Write32(locations, 76, data.Length - 4);
        return [("CBDT", data), ("CBLC", locations)];
    }

    private static byte[] Sbix(byte[] png, byte[] apple)
    {
        byte[] small = Strike(16, png), large = Strike(32, apple);
        var result = new byte[16 + small.Length + large.Length];
        Write16(result, 0, 1); Write16(result, 2, 1); Write32(result, 4, 2);
        Write32(result, 8, 16); Write32(result, 12, 16 + small.Length);
        small.CopyTo(result, 16); large.CopyTo(result, 16 + small.Length);
        return result;

        static byte[] Strike(int ppem, byte[] image)
        {
            var strike = new byte[38 + image.Length];
            Write16(strike, 0, ppem); Write16(strike, 2, 96);
            Write32(strike, 4, 20); Write32(strike, 8, 20);
            Write32(strike, 12, 28 + image.Length); Write32(strike, 16, strike.Length);
            "png "u8.CopyTo(strike.AsSpan(24)); image.CopyTo(strike, 28);
            int alias = 28 + image.Length;
            Write16(strike, alias, 3); Write16(strike, alias + 2, 65535);
            "dupe"u8.CopyTo(strike.AsSpan(alias + 4)); Write16(strike, alias + 8, 1);
            return strike;
        }
    }

    private static byte[] ApplePng()
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Chunk("CgBI"u8, [0, 0, 0, 0]);
        Chunk("IHDR"u8, [0, 0, 0, 2, 0, 0, 0, 2, 8, 6, 0, 0, 0]);
        byte[] raw = ManagedZlib.EncodeStored([0, 64, 20, 100, 128, 0, 255, 0, 255,
            2, 2, 3, 4, 5, 0, 0, 0, 0]);
        Chunk("IDAT"u8, raw.AsSpan(2, 7));
        Chunk("IDAT"u8, raw.AsSpan(9, raw.Length - 13));
        Chunk("IEND"u8, []);
        return stream.ToArray();

        void Chunk(ReadOnlySpan<byte> name, ReadOnlySpan<byte> data)
        {
            Span<byte> length = stackalloc byte[4];
            Write32(length, 0, data.Length);
            stream.Write(length); stream.Write(name); stream.Write(data);
            uint crc = uint.MaxValue;
            foreach (byte value in name) crc = Update(crc, value);
            foreach (byte value in data) crc = Update(crc, value);
            BinaryPrimitives.WriteUInt32BigEndian(length, ~crc);
            stream.Write(length);
        }
        static uint Update(uint crc, byte value)
        {
            crc ^= value;
            for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
            return crc;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Write16(Span<byte> data, int offset, int value) => BinaryPrimitives.WriteUInt16BigEndian(data[offset..], (ushort)value);
    private static void Write32(Span<byte> data, int offset, int value) => BinaryPrimitives.WriteInt32BigEndian(data[offset..], value);
}
