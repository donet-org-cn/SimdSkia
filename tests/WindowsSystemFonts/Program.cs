using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Win32;
using SimdSkia.Avalonia;
using SimdSkia.ControlValidation;
using SimdSkia.Text;

if(!OperatingSystem.IsWindows()){Console.WriteLine("SKIP: Windows system font integration requires Windows.");return;}
int checks=0;
void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);checks++;}
var builder=AppBuilder.Configure<Application>().UseStandardRuntimePlatformSubsystem().UseManagedHeadless();
bool explicitFont=args.Length!=0;
if(explicitFont)builder.UseSimdSkia(options=>options.FontFiles.Add(Path.GetFullPath(args[0])));
else builder.UseSimdSkia();
builder.SetupWithoutStarting();
var manager=AvaloniaLocator.Current.GetRequiredService<IFontManagerImpl>();
string family=manager.GetDefaultFontFamilyName();
if(explicitFont)
{
    Check(family=="站酷小薇体","Explicit fixture font must override automatic system discovery.");
    Check(manager.GetInstalledFontFamilyNames().Length==1,"Explicit font mode must not preload system fonts.");
}
else
{
    // Check the independently persisted LOGFONT record, rather than copying
    // the production SystemParametersInfo interop structure into the test.
    using var metrics=Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
    if(metrics?.GetValue("MessageFont") is byte[]{Length:>=92} logFont)
        Check(family==Encoding.Unicode.GetString(logFont,28,64).TrimEnd('\0'),"Default family must follow the actual Windows interface font.");
    Check(manager.GetInstalledFontFamilyNames().Contains(family),"Default family must be an actual parsed face, including the correct TTC face.");
    Check(manager.GetInstalledFontFamilyNames().Contains("Consolas"),"Installed terminal font must be discoverable.");
}
Check(manager.TryCreateGlyphTypeface(family,FontStyle.Normal,FontWeight.Normal,FontStretch.Normal,out var normal),"Default normal typeface");
Check(normal!.FamilyName==family,"No silent family substitution for the Windows default.");
Check(normal.TryGetStream(out var normalStream),"Default typeface exposes real managed font bytes.");
using(normalStream){using var bytes=new MemoryStream();normalStream!.CopyTo(bytes);var font=new TrueTypeFont(bytes.ToArray());Check(font.GetGlyph('A')!=0,"Real Latin outline");}
normal.Dispose();
Check(manager.TryMatchCharacter('中',FontStyle.Normal,FontWeight.Normal,FontStretch.Normal,family,CultureInfo.GetCultureInfo("zh-CN"),out var chinese),"Installed Chinese fallback is required for this Windows validation machine.");
chinese!.Dispose();
if(!explicitFont)
{
    Check(manager.TryCreateGlyphTypeface(family,FontStyle.Normal,FontWeight.Bold,FontStretch.Normal,out var bold),"Default bold typeface");
    Check(bold!.Weight>=FontWeight.SemiBold,"Bold text uses an actual heavy font face.");bold.Dispose();
}
var text=new TextBlock{Text="中文系统字体 / Windows system font 123",FontSize=24,FontWeight=explicitFont?FontWeight.Normal:FontWeight.Bold,Foreground=Brushes.Black,Background=Brushes.White};
text.Measure(new Size(900,100));text.Arrange(new Rect(0,0,900,100));
using var bitmap=new RenderTargetBitmap(new PixelSize(900,100),new Vector(96,96));bitmap.Render(text);
byte[] pixels=new byte[900*100*4];var pin=GCHandle.Alloc(pixels,GCHandleType.Pinned);
try{bitmap.CopyPixels(new PixelRect(0,0,900,100),pin.AddrOfPinnedObject(),pixels.Length,900*4);}finally{pin.Free();}
Check(pixels.Where((_,index)=>index%4!=3).Any(channel=>channel<80),"Text has visible dark pixels.");
Check(pixels.Where((_,index)=>index%4!=3).Any(channel=>channel is >80 and <240),"Text has antialiased coverage.");
using(var process=Process.GetCurrentProcess())
    Check(!process.Modules.Cast<ProcessModule>().Any(module=>module.ModuleName.Contains("SkiaSharp",StringComparison.OrdinalIgnoreCase)||module.ModuleName.Contains("HarfBuzz",StringComparison.OrdinalIgnoreCase)||module.ModuleName.Contains("FreeType",StringComparison.OrdinalIgnoreCase)),"No native font/drawing engine loaded.");
Directory.CreateDirectory("artifacts/windows-system-fonts");
using(var output=File.Create($"artifacts/windows-system-fonts/{(explicitFont?"explicit":"system")}.png"))bitmap.Save(output,PngBitmapEncoderOptions.Default);
Console.WriteLine($"PASS {checks} checks; default font: {family}; {(explicitFont?"explicit fixture":"installed system fonts, no bundled font")}");
