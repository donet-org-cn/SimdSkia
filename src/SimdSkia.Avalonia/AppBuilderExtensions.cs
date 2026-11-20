using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;

namespace SimdSkia.Avalonia;

public sealed class SimdSkiaOptions
{
    /// <summary>Compiles common drawing paths during classic desktop JIT initialization. No window, font, or GPU context is created. Headless applications and Native AOT skip this work.</summary>
    public bool EnableStartupPreparation {get;set;}=true;
    /// <summary>Explicit TrueType glyf fonts; the first family is the default. An empty list discovers supported installed fonts.</summary>
    public IList<string> FontFiles {get;}=new List<string>();
    /// <summary>Optional persistent window renderer. Flush must publish completed pixels into the supplied buffer.</summary>
    public Func<SimdSkia.PixelBuffer,SimdSkia.ICanvasBackend>? CanvasBackendFactory {get;set;}
    /// <summary>Optional direct native-window GPU target. The drawing and lifetime contracts remain version-specific.</summary>
    public Func<INativePlatformHandleSurface,IRenderTarget>? NativeRenderTargetFactory {get;set;}
    internal Func<IPlatformGraphicsContext?,IReadOnlyList<IPlatformRenderSurface>,IRenderTarget>? PreferredRenderTargetFactory {get;set;}
    /// <summary>The preferred factory can use a platform GPU context (macOS
    /// Metal), so the OS host should create one.</summary>
    internal bool UsesPlatformGraphics {get;set;}
    /// <summary>Additional background JIT warm-up for the selected renderer.</summary>
    internal List<Action> Preparations {get;}=[];
}

public static class AppBuilderExtensions
{
    /// <summary>Explicitly selects managed CPU/SIMD rendering, including when the desktop host prefers GPU.</summary>
    public static void UseCpu(this SimdSkiaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.CanvasBackendFactory=null;options.NativeRenderTargetFactory=null;options.PreferredRenderTargetFactory=null;options.UsesPlatformGraphics=false;options.Preparations.Clear();
    }
    /// <summary>Uses installed static TrueType fonts on Windows, Linux and macOS, without bundled font files.</summary>
    public static AppBuilder UseSimdSkia(this AppBuilder builder)=>UseSimdSkia(builder,static _=>{});

    /// <summary>Replaces compatible Avalonia 12.x drawing and font services, starting at 12.1.0.</summary>
    public static AppBuilder UseSimdSkia(this AppBuilder builder,Action<SimdSkiaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);ArgumentNullException.ThrowIfNull(configure);
        var options=new SimdSkiaOptions();configure(options);return UseSimdSkia(builder,options);
    }

    /// <summary>Uses already configured options (the desktop host inspects them first).</summary>
    internal static AppBuilder UseSimdSkia(AppBuilder builder,SimdSkiaOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);ArgumentNullException.ThrowIfNull(options);
        AvaloniaCompatibility.Validate();
        var paths=options.FontFiles.ToArray();var factory=options.CanvasBackendFactory;var nativeFactory=options.NativeRenderTargetFactory;var preferredFactory=options.PreferredRenderTargetFactory;bool prepare=options.EnableStartupPreparation;var preparations=options.Preparations.ToArray();
        return builder.UseRenderingSubsystem(()=>
        {
            AvaloniaLocator.CurrentMutable.Bind<IPlatformRenderInterface>().ToConstant(new RenderInterface(factory,nativeFactory,preferredFactory));
            FontManager fontManager;
            if(paths.Length!=0)fontManager=new FontManager(paths);
            else
            {
                var system=SimdSkiaSystemFonts.Discover();
                fontManager=new FontManager(system.Files,system.DefaultFamily,supportedFacesOnly:true);
            }
            AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(fontManager);
        },"SimdSkia").UseTextShapingSubsystem(()=>AvaloniaLocator.CurrentMutable.Bind<ITextShaperImpl>().ToConstant(new TextShaper()),"SimdSkia.ManagedText")
            .AfterSetup(app=>
            {
                // Lifetime is assigned after platform services are initialized.
                // Prepare before OnFrameworkInitializationCompleted builds the
                // desktop UI; bitmap/headless users should pay no warmup cost.
                if(prepare&&app.Instance?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                    StartupPreparation.Start(preparations);
            });
    }
}
