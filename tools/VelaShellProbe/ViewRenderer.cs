using Avalonia;
using SimdSkia.Avalonia;

namespace VelaShell.Tests.Views;

internal static class ProbeViewRenderer
{
    internal static AppBuilder UseVelaViewRenderer(this AppBuilder builder) =>
        Environment.GetEnvironmentVariable("VELASHELL_PROBE_BACKEND") == "cpu"
            ? builder.UseSimdSkia()
            : builder.UseSkia();
}
