using Avalonia;
using SimdSkia.Avalonia;

namespace VelaShell.Terminal.RenderTests;

internal static class ProbeTerminalRenderer
{
    internal static AppBuilder UseVelaTestRenderer(this AppBuilder builder) =>
        Environment.GetEnvironmentVariable("VELASHELL_PROBE_BACKEND") == "cpu"
            ? builder.UseSimdSkia()
            : builder.UseSkia();
}
