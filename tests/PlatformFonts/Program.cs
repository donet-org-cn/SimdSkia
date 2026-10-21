using System.Buffers.Binary;
using SimdSkia.Avalonia;
using SimdSkia.Text;

string fixture = Path.GetFullPath(args.FirstOrDefault() ?? "baseline/fonts/ZCOOLXiaoWei-Regular.ttf");
byte[] bytes = File.ReadAllBytes(fixture);
var font = new TrueTypeFont(bytes);
if (font.GetGlyph('中') == 0) throw new InvalidOperationException("The licensed fixture must contain real Chinese glyphs.");
int checks = 0;
void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { checks++; return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
string temp = Path.Combine(Path.GetTempPath(), "simdskia-fonts-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    Throws<NotSupportedException>(() => UnixSystemFonts.Discover([temp]));
    File.WriteAllBytes(Path.Combine(temp, "empty.ttf"), []);
    Throws<NotSupportedException>(() => UnixSystemFonts.Discover([temp]));
    string nested = Path.Combine(temp, "nested"); Directory.CreateDirectory(nested);
    string valid = Path.Combine(nested, "fixture.TTF"); File.WriteAllBytes(valid, bytes);
    var found = UnixSystemFonts.Discover([temp, nested, temp]);
    Check(found.Files.Count == 1 && found.Files[0] == valid, "Duplicate directories must not load a file twice; extensions are case insensitive.");
    Check(found.DefaultFamily == new FontData(bytes).Family, "Default family must come from actual Unicode name records.");
    Check(found.Exclusions.Count == 1, "Malformed installed files must have an explicit exclusion reason.");
    var selected = UnixSystemFonts.Discover([temp], found.DefaultFamily);
    Check(selected.DefaultFamily == found.DefaultFamily, "An installed preferred family is selected.");
    Throws<NotSupportedException>(() => UnixSystemFonts.Discover([temp], "not-installed-font"));
    byte[] cff = (byte[])bytes.Clone(); BinaryPrimitives.WriteUInt32BigEndian(cff, 0x4f54544f); File.WriteAllBytes(Path.Combine(temp, "cff.otf"), cff);
    var withCff = UnixSystemFonts.Discover([temp]);
    Check(withCff.Files.Count == 1 && withCff.Exclusions.Count == 2, "Unsupported CFF is reported and not substituted as a supported file.");
    using var managerTypeface = CreateTypeface(found);
    Check(managerTypeface.FamilyName == found.DefaultFamily, "Discovered files must load into the real Avalonia managed font manager.");
    var again = UnixSystemFonts.Discover([temp]);
    Check(again.Files.SequenceEqual(found.Files) && again.DefaultFamily == found.DefaultFamily, "Discovery order is deterministic.");
    Console.WriteLine($"PASS {checks} portable font discovery checks; {found.DefaultFamily}");
}
finally { Directory.Delete(temp, true); }

static global::Avalonia.Media.IPlatformTypeface CreateTypeface(SimdSkiaSystemFontInfo info)
{
    var manager = new SimdSkia.Avalonia.FontManager(info.Files, info.DefaultFamily, supportedFacesOnly: true);
    if (!manager.TryCreateGlyphTypeface(info.DefaultFamily, global::Avalonia.Media.FontStyle.Normal, global::Avalonia.Media.FontWeight.Normal, global::Avalonia.Media.FontStretch.Normal, out var typeface)) throw new InvalidOperationException("Cannot load discovered face.");
    return typeface;
}
