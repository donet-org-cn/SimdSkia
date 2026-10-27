// Compiled only into the isolated VelaShell compatibility checkout, never into SimdSkia packages.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SimdSkia.Avalonia;
using VelaShell.Terminal.Rendering;
using VelaShell.ViewModels;

internal static class SimdSkiaVelaProbe
{
    private static readonly string Mode = Environment.GetEnvironmentVariable("VELASHELL_PROBE_BACKEND") ?? "original";
    private static readonly string Output = Path.GetFullPath(Environment.GetEnvironmentVariable("VELASHELL_PROBE_OUTPUT") ?? "probe-results");
    private static readonly List<object> Scenes = [];
    private static readonly List<object> Errors = [];
    private static readonly Queue<(string Name, Action Prepare, Func<Window> Window)> Steps = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Gate = new();
    private static int failureCount, logCount;
    private static string currentScene = "startup";
    private static (string Name, Action Prepare, Func<Window> Window)? pending;
    private static IClassicDesktopStyleApplicationLifetime desktop = null!;
    private static Window main = null!, terminal = null!, editor = null!;
    private static VelaTerminalControl terminalControl = null!;

    internal static AppBuilder Configure(AppBuilder builder)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "started.txt"), DateTimeOffset.Now.ToString("O"));
        if (Mode == "bad-host") builder.UseSimdSkia();
        else if (Mode == "cpu") builder.UseSimdSkiaDesktop();
        else if (Mode == "direct2d") builder.UseSimdSkiaDesktop(o => o.UseGpu(SimdSkiaGpuBackend.Direct2D));
        else if (Mode == "vulkan") builder.UseSimdSkiaDesktop(o => o.UseGpu(SimdSkiaGpuBackend.Vulkan));
        else if (Mode != "original") throw new ArgumentException("Unknown probe backend: " + Mode);
        if (Environment.GetEnvironmentVariable("VELASHELL_PROBE_INTERACTIVE") == "1") return builder;
        if (Mode != "original") SimdSkiaDiagnostics.CapturePresentedFrames = true;
        return builder.AfterSetup(app =>
        {
            Logger.Sink = new TeeSink(Logger.Sink);
            DispatcherTimer.Run(() => Tick(app), TimeSpan.FromSeconds(1));
        });
    }

    private static bool Tick(AppBuilder app)
    {
        try
        {
            if (Clock.Elapsed.TotalSeconds > 100) { failureCount++; Finish("Timeout waiting for application/scenes"); return false; }
            if (desktop is null)
            {
                if (app.Instance?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime || lifetime.MainWindow is not { IsVisible: true } window) return true;
                desktop = lifetime; main = window;
                if (Environment.GetEnvironmentVariable("VELASHELL_PROBE_OFFSCREEN") == "1") { main.Position = new PixelPoint(-20000, -20000); main.ShowInTaskbar = false; }
                BuildSteps();
                if (Environment.GetEnvironmentVariable("VELASHELL_PROBE_SCENES") == "terminal")
                { var terminalSteps = Steps.Where(s => s.Name.StartsWith("terminal-", StringComparison.Ordinal)).ToArray(); Steps.Clear(); foreach (var step in terminalSteps) Steps.Enqueue(step); }
                if (Environment.GetEnvironmentVariable("VELASHELL_PROBE_SCENES") == "support")
                { var supportSteps = Steps.Where(s => s.Name is "main" or "settings-00" or "settings-14").ToArray(); Steps.Clear(); foreach (var step in supportSteps) Steps.Enqueue(step); }
                File.WriteAllText(Path.Combine(Output, "main-window.txt"), main.GetType().FullName + "\n");
            }
            if (pending is { } scene)
            {
                Capture(scene.Name, scene.Window()); pending = null;
            }
            if (Steps.Count == 0) { Finish("Completed"); return false; }
            pending = Steps.Dequeue(); currentScene = pending.Value.Name;
            File.WriteAllText(Path.Combine(Output, "current-scene.txt"), currentScene);
            try { pending.Value.Prepare(); }
            catch (Exception error) { RecordFailure(pending.Value.Name, error); pending = null; }
            return true;
        }
        catch (Exception error) { RecordFailure(currentScene, error); pending = null; if (desktop is not null && Steps.Count == 0) { Finish("Failed"); return false; } return true; }
    }

    private static Window Settings() => desktop.Windows.First(w => w.DataContext is SettingsViewModel);
    private static SettingsViewModel SettingsModel => (SettingsViewModel)Settings().DataContext!;
    private static void Command(object command) => ((ICommand)command).Execute(null);
    private static void BuildSteps()
    {
        Steps.Enqueue(("main", () => { }, () => main));
        if (Mode == "bad-host") return;
        Steps.Enqueue(("settings-00", () => Command(((MainWindowViewModel)main.DataContext!).OpenSettingsCommand), Settings));
        for (int i = 1; i < 15; i++) { int page = i; Steps.Enqueue(($"settings-{page:D2}", () => SettingsModel.SelectedSectionIndex = page, Settings)); }
        Steps.Enqueue(("settings-light", () => { SettingsModel.SelectedSectionIndex = 1; SettingsModel.Theme = "light"; }, Settings));
        Steps.Enqueue(("settings-english", () => SettingsModel.Language = "en", Settings));
        Steps.Enqueue(("settings-chinese", () => SettingsModel.Language = "zh-CN", Settings));
        Steps.Enqueue(("terminal-latin", () =>
        {
            terminalControl = new VelaTerminalControl { CursorBlink = false, ShowLineNumber = false, ShowLineTimestamp = false, ShowFoldMarker = false };
            terminal = new Window { Width = 800, Height = 420, Content = terminalControl };
            terminalControl.Feed(Encoding.UTF8.GetBytes("SimdSkia / VelaShell terminal 0123456789\r\n\u001b[31mRED\u001b[0m plain\r\n"));
            terminal.Show();
        }, () => terminal));
        Steps.Enqueue(("terminal-cjk", () => terminalControl.Feed(Encoding.UTF8.GetBytes("\u001b[2J\u001b[H中文终端测试 English 123\r\n\u001b[32m成功 / Success\u001b[0m\r\n")), () => terminal));
        Steps.Enqueue(("terminal-styles", () => terminalControl.Feed(Encoding.UTF8.GetBytes("\u001b[2J\u001b[H\u001b[1mBold\u001b[22m \u001b[3mItalic\u001b[23m \u001b[4mUnderline\u001b[0m\r\n")), () => terminal));
        Steps.Enqueue(("textbox", () =>
        {
            var text = new TextBox { Text = "Editable 中文 0123456789", AcceptsReturn = true };
            editor = new Window { Width = 650, Height = 240, Content = text }; editor.Show(); text.Focus();
            text.Text += "\nSecond line / 第二行"; text.SelectAll();
        }, () => editor));
        if (Environment.GetEnvironmentVariable("VELASHELL_PROBE_COMBINING") == "1")
            Steps.Enqueue(("terminal-combining", () => terminalControl.Feed(Encoding.UTF8.GetBytes("\u001b[2J\u001b[He\u0301 A\u030a\r\n")), () => terminal));
    }

    private static void Capture(string scene, Window window)
    {
        try
        {
            if (!window.IsVisible) throw new InvalidOperationException("Window did not complete Show/layout; inspect the application's own crash log.");
            int width = (int)Math.Ceiling(window.ClientSize.Width), height = (int)Math.Ceiling(window.ClientSize.Height);
            if (width <= 0 || height <= 0) throw new InvalidOperationException("Empty client area");
            var start = Stopwatch.StartNew();
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(window); start.Stop();
            using (var png = File.Create(Path.Combine(Output, scene + ".png"))) bitmap.Save(png, PngBitmapEncoderOptions.Default);
            byte[] pixels = new byte[checked(width * height * 4)]; var pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { bitmap.CopyPixels(new PixelRect(0, 0, width, height), pin.AddrOfPinnedObject(), pixels.Length, width * 4); } finally { pin.Free(); }
            File.WriteAllBytes(Path.Combine(Output, scene + ".bgra"), pixels);
            uint first = BitConverter.ToUInt32(pixels); int changed = 0;
            for (int i = 0; i < pixels.Length; i += 4) if (BitConverter.ToUInt32(pixels, i) != first) changed++;
            if (changed == 0) throw new InvalidOperationException("Rendered image is a single color");
            var process = Process.GetCurrentProcess(); process.Refresh();
            Scenes.Add(new { scene, passed = true, width, height, renderMilliseconds = start.Elapsed.TotalMilliseconds, changedPixels = changed,
                workingSetMiB = process.WorkingSet64 / 1048576d, privateMiB = process.PrivateMemorySize64 / 1048576d,
                visible = window.IsVisible, window = window.GetType().FullName,
                directSkiaPreviews = window.GetVisualDescendants().Count(v => v.GetType().Name == "SplashPreview"),
                renderer = Renderer(),
                presentedFrames = Mode == "original" ? -1 : SimdSkiaDiagnostics.FramesPresented });
            File.WriteAllText(Path.Combine(Output, "scenes.json"), JsonSerializer.Serialize(Scenes, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error) { RecordFailure(scene, error); }
    }

    private static void RecordFailure(string scene, Exception error)
    { failureCount++; Scenes.Add(new { scene, passed = false, error = error.ToString() }); File.AppendAllText(Path.Combine(Output, "failures.log"), scene + "\n" + error + "\n"); File.WriteAllText(Path.Combine(Output, "scenes.json"), JsonSerializer.Serialize(Scenes, new JsonSerializerOptions { WriteIndented = true })); }

    private static void Finish(string state)
    {
        var process = Process.GetCurrentProcess();
        string[] modules = process.Modules.Cast<ProcessModule>().Select(m => m.ModuleName).ToArray();
        object[] errors; lock (Gate) errors = Errors.ToArray();
        var result = new { backend = Mode, state, failureCount, seconds = Clock.Elapsed.TotalSeconds, scenes = Scenes,
            errors, loggedEvents = logCount, runtime = Environment.Version.ToString(), modules,
            renderer = Renderer(),
            presentedFrames = Mode == "original" ? -1 : SimdSkiaDiagnostics.FramesPresented,
            gpu = SimdSkiaGpuDiagnostics.GetStatistics(), nativeSwapchainPresents = SimdSkiaGpuDiagnostics.NativeSwapchainPresents };
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        desktop.Shutdown(failureCount == 0 && errors.Length == 0 ? 0 : 2);
    }

    private static string? Renderer()
    {
        // Avalonia's consumer reference assembly hides backend service access.
        // The runtime adapter is pinned to 12.1.4; reflection is diagnostic only.
        var locator = typeof(AvaloniaLocator).GetProperty("Current", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var getService = locator.GetType().GetMethod("GetService", [typeof(Type)])!;
        return getService.Invoke(locator, [typeof(IPlatformRenderInterface)])?.GetType().FullName;
    }

    private sealed class TeeSink(ILogSink? next) : ILogSink
    {
        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning || next?.IsEnabled(level, area) == true;
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Log(level, area, source, messageTemplate, []);
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            next?.Log(level, area, source, messageTemplate, propertyValues);
            if (level < LogEventLevel.Warning) return;
            string message = messageTemplate + " | " + string.Join(" | ", propertyValues.Select(v => v?.ToString()));
            lock (Gate)
            {
                logCount++;
                if (logCount <= 3000) File.AppendAllText(Path.Combine(Output, "avalonia.log"), $"[{level}] [{area}] [{currentScene}] {message}\n");
                if (level >= LogEventLevel.Error && Errors.Count < 200) Errors.Add(new { scene = currentScene, area, message });
            }
        }
    }
}
