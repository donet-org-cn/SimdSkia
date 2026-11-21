using Avalonia;
using Avalonia.Media;
using SimdSkia;
using SimdSkia.Avalonia;
using SimdSkia.Cpu;
using AlphaFormat=SimdSkia.AlphaFormat;
using PathGeometry=SimdSkia.PathGeometry;
using PixelFormat=SimdSkia.PixelFormat;

internal static class Direct2DRadialValidation
{
    internal static void Run()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Direct2D requires Windows.");
        const int width=203,height=144;
        var bounds=new Rect(12,14,164,104);
        var rect=new RectF(12,14,176,118);
        using var actual=new PixelBuffer(width,height,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        using var expected=new PixelBuffer(width,height,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        using var gpu=new WindowGpuCanvas(actual,SimdSkiaGpuBackend.Direct2D,fallback:true);
        using var cpu=new RasterCanvas(expected);
        var brush=new RadialGradientBrush
        {
            Center=new RelativePoint(.45,.52,RelativeUnit.Relative),
            RadiusX=new RelativeScalar(.43,RelativeUnit.Relative),
            RadiusY=new RelativeScalar(.37,RelativeUnit.Relative),
            GradientStops=new GradientStops
            {
                new(Color.FromArgb(192,243,46,76),0),
                new(Color.FromArgb(255,42,181,98),.37),
                new(Color.FromArgb(147,22,83,234),1)
            }
        };
        int maximum=0,drivenByGpu=0;
        foreach(bool focal in new[]{false,true})
        foreach(bool path in new[]{false,true})
        foreach(double opacity in new[]{1,.6})
        foreach(var spread in new[]{GradientSpreadMethod.Pad,GradientSpreadMethod.Reflect})
        {
            brush.GradientOrigin=focal?new RelativePoint(.31,.58,RelativeUnit.Relative):brush.Center;
            brush.Opacity=opacity;
            brush.SpreadMethod=spread;
            var paint=BrushFactory.Create(brush,bounds,false);
            var command=path
                ? new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,paint){Path=PathGeometry.Rectangle(rect)}
                : DrawingCommand.FillRectangle(rect,Matrix3.Identity,paint);
            int error=Compare(command,expectFallback:false);
            maximum=Math.Max(maximum,error);drivenByGpu++;
            if(error>3)throw new InvalidOperationException($"Direct2D radial error {error}, focal {focal}, path {path}, opacity {opacity}, spread {spread}.");
        }
        brush.GradientOrigin=new RelativePoint(2,2,RelativeUnit.Relative);
        int outside=Compare(DrawingCommand.FillRectangle(rect,Matrix3.Identity,BrushFactory.Create(brush,bounds,false)),true);
        brush.GradientOrigin=brush.Center;
        brush.Transform=new ScaleTransform(1.25,1.1);
        int transformed=Compare(DrawingCommand.FillRectangle(rect,Matrix3.Identity,BrushFactory.Create(brush,bounds,false)),true);
        brush.Transform=null;
        brush.SpreadMethod=GradientSpreadMethod.Repeat;
        int repeat=Compare(DrawingCommand.FillRectangle(rect,Matrix3.Identity,BrushFactory.Create(brush,bounds,false)),true);
        if(outside!=0||transformed!=0||repeat!=0)throw new InvalidOperationException("Unsupported radial fallback did not preserve managed pixels.");
        Console.WriteLine($"PASS {drivenByGpu} native radial commands and 3 exact CPU fallbacks on {SimdSkiaGpuDiagnostics.GetStatistics().Device}; max channel delta {maximum}.");

        brush.SpreadMethod=GradientSpreadMethod.Pad;
        var linear=new LinearGradientBrush();
        int extended=0;
        foreach(int count in new[]{257,1025})
        foreach(var gradient in new GradientBrush[]{linear,brush})
        {
            var stops=new GradientStops();
            for(int i=0;i<count;i++)
            {
                double offset=(double)i/(count-1),ramp=1-Math.Abs(offset-.73)/.73;
                stops.Add(new(Color.FromRgb((byte)(29+193*ramp),(byte)(240-190*ramp),(byte)(50+176*ramp)),offset));
            }
            gradient.GradientStops=stops;
            int error=Compare(DrawingCommand.FillRectangle(rect,Matrix3.Identity,BrushFactory.Create(gradient,bounds,false)),false);
            if(error>3)throw new InvalidOperationException($"Direct2D extended {gradient.GetType().Name} error {error}, {count} stops.");
            extended++;
        }
        Console.WriteLine($"PASS {extended} Direct2D gradient draws with 257 and 1025 stops.");

        int Compare(DrawingCommand command,bool expectFallback)
        {
            var clear=DrawingCommand.Clear(new(18,29,41,255));
            gpu.Execute(clear);cpu.Execute(clear);
            var before=SimdSkiaGpuDiagnostics.GetStatistics();
            gpu.Execute(command);gpu.Flush();cpu.Execute(command);
            var after=SimdSkiaGpuDiagnostics.GetStatistics();
            if(after.GpuCommands-before.GpuCommands!=(expectFallback?0:1)||
                after.CpuFallbackCommands-before.CpuFallbackCommands!=(expectFallback?1:0))
                throw new InvalidOperationException("Radial brush executed on the wrong backend.");
            int error=0;
            var a=actual.GetPixelSpan();var b=expected.GetPixelSpan();
            for(int i=0;i<a.Length;i++)error=Math.Max(error,Math.Abs(a[i]-b[i]));
            return error;
        }
    }
}
