using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
#if MANAGED
using SimdSkia.Avalonia;
#endif

internal static class Program
{
    internal static readonly long Started=Stopwatch.GetTimestamp();
    internal static string Output="artifacts/startup/probe",Font="",Backend="cpu";
    internal static double Opened;
    internal static double Milliseconds=>(Stopwatch.GetTimestamp()-Started)*1000d/Stopwatch.Frequency;
    [STAThread]
    public static int Main(string[] args)
    {
        string Value(string name,string fallback){int i=Array.IndexOf(args,name);return i>=0?args[i+1]:fallback;}
        Output=Path.GetFullPath(Value("--output",Output));Font=Value("--font","");Backend=Value("--backend","cpu");
        Directory.CreateDirectory(Output);
        var builder=AppBuilder.Configure<ProbeApp>().UseStandardRuntimePlatformSubsystem()
            .UseWin32().With(new Win32PlatformOptions{RenderingMode=[Win32RenderingMode.Software]});
#if MANAGED
        builder.UseSimdSkia(o=>
        {
            if(Font.Length!=0)o.FontFiles.Add(Path.GetFullPath(Font));
            if(Backend!="cpu")o.UseGpu(Backend=="vulkan"?SimdSkiaGpuBackend.Vulkan:Backend=="direct2d"?SimdSkiaGpuBackend.Direct2D:throw new ArgumentException("Unknown backend"),allowCpuFallback:true);
        });
#else
        if(Backend!="cpu")throw new NotSupportedException("Native startup reference currently uses the same software window host.");
        builder.UseSkia().UseHarfBuzz();
#endif
        return builder.StartWithClassicDesktopLifetime(args);
    }
}

internal sealed class ProbeApp:Application
{
    public override void Initialize()=>Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var scene=SimdSkia.ControlValidation.ControlScene.Create();
        FontFamily family;
        if(Program.Font.Length!=0)
        {
            var collection=new EmbeddedFontCollection(new Uri("fonts:Startup"),new Uri(Path.GetFullPath(Program.Font)));
            FontManager.Current.AddFontCollection(collection);family=collection[0];
        }
        else family=new FontFamily("Microsoft YaHei UI");
        TextElement.SetFontFamily(scene,family);
        var window=new Window{Title="Startup probe "+Environment.ProcessId,Width=1000,Height=720,
            Position=new PixelPoint(60,60),ShowActivated=false,ShowInTaskbar=false,Topmost=true,
            Content=new Border{Background=new SolidColorBrush(Color.FromRgb(18,52,86)),Padding=new Thickness(8),Child=scene}};
        desktop.MainWindow=window;
        window.Opened+=(_,_)=>
        {
            Program.Opened=Program.Milliseconds;
            DispatcherTimer.RunOnce(()=>
            {
                using var process=Process.GetCurrentProcess();process.Refresh();
                using var stream=File.Create(Path.Combine(Program.Output,"metrics.json"));
                using(var json=new Utf8JsonWriter(stream,new(){Indented=true}))
                {
                    json.WriteStartObject();json.WriteNumber("pid",Environment.ProcessId);json.WriteNumber("mainTimestamp",Program.Started);json.WriteNumber("timestampFrequency",Stopwatch.Frequency);
                    json.WriteNumber("openedMs",Program.Opened);json.WriteNumber("endMs",Program.Milliseconds);json.WriteBoolean("nativeAot",!RuntimeFeature.IsDynamicCodeSupported);
                    json.WriteString("runtime",Environment.Version.ToString());json.WriteString("fontFamily",family.Name);json.WriteString("backend",Program.Backend);
                    json.WriteNumber("workingSetMiB",process.WorkingSet64/1048576d);json.WriteNumber("privateMiB",process.PrivateMemorySize64/1048576d);
                    json.WriteEndObject();
                }
                desktop.Shutdown();
            },TimeSpan.FromSeconds(2));
        };
        base.OnFrameworkInitializationCompleted();
    }
}
