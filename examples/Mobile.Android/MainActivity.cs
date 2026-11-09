using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using SimdSkia.Avalonia;

namespace SimdSkia.MobileSample;

[Application]
public sealed class AndroidApplication(nint handle, JniHandleOwnership ownership)
    : AvaloniaAndroidApplication<DemoApp>(handle, ownership)
{
    protected override AppBuilder CreateAppBuilder()
    {
        string font = Path.Combine(FilesDir!.AbsolutePath, "ZCOOLXiaoWei-Regular.ttf");
        // The sample explicitly bundles an OFL static TrueType font. Many phones
        // provide CJK only as variable/CFF fonts which this engine cannot read.
        using (var input = Assets!.Open("Fonts/ZCOOLXiaoWei-Regular.ttf"))
        using (var output = File.Create(font))
            input.CopyTo(output);
        return AppBuilder.Configure<DemoApp>().UseSimdSkiaAndroid(options => options.FontFiles.Add(font));
    }
}

[Activity(Label = "SimdSkia Android", MainLauncher = true, Exported = true, Theme = "@style/SimdSkiaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnResume()
    {
        base.OnResume();
        (global::Avalonia.Application.Current as DemoApp)?.SetAnimationActive(true);
    }

    protected override void OnPause()
    {
        (global::Avalonia.Application.Current as DemoApp)?.SetAnimationActive(false);
        base.OnPause();
    }
}
