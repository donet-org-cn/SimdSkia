using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using SimdSkia.Gpu;

namespace SimdSkia.Avalonia;

// Background warm-up for the Metal window path, run by the startup preparation
// worker while the UI thread still loads styles. It draws one small frame into
// an offscreen shared buffer on the system device: every managed code path of
// the first real frame (window canvas, analytic shapes, cached paths, glyph
// atlas, device layers, GPU effects) is compiled before the window needs it,
// and the driver's pipeline cache is warm. It never touches a window or the
// host's graphics context.
internal static partial class MetalStartupPreparation
{
    [LibraryImport("/System/Library/Frameworks/Metal.framework/Metal")]
    private static partial nint MTLCreateSystemDefaultDevice();

    internal static void Prepare()
    {
        if(!OperatingSystem.IsMacOS())return;
        nint system=MTLCreateSystemDefaultDevice(),queue=0;
        if(system==0)return;
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            using(new MetalApi.Pool(true))queue=MetalApi.Retain(MetalApi.Call(system,"newCommandQueue"));
            if(queue==0)return;
            // newCommandQueue returns +1 and Retain added one more.
            nint extra=queue;MetalApi.Release(ref extra);
            using var device=new MetalDevice(system,queue);
            using var pixels=device.RentShared(64,64,true);
            using var canvas=new WindowGpuCanvas(pixels,SimdSkiaGpuBackend.Metal,true,metal:device);
            var brush=new ImmutableSolidColorBrush(Color.FromArgb(220,40,90,170));
            var pen=new ImmutablePen(brush,1.5);
            var round=new RoundedRect(new Rect(4,4,40,24),6);
            using(var context=new DrawingContext(pixels,new Vector(192,192),false,null,canvas,flushBackend:true))
            {
                context.Clear(Colors.Transparent);
                context.DrawRectangle(brush,null,new RoundedRect(new Rect(1,1,20,10)));
                context.DrawRectangle(brush,pen,round);
                context.DrawRectangle(null,pen,new RoundedRect(new Rect(2,30,30,12)));
                context.DrawEllipse(brush,pen,new Rect(30,30,20,14));
                context.PushClip(new RoundedRect(new Rect(0,0,60,60),8));
                context.PushOpacity(.7,new Rect(0,0,64,64));
                context.DrawRectangle(brush,null,new RoundedRect(new Rect(8,8,30,30),4));
                context.PopOpacity();
                context.PushEffect(new Rect(0,0,64,64),new ImmutableDropShadowEffect(0,2,8,Colors.Black,.4));
                context.DrawRectangle(brush,null,round);
                context.PopEffect();
                context.PopClip();
                using var bitmap=new Bitmap(new PixelBuffer(8,8,PixelFormat.Bgra8888,AlphaFormat.Premultiplied),new Vector(96,96));
                context.DrawBitmap(bitmap,1,new Rect(0,0,8,8),new Rect(40,4,16,16));
            }
            // Text goes through the managed glyph atlas without a font manager.
            var shape=Geometry.Rounded(new RoundedRect(new Rect(0,-8,6,8),1)).Path;
            var glyphs=new GlyphRunGeometry([new(shape,new PointF(0,0)),new(shape,new PointF(7,0))],Matrix3.Identity,GlyphPositioning.QuarterPixel);
            canvas.Execute(new DrawingCommand(CommandKind.DrawGlyphRun,default,new(1,0,10.25f,0,1,50,0,0,1),new(new Rgba32(220,220,220,255),CompositeMode.SourceOver,true)){GlyphRun=glyphs,TextMaskPreblend=true,CurveTolerance=.015625f});
            canvas.Flush();
            if(MetalDevice.Trace)Console.Error.WriteLine($"[metal] warm-up done in {System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms, gpu commands {SimdSkiaGpuDiagnostics.GetStatistics().GpuCommands}, cpu fallback {SimdSkiaGpuDiagnostics.GetStatistics().CpuFallbackCommands}");
        }
        catch(Exception error) when(MetalDevice.Trace){Console.Error.WriteLine("[metal] warm-up failed: "+error);throw;}
        finally{MetalApi.Release(ref queue);MetalApi.Release(ref system);}
    }
}
