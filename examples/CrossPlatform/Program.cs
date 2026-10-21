using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SimdSkia.Avalonia;

internal static class Program
{
    internal static bool Smoke;
    internal static string Output = "artifacts/cross-platform";
    [STAThread]
    public static int Main(string[] args)
    {
        Smoke = args.Contains("--smoke");
        int output = Array.IndexOf(args, "--output"); if (output >= 0) Output = args[output + 1];
        int font = Array.IndexOf(args, "--font");
        return AppBuilder.Configure<DemoApp>().UseSimdSkiaDesktop(options =>
        {
            if (font >= 0) options.FontFiles.Add(Path.GetFullPath(args[font + 1]));
        }).StartWithClassicDesktopLifetime(args);
    }
    internal static void Verify(Window window, Button button, IClassicDesktopStyleApplicationLifetime desktop, long before)
    {
        try
        {
            if (SimdSkiaDiagnostics.FramesPresented <= before) throw new InvalidOperationException("The OS framebuffer did not present a frame.");
            if (!window.IsVisible) throw new InvalidOperationException("The native window was not shown.");
            if (window.ClientSize.Width < 500 || window.ClientSize.Height < 200) throw new InvalidOperationException("The native window did not resize.");
            button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            if (!Equals(button.Content, "Clicked / 已点击")) throw new InvalidOperationException("The control event route failed.");
            using var target = new RenderTargetBitmap(new PixelSize(600, 300), new Vector(96, 96)); target.Render(window);
            byte[] pixels = new byte[600 * 300 * 4]; var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { target.CopyPixels(new PixelRect(0, 0, 600, 300), handle.AddrOfPinnedObject(), pixels.Length, 600 * 4); } finally { handle.Free(); }
            if (!pixels.Where((_, i) => i % 4 != 3).Any(c => c < 80)) throw new InvalidOperationException("The UI rendered blank pixels.");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
            if (assemblies.Any(n => n is "Avalonia.Skia" or "SkiaSharp" or "HarfBuzzSharp" or "Avalonia.HarfBuzz")) throw new InvalidOperationException("A native drawing/font engine loaded.");
            Directory.CreateDirectory(Output);
            using (var stream = File.Create(Path.Combine(Output, "window.png"))) target.Save(stream, PngBitmapEncoderOptions.Default);
            string family = FontManager.Current.DefaultFontFamily.Name;
            File.WriteAllText(Path.Combine(Output, "result.txt"), $"PASS\nOS={RuntimeInformation.OSDescription}\nArchitecture={RuntimeInformation.ProcessArchitecture}\nFrames={SimdSkiaDiagnostics.FramesPresented}\nFont={family}\nClientSize={window.ClientSize}\nNativeHandle={window.TryGetPlatformHandle()?.Handle}\nManagedAssemblies={string.Join(',', assemblies)}\n");
            Console.WriteLine($"PASS actual desktop: {RuntimeInformation.OSDescription}; {SimdSkiaDiagnostics.FramesPresented} frames; {family}");
            desktop.Shutdown();
        }
        catch (Exception error) { Console.Error.WriteLine(error); desktop.Shutdown(1); }
    }
}

internal sealed class DemoApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var button = new Button { Content = "Click / 点击", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
            button.Click += (_, _) => button.Content = "Clicked / 已点击";
            var window = new Window
            {
                Title = "SimdSkia cross-platform CPU",
                Width = 560, Height = 260,
                Content = new StackPanel
                {
                    Margin = new Thickness(24), Spacing = 16,
                    Children = { new TextBlock { Text = "SimdSkia / 中文界面", FontSize = 24 }, new TextBox { Text = "Editable text / 可编辑" }, button }
                }
            };
            desktop.MainWindow = window;
            if (Program.Smoke)
            {
                if (OperatingSystem.IsWindows()) { window.Position = new PixelPoint(-20000, -20000); window.ShowInTaskbar = false; }
                long before = SimdSkiaDiagnostics.FramesPresented;
                window.Opened += (_, _) =>
                {
                    window.Width = 600; window.Height = 300;
                    DispatcherTimer.RunOnce(() => Program.Verify(window, button, desktop, before), TimeSpan.FromSeconds(2));
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
