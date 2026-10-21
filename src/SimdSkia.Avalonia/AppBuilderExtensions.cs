using Avalonia;
using Avalonia.Platform;

namespace SimdSkia.Avalonia;

public sealed class SimdSkiaOptions
{
    /// <summary>Explicit, redistributable TrueType glyf fonts; the first family is the default.</summary>
    public IList<string> FontFiles {get;}=new List<string>();
}

public static class AppBuilderExtensions
{
    /// <summary>Replaces Avalonia 12.1.4's drawing and font services with the managed CPU renderer.</summary>
    public static AppBuilder UseSimdSkia(this AppBuilder builder,Action<SimdSkiaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);ArgumentNullException.ThrowIfNull(configure);
        if(typeof(AppBuilder).Assembly.GetName().Version!=new Version(12,1,4,0))throw new NotSupportedException("This backend is validated only against Avalonia 12.1.4; its private rendering contracts require a version-specific adapter.");
        var options=new SimdSkiaOptions();configure(options);var paths=options.FontFiles.ToArray();
        return builder.UseRenderingSubsystem(()=>
        {
            AvaloniaLocator.CurrentMutable.Bind<IPlatformRenderInterface>().ToConstant(new RenderInterface());
            AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(new FontManager(paths));
        },"SimdSkia.Cpu").UseTextShapingSubsystem(()=>AvaloniaLocator.CurrentMutable.Bind<ITextShaperImpl>().ToConstant(new TextShaper()),"SimdSkia.ManagedText");
    }
}
