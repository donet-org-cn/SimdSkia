using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SimdSkia.ControlValidation;
#if MANAGED
using SimdSkia.Avalonia;
#endif

namespace SimdSkia.AotComparison;

internal static class Program
{
    internal static readonly Stopwatch Startup = Stopwatch.StartNew();
    internal static Options Options = null!;
    internal static string Font = Path.Combine(AppContext.BaseDirectory, "fonts", "ZCOOLXiaoWei-Regular.ttf");
#if MANAGED
    internal const string Engine = "SimdSkia";
#else
    internal const string Engine = "Avalonia.Skia";
#endif
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Options = Options.Parse(args);
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This desktop comparison targets Windows x64 software presentation.");
            if (!File.Exists(Font)) throw new FileNotFoundException("The published shared CJK TrueType font is missing.", Font);
            if (Options.Output is not null)
            {
                if (RuntimeFeature.IsDynamicCodeSupported) throw new InvalidOperationException("Benchmark mode requires a genuine Native AOT executable; JIT results are not accepted.");
                Directory.CreateDirectory(Options.Output);
            }
            var builder = AppBuilder.Configure<ComparisonApp>().UseStandardRuntimePlatformSubsystem();
#if MANAGED
            builder.UseSimdSkia(o => o.FontFiles.Add(Font));
#else
            builder.UseSkia().UseHarfBuzz();
#endif
            return builder.UseWin32().With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

internal sealed class ComparisonApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            throw new InvalidOperationException("A real desktop lifetime is required.");
        var collection = new EmbeddedFontCollection(new Uri("fonts:AotComparison"), new Uri(Program.Font));
        if (collection.Count == 0) throw new InvalidOperationException("The shared TrueType font was not registered.");
        FontManager.Current.AddFontCollection(collection);
        var button = new Button { Content = "更新 / Update" };
        int clicks = 0; button.Click += (_, _) => button.Content = $"已点击 / Clicked {++clicks}";
        var scene = Program.Options.Scene == "controls"
            ? ControlScene.Create(button, Program.Options.FullOpacity) : ControlScene.CreateList();
        TextElement.SetFontFamily(scene, collection[0]);
        var window = new Window
        {
            Title = $"{Program.Engine} · Native AOT · Avalonia 12.1.4", Width = 1000, Height = 760, Content = scene,
            ShowInTaskbar = !Program.Options.Hidden,
        };
        if (Program.Options.Hidden) window.Position = new PixelPoint(-20000, -20000);
        desktop.MainWindow = window;
        if (Program.Options.Output is not null)
            window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await Benchmark.Run(scene, window);
                    desktop.Shutdown();
                }
                catch (Exception error) { Console.Error.WriteLine(error); desktop.Shutdown(1); }
            }, DispatcherPriority.Background);
        base.OnFrameworkInitializationCompleted();
    }
}
