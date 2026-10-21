using System.Globalization;

namespace SimdSkia.AotComparison;

internal sealed record Options(string? Output, string Scene, double Scale, string Mode,
    double Seconds, double Warmup, bool Hidden, bool FullOpacity)
{
    internal static Options Parse(string[] args)
    {
        string? output = null; string scene = "controls", mode = "throughput";
        double scale = 1, seconds = 5, warmup = 2; bool hidden = false, full = false;
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing option value.");
            switch (args[i])
            {
                case "--output": output = Path.GetFullPath(Value()); break;
                case "--scene": scene = Value(); break;
                case "--scale": scale = double.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--mode": mode = Value(); break;
                case "--seconds": seconds = double.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--warmup": warmup = double.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--hidden": hidden = true; break;
                case "--full-opacity": full = true; break;
                default: throw new ArgumentException("Unknown option: " + args[i]);
            }
        }
        if (scene is not ("controls" or "scroll") || mode is not ("throughput" or "paced"))
            throw new ArgumentException("Scene must be controls/scroll; mode must be throughput/paced.");
        if (!double.IsFinite(scale) || scale is < .5 or > 3 || !double.IsFinite(seconds) || seconds is < .1 or > 120
            || !double.IsFinite(warmup) || warmup is < 0 or > 30)
            throw new ArgumentOutOfRangeException(nameof(args));
        if (hidden && output is null) throw new ArgumentException("--hidden requires --output so the application exits automatically.");
        return new(output, scene, scale, mode, seconds, warmup, hidden, full);
    }
}
