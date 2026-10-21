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

var framebuffer=new FailureFramebuffer();var failing=new FailureCanvas();long completed=SimdSkiaDiagnostics.FramesPresented;
using(var target=new SimdSkia.Avalonia.FramebufferTarget(framebuffer,_=>failing))
{
    var context=target.CreateDrawingContext(new(new global::Avalonia.PixelSize(16,16),1,default),out _);
    context.Clear(global::Avalonia.Media.Colors.White);
    Check(failing.Commands==1,"Window commands must reach the selected backend.");
    Throws<InvalidOperationException>(()=>context.Dispose());
    Check(framebuffer.LockReleased,"A failing GPU flush must release the OS framebuffer lock.");
    Check(SimdSkiaDiagnostics.FramesPresented==completed,"A failed GPU frame must not increment completed presentation counts.");
    Check(target.PlatformRenderTargetState.IsCorrupted,"GPU resource failure must be reported to the compositor.");
}
Check(failing.Disposed,"GPU resources must be disposed after a failed frame.");
Console.WriteLine("PASS framebuffer failure cleanup and completion accounting");

static global::Avalonia.Media.IPlatformTypeface CreateTypeface(SimdSkiaSystemFontInfo info)
{
    var manager = new SimdSkia.Avalonia.FontManager(info.Files, info.DefaultFamily, supportedFacesOnly: true);
    if (!manager.TryCreateGlyphTypeface(info.DefaultFamily, global::Avalonia.Media.FontStyle.Normal, global::Avalonia.Media.FontWeight.Normal, global::Avalonia.Media.FontStretch.Normal, out var typeface)) throw new InvalidOperationException("Cannot load discovered face.");
    return typeface;
}

sealed class FailureCanvas:SimdSkia.ICanvasBackend
{
    public bool IsFaulted{get;private set;}
    internal bool Disposed;
    internal int Commands;
    public void Execute(in SimdSkia.DrawingCommand command)=>Commands++;
    public void Flush(){IsFaulted=true;throw new InvalidOperationException("Injected GPU submission failure");}
    public void Dispose()=>Disposed=true;
}
sealed class FailureFramebuffer:global::Avalonia.Platform.Surfaces.IFramebufferRenderTarget
{
    private readonly SimdSkia.PixelBuffer pixels=SimdSkia.PixelBuffer.Rent(16,16);
    internal bool LockReleased;
    public global::Avalonia.Platform.ILockedFramebuffer Lock(global::Avalonia.Platform.IRenderTarget.RenderTargetSceneInfo info,out global::Avalonia.Platform.Surfaces.FramebufferLockProperties properties)
    {properties=default;return new SimdSkia.Avalonia.Bitmap.Locked(pixels,new(96,96),()=>LockReleased=true);}
    public void Dispose()=>pixels.Dispose();
}
