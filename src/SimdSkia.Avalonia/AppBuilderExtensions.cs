using Avalonia;
using Avalonia.Platform;

namespace SimdSkia.Avalonia;

public sealed class SimdSkiaOptions
{
    /// <summary>Explicit TrueType glyf fonts; the first family is the default. An empty list discovers supported installed fonts.</summary>
    public IList<string> FontFiles {get;}=new List<string>();
}

public static class AppBuilderExtensions
{
    /// <summary>Uses installed static TrueType fonts on Windows, Linux and macOS, without bundled font files.</summary>
    public static AppBuilder UseSimdSkia(this AppBuilder builder)=>UseSimdSkia(builder,static _=>{});

    /// <summary>Replaces Avalonia 12.1.4's drawing and font services with the managed CPU renderer.</summary>
    public static AppBuilder UseSimdSkia(this AppBuilder builder,Action<SimdSkiaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);ArgumentNullException.ThrowIfNull(configure);
        if(typeof(AppBuilder).Assembly.GetName().Version!=new Version(12,1,4,0))throw new NotSupportedException("This backend is validated only against Avalonia 12.1.4; its private rendering contracts require a version-specific adapter.");
        var options=new SimdSkiaOptions();configure(options);var paths=options.FontFiles.ToArray();
        return builder.UseRenderingSubsystem(()=>
        {
            AvaloniaLocator.CurrentMutable.Bind<IPlatformRenderInterface>().ToConstant(new RenderInterface());
            FontManager fontManager;
            if(paths.Length!=0)fontManager=new FontManager(paths);
            else
            {
                var system=SimdSkiaSystemFonts.Discover();
                fontManager=new FontManager(system.Files,system.DefaultFamily,supportedFacesOnly:true);
            }
            AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(fontManager);
        },"SimdSkia.Cpu").UseTextShapingSubsystem(()=>AvaloniaLocator.CurrentMutable.Bind<ITextShaperImpl>().ToConstant(new TextShaper()),"SimdSkia.ManagedText");
    }
}
