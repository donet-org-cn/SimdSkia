using System.Collections.Concurrent;
using System.Diagnostics;

namespace SimdSkia.Avalonia;

public enum SimdSkiaGpuBackend { Vulkan, Direct2D }
public enum SimdSkiaGpuPresentation { NativeSwapchain, FramebufferReadback }
public enum SimdSkiaGpuTextMode { ManagedGlyphAtlas, GpuOutlines }

public static class SimdSkiaGpuOptions
{
    /// <summary>Prefers Direct2D native presentation on Windows, Vulkan on Linux, then managed CPU.
    /// Initialization failures and selected targets are reported in SimdSkiaGpuDiagnostics.
    /// macOS currently uses CPU until a window Metal backend is implemented and verified.</summary>
    public static void UseGpuPreferred(this SimdSkiaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);options.UseCpu();
        var preference=new GpuPreference();
        options.PreferredRenderTargetFactory=surfaces=>GpuPreferredTarget.Create(surfaces,preference);
    }
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
        options.PreferredRenderTargetFactory=null;
        if(selected==SimdSkiaGpuPresentation.NativeSwapchain){options.NativeRenderTargetFactory=surface=>new NativeWindowTarget(surface,allowCpuFallback,textMode);options.CanvasBackendFactory=null;}
        else{options.NativeRenderTargetFactory=null;options.CanvasBackendFactory = target => new WindowGpuCanvas(target, backend, allowCpuFallback,textMode:textMode);}
    }
}

/// <summary>CompletedBatches counts retired GPU work. CompletionMilliseconds is
/// cumulative host cost in flush/presentation calls, not GPU timestamp duration
/// or display latency; native queued/completed/pending frame counters are separate.</summary>
public sealed record SimdSkiaGpuStatistics(string Device, long GpuCommands, long CpuFallbackCommands, long CompletedBatches, long Uploads, double CompletionMilliseconds, IReadOnlyDictionary<string, long> FallbackReasons);

public static class SimdSkiaGpuDiagnostics
{
    private static int synchronizeNativeFrames;
    /// <summary>Diagnostic serialization of native frames. Normally false so CPU
    /// recording overlaps GPU execution. Set true and render another frame to
    /// drain pending work before reading completion statistics.</summary>
    public static bool SynchronizeNativeFrames{get=>Volatile.Read(ref synchronizeNativeFrames)!=0;set=>Volatile.Write(ref synchronizeNativeFrames,value?1:0);}
    private static long nativeSubmitted,nativeCompleted,nativePending,nativeWaitTicks,nativeOccluded;
    public static long NativeSubmittedFrames=>Interlocked.Read(ref nativeSubmitted);
    public static long NativeCompletedFrames=>Interlocked.Read(ref nativeCompleted);
    public static long NativePendingFrames=>Interlocked.Read(ref nativePending);
    public static double NativeCompletionWaitMilliseconds=>Interlocked.Read(ref nativeWaitTicks)/(double)TimeSpan.TicksPerMillisecond;
    public static long NativeOccludedPresents=>Interlocked.Read(ref nativeOccluded);
    private static long nativeStateChanges;
    public static long NativeStateChanges=>Interlocked.Read(ref nativeStateChanges);
    internal static void StateChanged(long count)=>Interlocked.Add(ref nativeStateChanges,count);
    internal static void NativeProgress(long submitted,long completed,long pending,double waitMs,long occluded)
    {Interlocked.Add(ref nativeSubmitted,submitted);Interlocked.Add(ref nativeCompleted,completed);Interlocked.Add(ref nativePending,pending);Interlocked.Add(ref nativeWaitTicks,(long)(waitMs*TimeSpan.TicksPerMillisecond));Interlocked.Add(ref nativeOccluded,occluded);}
    private static long initializationFailures,atlasEvictions;
    private static string selectedRenderer="Not initialized",lastInitializationFailure="";
    public static string SelectedRenderer=>Volatile.Read(ref selectedRenderer);
    public static string LastInitializationFailure=>Volatile.Read(ref lastInitializationFailure);
    public static long InitializationFailures=>Interlocked.Read(ref initializationFailures);
    public static long ManagedGlyphAtlasEvictions=>Interlocked.Read(ref atlasEvictions);
    internal static void Selected(string renderer)=>Volatile.Write(ref selectedRenderer,renderer);
    internal static void InitializationFailed(string renderer,Exception error)
    {
        string message=$"{renderer}: {error.GetType().Name}: {error.Message}";
        Volatile.Write(ref lastInitializationFailure,message);Interlocked.Increment(ref initializationFailures);
        Trace.TraceWarning("SimdSkia GPU selection: {0}",message);
    }
    internal static void AtlasEvicted()=>Interlocked.Increment(ref atlasEvictions);
    /// <summary>Opt-in, serializing GPU diagnostics. Leave disabled for performance measurements.</summary>
    public static bool CaptureVulkanProfiles {get;set;}
    private static global::SimdSkia.Gpu.VulkanProfile? lastVulkanProfile;
    public static global::SimdSkia.Gpu.VulkanProfile? LastVulkanProfile {get=>Volatile.Read(ref lastVulkanProfile);internal set=>Volatile.Write(ref lastVulkanProfile,value);}
    private static long maskBuilds,maskHits,maskPeakBytes,drawDispatches,drawBarriers;
    public static long VulkanPathMaskBuilds=>Interlocked.Read(ref maskBuilds);
    public static long VulkanPathMaskHits=>Interlocked.Read(ref maskHits);
    public static long VulkanPathMaskPeakBytes=>Interlocked.Read(ref maskPeakBytes);
    public static long VulkanDrawDispatches=>Interlocked.Read(ref drawDispatches);
    public static long VulkanDrawDependencyBarriers=>Interlocked.Read(ref drawBarriers);
    internal static void VulkanCounters(long builds,long hits,long bytes,long dispatches,long barriers)
    {
        Interlocked.Add(ref maskBuilds,builds);Interlocked.Add(ref maskHits,hits);Interlocked.Add(ref drawDispatches,dispatches);Interlocked.Add(ref drawBarriers,barriers);
        long previous;do{previous=Interlocked.Read(ref maskPeakBytes);if(previous>=bytes)break;}while(Interlocked.CompareExchange(ref maskPeakBytes,bytes,previous)!=previous);
    }
    private static string device = "";
    private static long gpu, cpu, batches, uploads, ticks;
    private static long nativePresents;
    private static long blockingWaits,completionPolls;
    private static long flipPresents;
    public static long FlipSwapchainPresents=>Interlocked.Read(ref flipPresents);
    internal static void FlipPresented()=>Interlocked.Increment(ref flipPresents);
    public static long BlockingCompletionWaits=>Interlocked.Read(ref blockingWaits);
    public static long CompletionPolls=>Interlocked.Read(ref completionPolls);
    internal static void CompletionMode(long waits,long polls){Interlocked.Add(ref blockingWaits,waits);Interlocked.Add(ref completionPolls,polls);}
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
    internal static void Completed(long elapsed,int count=1) { Interlocked.Add(ref batches,count); Interlocked.Add(ref ticks, elapsed); }
    public static SimdSkiaGpuStatistics GetStatistics() => new(Volatile.Read(ref device), Interlocked.Read(ref gpu), Interlocked.Read(ref cpu), Interlocked.Read(ref batches), Interlocked.Read(ref uploads), Interlocked.Read(ref ticks) * 1000d / Stopwatch.Frequency, new Dictionary<string, long>(reasons));
}
