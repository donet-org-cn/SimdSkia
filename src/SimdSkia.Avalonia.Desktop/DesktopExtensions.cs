using Avalonia;

namespace SimdSkia.Avalonia;

public static class SimdSkiaDesktopExtensions
{
    /// <summary>Selects an OS window host, preferring supported hardware rendering with diagnosed CPU fallback.</summary>
    public static AppBuilder UseSimdSkiaDesktop(this AppBuilder builder, Action<SimdSkiaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Options are resolved first: the macOS host must know whether to create
        // a Metal device and CAMetalLayer surfaces.
        var options=new SimdSkiaOptions();
        options.UseGpuPreferred();
        configure?.Invoke(options);
        // Existing explicit canvas/native factories remain valid overrides.
        if(options.CanvasBackendFactory is not null||options.NativeRenderTargetFactory is not null)
        {options.PreferredRenderTargetFactory=null;options.UsesPlatformGraphics=false;}
        builder.UseStandardRuntimePlatformSubsystem();
        if (OperatingSystem.IsWindows())
            builder.UseWin32().With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        else if (OperatingSystem.IsLinux())
            builder.UseX11().With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        else if (OperatingSystem.IsMacOS())
            // Metal mode gives SimdSkia the host's device and CAMetalLayer; every
            // window still offers its framebuffer surface for CPU fallback.
            builder.UseAvaloniaNative().With(new AvaloniaNativePlatformOptions { RenderingMode = options.UsesPlatformGraphics
                ? [AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.Software]
                : [AvaloniaNativeRenderingMode.Software] });
        else throw new PlatformNotSupportedException("The desktop window hosts support Windows, Linux X11/XWayland and macOS.");
        return AppBuilderExtensions.UseSimdSkia(builder,options);
    }
}
