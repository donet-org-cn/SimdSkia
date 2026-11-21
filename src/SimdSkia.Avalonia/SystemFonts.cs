using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SimdSkia.Text;

namespace SimdSkia.Avalonia;

/// <summary>Installed static TrueType faces selected without a native font engine.</summary>
public sealed record SimdSkiaSystemFontInfo(string DefaultFamily, IReadOnlyList<string> Files, IReadOnlyList<string> Exclusions);

public static class SimdSkiaSystemFonts
{
    public static SimdSkiaSystemFontInfo Discover()=>DiscoverForPlatform().Primary;

    internal static (SimdSkiaSystemFontInfo Primary,string[] Fallbacks) DiscoverForPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            var found = WindowsSystemFonts.Discover();
            return (new(found.DefaultFamily, found.Files, Array.Empty<string>()),found.Fallbacks);
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots = OperatingSystem.IsAndroid()
            ? ["/system/fonts", "/product/fonts", "/system/product/fonts"]
            : OperatingSystem.IsLinux()
            ? [Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share"), "fonts"), Path.Combine(home, ".fonts"), "/usr/local/share/fonts", "/usr/share/fonts"]
            : OperatingSystem.IsMacOS()
                ? [Path.Combine(home, "Library", "Fonts"), "/Library/Fonts", "/System/Library/Fonts", "/System/Library/Fonts/Supplemental"]
                : throw new PlatformNotSupportedException("System font discovery supports Windows, Linux, macOS and Android.");
        return UnixSystemFonts.DiscoverWithFallbacks(roots, Environment.GetEnvironmentVariable("SIMDSKIA_DEFAULT_FONT_FAMILY"));
    }
}

internal static class UnixSystemFonts
{
    private sealed record Face(string Path, string Family, bool Bold, bool Cjk);
    private static readonly string[] Preferred = ["Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Noto Sans", "Ubuntu", "FreeSans"];

    internal static SimdSkiaSystemFontInfo Discover(IEnumerable<string> directories, string? preferred = null, CultureInfo? culture = null)
        => DiscoverWithFallbacks(directories,preferred,culture).Primary;

    internal static (SimdSkiaSystemFontInfo Primary,string[] Fallbacks) DiscoverWithFallbacks(
        IEnumerable<string> directories,string? preferred=null,CultureInfo? culture=null)
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
        // Keep the common UI fonts ahead of the supplementary character index.
        var locale = CjkFontPreference.ForCulture(culture ?? CultureInfo.CurrentUICulture);
        string? cjk = faces.Where(f => f.Cjk)
            .OrderBy(f => CjkFontPreference.Rank(locale,CjkFontPreference.ForFamily(f.Family)))
            .ThenBy(f => f.Family.Contains("Sans", StringComparison.OrdinalIgnoreCase) || f.Family.Contains("Hei", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(f => f.Family).FirstOrDefault();
        string? mono = faces.FirstOrDefault(f => f.Family.Contains("Mono", StringComparison.OrdinalIgnoreCase) || f.Family == "Menlo")?.Family;
        var files = faces.Where(f => f.Family == family || f.Family == cjk || f.Family == mono)
            .OrderBy(f => f.Family == family ? 0 : f.Family == cjk ? 1 : 2).ThenBy(f => f.Bold).Select(f => f.Path).Distinct(StringComparer.Ordinal).ToArray();
        var fallbacks=faces.Select(f=>f.Path).Except(files,StringComparer.Ordinal).ToArray();
        return (new(family,files,exclusions.ToArray()),fallbacks);
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
            try
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
                bool colorBitmaps=tables.ContainsKey("sbix")||tables.ContainsKey("CBDT")&&tables.ContainsKey("CBLC");
                if ((!tables.ContainsKey("glyf")&&!colorBitmaps) || new[] { "fvar", "gvar", "SVG " }.Any(tables.ContainsKey)||
                    tables.ContainsKey("CBDT")!=tables.ContainsKey("CBLC")||
                    tables.TryGetValue("COLR",out var color)&&(!tables.ContainsKey("CPAL")||color.Length<ColorGlyphLayers.HeaderBytes||
                    !ColorGlyphLayers.HasLegacyRecords(Read(file,color.Offset,ColorGlyphLayers.HeaderBytes))))
                { exclusions.Add($"{path}: face at {offset} requires unsupported variable/color/CFF rendering."); continue; }
                byte[] Table(string name, int limit)
                {
                    if (!tables.TryGetValue(name, out var entry) || entry.Length > limit) throw new InvalidDataException($"Missing/oversized font metadata table {name}.");
                    return Read(file, entry.Offset, entry.Length);
                }
                var names = Table("name", 1024 * 1024);
                string? family = FontData.ReadFamilyName(names);
                if (string.IsNullOrWhiteSpace(family)) throw new InvalidDataException("Font has no supported family name.");
                var os2 = tables.ContainsKey("OS/2")?Table("OS/2", 4096):[];
                if(!tables.TryGetValue("cmap",out var cmap)||cmap.Length>4*1024*1024)
                    throw new InvalidDataException("Missing or oversized font character map.");
                bool cjk = HasGlyph(file,cmap,0x4e2d,Read16(Table("maxp",4096),4));
                output.Add(new(path, family, os2.Length >= 6 && Read16(os2, 4) >= 600, cjk));
            }
            catch (Exception error) when (error is IOException or InvalidDataException or NotSupportedException or OverflowException or ArgumentOutOfRangeException)
            {
                exclusions.Add($"{path}: face at {offset}: {error.Message}");
            }
        }
    }
    private static bool HasGlyph(FileStream file,(uint Offset,int Length) cmap,int codepoint,int glyphCount)
    {
        var selected=UnicodeCmap.Select(cmap.Length,(offset,length)=>Read(file,(long)cmap.Offset+offset,length));
        var map=Read(file,(long)cmap.Offset+selected.Offset,selected.Length);
        selected.Validate(map);
        return selected.GetGlyph(map,codepoint,glyphCount)!=0;
    }
}
