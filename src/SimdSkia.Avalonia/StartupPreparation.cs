using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using SimdSkia.Cpu;

namespace SimdSkia.Avalonia;

// The first UI frame otherwise pays for JIT compilation of the rasterizer,
// clipping, stroking and glyph cache while the host window is already visible.
// Exercise those actual paths while the UI thread is still loading its styles.
// This worker never waits for or touches a window, font manager or GPU context.
internal static class StartupPreparation
{
    private static readonly object gate=new();
    private static Task? pending;
    internal static Task? Pending {get {lock(gate)return pending;}}

    internal static void Start()
    {
        if(!RuntimeFeature.IsDynamicCodeSupported)return;
        lock(gate)
        {
            if(pending is not null)return;
            // A short-lived thread releases its stack/TLS after preparation and
            // does not start or keep an otherwise unused thread pool alive.
            var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            pending=completion.Task;
            var worker=new Thread(()=>
            {
                try{Prepare();completion.SetResult();}
                catch(Exception error)
                {
                    Trace.TraceError("SimdSkia startup preparation failed: {0}",error);
                    completion.SetException(error);_=completion.Task.Exception;
                }
            }){IsBackground=true,Name="SimdSkia.Startup"};
            try{worker.Start();}
            catch(Exception error){completion.SetException(error);_=completion.Task.Exception;throw;}
        }
    }

    private static void Prepare()
    {
        using var pixels=new PixelBuffer(16,16,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        using var raster=new RasterCanvas(pixels);
        var shape=Geometry.Rounded(new RoundedRect(new Rect(1,1,12,12),3));
        var brush=new ImmutableSolidColorBrush(Color.FromArgb(210,37,89,167));
        var pen=new ImmutablePen(brush,1);
        using(var context=new DrawingContext(pixels,new Vector(96,96),backend:raster))
        {
            context.Clear(Colors.Transparent);
            context.DrawGeometry(brush,pen,shape);
            context.PushGeometryClip(shape);
            context.DrawRectangle(brush,null,new RoundedRect(new Rect(0,0,14,14)));
            context.PushOpacity(.6,new Rect(0,0,16,16));
            context.DrawGeometry(brush,null,shape);
            context.PopOpacity();
            context.PopGeometryClip();
        }
        var glyphs=new GlyphRunGeometry([new(shape.Path,new PointF(0,0)),new(shape.Path,new PointF(4,0))],Matrix3.Identity,GlyphPositioning.QuarterPixel);
        raster.Execute(new DrawingCommand(CommandKind.DrawGlyphRun,default,Matrix3.Identity,new(new Rgba32(37,89,167,255),CompositeMode.SourceOver)){GlyphRun=glyphs,CurveTolerance=.015625f,CacheTextCoverage=true});
    }
}
