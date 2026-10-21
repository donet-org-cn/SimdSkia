using System.Collections.Concurrent;
using System.Diagnostics;

namespace SimdSkia.Avalonia;

public enum SimdSkiaGpuBackend { Vulkan, Direct2D }
public enum SimdSkiaGpuPresentation { NativeSwapchain, FramebufferReadback }
public enum SimdSkiaGpuTextMode { ManagedGlyphAtlas, GpuOutlines }

public static class SimdSkiaGpuOptions
{
    /// <summary>Enables hardware drawing. Direct2D defaults to a native swapchain; Vulkan uses completed-pixel framebuffer readback.</summary>
    public static void UseGpu(this SimdSkiaOptions options, SimdSkiaGpuBackend backend, bool allowCpuFallback = false, SimdSkiaGpuPresentation? presentation = null, SimdSkiaGpuTextMode textMode=SimdSkiaGpuTextMode.ManagedGlyphAtlas)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(backend)) throw new ArgumentOutOfRangeException(nameof(backend));
        if(presentation.HasValue&&!Enum.IsDefined(presentation.Value))throw new ArgumentOutOfRangeException(nameof(presentation));
        if(!Enum.IsDefined(textMode))throw new ArgumentOutOfRangeException(nameof(textMode));
        if (backend == SimdSkiaGpuBackend.Direct2D && !OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Direct2D requires Windows.");
        if (backend == SimdSkiaGpuBackend.Vulkan && !OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This Vulkan backend supports Windows/Linux. macOS GPU/Metal is not implemented.");
        var selected=presentation??(backend==SimdSkiaGpuBackend.Direct2D?SimdSkiaGpuPresentation.NativeSwapchain:SimdSkiaGpuPresentation.FramebufferReadback);
        if(selected==SimdSkiaGpuPresentation.NativeSwapchain&&backend!=SimdSkiaGpuBackend.Direct2D)throw new NotSupportedException("Native swapchain presentation is currently implemented only by Direct2D on Windows.");
        if(selected==SimdSkiaGpuPresentation.NativeSwapchain){options.NativeRenderTargetFactory=surface=>new NativeWindowTarget(surface,allowCpuFallback,textMode);options.CanvasBackendFactory=null;}
        else{options.NativeRenderTargetFactory=null;options.CanvasBackendFactory = target => new WindowGpuCanvas(target, backend, allowCpuFallback,textMode:textMode);}
    }
}

public sealed record SimdSkiaGpuStatistics(string Device, long GpuCommands, long CpuFallbackCommands, long CompletedBatches, long Uploads, double CompletionMilliseconds, IReadOnlyDictionary<string, long> FallbackReasons);

public static class SimdSkiaGpuDiagnostics
{
    private static string device = "";
    private static long gpu, cpu, batches, uploads, ticks;
    private static long nativePresents;
    private static long atlasBuilds,atlasHits;
    public static long ManagedGlyphAtlasBuilds=>Interlocked.Read(ref atlasBuilds);
    public static long ManagedGlyphAtlasHits=>Interlocked.Read(ref atlasHits);
    internal static void AtlasBuilt()=>Interlocked.Increment(ref atlasBuilds);
    internal static void AtlasHit()=>Interlocked.Increment(ref atlasHits);
    public static long NativeSwapchainPresents=>Interlocked.Read(ref nativePresents);
    internal static void NativePresented()=>Interlocked.Increment(ref nativePresents);
    private static readonly ConcurrentDictionary<string, long> reasons = new();
    internal static void SetDevice(string value) => Volatile.Write(ref device, value);
    internal static void GpuCommand() => Interlocked.Increment(ref gpu);
    internal static void CpuCommand(string reason) { Interlocked.Increment(ref cpu); reasons.AddOrUpdate(reason, 1, static (_, n) => n + 1); }
    internal static void Uploaded() => Interlocked.Increment(ref uploads);
    internal static void Completed(long elapsed) { Interlocked.Increment(ref batches); Interlocked.Add(ref ticks, elapsed); }
    public static SimdSkiaGpuStatistics GetStatistics() => new(Volatile.Read(ref device), Interlocked.Read(ref gpu), Interlocked.Read(ref cpu), Interlocked.Read(ref batches), Interlocked.Read(ref uploads), Interlocked.Read(ref ticks) * 1000d / Stopwatch.Frequency, new Dictionary<string, long>(reasons));
}
