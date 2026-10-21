using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;

namespace SimdSkia.Avalonia;

internal sealed partial class NativeWindowTarget(INativePlatformHandleSurface surface, bool fallback,SimdSkiaGpuTextMode textMode) : IRenderTarget
{
    private PixelBuffer? pixels;
    private WindowGpuCanvas? canvas;
    private bool disposed,active,retained;
    // The sequential blit-model swapchain preserves its backbuffer across Present.
    // This lets Avalonia draw controls directly instead of inserting a CPU raster bitmap.
    public RenderTargetProperties Properties => new(){RetainsPreviousFrameContents=true,IsSuitableForDirectRendering=true};
    public PlatformRenderTargetState PlatformRenderTargetState=>disposed?PlatformRenderTargetState.Disposed:canvas?.IsFaulted==true?new(){IsCorrupted=true}:surface.IsReady?PlatformRenderTargetState.Ready:new(){IsReady=false};
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo scene,out RenderTargetDrawingContextProperties properties)
    {
        ObjectDisposedException.ThrowIf(disposed,this);if(active)throw new InvalidOperationException("The native GPU target is already drawing.");
        long started=SimdSkiaDiagnostics.FrameStarted();
        if(!OperatingSystem.IsWindows()||surface.Handle==nint.Zero)throw new PlatformNotSupportedException("Direct2D native presentation requires a live Windows HWND.");
        if((GetWindowLong(surface.Handle,-20)&0x80000)!=0)throw new NotSupportedException("Per-pixel transparent/layered windows require framebuffer presentation; select Direct2D readback presentation explicitly.");
        var size=surface.Size;
        if(size.Width<=0||size.Height<=0)throw new InvalidOperationException("The native window has no drawable area.");
        if(pixels is null||pixels.Width!=size.Width||pixels.Height!=size.Height)
        {
            canvas?.Dispose();canvas=null;pixels?.Dispose();pixels=PixelBuffer.Rent(size.Width,size.Height);retained=false;
            canvas=new WindowGpuCanvas(pixels,SimdSkiaGpuBackend.Direct2D,fallback,surface.Handle,textMode);
        }
        properties=new(){PreviousFrameIsRetained=retained};active=true;
        return new DrawingContext(pixels,new Vector(surface.Scaling*96,surface.Scaling*96),false,success=>
        {
            bool complete=false;
            try{if(success){canvas!.Present();retained=true;SimdSkiaDiagnostics.Presented(pixels);complete=true;}else retained=false;}
            finally{active=false;if(complete)SimdSkiaDiagnostics.FrameCompleted(started);else retained=false;}
        },canvas,flushBackend:false);
    }
    public void Dispose()
    {
        if(disposed)return;if(active)throw new InvalidOperationException("Dispose the active GPU drawing context first.");
        disposed=true;try{canvas?.Dispose();}finally{pixels?.Dispose();}
    }
    [LibraryImport("user32.dll",EntryPoint="GetWindowLongW")]
    private static partial int GetWindowLong(nint hwnd,int index);
}
