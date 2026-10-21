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
    internal static bool Benchmark,Controls;
    internal static SimdSkiaGpuBackend? Gpu;
    internal static string Output = "artifacts/cross-platform";
    [STAThread]
    public static int Main(string[] args)
    {
        Smoke = args.Contains("--smoke");
        Benchmark=args.Contains("--benchmark");Controls=args.Contains("--controls");
        int output = Array.IndexOf(args, "--output"); if (output >= 0) Output = args[output + 1];
        int font = Array.IndexOf(args, "--font");
        int gpu = Array.IndexOf(args, "--gpu");
        if (gpu >= 0) Gpu = args[gpu + 1] switch { "direct2d" => SimdSkiaGpuBackend.Direct2D, "vulkan" => SimdSkiaGpuBackend.Vulkan, _ => throw new ArgumentException("--gpu must be direct2d or vulkan.") };
        SimdSkiaDiagnostics.CapturePresentedFrames = Smoke;
        return AppBuilder.Configure<DemoApp>().UseSimdSkiaDesktop(options =>
        {
            if (font >= 0) options.FontFiles.Add(Path.GetFullPath(args[font + 1]));
            if (Gpu is {} backend) options.UseGpu(backend, allowCpuFallback: args.Contains("--allow-cpu-fallback"));
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
            int width=(int)Math.Ceiling(window.ClientSize.Width),height=(int)Math.Ceiling(window.ClientSize.Height);
            using var target = new RenderTargetBitmap(new PixelSize(width,height), new Vector(96, 96)); target.Render(window);
            byte[] pixels = new byte[width * height * 4]; var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { target.CopyPixels(new PixelRect(0, 0, width,height), handle.AddrOfPinnedObject(), pixels.Length, width * 4); } finally { handle.Free(); }
            if (!pixels.Where((_, i) => i % 4 != 3).Any(c => c < 80)) throw new InvalidOperationException("The UI rendered blank pixels.");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
            if (assemblies.Any(n => n is "Avalonia.Skia" or "SkiaSharp" or "HarfBuzzSharp" or "Avalonia.HarfBuzz")) throw new InvalidOperationException("A native drawing/font engine loaded.");
            Directory.CreateDirectory(Output);
            using (var stream = File.Create(Path.Combine(Output, "window.png"))) target.Save(stream, PngBitmapEncoderOptions.Default);
            string family = FontManager.Current.DefaultFontFamily.Name;
            var gpuStats = SimdSkiaGpuDiagnostics.GetStatistics();
            bool nativeAot=!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
            string[] modules=OperatingSystem.IsWindows()?Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Select(m=>m.ModuleName).ToArray():[];
            if(modules.Any(n=>n.Contains("SkiaSharp",StringComparison.OrdinalIgnoreCase)||n.Contains("HarfBuzz",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("A forbidden native drawing/font module loaded.");
            if(nativeAot&&modules.Any(n=>n.Equals("coreclr.dll",StringComparison.OrdinalIgnoreCase)||n.Equals("clrjit.dll",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("The AOT consumer loaded CLR/JIT.");
            if (Gpu.HasValue && (gpuStats.GpuCommands == 0 || gpuStats.CompletedBatches == 0)) throw new InvalidOperationException("GPU drawing/completion was requested but did not execute.");
            using var frame = SimdSkiaDiagnostics.GetLastPresentedFrame() ?? throw new InvalidOperationException("No actual presented framebuffer was captured.");
            var expectedSize=PixelSize.FromSize(window.ClientSize,window.RenderScaling);
            if(frame.Width!=expectedSize.Width||frame.Height!=expectedSize.Height)throw new InvalidOperationException("The presented framebuffer did not follow the resized native window/DPI.");
            if (!Enumerable.Range(0, frame.Height).Any(y => frame.GetRowSpan(y)[..(frame.Width * 4)].ToArray().Where((_, i) => i % 4 != 3).Any(c => c < 80))) throw new InvalidOperationException("The actual presented framebuffer is blank.");
            File.WriteAllBytes(Path.Combine(Output, "presented.png"), SimdSkia.Imaging.PngCodec.Encode(frame));
            File.WriteAllText(Path.Combine(Output,"runtime.txt"),$"NativeAot={nativeAot}\nNativeModules={string.Join(',',modules)}\nManagedGlyphAtlasBuilds={SimdSkiaGpuDiagnostics.ManagedGlyphAtlasBuilds}\nManagedGlyphAtlasHits={SimdSkiaGpuDiagnostics.ManagedGlyphAtlasHits}\n");
            File.WriteAllText(Path.Combine(Output, "result.txt"), $"PASS\nOS={RuntimeInformation.OSDescription}\nArchitecture={RuntimeInformation.ProcessArchitecture}\nFrames={SimdSkiaDiagnostics.FramesPresented}\nFont={family}\nClientSize={window.ClientSize}\nNativeHandle={window.TryGetPlatformHandle()?.Handle}\nGPU={gpuStats.Device}\nGpuCommands={gpuStats.GpuCommands}\nNativeSwapchainPresents={SimdSkiaGpuDiagnostics.NativeSwapchainPresents}\nCpuFallbackCommands={gpuStats.CpuFallbackCommands}\nCpuLayerCommands={SimdSkiaDiagnostics.SoftwareLayerCommands}\nCompletedBatches={gpuStats.CompletedBatches}\nGpuCompletionMs={gpuStats.CompletionMilliseconds}\nFallbackReasons={string.Join(';', gpuStats.FallbackReasons.Select(p => p.Key + '=' + p.Value))}\nManagedAssemblies={string.Join(',', assemblies)}\n");
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
                Title = "SimdSkia cross-platform " + (Program.Gpu?.ToString() ?? "CPU"),
                Width = 560, Height = 260,
                Content = Program.Controls?SimdSkia.ControlValidation.ControlScene.Create(button):new StackPanel
                {
                    Margin = new Thickness(24), Spacing = 16,
                    Children = { new TextBlock { Text = "SimdSkia / 中文界面", FontSize = 24 }, new TextBox { Text = "Editable text / 可编辑" }, button }
                }
            };
            if(Program.Controls){window.Width=1000;window.Height=720;}
            desktop.MainWindow = window;
            if(Program.Benchmark)
            {
                if(OperatingSystem.IsWindows()){window.Position=new PixelPoint(-20000,-20000);window.ShowInTaskbar=false;}
                window.Opened+=(_,_)=>WindowBenchmark.Start(window,button,desktop);
            }
            if (Program.Smoke)
            {
                if (OperatingSystem.IsWindows()) { window.Position = new PixelPoint(-20000, -20000); window.ShowInTaskbar = false; }
                if(Program.Controls){window.Width=980;window.Height=700;}
                long before = SimdSkiaDiagnostics.FramesPresented;
                window.Opened += (_, _) =>
                {
                    DispatcherTimer.RunOnce(()=>
                    {
                        if(SimdSkiaDiagnostics.FramesPresented<=before){Console.Error.WriteLine("The initial native frame did not complete before resize.");desktop.Shutdown(1);return;}
                        window.Width = Program.Controls?1000:600; window.Height = Program.Controls?720:300;
                        DispatcherTimer.RunOnce(() => Program.Verify(window, button, desktop, before), TimeSpan.FromSeconds(2));
                    },TimeSpan.FromMilliseconds(600));
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
