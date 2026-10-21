using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SimdSkia.Avalonia;
using SkiaSharp;

internal static class Program
{
    internal static bool Hidden;
    internal static string Output="artifacts/nuget-consumer";
    [STAThread]
    public static int Main(string[] args)
    {
        Hidden=args.Contains("--hidden");
        int output=Array.IndexOf(args,"--output");if(output>=0)Output=args[output+1];Directory.CreateDirectory(Output);
        using(var bitmap=new SKBitmap(16,16))
        using(var canvas=new SKCanvas(bitmap))
        using(var paint=new SKPaint{Color=SKColors.Red})
        {
            canvas.Clear(SKColors.White);canvas.DrawRect(new SKRect(2,3,8,10),paint);
            if(bitmap.GetPixel(3,4)!=SKColors.Red||bitmap.GetPixel(0,0)!=SKColors.White)throw new InvalidOperationException("NuGet SK* drawing chain failed.");
        }
        var builder=AppBuilder.Configure<PreviewApp>().UseStandardRuntimePlatformSubsystem().UseWin32()
            .With(new Win32PlatformOptions{RenderingMode=[Win32RenderingMode.Software]});
        int font=Array.IndexOf(args,"--font");
        if(font>=0)builder.UseSimdSkia(options=>options.FontFiles.Add(Path.GetFullPath(args[font+1])));
        else builder.UseSimdSkia();
        return builder.StartWithClassicDesktopLifetime(args);
    }

    internal static void Verify(Window window,IClassicDesktopStyleApplicationLifetime lifetime)
    {
        try
        {
            if(SimdSkiaDiagnostics.FramesPresented==0)throw new InvalidOperationException("No real Win32 framebuffer presented.");
            using var target=new RenderTargetBitmap(new PixelSize(520,220),new Vector(96,96));target.Render(window);
            using(var png=File.Create(Path.Combine(Output,"window.png")))target.Save(png,PngBitmapEncoderOptions.Default);
            using var process=Process.GetCurrentProcess();var modules=process.Modules.Cast<ProcessModule>().Select(m=>m.ModuleName).ToArray();
            if(modules.Any(name=>name.Contains("SkiaSharp",StringComparison.OrdinalIgnoreCase)||name.Contains("HarfBuzz",StringComparison.OrdinalIgnoreCase)||name.Contains("libEGL",StringComparison.OrdinalIgnoreCase)||name.Contains("libGLES",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Original native renderer leaked into NuGet consumer.");
            byte[] pixels=new byte[520*220*4];var pin=GCHandle.Alloc(pixels,GCHandleType.Pinned);
            try{target.CopyPixels(new PixelRect(0,0,520,220),pin.AddrOfPinnedObject(),pixels.Length,520*4);}finally{pin.Free();}
            if(!pixels.Where((_,index)=>index%4!=3).Any(value=>value<100))throw new InvalidOperationException("NuGet consumer rendered a blank window.");
            var metrics=new Metrics(!RuntimeFeature.IsDynamicCodeSupported,SimdSkiaDiagnostics.FramesPresented,FontManager.Current.DefaultFontFamily.Name,modules,true);
            File.WriteAllText(Path.Combine(Output,"metrics.json"),JsonSerializer.Serialize(metrics,MetricsJsonContext.Default.Metrics));
            Console.WriteLine($"PASS actual NuGet consumer: SK* pixels, Avalonia Win32, {metrics.Frames} frames, font {metrics.FontFamily}, nativeAot={metrics.NativeAot}");
            lifetime.Shutdown();
        }
        catch(Exception error){Console.Error.WriteLine(error);lifetime.Shutdown(1);}
    }
}

internal sealed class PreviewApp:Application
{
    public override void Initialize()=>Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var panel=new StackPanel{Margin=new Thickness(20),Spacing=12,Background=Brushes.White};
            panel.Children.Add(new TextBlock{Text="SimdSkia NuGet preview / 中文界面",FontSize=24,Foreground=Brushes.Black});
            panel.Children.Add(new Button{Content="Managed CPU rendering",HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Left});
            var window=new Window{Title="SimdSkia NuGet preview",Width=520,Height=220,Background=Brushes.White,Content=panel};
            if(Program.Hidden){window.Position=new PixelPoint(-20000,-20000);window.WindowStartupLocation=WindowStartupLocation.Manual;window.ShowInTaskbar=false;}
            window.Opened+=(_,_)=>DispatcherTimer.RunOnce(()=>Program.Verify(window,desktop),TimeSpan.FromSeconds(1));
            desktop.MainWindow=window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
internal sealed record Metrics(bool NativeAot,long Frames,string FontFamily,string[] LoadedModules,bool PixelsVerified);
[JsonSourceGenerationOptions(WriteIndented=true)]
[JsonSerializable(typeof(Metrics))]
internal partial class MetricsJsonContext:JsonSerializerContext;
