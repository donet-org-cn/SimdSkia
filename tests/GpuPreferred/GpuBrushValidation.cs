using Avalonia;
using Avalonia.Media;
using System.Runtime.InteropServices;
using SimdSkia;
using SimdSkia.Avalonia;
using SimdSkia.Cpu;
using SimdSkia.Gpu;
using PathGeometry=SimdSkia.PathGeometry;

internal static partial class GpuBrushValidation
{
    [LibraryImport("/System/Library/Frameworks/Metal.framework/Metal")]
    private static partial nint MTLCreateSystemDefaultDevice();

    internal static void CheckMetal()
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Metal tests require macOS.");
        nint system=MTLCreateSystemDefaultDevice(),queue=0;
        if(system==0)
        {
            Console.WriteLine("SKIP Metal brush tests: the macOS host has no Metal device.");
            return;
        }
        try
        {
            using var pool=new MetalApi.Pool(true);
            queue=MetalApi.Call(system,"newCommandQueue");
            using var device=new MetalDevice(system,queue);
            CheckGradients(SimdSkiaGpuBackend.Metal,device);
            CheckTiles(SimdSkiaGpuBackend.Metal,device);
        }
        finally{MetalApi.Release(ref queue);MetalApi.Release(ref system);}
    }

    internal static void CheckGradients(SimdSkiaGpuBackend backend,MetalDevice? metal=null)
    {
        const int width=221,height=165;
        var bounds=new Rect(12,14,181,131);
        var rect=new RectF(12,14,193,145);
        using var actual=metal?.RentShared(width,height,true)??PixelBuffer.Rent(width,height);
        using var expected=PixelBuffer.Rent(width,height,actual.Format);
        using var gpu=new WindowGpuCanvas(actual,backend,fallback:true,metal:metal);
        using var cpu=new RasterCanvas(expected);
        var center=new RelativePoint(.47,.53,RelativeUnit.Relative);
        var first=Color.FromArgb(193,241,61,98);
        var last=Color.FromArgb(155,48,193,216);
        var radial=new RadialGradientBrush { Center=center,RadiusX=new(.38,RelativeUnit.Relative),
            RadiusY=new(.45,RelativeUnit.Relative),GradientStops=new(){new(first,0),new(Colors.Lime,.48),new(last,1)} };
        var conic=new ConicGradientBrush { Center=center,Angle=46,GradientStops=new(){new(first,0),new(last,.53),new(first,1)} };
        int max=0,accelerated=0;
        foreach(bool path in new[]{false,true})
        foreach(bool transformed in new[]{false,true})
        foreach(var spread in Enum.GetValues<GradientSpreadMethod>())
        {
            radial.SpreadMethod=spread;
            radial.GradientOrigin=path?new(.31,.55,RelativeUnit.Relative):center;
            radial.Transform=transformed?new ScaleTransform(1.13,.85):null;
            conic.SpreadMethod=spread;
            conic.Transform=radial.Transform;
            conic.Opacity=transformed?.6:1;
            Compare(radial,path,spread==GradientSpreadMethod.Repeat);
            Compare(conic,path,false);
        }
        conic.GradientStops[^1]=new(last,1);
        Compare(conic,false,true);
        radial.SpreadMethod=GradientSpreadMethod.Pad;
        radial.GradientOrigin=new(2,2,RelativeUnit.Relative);
        Compare(radial,false,true);
        radial.GradientOrigin=center;
        Compare(radial,false,true,independent:true);
        Console.WriteLine($"PASS {accelerated} {backend} radial/conic gradient draws; max channel delta {max}; exact fallback. {SimdSkiaGpuDiagnostics.GetStatistics().Device}");
        CheckExtendedStops();

        void CheckExtendedStops()
        {
            int count=0;
            var linear=new LinearGradientBrush();
            var clipGeometry=PathGeometry.Rectangle(new(35,20,179,129));
            var mask=cpu.CreateClip(clipGeometry,Matrix3.Identity,true);
            foreach(int stops in new[]{32,33,257,1025})
            foreach(var brush in new GradientBrush[]{linear,radial,conic})
            {
                var colors=new GradientStops();
                for(int i=0;i<stops;i++)
                {
                    var color=i==0||i==stops-1?Colors.Purple:
                        Color.FromArgb((byte)(138+i%101),(byte)(46+i%181),(byte)(23+i%197),(byte)(61+i%160));
                    colors.Add(new(color,(double)i/(stops-1)));
                }
                brush.GradientStops=colors;
                Compare(brush,false,false);
                Compare(brush,true,false,coverage:mask);
                count+=2;
            }
            Console.WriteLine($"PASS {count} {backend} gradient draws with 32 to 1025 stops, including path and coverage storage.");
        }

        void Compare(GradientBrush brush,bool path,bool fallback,bool independent=false,CoverageClip? coverage=null)
        {
            var clear=DrawingCommand.Clear(new(32,17,41,255));
            gpu.Execute(clear);cpu.Execute(clear);
            var before=SimdSkiaGpuDiagnostics.GetStatistics();
            var paint=BrushFactory.Create(brush,bounds,false);
            var draw=path?new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,paint){Path=PathGeometry.Rectangle(rect)}:
                DrawingCommand.FillRectangle(rect,Matrix3.Identity,paint);
            if(coverage is not null)draw=draw with {CoverageClip=coverage};
            if(independent)draw=draw with {ShaderTransform=Matrix3.Identity with {M02=5}};
            gpu.Execute(draw);gpu.Flush();cpu.Execute(draw);
            var after=SimdSkiaGpuDiagnostics.GetStatistics();
            if(after.GpuCommands-before.GpuCommands!=(fallback?0:1)||after.CpuFallbackCommands-before.CpuFallbackCommands!=(fallback?1:0))
                throw new InvalidOperationException("Unexpected gradient dispatch or fallback.");
            int error=0;
            var a=actual.GetPixelSpan();var b=expected.GetPixelSpan();
            for(int i=0;i<a.Length;i++)error=Math.Max(error,Math.Abs(a[i]-b[i]));
            if(error>(fallback?0:3))
                throw new InvalidOperationException($"Gradient difference {error}: {brush.GetType().Name}, {brush.SpreadMethod}, path {path}, fallback {fallback}.");
            if(!fallback){max=Math.Max(max,error);accelerated++;}
        }

    }

    internal static void CheckTiles(SimdSkiaGpuBackend backend,MetalDevice? metal=null)
    {
        const int width=221,height=165;
        var rect=new RectF(12,14,193,145);
        using var actual=metal?.RentShared(width,height,true)??PixelBuffer.Rent(width,height);
        using var expected=PixelBuffer.Rent(width,height,actual.Format);
        using var gpu=new WindowGpuCanvas(actual,backend,fallback:true,metal:metal);
        using var cpu=new RasterCanvas(expected);
        using var image=PixelBuffer.Rent(7,5);
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
            image.WritePixel(x,y,new((byte)(x*29+16),(byte)(y*33+25),(byte)(x*19+y*22),(byte)(x*21+y*19)));
        int accelerated=0,fallbacks=0,maximumDifference=0;
        foreach(var mode in Enum.GetValues<TileMode>())
        foreach(bool path in new[]{false,true})
        foreach(var (sampling,offset,accelerate) in new (ImageSampling,double,bool)[]
        {
            (ImageSampling.Nearest,-29,true),(ImageSampling.Bilinear,-29,true),
            (ImageSampling.Bilinear,-29.25,false),(ImageSampling.Mitchell,-29,false)
        })
        {
            var clear=DrawingCommand.Clear(new(32,17,41,255));
            gpu.Execute(clear);cpu.Execute(clear);
            var begin=SimdSkiaGpuDiagnostics.GetStatistics();
            using(var shader=new TileShader(image.Share(),Matrix.CreateTranslation(offset,-22),mode,sampling))
            {
                var paint=new Paint(new(255,255,255,191),CompositeMode.SourceOver,false,shader);
                var transform=Matrix3.Identity with {M02=2,M12=-1};
                var command=path?new DrawingCommand(CommandKind.FillPath,default,transform,paint){Path=PathGeometry.Rectangle(rect)}:
                    DrawingCommand.FillRectangle(rect,transform,paint);
                gpu.Execute(command);cpu.Execute(command);
            }
            // Distinct brush configuration in the same batch must keep its
            // own wrap and transform after the bitmap lease is released.
            using(var second=new TileShader(image.Share(),Matrix.CreateTranslation(3,2),TileMode.FlipY,ImageSampling.Nearest))
            {
                var command=DrawingCommand.FillRectangle(new(198,11,211,141),Matrix3.Identity,
                    new(new(255,255,255,221),CompositeMode.SourceOver,false,second));
                gpu.Execute(command);cpu.Execute(command);
            }
            gpu.Flush();
            var end=SimdSkiaGpuDiagnostics.GetStatistics();
            int expectedGpu=accelerate?2:1;
            if(end.GpuCommands-begin.GpuCommands!=expectedGpu||end.CpuFallbackCommands-begin.CpuFallbackCommands!=2-expectedGpu)
                throw new InvalidOperationException("Unexpected tile brush dispatch.");
            int limit=backend==SimdSkiaGpuBackend.Vulkan?0:2;
            var a=actual.GetPixelSpan();var b=expected.GetPixelSpan();
            for(int at=0;at<a.Length;at++)
            {
                int delta=Math.Abs(a[at]-b[at]);
                maximumDifference=Math.Max(maximumDifference,delta);
                if(delta>limit)throw new InvalidOperationException($"Tile brush pixels differ: {mode}, {sampling}, offset {offset}, path {path}, pixel {at/4%width},{at/4/width}: [{string.Join(',',a.Slice(at/4*4,4).ToArray())}] vs [{string.Join(',',b.Slice(at/4*4,4).ToArray())}].");
            }
            accelerated+=expectedGpu;
            fallbacks+=2-expectedGpu;
        }
        Console.WriteLine($"PASS {backend}: {accelerated} hardware tile draws, {fallbacks} CPU fallbacks, max channel difference {maximumDifference}.");
    }
}
