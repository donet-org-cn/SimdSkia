using SimdSkia.Cpu;

namespace SimdSkia.ControlValidation;

internal static class GlyphValidation
{
    internal static int Run()
    {
        int count=0;
        void Check(bool condition,string message){count++;if(!condition)throw new InvalidOperationException(message);}
        var outline=new PathGeometry([new(PathVerb.Move,new(.25f,.125f)),new(PathVerb.Line,new(9.25f,1.25f)),new(PathVerb.Quadratic,new(12.5f,10.5f),new(1.125f,11.75f)),new(PathVerb.Close,default)]);
        Matrix3[] matrices=[new(1,0,2.125f,0,1,3.375f,0,0,1),new(1.5f,0,1.625f,0,1.5f,.125f,0,0,1),new(-1,0,57.375f,0,1,2.625f,0,0,1),new(.875f,-.25f,5.125f,.25f,.875f,1.625f,0,0,1),new(0,-1,40.375f,1,0,.625f,0,0,1)];
        var random=new Random(791);
        foreach(var positioning in Enum.GetValues<GlyphPositioning>())foreach(bool overlap in new[]{false,true})
        {
            var input=new[]{new PositionedGlyph(outline,new(0,0)),new PositionedGlyph(outline,new(overlap?2.375f:16.375f,1.125f))};
            var run=new GlyphRunGeometry(input,Matrix3.Identity,positioning);
            input[0]=new(PathGeometry.Rectangle(new(0,0,1,1)),default);
            Check(ReferenceEquals(run.Glyphs[0].Outline,outline),"Glyph geometry retained a mutable caller array.");
            foreach(var matrix in matrices)foreach(var format in Enum.GetValues<PixelFormat>())foreach(var alpha in Enum.GetValues<AlphaFormat>())foreach(var composite in Enum.GetValues<CompositeMode>())foreach(bool aa in new[]{false,true})foreach(bool snap in new[]{false,true})
            {
                using var actual=new PixelBuffer(64,36,format,alpha,64*4+7);random.NextBytes(actual.GetPixelSpan());
                using var expected=actual.Clone();using var canvas=new RasterCanvas(actual);using var reference=new RasterCanvas(expected);
                var command=new DrawingCommand(CommandKind.DrawGlyphRun,default,matrix,new(new(31,107,223,173),composite,aa))
                {GlyphRun=run,Clip=new(1,1,62,34),SnapGlyphBaseline=snap,CurveTolerance=.015625f};
                canvas.Execute(command);
                foreach(var glyph in run.Glyphs)
                {
                    var origin=matrix.Map(glyph.Position.X,glyph.Position.Y);float px=origin.X,py=origin.Y;
                    if(positioning==GlyphPositioning.QuarterPixel)
                    {
                        float fx=aa?4:1,fy=fx;if(snap){if(matrix.M10==0)fy=1;else if(matrix.M00==0)fx=1;}
                        px=MathF.Floor(px*fx+.5f)/fx;py=MathF.Floor(py*fy+.5f)/fy;
                    }
                    var transform=(matrix with{M02=0,M12=0})*run.FontTransform;
                    transform=transform with{M02=transform.M02+px,M12=transform.M12+py};
                    reference.Execute(command with{Kind=CommandKind.FillPath,Path=glyph.Outline,Transform=transform,ShaderTransform=matrix,CacheTextCoverage=true});
                }
                Check(actual.GetPixelSpan().SequenceEqual(expected.GetPixelSpan()),$"Glyph mask/ordered path oracle: {positioning}/{overlap}/{matrix}/{format}/{alpha}/{composite}/{aa}/{snap}.");
            }
        }
        // Overlapping coverage must blend twice for translucent source colors.
        var rectangle=PathGeometry.Rectangle(new(0,0,4,4));
        var repeated=new GlyphRunGeometry([new(rectangle,new(2,2)),new(rectangle,new(2,2))],Matrix3.Identity);
        using(var image=new PixelBuffer(12,12,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))using(var canvas=new RasterCanvas(image))
        {canvas.Execute(new(CommandKind.DrawGlyphRun,default,Matrix3.Identity,new(new(255,0,0,128),CompositeMode.SourceOver,true)){GlyphRun=repeated});Check(image.ReadPixel(3,3).A==192,"Overlapping glyph masks were incorrectly unioned before source alpha.");}
        // Compare cached and uncached runs through mutable destination, coverage
        // clips, gradients, and changing luminance/opacity (none is mask identity).
        var glyphs=new GlyphRunGeometry([new(outline,new(0,0)),new(outline,new(16,0))],Matrix3.Identity,GlyphPositioning.QuarterPixel);
        foreach(var sourceColor in new[]{new Rgba32(0,0,0,255),new Rgba32(255,255,255,255),new Rgba32(222,83,27,255)})foreach(bool preblend in new[]{false,true})foreach(byte opacity in new byte[]{0,63,128,255})foreach(bool gradient in new[]{false,true})
        {
            using var actual=new PixelBuffer(64,36,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);using var expected=actual.Clone();
            using var canvas=new RasterCanvas(actual);using var reference=new RasterCanvas(expected);
            var clip=Enumerable.Range(0,64*36).Select(i=>(byte)(i%256)).ToArray();
            var command=new DrawingCommand(CommandKind.DrawGlyphRun,default,matrices[0],new(sourceColor with{A=opacity},CompositeMode.SourceOver,true,gradient?new LinearGradient(new(0,0),new(48,0),new(27,93,241,255),new(227,192,41,255)):null))
            {GlyphRun=glyphs,CoverageClip=new(0,0,64,36,clip),TextMaskPreblend=preblend};
            canvas.Execute(command);
            try{GlyphCoverageCache.Enabled=false;reference.Execute(command);}finally{GlyphCoverageCache.Enabled=true;}
            Check(actual.GetPixelSpan().SequenceEqual(expected.GetPixelSpan()),"Cached glyph mask leaked clip, opacity, shader, or color state.");
        }
        // Integer scrolling shares masks but moves the ink exactly.
        using(var image=new PixelBuffer(64,48,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))using(var shifted=new PixelBuffer(64,48,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))
        {
            var command=new DrawingCommand(CommandKind.DrawGlyphRun,default,matrices[0],new(new(0,0,0,255),CompositeMode.SourceOver,true)){GlyphRun=glyphs,TextMaskPreblend=true};
            using(var c=new RasterCanvas(image))c.Execute(command);
            using(var c=new RasterCanvas(shifted))c.Execute(command with{Transform=matrices[0] with{M12=matrices[0].M12+7}});
            for(int y=0;y<41;y++)Check(image.GetRowSpan(y)[..(64*4)].SequenceEqual(shifted.GetRowSpan(y+7)[..(64*4)]),"Integer scrolling changed glyph coverage.");
        }
        GlyphCoverageCache.Clear();
        using(var image=new PixelBuffer(32,24,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))using(var canvas=new RasterCanvas(image))
        {
            for(int i=0;i<700;i++)canvas.Execute(new(CommandKind.DrawGlyphRun,default,Matrix3.Identity,new(new(0,0,0,255),CompositeMode.SourceOver,true)){GlyphRun=new([new(outline,new(1,1))],Matrix3.Identity)});
            var stats=GlyphCoverageCache.Statistics;
            Check(stats.Runs<=GlyphCoverageCache.MaximumRuns&&stats.RunBytes<=GlyphCoverageCache.MaximumRunBytes,"Glyph run cache exceeded its retention budget.");
            Check(GlyphCoverageCache.RecentRunCount<=stats.Runs,"Recent-run index retained evicted cache entries.");
            for(int i=0;i<4200;i++)canvas.Execute(new(CommandKind.DrawGlyphRun,default,Matrix3.Identity,new(new(0,0,0,255),CompositeMode.SourceOver)){GlyphRun=new([new(PathGeometry.Rectangle(new(0,0,2,2)),new(1,1))],Matrix3.Identity)});
            stats=GlyphCoverageCache.Statistics;
            Check(stats.Glyphs<=GlyphCoverageCache.MaximumGlyphs&&stats.GlyphBytes<=GlyphCoverageCache.MaximumGlyphBytes,"Glyph atlas exceeded its retention budget.");
        }
        GlyphCoverageCache.Clear();
        Parallel.For(0,32,i=>{using var image=new PixelBuffer(64,36,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);using var c=new RasterCanvas(image);c.Execute(new(CommandKind.DrawGlyphRun,default,matrices[0],new(new(0,0,0,255),CompositeMode.SourceOver,true)){GlyphRun=glyphs,TextMaskPreblend=true});if(image.ReadPixel(3,4).A==0)throw new InvalidOperationException("Concurrent glyph rendering lost its ink.");});count++;
        Check(GlyphCoverageCache.RecentRunCount<=GlyphCoverageCache.Statistics.Runs,"Concurrent glyph misses exceeded the recent-run index budget.");
        // Invalid runs fail before compositing any valid leading glyph.
        using(var image=new PixelBuffer(64,36,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))using(var canvas=new RasterCanvas(image))
        {
            var invalid=new GlyphRunGeometry([new(outline,new(0,0)),new(outline,new(70000,0))],Matrix3.Identity);
            bool rejected=false;try{canvas.Execute(new(CommandKind.DrawGlyphRun,default,Matrix3.Identity,new(new(255,0,0,255),CompositeMode.SourceOver)){GlyphRun=invalid});}catch(NotSupportedException){rejected=true;}
            Check(rejected&&image.GetPixelSpan().IndexOfAnyExcept((byte)0)<0,"Invalid glyph run partially modified destination.");
        }
        GlyphCoverageCache.Clear();
        // Verify the forward blend in linear light rather than rebuilding the
        // correcting LUT. This also covers both sRGB transfer function branches.
        static double Decode(double c)=>c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);
        byte[] canonical=[0,36,73,109,146,182,219,255];
        for(int luma=0;luma<8;luma++)
        {
            var table=GlyphMaskGamma.Table(luma);double source=canonical[luma]/255d,destination=1-source;
            for(int i=0;i<256;i++)
            {
                double coverage=i/255d;coverage+=coverage*(1-coverage)*(128/255d)*Decode(destination);
                double expected=Decode(source)*coverage+Decode(destination)*(1-coverage);
                double encoded=source*table[i]/255d+destination*(1-table[i]/255d);
                Check(Math.Abs(Decode(encoded)-expected)<=.009,"sRGB glyph preblend violated its forward linear-light equation.");
                Check(i==0||table[i]>=table[i-1],"Glyph preblend was not monotonic.");
            }
            Check(table[0]==0&&table[255]==255,"Glyph preblend changed transparent/opaque endpoints.");
        }
        // Color correction must never mutate the shared, color-free masks or
        // another luminance's run. Exercise both single and overlapping groups.
        foreach(float spacing in new[]{4f,16f})
        {
            var run=new GlyphRunGeometry([new(outline,new(0,0)),new(outline,new(spacing,0))],Matrix3.Identity);
            var raw=GlyphCoverageCache.Resolve(run,Matrix3.Identity,true,.015625f,false,-1,out _,out _);
            var originals=raw.Glyphs.Select(g=>g.Mask.Data.ToArray()).ToArray();
            for(int luma=0;luma<8;luma++)
            {
                var corrected=GlyphCoverageCache.Resolve(run,Matrix3.Identity,true,.015625f,false,luma,out _,out _);
                var table=GlyphMaskGamma.Table(luma);
                Check(corrected.Glyphs.Length==raw.Glyphs.Length,"Gamma correction changed the overlap groups.");
                for(int i=0;i<raw.Glyphs.Length;i++)
                {
                    var expected=new byte[originals[i].Length];for(int j=0;j<expected.Length;j++)expected[j]=table[originals[i][j]];
                    Check(corrected.Glyphs[i].Mask.Data.AsSpan().SequenceEqual(expected),"Glyph mask was corrected twice or used another color's cache.");
                    Check(raw.Glyphs[i].Mask.Data.AsSpan().SequenceEqual(originals[i]),"Color correction modified a shared raw glyph mask.");
                }
            }
        }
        return count;
    }
}
