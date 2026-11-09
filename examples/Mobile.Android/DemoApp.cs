using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SimdSkia.Avalonia;

namespace SimdSkia.MobileSample;

public sealed class DemoApp : global::Avalonia.Application
{
    private DispatcherTimer? animationTimer;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = global::Avalonia.Styling.ThemeVariant.Dark;
        Log("Application initialized");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            single.MainView = CreateView();
        else
            throw new InvalidOperationException("Android requires a single-view application lifetime.");
        base.OnFrameworkInitializationCompleted();
    }

    internal void SetAnimationActive(bool active)
    {
        if (active)
        {
            animationTimer?.Start();
            DispatcherTimer.RunOnce(WriteDiagnostics, TimeSpan.FromSeconds(3));
        }
        else
            animationTimer?.Stop();
        Log(active ? "Activity resumed" : "Activity paused");
    }

    private Control CreateView()
    {
        int clicks = 0;
        var status = new TextBlock { Text = "等待点击 / Ready", FontSize = 18 };
        var input = new TextBox { PlaceholderText = "输入中文或 English", Text = "你好 SimdSkia", MinHeight = 48 };
        input.TextChanged += (_, _) => Log("TextChanged " + input.Text);
        var button = new global::Avalonia.Controls.Button { Content = "点击测试 / Tap", HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) =>
        {
            status.Text = $"点击成功 / Clicks {++clicks}";
            Log("Click " + clicks);
        };
        var progress = new global::Avalonia.Controls.ProgressBar { Minimum = 0, Maximum = 100, Height = 12 };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        animationTimer = timer;
        timer.Tick += (_, _) => progress.Value = (progress.Value + 1) % 101;
        var content = new StackPanel
        {
            Margin = new Thickness(24, 48, 24, 24), Spacing = 20,
            Children =
            {
                new TextBlock { Text = "SimdSkia 安卓真机", FontSize = 28, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "纯 C# CPU / SIMD / Avalonia 12.1.5", TextWrapping = TextWrapping.Wrap },
                input, button, status, progress,
                new Border
                {
                    Height = 100, CornerRadius = new CornerRadius(18),
                    Background = new LinearGradientBrush
                    {
                        GradientStops = { new GradientStop(Colors.DodgerBlue, 0), new GradientStop(Colors.MediumPurple, 1) }
                    },
                    Child = new TextBlock { Text = "圆角 / 渐变 / 中文", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        var root = new ScrollViewer { Content = content, Background = new SolidColorBrush(Color.FromRgb(32, 32, 36)) };
        root.AttachedToVisualTree += (_, _) => { timer.Start(); Log("View attached"); };
        root.DetachedFromVisualTree += (_, _) => { timer.Stop(); Log("View detached"); };
        return root;
    }

    private static void Log(string message)
    {
        File.AppendAllText(Path.Combine(DataDirectory, "smoke.log"), $"{DateTime.UtcNow:O} {message} frames={SimdSkiaDiagnostics.FramesPresented}{Environment.NewLine}");
        global::Android.Util.Log.Info("SimdSkiaSample", message);
    }

    private static string DataDirectory => global::Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath
        ?? global::Android.App.Application.Context.FilesDir!.AbsolutePath;

    private static void WriteDiagnostics()
    {
        string[] assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name!)
            .Order(StringComparer.Ordinal).ToArray();
        if (assemblies.Any(name => name == "Avalonia.Skia" || name.Contains("SkiaSharp", StringComparison.OrdinalIgnoreCase)
            || name.Contains("HarfBuzz", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("An unexpected native rendering/text assembly was loaded.");
        string[] lines =
        [
            "Frames=" + SimdSkiaDiagnostics.FramesPresented,
            "AdvSimd=" + System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported,
            "Vector=" + System.Numerics.Vector.IsHardwareAccelerated,
            "Assemblies=" + string.Join(',', assemblies)
        ];
        File.WriteAllLines(Path.Combine(DataDirectory, "diagnostics.txt"), lines);
        Log("Diagnostics " + string.Join(' ', lines[..3]));
    }
}
