namespace SimdSkia.Gpu;

/// <summary>Opt-in diagnostic timestamps in device ticks, not CPU milliseconds.
/// Timestamp instrumentation serializes measured dispatches and must be disabled for benchmarks.</summary>
public sealed record VulkanDispatchProfile(CommandKind Kind,bool Antialias,int Width,int Height,int EdgeCount,ulong DeviceTicks)
{
    public Rgba32 Color {get;init;}
    public CompositeMode Composite {get;init;}
    public bool Shader {get;init;}
}

public sealed record VulkanProfile(ulong InitializationTicks,ulong DrawingTicks,ulong ReadbackTicks,
    double RecordingMilliseconds,double FenceMilliseconds,double HostCopyMilliseconds,
    IReadOnlyList<VulkanDispatchProfile> Dispatches);
