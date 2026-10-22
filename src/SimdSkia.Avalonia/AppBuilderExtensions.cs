using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;

namespace SimdSkia.Avalonia;

public sealed class SimdSkiaOptions
{
    /// <summary>Compiles common managed drawing paths on a background worker during JIT startup. No window, font, or GPU context is created. Native AOT skips this work.</summary>
    public bool EnableStartupPreparation {get;set;}=true;
    /// <summary>Explicit TrueType glyf fonts; the first family is the default. An empty list discovers supported installed fonts.</summary>
    public IList<string> FontFiles {get;}=new List<string>();
    /// <summary>Optional persistent window renderer. Flush must publish completed pixels into the supplied buffer.</summary>
    public Func<SimdSkia.PixelBuffer,SimdSkia.ICanvasBackend>? CanvasBackendFactory {get;set;}
    /// <summary>Optional direct native-window GPU target. The drawing and lifetime contracts remain version-specific.</summary>
    public Func<INativePlatformHandleSurface,IRenderTarget>? NativeRenderTargetFactory {get;set;}
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
        var options=new SimdSkiaOptions();configure(options);var paths=options.FontFiles.ToArray();var factory=options.CanvasBackendFactory;var nativeFactory=options.NativeRenderTargetFactory;bool prepare=options.EnableStartupPreparation;
        return builder.UseRenderingSubsystem(()=>
        {
            if(prepare)StartupPreparation.Start();
            AvaloniaLocator.CurrentMutable.Bind<IPlatformRenderInterface>().ToConstant(new RenderInterface(factory,nativeFactory));
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
