using SkiaSharp;
using SimdSkia;
using SimdSkia.Benchmarks;

// Managed GPU worker: SimdSkia Vulkan compute path vs original SkiaSharp GPU dispatch.
int engineIndex = Array.IndexOf(args, "--engine");
if (engineIndex >= 0 && engineIndex + 1 < args.Length && args[engineIndex + 1] != "simd")
    throw new ArgumentException("Managed GPU worker needs --engine simd.");

string description;
using (var probe = SimdSkia.VulkanSurface.Create(1, 1))
{
    probe.Canvas.Clear();
    probe.Flush();
    description = $"SimdSkia Vulkan compute ({probe.DeviceName})";
}

return GpuWorker.Run(args, (width, height) => new SimdSurfaceTarget(SimdSkia.VulkanSurface.Create(width, height)), description);

file sealed class SimdSurfaceTarget : IGpuTarget
{
    private readonly VulkanSurface surface;

    public SimdSurfaceTarget(VulkanSurface surface) => this.surface = surface;

    public SKCanvas Canvas => surface.Canvas;
    public void Present() => surface.Flush();
    public SKBitmap Capture() => surface.CopyBitmap();
    public void Dispose() => surface.Dispose();
}
