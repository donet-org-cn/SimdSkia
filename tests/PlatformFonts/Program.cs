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
    byte[] boldBytes=(byte[])bytes.Clone();
    int tableCount=BinaryPrimitives.ReadUInt16BigEndian(boldBytes.AsSpan(4));bool changed=false;
    for(int i=0;i<tableCount;i++)
    {
        var entry=boldBytes.AsSpan(12+i*16,16);
        if(!entry[..4].SequenceEqual("OS/2"u8))continue;
        int offset=checked((int)BinaryPrimitives.ReadUInt32BigEndian(entry[8..]));
        BinaryPrimitives.WriteUInt16BigEndian(boldBytes.AsSpan(offset+4),700);changed=true;break;
    }
    Check(changed,"Fixture must provide a real OS/2 weight table.");
    string boldFile=Path.Combine(temp,"bold.ttf");File.WriteAllBytes(boldFile,boldBytes);
    var lazy=new SimdSkia.Avalonia.FontManager([valid,boldFile],found.DefaultFamily,supportedFacesOnly:true);
    Check(lazy.LoadedFaceCount==0,"Installed font discovery must not read glyph data eagerly.");
    Check(lazy.GetInstalledFontFamilyNames().SequenceEqual([found.DefaultFamily]),"Lazy family enumeration uses actual metadata.");
    Check(lazy.TryGetFamilyTypefaces(found.DefaultFamily,out var faces)&&faces.Count==2,"All styles remain enumerable before loading.");
    Check(lazy.LoadedFaceCount==0,"Enumerating families and styles must not load unused faces.");
    using(var regular=Select(lazy,global::Avalonia.Media.FontWeight.Normal))
    {
        Check(regular.Weight==global::Avalonia.Media.FontWeight.Normal,"Regular selection remains correct.");
        Check(((PlatformTypeface)regular).Data.Font.GetGlyph('中')!=0,"On-demand loading preserves real Chinese outlines.");
    }
    Check(lazy.LoadedFaceCount==1,"Only the requested face is loaded.");
    using(var repeat=Select(lazy,global::Avalonia.Media.FontWeight.Normal))
        Check(lazy.LoadedFaceCount==1,"Repeated selection reuses the loaded face.");
    using(var bold=Select(lazy,global::Avalonia.Media.FontWeight.Bold))
        Check(bold.Weight==global::Avalonia.Media.FontWeight.Bold,"Lazy style selection loads the actual bold face.");
    Check(lazy.LoadedFaceCount==2,"Both requested styles are now loaded.");
    // Actual file source versus immutable input: cross-page glyph reads,
    // concurrent readers, escaped table memory and streams, and TTC exports.
    var fileSource=FontSource.OpenInstalled(valid,bytes.Length,File.GetLastWriteTimeUtc(valid));
    var pagedFont=TrueTypeFont.FromInstalledSource(fileSource,0);
    Parallel.For(0,256,i=>
    {
        ushort glyph=(ushort)(i*(pagedFont.GlyphCount-1)/255);
        if(pagedFont.Advance(glyph)!=font.Advance(glyph)||!pagedFont.Outline(glyph).Segments.SequenceEqual(font.Outline(glyph).Segments))
            throw new InvalidOperationException("Paged glyph outlines/metrics differ from the immutable font.");
    });
    Check(true,"Concurrent glyph reads across the font preserve outlines and advances.");
    Check(pagedFont.GetGlyph('中')==font.GetGlyph('中')&&pagedFont.GetGlyph('A')==font.GetGlyph('A'),"Paged cmap preserves CJK/Latin mappings.");
    using(var stream=pagedFont.OpenStream())
    {
        Check(stream.CanRead&&stream.CanSeek&&!stream.CanWrite,"File font stream capabilities remain read-only/seekable.");
        var actual=new byte[bytes.Length];stream.ReadExactly(actual);
        Check(actual.AsSpan().SequenceEqual(bytes),"OpenStream preserves every byte, including unparsed font tables.");
        stream.Seek(16382,SeekOrigin.Begin);Span<byte> crossing=stackalloc byte[8];stream.ReadExactly(crossing);
        Check(crossing.SequenceEqual(bytes.AsSpan(16382,8)),"Stream reads cross page boundaries correctly.");
        stream.Seek(2,SeekOrigin.End);Check(stream.ReadByte()==-1,"Seeking past EOF reads zero bytes.");
        Throws<IOException>(()=>stream.Seek(-1,SeekOrigin.Begin));
        Throws<ArgumentNullException>(()=>stream.Read(null!,0,0));
        stream.Dispose();Throws<ObjectDisposedException>(()=>stream.ReadByte());
    }
    Check(pagedFont.TryGetTable("glyf",out var exported)&&font.TryGetTable("glyf",out var original)&&exported.Span.SequenceEqual(original.Span),"Large explicit table exports are exact.");
    if(OperatingSystem.IsWindows())Throws<IOException>(()=>File.WriteAllBytes(valid,bytes));
    string corrupt=Path.Combine(temp,"invalid-offset.ttf");var badBytes=(byte[])bytes.Clone();
    BinaryPrimitives.WriteUInt32BigEndian(badBytes.AsSpan(20),uint.MaxValue);File.WriteAllBytes(corrupt,badBytes);
    Throws<OverflowException>(()=>TrueTypeFont.FromInstalledSource(FontSource.OpenInstalled(corrupt,badBytes.Length,File.GetLastWriteTimeUtc(corrupt)),0));
    string renamed=Path.Combine(temp,"renamed.ttf");File.Move(valid,renamed);
    Check(pagedFont.Outline(pagedFont.GetGlyph('中')).Segments.SequenceEqual(font.Outline(font.GetGlyph('中')).Segments),"An open source survives atomic font path replacement.");
    File.Move(renamed,valid);
    var eager=new SimdSkia.Avalonia.FontManager([valid,boldFile],found.DefaultFamily);
    Check(eager.LoadedFaceCount==2,"Explicit font files keep eager validation semantics.");
    string mutableFile=Path.Combine(temp,"mutable.ttf");File.WriteAllBytes(mutableFile,boldBytes);
    var changedFile=new SimdSkia.Avalonia.FontManager([mutableFile],found.DefaultFamily,supportedFacesOnly:true);
    File.WriteAllBytes(mutableFile,bytes);
    Throws<IOException>(()=>Select(changedFile,global::Avalonia.Media.FontWeight.Bold));
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

static global::Avalonia.Media.IPlatformTypeface Select(SimdSkia.Avalonia.FontManager manager,global::Avalonia.Media.FontWeight weight)
{
    if(!manager.TryCreateGlyphTypeface(manager.GetDefaultFontFamilyName(),global::Avalonia.Media.FontStyle.Normal,weight,global::Avalonia.Media.FontStretch.Normal,out var face))throw new InvalidOperationException("Requested face missing.");
    return face;
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
