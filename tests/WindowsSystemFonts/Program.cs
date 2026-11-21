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
bool bitmapEmoji=args is ["--bitmap-emoji",_];
if(bitmapEmoji)builder.UseSimdSkia(options=>
{
    options.FontFiles.Add(Path.GetFullPath("baseline/fonts/ZCOOLXiaoWei-Regular.ttf"));
    options.FontFiles.Add(Path.GetFullPath(args[1]));
});
else if(explicitFont)builder.UseSimdSkia(options=>options.FontFiles.Add(Path.GetFullPath(args[0])));
else builder.UseSimdSkia();
builder.SetupWithoutStarting();
var manager=AvaloniaLocator.Current.GetRequiredService<IFontManagerImpl>();
string family=manager.GetDefaultFontFamilyName();
if(explicitFont)
{
    Check(family=="站酷小薇体","Explicit fixture font must override automatic system discovery.");
    Check(manager.GetInstalledFontFamilyNames().Length==(bitmapEmoji?2:1),"Explicit font mode must not preload system fonts.");
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
    // Cambria is outside the UI/CJK/monospace selection on normal Windows
    // desktop installs. A registered Cambria file should still be usable.
    string cambriaPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"cambria.ttc");
    using var registrations=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
    bool cambriaRegistered=false;
    if(registrations is not null)
        foreach(string name in registrations.GetValueNames())
            if(registrations.GetValue(name) is string file&&Path.GetFileName(file).Equals("cambria.ttc",StringComparison.OrdinalIgnoreCase))
            {cambriaRegistered=true;break;}
    if(cambriaRegistered&&File.Exists(cambriaPath)&&!SimdSkiaSystemFonts.Discover().Files.Contains(cambriaPath,StringComparer.OrdinalIgnoreCase))
    {
        Check(manager.GetInstalledFontFamilyNames().Contains("Cambria",StringComparer.OrdinalIgnoreCase),"Cambria is indexed outside the preferred installed faces.");
        Check(manager.TryMatchCharacter('A',FontStyle.Normal,FontWeight.Normal,FontStretch.Normal,"Cambria",CultureInfo.InvariantCulture,out var extra),
            "A supplementary installed family can be matched by character.");
        using(extra) Check(extra!.FamilyName=="Cambria","A supplementary explicitly named face takes priority over the interface face.");
    }
}
var text=new TextBlock{Text="中文系统字体 / Windows system font 123",FontSize=24,FontWeight=explicitFont?FontWeight.Normal:FontWeight.Bold,Foreground=Brushes.Black,Background=Brushes.White};
text.Measure(new Size(900,100));text.Arrange(new Rect(0,0,900,100));
using var bitmap=new RenderTargetBitmap(new PixelSize(900,100),new Vector(96,96));bitmap.Render(text);
byte[] pixels=new byte[900*100*4];var pin=GCHandle.Alloc(pixels,GCHandleType.Pinned);
try{bitmap.CopyPixels(new PixelRect(0,0,900,100),pin.AddrOfPinnedObject(),pixels.Length,900*4);}finally{pin.Free();}
Check(pixels.Where((_,index)=>index%4!=3).Any(channel=>channel<80),"Text has visible dark pixels.");
Check(pixels.Where((_,index)=>index%4!=3).Any(channel=>channel is >80 and <240),"Text has antialiased coverage.");
if(bitmapEmoji||!explicitFont&&manager.GetInstalledFontFamilyNames().Contains("Segoe UI Emoji",StringComparer.OrdinalIgnoreCase))
{
    var octopus=RenderEmoji("🐙");
    Check(octopus.Colored>100,"The color font must render colored octopus ink through Avalonia fallback.");
    Console.WriteLine($"{(bitmapEmoji?"Noto Color Emoji":"Segoe UI Emoji")} octopus TextBlock: {octopus.Colored} colored pixels.");
    if(bitmapEmoji)
    {
        var joined=RenderEmoji("👩‍💻",new FontFamily("Noto Color Emoji"));
        var separate=RenderEmoji("👩💻",new FontFamily("Noto Color Emoji"));
        Check(joined.Colored>100&&joined.Width<separate.Width*.75,"Avalonia must measure and render the joined bitmap emoji as one glyph.");
        Console.WriteLine($"Noto TextBlock joined width {joined.Width:F1} vs separate width {separate.Width:F1}.");
    }
}
static (int Colored,double Width) RenderEmoji(string value,FontFamily? family=null)
{
    var emoji=new TextBlock{Text=value,FontSize=48,Foreground=Brushes.Black,Background=Brushes.White};
    if(family is not null)emoji.FontFamily=family;
    emoji.Measure(new Size(180,96));emoji.Arrange(new Rect(0,0,180,96));
    using var image=new RenderTargetBitmap(new PixelSize(180,96),new Vector(96,96));image.Render(emoji);
    var rgba=new byte[180*96*4];var pin=GCHandle.Alloc(rgba,GCHandleType.Pinned);
    try{image.CopyPixels(new PixelRect(0,0,180,96),pin.AddrOfPinnedObject(),rgba.Length,180*4);}finally{pin.Free();}
    int colored=0;
    for(int i=0;i<rgba.Length;i+=4)
        if(Math.Abs(rgba[i]-rgba[i+1])>12||Math.Abs(rgba[i+1]-rgba[i+2])>12)colored++;
    return (colored,emoji.DesiredSize.Width);
}
using(var process=Process.GetCurrentProcess())
    Check(!process.Modules.Cast<ProcessModule>().Any(module=>module.ModuleName.Contains("SkiaSharp",StringComparison.OrdinalIgnoreCase)||module.ModuleName.Contains("HarfBuzz",StringComparison.OrdinalIgnoreCase)||module.ModuleName.Contains("FreeType",StringComparison.OrdinalIgnoreCase)),"No native font/drawing engine loaded.");
Directory.CreateDirectory("artifacts/windows-system-fonts");
using(var output=File.Create($"artifacts/windows-system-fonts/{(explicitFont?"explicit":"system")}.png"))bitmap.Save(output,PngBitmapEncoderOptions.Default);
Console.WriteLine($"PASS {checks} checks; default font: {family}; {(explicitFont?"explicit fixture":"installed system fonts, no bundled font")}");
