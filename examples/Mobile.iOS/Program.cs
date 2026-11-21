using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Foundation;
using SimdSkia.Avalonia;
using UIKit;

namespace SimdSkia.MobileIos;

internal static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<DemoApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        var font = Path.Combine(NSBundle.MainBundle.BundlePath, "Fonts", "ZCOOLXiaoWei-Regular.ttf");
        return builder
            .With(new iOSPlatformOptions { RenderingMode = [iOSRenderingMode.Metal] })
            .UseSimdSkia(options =>
            {
                options.FontFiles.Add(font);
                options.UseGpu(SimdSkiaGpuBackend.Metal, allowCpuFallback: true);
            });
    }
}

public sealed class DemoApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime lifetime)
        {
            int taps = 0;
            var count = new TextBlock { Text = "触摸按钮，验证输入", FontSize = 18 };
            var button = new Button { Content = "点一下" };
            button.Click += (_, _) => count.Text = $"已点击 {++taps} 次";
            var preview = new Border
            {
                Height = 112,
                CornerRadius = new CornerRadius(16),
                Background = new RadialGradientBrush
                {
                    GradientStops = new GradientStops
                    {
                        new(Color.FromRgb(99, 179, 255), 0),
                        new(Color.FromRgb(37, 74, 163), 1)
                    }
                }
            };
            lifetime.MainView = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(24),
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock { Text = "SimdSkia iOS", FontSize = 28 },
                        preview, button, count,
                        new TextBox { PlaceholderText = "在这里输入文字" }
                    }
                }
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
