using Avalonia;

namespace SimdSkia.Avalonia;

public static class SimdSkiaDesktopExtensions
{
    /// <summary>Selects an OS window host with software presentation, then installs SimdSkia drawing/fonts.</summary>
    public static AppBuilder UseSimdSkiaDesktop(this AppBuilder builder, Action<SimdSkiaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseStandardRuntimePlatformSubsystem();
        if (OperatingSystem.IsWindows())
            builder.UseWin32().With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        else if (OperatingSystem.IsLinux())
            builder.UseX11().With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        else if (OperatingSystem.IsMacOS())
            builder.UseAvaloniaNative().With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] });
        else throw new PlatformNotSupportedException("The desktop window hosts support Windows, Linux X11/XWayland and macOS.");
        return builder.UseSimdSkia(configure ?? (static _ => { }));
    }
}
