using SimdSkia.Cpu;

namespace SimdSkia.ControlValidation;

internal static class CoverageLeaseValidation
{
    internal static int Run()
    {
        int checks=0;
        void Check(bool value,string reason)
        {
            if(!value)throw new InvalidOperationException(reason);
            checks++;
        }

        const int width=384,height=320;
        var normal=Stripes(width,height,FillRule.Winding);
        var inverse=Stripes(width,height,FillRule.InverseWinding);
        using(var storage=new RasterCoverageStorage())
        {
            var pooled=PathRasterizer.Rasterize(normal,Matrix3.Identity,.125f,width,height,true,compactRuns:true,storage:storage);
            var expected=PathRasterizer.Rasterize(normal,Matrix3.Identity,.125f,width,height,true,compactRuns:true);
            Check(storage.HasCoverage&&pooled.Dense&&pooled.Data.Length>expected.Data.Length,"The dense coverage test did not rent a larger bucket.");
            Check(pooled.Data.AsSpan(0,pooled.Width*pooled.Height).SequenceEqual(expected.Data),"Dense pooled coverage differs from exact owned coverage.");
        }
        using(var storage=new RasterCoverageStorage())
        {
            var narrow=PathGeometry.Rectangle(new(10.25f,1.25f,70.25f,3000.25f));
            var pooled=PathRasterizer.Rasterize(narrow,Matrix3.Identity,.125f,128,3002,true,compactRuns:true,storage:storage);
            var expected=PathRasterizer.Rasterize(narrow,Matrix3.Identity,.125f,128,3002,true,compactRuns:true);
            Check(pooled.RunArray.Length>pooled.Runs.Length&&pooled.Runs.Length>=5500,"The scanline-run test did not rent a larger bucket.");
            Check(pooled.Runs.SequenceEqual(expected.Runs),"Pooled scanline ordering or run count changed.");
        }
        const int largeWidth=3840,largeHeight=2160;
        var large=Stripes(largeWidth,largeHeight,FillRule.InverseWinding,256);
        byte[] held;
        using(var storage=new RasterCoverageStorage())
        {
            var mask=PathRasterizer.Rasterize(large,Matrix3.Identity,.125f,largeWidth,largeHeight,true,includeRuns:false,storage:storage);
            Check(storage.HasCoverage&&mask.Dense&&mask.Data.Length>2*1024*1024,"A large dense mask must rent coverage.");
            held=mask.Data;
            held.AsSpan().Fill(143);
        }
        using(var storage=new RasterCoverageStorage())
        {
            var mask=PathRasterizer.Rasterize(large,Matrix3.Identity,.125f,largeWidth,largeHeight,true,includeRuns:false,storage:storage);
            Check(ReferenceEquals(held,mask.Data),"A sequential large draw must reuse the released coverage buffer.");
            var expected=PathRasterizer.Rasterize(large,Matrix3.Identity,.125f,largeWidth,largeHeight,true,includeRuns:false);
            Check(mask.Data.AsSpan(0,largeWidth*largeHeight).SequenceEqual(expected.Data),"Reused large coverage differs from the owned reference.");
        }

        using var pixels=new PixelBuffer(width,height,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        CoverageClip clip;
        using(var canvas=new RasterCanvas(pixels))
        {
            canvas.Execute(Command(normal,Ink) with {CurveTolerance=.015625f});
            clip=canvas.CreateClip(normal,Matrix3.Identity);
        }
        using(var canvas=new RasterCanvas(pixels))canvas.Execute(Command(inverse,new(72,24,113,255)));
        Check(clip.At(0,123)==255&&clip.At(3,123)==0,"A retained clip borrowed memory after canvas disposal.");

        using var actual=new PixelBuffer(width,height,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        using(var canvas=new RasterCanvas(actual))
        {
            var shader=new DuringDraw(()=>
            {
                canvas.DropCaches();
                using var other=new RasterCanvas(pixels);
                other.Execute(Command(inverse,new(72,24,113,255)));
            });
            canvas.Execute(Command(normal,new(255,255,255,255),shader));
            Check(shader.Called,"The draw did not exercise cache eviction during sampling.");
        }
        using var expectedPixels=new PixelBuffer(width,height,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        using(var canvas=new RasterCanvas(expectedPixels))canvas.Execute(Command(normal,Ink));
        Check(actual.GetPixelSpan().SequenceEqual(expectedPixels.GetPixelSpan()),"A draw lost its live cached coverage during eviction.");
        return checks;
    }

    private static readonly Rgba32 Ink=new(173,100,47,255);
    private static DrawingCommand Command(PathGeometry path,Rgba32 color,IColorShader? shader=null)
        =>new(CommandKind.FillPath,default,Matrix3.Identity,new(color,CompositeMode.SourceOver,true,shader)){Path=path};

    private static PathGeometry Stripes(int width,int height,FillRule rule,int interval=4)
    {
        var segments=new List<PathSegment>();
        for(int x=0;x<width;x+=interval)
        {
            segments.Add(new(PathVerb.Move,new(x,0)));
            segments.Add(new(PathVerb.Line,new(x+2,0)));
            segments.Add(new(PathVerb.Line,new(x+2,height)));
            segments.Add(new(PathVerb.Line,new(x,height)));
            segments.Add(new(PathVerb.Close,default));
        }
        return new(segments,rule);
    }

    private sealed class DuringDraw(Action once) : IColorShader
    {
        internal bool Called {get;private set;}
        public Rgba32 Sample(float x,float y)
        {
            if(!Called){Called=true;once();}
            return Ink;
        }
    }
}
