using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using SimdSkia.Avalonia;
using SimdSkia.ControlValidation;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        foreach(string option in args.Where(a=>a.StartsWith("--",StringComparison.Ordinal)))if(option is not ("--window" or "--smoke-window"))throw new ArgumentException($"Unknown option: {option}");
        string font=args.FirstOrDefault(a=>!a.StartsWith("--",StringComparison.Ordinal))??"baseline/fonts/ZCOOLXiaoWei-Regular.ttf";
        DemoApp.Smoke=args.Contains("--smoke-window");bool window=args.Contains("--window")||DemoApp.Smoke;
        var builder=AppBuilder.Configure<DemoApp>().UseStandardRuntimePlatformSubsystem().UseSimdSkia(o=>o.FontFiles.Add(font));
        if(window)
        {
            if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This example's window host currently uses Windows software rendering; PNG rendering is cross-platform.");
            return builder.UseWin32().With(new Win32PlatformOptions{RenderingMode=[Win32RenderingMode.Software]}).StartWithClassicDesktopLifetime(args);
        }
        builder.UseManagedHeadless().SetupWithoutStarting();
        var scene=ControlScene.Create();var host=new Window{Content=scene,Width=1000,Height=720};host.Show();global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();ControlScene.Prepare(scene);
        using var bitmap=new RenderTargetBitmap(new PixelSize(1000,720),new Vector(96,96));bitmap.Render(scene);
        Directory.CreateDirectory("artifacts/avalonia-controls");using(var output=File.Create("artifacts/avalonia-controls/managed.png"))bitmap.Save(output,PngBitmapEncoderOptions.Default);
        Console.WriteLine("Real Avalonia controls rendered with SimdSkia: artifacts/avalonia-controls/managed.png");
        if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name is "SkiaSharp" or "HarfBuzzSharp" or "Avalonia.Skia"))throw new InvalidOperationException("A forbidden native rendering/font backend was loaded.");
        host.Close();return 0;
    }
}

sealed class DemoApp:Application
{
    internal static bool Smoke;
    public override void Initialize()=>Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var button=new Button{Content="更新 / Update",HorizontalAlignment=global::Avalonia.Layout.HorizontalAlignment.Left};
            var content=ControlScene.Create(button);int count=0;button.Click+=(_,_)=>button.Content=$"已点击 / Clicked {++count}";
            desktop.MainWindow=new Window{Title="SimdSkia · Avalonia 12.1.4 · Managed CPU",Width=1000,Height=760,Content=content};
            if(Smoke)
            {
                desktop.MainWindow.Position=new PixelPoint(-20000,-20000);desktop.MainWindow.ShowInTaskbar=false;
                desktop.MainWindow.Opened+=(_,_)=>global::Avalonia.Threading.DispatcherTimer.RunOnce(()=>
                {
                    try
                    {
                        if(SimdSkiaDiagnostics.FramesPresented==0)throw new InvalidOperationException("The real Win32 software window never presented a frame.");
                        if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name is "SkiaSharp" or "HarfBuzzSharp" or "Avalonia.Skia"))throw new InvalidOperationException("A native drawing/font backend was loaded.");
                        Directory.CreateDirectory("artifacts/avalonia-controls");using var bitmap=new RenderTargetBitmap(new PixelSize(1000,720),new Vector(96,96));bitmap.Render(content);
                        using var stream=File.Create("artifacts/avalonia-controls/win32.png");bitmap.Save(stream,PngBitmapEncoderOptions.Default);
                        Console.WriteLine($"PASS actual Win32 software window: {SimdSkiaDiagnostics.FramesPresented} presented frames; managed rendering/fonts only.");desktop.Shutdown();
                    }
                    catch(Exception error){Console.Error.WriteLine(error);desktop.Shutdown(1);}
                },TimeSpan.FromSeconds(2));
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
