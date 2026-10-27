using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SimdSkia.Avalonia;
using SimdSkia.Pdf;
using SkiaSharp;

internal static class Program
{
    internal static string FileName = "tests/Pdf/Fixtures/external.pdf";
    internal static bool Smoke;
    [STAThread]
    public static int Main(string[] args)
    {
        Smoke = args.Contains("--smoke");
        FileName = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? FileName;
        SimdSkiaDiagnostics.CapturePresentedFrames = Smoke;
        return AppBuilder.Configure<ViewerApp>().UseSimdSkiaDesktop().StartWithClassicDesktopLifetime(args);
    }
}

internal sealed class ViewerApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new ViewerWindow(desktop);
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class ViewerWindow : Window
{
    private readonly TextBox path = new() { Text = Path.GetFullPath(Program.FileName), MinWidth = 420 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Image image = new() { Stretch = Stretch.None };
    private PdfReader? reader;
    private Bitmap? shown;
    private int page;
    private bool rendered;

    public ViewerWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Title = "SimdSkia PDF viewer"; Width = 900; Height = 700;
        var open = new Button { Content = "Open" };
        var previous = new Button { Content = "Previous" };
        var next = new Button { Content = "Next" };
        open.Click += (_, _) => OpenDocument();
        previous.Click += (_, _) => ShowPage(page - 1);
        next.Click += (_, _) => ShowPage(page + 1);
        var toolbar = new StackPanel
        {
            Spacing = 8, Margin = new Thickness(12),
            Children =
            {
                new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { path, open, previous, next } },
                status
            }
        };
        DockPanel.SetDock(toolbar, Dock.Top);
        Content = new DockPanel { Children = { toolbar, new ScrollViewer { Content = image } } };
        Closed += (_, _) => { image.Source = null; shown?.Dispose(); reader?.Dispose(); };
        Opened += (_, _) =>
        {
            OpenDocument();
            if (Program.Smoke) DispatcherTimer.RunOnce(() =>
            {
                try
                {
                    if (reader is not null && reader.PageCount > 1)
                    {
                        next.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                        if (page != 2 || !rendered) throw new InvalidOperationException("Next page failed: " + status.Text);
                        previous.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                        if (page != 1 || !rendered) throw new InvalidOperationException("Previous page failed: " + status.Text);
                    }
                    if (!rendered || !IsVisible || SimdSkiaDiagnostics.FramesPresented == 0)
                        throw new InvalidOperationException("PDF window did not present a rendered page: " + status.Text);
                    using var frame = SimdSkiaDiagnostics.GetLastPresentedFrame() ?? throw new InvalidOperationException("No presented framebuffer.");
                    Directory.CreateDirectory("artifacts/pdf-viewer");
                    File.WriteAllBytes("artifacts/pdf-viewer/presented.png", SimdSkia.Imaging.PngCodec.Encode(frame));
                    File.WriteAllText("artifacts/pdf-viewer/result.txt", $"PASS actual PDF window; page {page}/{reader!.PageCount}; frames={SimdSkiaDiagnostics.FramesPresented}\n");
                    desktop.Shutdown();
                }
                catch (Exception error) { Directory.CreateDirectory("artifacts/pdf-viewer"); File.WriteAllText("artifacts/pdf-viewer/error.txt", error.ToString()); desktop.Shutdown(1); }
            }, TimeSpan.FromSeconds(2));
        };
        if (Program.Smoke && OperatingSystem.IsWindows()) { Position = new PixelPoint(-20000, -20000); ShowInTaskbar = false; }
    }

    private void OpenDocument()
    {
        try
        {
            var next = PdfReader.Open(path.Text ?? "");
            reader?.Dispose(); reader = next; page = 0; ShowPage(1);
        }
        catch (Exception error) { ShowError(error); }
    }

    private void ShowPage(int number)
    {
        if (reader is null || number < 1 || number > reader.PageCount) return;
        try
        {
            // This sample uses a PNG bridge for clarity; rendering itself returns an SKBitmap.
            using var renderedPage = reader.RenderPage(number, 96);
            using var encoded = renderedPage.Bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(encoded.ToArray());
            var next = new Bitmap(stream);
            image.Source = next; shown?.Dispose(); shown = next;
            page = number; rendered = true; status.Text = $"Page {page} / {reader.PageCount} — 96 DPI";
        }
        catch (Exception error) { ShowError(error); }
    }

    private void ShowError(Exception error)
    { rendered = false; image.Source = null; shown?.Dispose(); shown = null; status.Text = "Unable to render this document: " + error.Message; }
}
