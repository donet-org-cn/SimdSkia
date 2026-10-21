using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SimdSkia.Avalonia;

/// <summary>Reads Windows font metadata only. All font parsing, shaping and drawing remain managed.</summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsSystemFonts
{
    private const string FontKey=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    private static readonly HashSet<string> FallbackFiles=new(StringComparer.OrdinalIgnoreCase)
    {
        "msyh.ttc","msyhbd.ttc","msyhl.ttc","simsun.ttc","simhei.ttf",
        "segoeui.ttf","segoeuib.ttf","segoeuisb.ttf","segoeuii.ttf","segoeuiz.ttf",
        "consola.ttf","consolab.ttf","consolai.ttf","consolaz.ttf","seguisym.ttf"
    };
    private static readonly HashSet<string> StyleWords=new(StringComparer.OrdinalIgnoreCase)
    {"Regular","Bold","Italic","Oblique","Light","Semilight","Semibold","Black","ExtraLight","ExtraBold"};

    internal static (string DefaultFamily,string[] Files) Discover()
    {
        string family=GetMessageFontFamily();
        var fonts=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        ReadRegistry(Registry.LocalMachine,Environment.GetFolderPath(Environment.SpecialFolder.Fonts),fonts);
        ReadRegistry(Registry.CurrentUser,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Fonts"),fonts);
        var primary=fonts.Where(entry=>MatchesFamily(entry.Key,family)).Select(entry=>entry.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if(primary.Length==0)
            throw new NotSupportedException($"The Windows interface font '{family}' has no registered font file. Register explicit TrueType fonts in SimdSkiaOptions.FontFiles.");
        var available=fonts.Values.Where(path=>FallbackFiles.Contains(Path.GetFileName(path))).ToArray();
        string? cjk=available.Any(path=>Path.GetFileName(path).StartsWith("msyh",StringComparison.OrdinalIgnoreCase))?"msyh":
            available.Any(path=>Path.GetFileName(path).Equals("simsun.ttc",StringComparison.OrdinalIgnoreCase))?"simsun":
            available.Any(path=>Path.GetFileName(path).Equals("simhei.ttf",StringComparison.OrdinalIgnoreCase))?"simhei":null;
        var fallbacks=available.Where(path=>
            FallbackPriority(Path.GetFileName(path))>=3||Path.GetFileName(path).StartsWith(cjk??"\0",StringComparison.OrdinalIgnoreCase))
            .OrderBy(path=>FallbackPriority(Path.GetFileName(path))).ThenBy(path=>path,StringComparer.OrdinalIgnoreCase);
        // Resolve only the interface family and a bounded set of fallback families. Do not
        // eagerly parse every installed font, or include unsupported Windows color/variable fonts.
        return (family,primary.Concat(fallbacks).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static int FallbackPriority(string file)=>file.StartsWith("msyh",StringComparison.OrdinalIgnoreCase)?0:
        file.StartsWith("simsun",StringComparison.OrdinalIgnoreCase)?1:file.StartsWith("simhei",StringComparison.OrdinalIgnoreCase)?2:3;

    private static void ReadRegistry(RegistryKey hive,string directory,Dictionary<string,string> fonts)
    {
        using var key=hive.OpenSubKey(FontKey);
        if(key is null)return;
        foreach(string name in key.GetValueNames())
        {
            if(key.GetValue(name) is not string{Length:>0} file)continue;
            string path=Path.IsPathRooted(file)?file:Path.Combine(directory,file);
            // Missing registrations do not identify installed fonts. A missing primary
            // family is rejected above; explicit font paths continue to throw on read.
            if(File.Exists(path))fonts[name]=Path.GetFullPath(path);
        }
    }

    private static bool MatchesFamily(string displayName,string family)
    {
        int suffix=displayName.LastIndexOf(" (",StringComparison.Ordinal);
        if(suffix>=0)displayName=displayName[..suffix];
        foreach(string face in displayName.Split(" & ",StringSplitOptions.TrimEntries))
        {
            if(face.Equals(family,StringComparison.OrdinalIgnoreCase))return true;
            if(face.StartsWith(family+" ",StringComparison.OrdinalIgnoreCase)&&
                face[(family.Length+1)..].Split(' ',StringSplitOptions.RemoveEmptyEntries).All(StyleWords.Contains))return true;
        }
        return false;
    }

    private static unsafe string GetMessageFontFamily()
    {
        var metrics=new NonClientMetrics{Size=(uint)sizeof(NonClientMetrics)};
        if(SystemParametersInfo(0x0029,metrics.Size,&metrics,0)==0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(),"Cannot read the Windows interface font.");
        string family=new string(metrics.MessageFont.FaceName,0,32).TrimEnd('\0');
        if(string.IsNullOrWhiteSpace(family))throw new InvalidOperationException("Windows returned an empty interface font name.");
        return family;
    }

    // Win32's documented LOGFONTW and NONCLIENTMETRICSW layouts. No native
    // rasterizer or font handle is created by this metadata query.
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct LogFont
    {
        internal int Height,Width,Escapement,Orientation,Weight;
        internal byte Italic,Underline,StrikeOut,CharSet,OutPrecision,ClipPrecision,Quality,PitchAndFamily;
        internal fixed char FaceName[32];
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NonClientMetrics
    {
        internal uint Size;
        internal int BorderWidth,ScrollWidth,ScrollHeight,CaptionWidth,CaptionHeight;
        internal LogFont CaptionFont;
        internal int SmallCaptionWidth,SmallCaptionHeight;
        internal LogFont SmallCaptionFont;
        internal int MenuWidth,MenuHeight;
        internal LogFont MenuFont,StatusFont,MessageFont;
        internal int PaddedBorderWidth;
    }
    [LibraryImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]
    private static unsafe partial int SystemParametersInfo(uint action,uint parameter,NonClientMetrics* data,uint flags);
}
