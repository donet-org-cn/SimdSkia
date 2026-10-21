using System.Buffers.Binary;
using System.Text;

namespace SimdSkia.Avalonia;

/// <summary>Installed static TrueType faces selected without a native font engine.</summary>
public sealed record SimdSkiaSystemFontInfo(string DefaultFamily, IReadOnlyList<string> Files, IReadOnlyList<string> Exclusions);

public static class SimdSkiaSystemFonts
{
    public static SimdSkiaSystemFontInfo Discover()
    {
        if (OperatingSystem.IsWindows())
        {
            var found = WindowsSystemFonts.Discover();
            return new(found.DefaultFamily, found.Files, Array.Empty<string>());
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots = OperatingSystem.IsLinux()
            ? [Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share"), "fonts"), Path.Combine(home, ".fonts"), "/usr/local/share/fonts", "/usr/share/fonts"]
            : OperatingSystem.IsMacOS()
                ? [Path.Combine(home, "Library", "Fonts"), "/Library/Fonts", "/System/Library/Fonts", "/System/Library/Fonts/Supplemental"]
                : throw new PlatformNotSupportedException("System font discovery supports Windows, Linux and macOS.");
        return UnixSystemFonts.Discover(roots, Environment.GetEnvironmentVariable("SIMDSKIA_DEFAULT_FONT_FAMILY"));
    }
}

internal static class UnixSystemFonts
{
    private sealed record Face(string Path, string Family, bool Bold, bool Cjk);
    private static readonly string[] Preferred = ["Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Noto Sans", "Ubuntu", "FreeSans"];

    internal static SimdSkiaSystemFontInfo Discover(IEnumerable<string> directories, string? preferred = null)
    {
        var exclusions = new List<string>();
        var faces = new List<Face>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (string root in directories.Distinct(StringComparer.Ordinal))
        {
            if (!Directory.Exists(root)) continue;
            foreach (string path in Directory.EnumerateFiles(root, "*", options).Order(StringComparer.Ordinal))
            {
                string ext = Path.GetExtension(path);
                if (!ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".ttc", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".otf", StringComparison.OrdinalIgnoreCase)) continue;
                string fullPath = Path.GetFullPath(path);
                if (!seen.Add(fullPath)) continue;
                if (seen.Count > 8192) throw new NotSupportedException("Installed font discovery exceeds 8192 files; supply explicit FontFiles.");
                try { ReadFaces(fullPath, faces, exclusions); }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or OverflowException or ArgumentOutOfRangeException)
                { exclusions.Add($"{fullPath}: {error.Message}"); }
            }
        }
        if (faces.Count == 0) throw new NotSupportedException("No supported static TrueType glyf fonts were found. Install a static TTF/TTC font or configure SimdSkiaOptions.FontFiles. CFF, variable and color fonts are not supported.");
        string family;
        if (!string.IsNullOrWhiteSpace(preferred))
            family = faces.FirstOrDefault(f => f.Family.Equals(preferred, StringComparison.OrdinalIgnoreCase))?.Family
                ?? throw new NotSupportedException($"Requested default font '{preferred}' is not an installed supported static TrueType face.");
        else family = Preferred.Select(p => faces.FirstOrDefault(f => f.Family.Equals(p, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(f => f is not null)?.Family ?? faces[0].Family;
        // Load only selected UI, CJK and monospaced families, not every installed outline.
        string? cjk = faces.Where(f => f.Cjk).OrderBy(f => f.Family.Contains("Sans", StringComparison.OrdinalIgnoreCase) || f.Family.Contains("Hei", StringComparison.OrdinalIgnoreCase) ? 0 : 1).Select(f => f.Family).FirstOrDefault();
        string? mono = faces.FirstOrDefault(f => f.Family.Contains("Mono", StringComparison.OrdinalIgnoreCase) || f.Family == "Menlo")?.Family;
        var files = faces.Where(f => f.Family == family || f.Family == cjk || f.Family == mono)
            .OrderBy(f => f.Family == family ? 0 : f.Family == cjk ? 1 : 2).ThenBy(f => f.Bold).Select(f => f.Path).Distinct(StringComparer.Ordinal).ToArray();
        return new(family, files, exclusions.ToArray());
    }

    private static uint Read32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
    private static ushort Read16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
    private static byte[] Read(FileStream file, long offset, int length)
    {
        if (offset < 0 || length < 0 || offset > file.Length - length) throw new InvalidDataException("Font table outside file.");
        var bytes = new byte[length]; file.Position = offset; file.ReadExactly(bytes); return bytes;
    }
    private static void ReadFaces(string path, List<Face> output, List<string> exclusions)
    {
        using var file = File.OpenRead(path);
        var header = Read(file, 0, 12);
        uint[] offsets;
        if (Read32(header, 0) == 0x74746366)
        {
            if(Read32(header,4) is not (0x00010000 or 0x00020000))throw new NotSupportedException("Unsupported TTC version.");
            uint count = Read32(header, 8);
            if (count is 0 or >4096) throw new InvalidDataException("Invalid TTC face count.");
            var directory = Read(file, 12, checked((int)count * 4));
            offsets = Enumerable.Range(0, (int)count).Select(i => Read32(directory, i * 4)).ToArray();
        }
        else offsets = [0];
        foreach (uint offset in offsets)
        {
            var h = Read(file, offset, 12);
            uint magic = Read32(h, 0);
            if (magic is not (0x00010000 or 0x74727565)) { exclusions.Add($"{path}: face at {offset} uses non-TrueType outlines."); continue; }
            int count = Read16(h, 4);
            var directory = Read(file, offset + 12, checked(count * 16));
            var tables = new Dictionary<string, (uint Offset, int Length)>();
            for (int i = 0; i < count; i++)
            {
                var entry = directory.AsSpan(i * 16, 16);
                if (!tables.TryAdd(Encoding.ASCII.GetString(entry[..4]), (Read32(entry, 8), checked((int)Read32(entry, 12))))) throw new InvalidDataException("Duplicate font table.");
            }
            if (!tables.ContainsKey("glyf") || new[] { "fvar", "gvar", "COLR", "CPAL", "CBDT", "CBLC", "sbix", "SVG " }.Any(tables.ContainsKey))
            { exclusions.Add($"{path}: face at {offset} requires unsupported variable/color/CFF rendering."); continue; }
            byte[] Table(string name, int limit)
            {
                if (!tables.TryGetValue(name, out var entry) || entry.Length > limit) throw new InvalidDataException($"Missing/oversized font metadata table {name}.");
                return Read(file, entry.Offset, entry.Length);
            }
            var names = Table("name", 1024 * 1024);
            string? family = Name(names, 16) ?? Name(names, 1);
            if (string.IsNullOrWhiteSpace(family)) throw new InvalidDataException("Font has no Unicode family name.");
            var os2 = tables.ContainsKey("OS/2")?Table("OS/2", 4096):[];
            bool cjk = HasGlyph(Table("cmap", 4 * 1024 * 1024), 0x4e2d);
            output.Add(new(path, family, os2.Length >= 6 && Read16(os2, 4) >= 600, cjk));
        }
    }
    private static string? Name(byte[] names, int id)
    {
        if (names.Length < 6) throw new InvalidDataException("Truncated font names.");
        int count = Read16(names, 2), storage = Read16(names, 4), best = -1; string? result = null;
        if (count > (names.Length - 6) / 12) throw new InvalidDataException("Truncated font name directory.");
        for (int i = 0; i < count; i++)
        {
            var e = names.AsSpan(6 + i * 12, 12); int platform = Read16(e, 0), language = Read16(e, 4);
            if (Read16(e, 6) != id || platform is not (0 or 3)) continue;
            int length = Read16(e, 8), start = checked(storage + Read16(e, 10));
            if (start > names.Length - length || (length & 1) != 0) throw new InvalidDataException("Invalid font name string.");
            int score = (language == 0x409 ? 2 : 0) + (platform == 3 ? 1 : 0);
            if (score <= best) continue;
            result = Encoding.BigEndianUnicode.GetString(names, start, length); best = score;
        }
        return result;
    }
    private static bool HasGlyph(byte[] cmap, int codepoint)
    {
        if (cmap.Length < 4) throw new InvalidDataException("Truncated cmap.");
        int count = Read16(cmap, 2);
        if (count > (cmap.Length - 4) / 8) throw new InvalidDataException("Truncated cmap records.");
        for (int i = 0; i < count; i++)
        {
            int platform=Read16(cmap,4+i*8),encoding=Read16(cmap,6+i*8);
            if(platform!=0&&(platform!=3||encoding is not (1 or 10)))continue;
            int offset = checked((int)Read32(cmap, 8 + i * 8));
            if (offset < 0 || offset > cmap.Length - 4) throw new InvalidDataException("Invalid cmap offset.");
            int format = Read16(cmap, offset);
            if (format == 12)
            {
                if (offset > cmap.Length - 16) throw new InvalidDataException("Truncated cmap12.");
                int groups = checked((int)Read32(cmap, offset + 12));
                if (groups > (cmap.Length - offset - 16) / 12) throw new InvalidDataException("Invalid cmap12 groups.");
                for (int g = 0; g < groups; g++) { int at = offset + 16 + g * 12; if (codepoint >= Read32(cmap, at) && codepoint <= Read32(cmap, at + 4)) return Read32(cmap, at + 8) + codepoint - Read32(cmap, at) != 0; }
            }
            else if (format == 4)
            {
                if (offset > cmap.Length - 8) throw new InvalidDataException("Truncated cmap4.");
                int segments = Read16(cmap, offset + 6) / 2;
                if (segments <= 0 || offset + 16L + segments * 8 > cmap.Length) throw new InvalidDataException("Invalid cmap4 segments.");
                for (int s = 0; s < segments; s++)
                {
                    int end = Read16(cmap, offset + 14 + s * 2), start = Read16(cmap, offset + 16 + segments * 2 + s * 2);
                    if (codepoint < start || codepoint > end) continue;
                    int delta = Read16(cmap, offset + 16 + segments * 4 + s * 2), rangeAt = offset + 16 + segments * 6 + s * 2, range = Read16(cmap, rangeAt);
                    if (range == 0) return ((codepoint + delta) & 65535) != 0;
                    int at = checked(rangeAt + range + (codepoint - start) * 2);
                    if (at > cmap.Length - 2) throw new InvalidDataException("Invalid cmap4 glyph offset.");
                    int glyph = Read16(cmap, at); return glyph != 0 && ((glyph + delta) & 65535) != 0;
                }
            }
        }
        return false;
    }
}
