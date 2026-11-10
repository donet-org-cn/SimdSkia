using Avalonia;

namespace SimdSkia.Avalonia;

public static class AndroidAppBuilderExtensions
{
    /// <summary>Configures the Android window/input host and managed CPU/SIMD rendering.</summary>
    public static AppBuilder UseSimdSkiaAndroid(this AppBuilder builder, Action<SimdSkiaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseAndroid()
            .With(new AndroidPlatformOptions { RenderingMode = [AndroidRenderingMode.Software] })
            .UseSimdSkia(configure ?? (static _ => { }));
    }
}
