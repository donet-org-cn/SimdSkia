using System.Runtime.InteropServices;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;

namespace SimdSkia.Avalonia;

// Shared by windows belonging to one configured renderer. A device reported as
// lost is not recreated every render-loop tick. Surface restrictions stay local.
internal sealed class GpuPreference
{
    private readonly object gate=new();
    private readonly HashSet<SimdSkiaGpuBackend> unavailable=[];
    internal bool Allows(SimdSkiaGpuBackend backend){lock(gate)return !unavailable.Contains(backend);}
    internal void DeviceLost(SimdSkiaGpuBackend backend){lock(gate)unavailable.Add(backend);}
}

internal sealed class GpuPreferredTarget : IRenderTarget
{
    internal sealed record Candidate(string Name,SimdSkiaGpuBackend Backend,Func<IRenderTarget> Create);
    private readonly Candidate[] candidates;
    private readonly Func<IRenderTarget> createCpu;
    private readonly GpuPreference preference;
    private readonly Func<bool>? isReady;
    private IRenderTarget? selected;
    private Candidate? hardware;
    private bool disposed;

    internal GpuPreferredTarget(Candidate[] candidates,Func<IRenderTarget> createCpu,GpuPreference preference,Func<bool>? isReady=null)
    {this.candidates=candidates;this.createCpu=createCpu;this.preference=preference;this.isReady=isReady;}

    internal static IRenderTarget Create(IReadOnlyList<IPlatformRenderSurface> surfaces,GpuPreference preference)
    {
        var framebuffer=surfaces.OfType<IFramebufferPlatformSurface>().FirstOrDefault();
        var native=surfaces.OfType<INativePlatformHandleSurface>().FirstOrDefault();
        IRenderTarget Cpu()=>new FramebufferTarget((framebuffer??throw new NotSupportedException("GPU fallback requires an OS framebuffer surface.")).CreateFramebufferRenderTarget());
        IRenderTarget Readback(SimdSkiaGpuBackend backend)=>new FramebufferTarget(framebuffer!.CreateFramebufferRenderTarget(),
            pixels=>new WindowGpuCanvas(pixels,backend,true));
        var choices=new List<Candidate>();
        if(OperatingSystem.IsWindows())
        {
            if(native is not null)choices.Add(new("Direct2D native swapchain",SimdSkiaGpuBackend.Direct2D,()=>new NativeWindowTarget(native,true,SimdSkiaGpuTextMode.ManagedGlyphAtlas)));
            // Layered/transparent windows and hosts without an HWND can still use hardware drawing.
            if(framebuffer is not null)choices.Add(new("Direct2D framebuffer readback",SimdSkiaGpuBackend.Direct2D,()=>Readback(SimdSkiaGpuBackend.Direct2D)));
        }
        if((OperatingSystem.IsWindows()||OperatingSystem.IsLinux())&&framebuffer is not null)
            choices.Add(new("Vulkan framebuffer readback",SimdSkiaGpuBackend.Vulkan,()=>Readback(SimdSkiaGpuBackend.Vulkan)));
        if(choices.Count==0){SimdSkiaGpuDiagnostics.Selected("CPU/SIMD (no verified window GPU backend)");return Cpu();}
        return new GpuPreferredTarget(choices.ToArray(),Cpu,preference,()=>native is null||native.IsReady);
    }

    public RenderTargetProperties Properties=>new(){RetainsPreviousFrameContents=true,IsSuitableForDirectRendering=true};
    public PlatformRenderTargetState PlatformRenderTargetState
    {
        get
        {
            if(disposed)return PlatformRenderTargetState.Disposed;
            if(selected is null&&isReady?.Invoke()==false)return new(){IsReady=false};
            var state=selected?.PlatformRenderTargetState??PlatformRenderTargetState.Ready;
            if(state.IsCorrupted&&selected is IDeviceFaultSource{IsDeviceFaulted:true}&&hardware is {} gpu)
            {
                preference.DeviceLost(gpu.Backend);
                SimdSkiaGpuDiagnostics.InitializationFailed(gpu.Name,new InvalidOperationException("Device/target was lost; the next target skips this backend."));
                hardware=null;
            }
            return state;
        }
    }
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo scene,out RenderTargetDrawingContextProperties properties)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(selected is not null)return selected.CreateDrawingContext(scene,out properties);
        foreach(var candidate in candidates)
        {
            if(!preference.Allows(candidate.Backend))continue;
            IRenderTarget? attempt=null;
            try
            {
                attempt=candidate.Create();
                // Actual device/pipeline creation is lazy in the OS targets. Test
                // the first drawing context before declaring GPU selection success.
                var context=attempt.CreateDrawingContext(scene,out properties);
                selected=attempt;hardware=candidate;SimdSkiaGpuDiagnostics.Selected(candidate.Name);return context;
            }
            catch(Exception error) when(IsUnavailable(error))
            {
                attempt?.Dispose();
                SimdSkiaGpuDiagnostics.InitializationFailed(candidate.Name,error);
                // A missing loader affects all windows. A layered window or target
                // size restriction does not establish that the device is unavailable.
                if(error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
                    preference.DeviceLost(candidate.Backend);
            }
            catch{attempt?.Dispose();throw;}
        }
        selected=createCpu();SimdSkiaGpuDiagnostics.Selected("CPU/SIMD (GPU preferred fallback)");
        return selected.CreateDrawingContext(scene,out properties);
    }
    private static bool IsUnavailable(Exception error)=>error is NotSupportedException or DllNotFoundException
        or EntryPointNotFoundException or BadImageFormatException or ExternalException or global::SimdSkia.Gpu.VulkanDeviceLostException
        ||error is InvalidOperationException and not ObjectDisposedException;
    public void Dispose(){if(disposed)return;selected?.Dispose();selected=null;disposed=true;}
}
