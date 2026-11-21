using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using SimdSkia.Avalonia;

namespace SimdSkia.ControlValidation;

internal static class MemoryValidation
{
    internal static int Run()
    {
        int checks=0;var factory=AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();
        void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);checks++;}
        Check(StartupPreparation.Pending is null,"Headless bitmap applications must not start desktop preparation.");
        byte[] Bytes(IReadableBitmapImpl bitmap)
        {using var locked=bitmap.Lock();var bytes=new byte[locked.RowBytes*locked.Size.Height];Marshal.Copy(locked.Address,bytes,0,bytes.Length);return bytes;}
        using var reused=factory.CreateRenderTargetBitmap(new(64,48),new(96,96));
        using (var encoded = new MemoryStream())
        {
            using (var context = reused.CreateDrawingContext()) context.Clear(Colors.Lime);
            reused.Save(encoded, PngBitmapEncoderOptions.Default);
            var file = new byte[checked((int)encoded.Length + 31)];
            encoded.GetBuffer().AsSpan(0, (int)encoded.Length).CopyTo(file.AsSpan(18));
            foreach (bool exposeBuffer in new[] { false, true })
            {
                using var input = new MemoryStream(file, 7, 11 + (int)encoded.Length, false, exposeBuffer);
                input.Position = 11;
                using var loaded = factory.LoadWriteableBitmap(input);
                var pixels = Bytes(loaded);
                Check(input.Position == input.Length && loaded.PixelSize == reused.PixelSize &&
                    pixels[0] == 0 && pixels[1] == 255 && pixels[2] == 0 && pixels[3] == 255,
                    "Bitmap loading must honor the stream origin, position and length.");
            }
        }
        foreach(GradientBrush brush in new GradientBrush[]{new LinearGradientBrush(),new RadialGradientBrush(),new ConicGradientBrush()})
        {
            brush.GradientStops.Add(new GradientStop(Colors.Red,0));brush.GradientStops.Add(new GradientStop(Colors.Blue,1));
            for(int step=0;step<12;step++)
            {
                switch(step)
                {
                    case 1:brush.GradientStops[0].Color=Colors.Green;break;
                    case 2:brush.GradientStops[1].Offset=.7;break;
                    case 3:brush.GradientStops.Add(new GradientStop(Colors.Yellow,.4));break;
                    case 4:brush.SpreadMethod=GradientSpreadMethod.Reflect;break;
                    case 5:brush.Transform=new RotateTransform(17);break;
                    case 6:brush.TransformOrigin=new RelativePoint(.2,.6,RelativeUnit.Relative);break;
                    case 7:brush.Opacity=.37;break;
                    case 8:
                        if(brush is LinearGradientBrush linear)linear.EndPoint=new RelativePoint(.2,.8,RelativeUnit.Relative);
                        if(brush is RadialGradientBrush radial)radial.GradientOrigin=new RelativePoint(.25,.4,RelativeUnit.Relative);
                        if(brush is ConicGradientBrush conic)conic.Angle=73;
                        break;
                    case 9:
                        if(brush is LinearGradientBrush linear2)linear2.StartPoint=new RelativePoint(.1,.3,RelativeUnit.Relative);
                        if(brush is RadialGradientBrush radial2)radial2.RadiusX=new RelativeScalar(.3,RelativeUnit.Relative);
                        if(brush is ConicGradientBrush conic2)conic2.Center=new RelativePoint(.2,.3,RelativeUnit.Relative);
                        break;
                    case 10:brush.GradientStops.Clear();break;
                }
                var bounds=step==11?new Rect(4,7,33,25):new Rect(2,3,51,37);
                using(var context=reused.CreateDrawingContext()){context.Clear(Colors.White);context.DrawRectangle(brush,null,bounds);}
                using var fresh=factory.CreateRenderTargetBitmap(new(64,48),new(96,96));
                // Avalonia 12.1.5's radial copy constructor assigns RadiusX to
                // RadiusY. Pass both radii explicitly to preserve this fixture.
                IBrush snapshot=brush is RadialGradientBrush r
                    ?new ImmutableRadialGradientBrush(r.GradientStops.Select(s=>new ImmutableGradientStop(s.Offset,s.Color)).ToArray(),r.Opacity,r.Transform is {} t?new ImmutableTransform(t.Value):null,r.TransformOrigin,r.SpreadMethod,r.Center,r.GradientOrigin,r.RadiusX,r.RadiusY)
                    :brush.ToImmutable();
                using(var context=fresh.CreateDrawingContext()){context.Clear(Colors.White);context.DrawRectangle(snapshot,null,bounds);}
                Check(Bytes(reused).AsSpan().SequenceEqual(Bytes(fresh)), $"Reused {brush.GetType().Name} step {step} retained a stale stop, transform, opacity, geometry or bounds.");
            }
        }
        var owner=factory.CreateRenderTargetBitmap(new(8,8),new(96,96));
        var first=owner.CreateDrawingContext();var overlap=owner.CreateDrawingContext();
        first.Clear(Colors.Red);first.Dispose();owner.Dispose();
        overlap.DrawRectangle(Brushes.Blue,null,new Rect(0,0,4,4));overlap.Dispose();checks++;
        owner=factory.CreateRenderTargetBitmap(new(8,8),new(96,96));
        var retained=owner.CreateDrawingContext();owner.Dispose();retained.Clear(Colors.Green);retained.Dispose();checks++;
        var invalid=reused.CreateDrawingContext();invalid.PushClip(new Rect(0,0,2,2));
        bool rejected=false;try{invalid.Dispose();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Unbalanced reused bitmap context was accepted.");
        using(var context=reused.CreateDrawingContext()){context.Clear(Colors.Blue);context.DrawRectangle(Brushes.Red,null,new Rect(30,20,4,4));}
        var output=Bytes(reused);Check(output[(21*64+31)*4+2]==255,"Previous context clip leaked into the next frame.");
        long priorTileBytes=TileRasterCache.Statistics.Bytes;
        using(var changing=new WriteableBitmap(new(64,48),new(96,96),PixelFormats.Bgra8888,global::Avalonia.Platform.AlphaFormat.Premul))
        {
            var brush=new ImageBrush(changing){Stretch=Stretch.Fill,TileMode=TileMode.Tile};
            var pixels=new byte[64*48*4];
            for(int version=0;version<80;version++)
            {
                for(int i=0;i<pixels.Length;i+=4){pixels[i]=(byte)version;pixels[i+2]=(byte)(255-version);pixels[i+3]=255;}
                using(var locked=changing.Lock())Marshal.Copy(pixels,0,locked.Address,pixels.Length);
                using(var context=reused.CreateDrawingContext()){context.Clear(Colors.Transparent);context.DrawRectangle(brush,null,new Rect(0,0,64,48));}
                var actual=Bytes(reused);Check(actual[0]==version&&actual[2]==255-version,"ImageBrush returned an obsolete mutable bitmap version.");
            }
            Check(TileRasterCache.Statistics.Bytes<=priorTileBytes+64*48*4,"Tile cache retained unreachable old bitmap versions.");
        }
        var (weak,key)=CacheTransientSource();
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Check(!weak.IsAlive,"Tile cache identity retained its source object.");
        using var oldLease=TileRasterCache.Get(key);
        Check(oldLease?.Pixels.ReadPixel(0,0).R==255,"Cached pixels disappeared with the source object.");
        using var repeatLease=TileRasterCache.Get(key);
        Check(ReferenceEquals(oldLease?.Pixels,repeatLease?.Pixels),"Separate shader leases must carry one immutable GPU upload identity.");
        using var replacement=new SimdSkia.PixelBuffer(2,2,SimdSkia.PixelFormat.Bgra8888,SimdSkia.AlphaFormat.Premultiplied);
        replacement.WritePixel(0,0,new(0,0,255,255));
        using var newLease=TileRasterCache.Add(key with{Version=1},replacement);
        Check(oldLease!.Value.Pixels.ReadPixel(0,0).R==255&&newLease.Pixels.ReadPixel(0,0).B==255,"Evicting a bitmap version invalidated an existing shader lease.");
        var geometry=new SimdSkia.Avalonia.StreamGeometry();
        using(var edit=geometry.Open()){edit.BeginFigure(new(1,2),true);edit.QuadraticBezierTo(new(6,12),new(11,2));edit.EndFigure(true);}
        var oldBounds=geometry.Bounds;
        Check(oldBounds==new Rect(1,2,10,5),"Cached geometry bounds must remain tight, including curve extrema.");
        long beforeBounds=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)if(geometry.Bounds!=oldBounds)throw new InvalidOperationException("Immutable bounds changed.");
        Check(GC.GetAllocatedBytesForCurrentThread()-beforeBounds<=64,"Repeated immutable bounds queries allocate.");
        using(var edit=geometry.Open()){edit.BeginFigure(new(-5,-3),true);edit.LineTo(new(20,14));edit.LineTo(new(-5,14));edit.EndFigure(true);}
        Check(geometry.Bounds==new Rect(-5,-3,25,17),"Editing geometry did not invalidate its cached bounds.");
        var clone=(SimdSkia.Avalonia.Geometry)geometry.Clone();
        Check(clone.Bounds==geometry.Bounds,"Cloned geometry lost its bounds.");
        Parallel.For(0,64,_=>{if(clone.Bounds!=new Rect(-5,-3,25,17))throw new InvalidOperationException("Concurrent bounds query changed geometry.");});checks++;
        using var reference=factory.CreateRenderTargetBitmap(new(64,48),new(96,96));
        var changingPen=new Pen(Brushes.Navy,1);
        for(int i=0;i<80;i++)
        {
            var a=new Point(3.25+i%5,4.75+i%7);var b=new Point(52.5-i%9,30.75+i%11);
            if((i&1)!=0)(a,b)=(b,a);
            changingPen.Thickness=1+i%4;
            changingPen.LineCap=(PenLineCap)(i%3);
            changingPen.DashStyle=i%5==0?new DashStyle([2d,1d],i%3):null;
            var box=new Rect(7.25,9.5,34+i%3,23);
            using(var draw=reused.CreateDrawingContext())
            {draw.Clear(Colors.Transparent);draw.DrawRectangle(Brushes.Gold,changingPen,box);draw.DrawLine(changingPen,a,b);}
            using(var draw=reference.CreateDrawingContext())
            {
                draw.Clear(Colors.Transparent);draw.DrawRectangle(Brushes.Gold,null,box);
                draw.DrawGeometry(null,changingPen,factory.CreateRectangleGeometry(box));
                draw.DrawGeometry(null,changingPen,factory.CreateLineGeometry(a,b));
            }
            Check(Bytes(reused).AsSpan().SequenceEqual(Bytes(reference)),"Cached outlines diverged from the explicit geometry.");
        }
        Parallel.For(0,64,i=>
        {
            var pen=new ImmutablePen(Brushes.Black,1+i%4);
            if(((SimdSkia.Avalonia.Geometry)clone.GetWidenedGeometry(pen)).Bounds.Width<=0)
                throw new InvalidOperationException("Concurrent stroke queries lost geometry.");
        });checks++;
        const int stripeWidth=65,stripeHeight=13;
        var stripes=new List<SimdSkia.PathSegment>();
        for(int x=0;x<stripeWidth;x+=2)
        {
            stripes.Add(new(PathVerb.Move,new(x,0)));
            stripes.Add(new(PathVerb.Line,new(x+1,0)));
            stripes.Add(new(PathVerb.Line,new(x+1,stripeHeight)));
            stripes.Add(new(PathVerb.Line,new(x,stripeHeight)));
            stripes.Add(new(PathVerb.Close,default));
        }
        var noDraw=Cpu.RasterCanvas.CreateClipForBounds(new(stripes),Matrix3.Identity,stripeWidth,stripeHeight,true);
        for(int x=0;x<stripeWidth;x++)
            Check(noDraw.At(x,5)==((x&1)==0?255:0),"No-draw canvas lost dense path clip coverage.");

        using (var pixels = new PixelBuffer(32, 24, SimdSkia.PixelFormat.Bgra8888, SimdSkia.AlphaFormat.Premultiplied))
        using (var raster = new Cpu.RasterCanvas(pixels))
        {
            var path = PathGeometry.Rectangle(new(2.25f, 3, 20.25f, 17));
            var mask = raster.CreateClip(path, Matrix3.Identity);
            var rebuilt = new PathGeometry(path.Segments.ToArray());
            Check(ReferenceEquals(mask, raster.CreateClip(rebuilt, Matrix3.Identity)), "Equivalent paths must reuse their immutable clip.");
            var inverse = new PathGeometry(path.Segments.ToArray(), SimdSkia.FillRule.InverseWinding);
            Check(mask.At(0, 0) == 0 && raster.CreateClip(inverse, Matrix3.Identity).At(0, 0) == 255,
                "Clip reuse lost the fill rule.");
            Check(mask.At(2, 6) != raster.CreateClip(rebuilt, Matrix3.Identity with { M02 = .5f }).At(2, 6),
                "Clip reuse lost the device transform.");
        }
        var flatPen = new Pen(Brushes.Navy, 2);
        var firstLine = factory.CreateLineGeometry(new(2, 3), new(21, 12));
        var sameLine = factory.CreateLineGeometry(new(2, 3), new(21, 12));
        Check(ReferenceEquals(firstLine.GetWidenedGeometry(flatPen), sameLine.GetWidenedGeometry(flatPen)),
            "Rebuilt line geometry did not reuse its stroke.");

        for (int i = 0; i < 40; i++)
        {
            bool horizontal = (i & 1) == 0;
            double band = 8.125 + i * .231;
            var start = horizontal ? new Point(-3.125, band) : new Point(band, -3.125);
            var end = horizontal ? new Point(49.625, band) : new Point(band, 35.625);
            if ((i & 2) != 0) (start, end) = (end, start);
            var pen = new Pen(Brushes.Navy, (i % 6 + 1) * .5)
            {
                LineCap = PenLineCap.Flat,
                DashStyle = (i % 5) switch
                {
                    0 => null,
                    1 => new DashStyle([1, 1], i * .25),
                    2 => new DashStyle([0, 2], i * -.25),
                    3 => new DashStyle([2, 0], i * .25),
                    _ => new DashStyle([1.2, .7, .9], i * -.25),
                },
            };
            var line = factory.CreateLineGeometry(start, end);
            var outline = factory.CreateStreamGeometry();
            using (var edit = outline.Open())
            {
                edit.BeginFigure(start, false);
                edit.LineTo(end);
                edit.LineTo(end); // Same stroke, with a contour that uses the polygon stroker.
                edit.EndFigure(false);
            }
            Check(line.GetRenderBounds(pen) == outline.GetRenderBounds(pen), "Flat-axis stroke bounds diverged from the polygon stroker.");
            var transform = i % 3 == 0 ? Matrix.CreateRotation(.137) : Matrix.Identity;
            using (var draw = reused.CreateDrawingContext())
            {
                draw.Clear(Colors.Transparent);
                draw.Transform = transform;
                draw.DrawGeometry(null, pen, line);
            }
            using (var draw = reference.CreateDrawingContext())
            {
                draw.Clear(Colors.Transparent);
                draw.Transform = transform;
                draw.DrawGeometry(null, pen, outline);
            }
            Check(Bytes(reused).AsSpan().SequenceEqual(Bytes(reference)), "Flat-axis stroke changed dashed or transformed pixels.");
        }

        var ellipse = factory.CreateEllipseGeometry(new(3.5, 2.75, 42, 33));
        var rectangle = new Rect(12, 10, 31, 29);
        void RenderNestedClip(IRenderTargetBitmapImpl bitmap, bool geometryClip)
        {
            using var context = bitmap.CreateDrawingContext();
            context.Clear(Colors.Transparent);
            context.PushGeometryClip(ellipse);
            if (geometryClip)
                context.PushGeometryClip(factory.CreateRectangleGeometry(rectangle));
            else
                context.PushClip(rectangle);
            context.DrawRectangle(Brushes.Red, null, new Rect(0, 0, 64, 48));
            context.PopClip();
            context.DrawRectangle(Brushes.Blue, null, new Rect(6, 7, 6, 7));
            context.PopClip();
        }
        RenderNestedClip(reused, true);
        RenderNestedClip(reference, false);
        Check(Bytes(reused).AsSpan().SequenceEqual(Bytes(reference)), "Integral geometry clips changed inherited coverage or clip restoration.");

        var shadow = new BoxShadow { OffsetX = 2.25, OffsetY = 2, Blur = 4, Color = Color.FromArgb(128, 10, 20, 30) };
        byte[] RenderShadow(BoxShadow style)
        {
            using (var context = reused.CreateDrawingContext())
            {
                context.Clear(Colors.Transparent);
                context.DrawRectangle(Brushes.White, null, new RoundedRect(new Rect(8, 8, 40, 24), 5), new BoxShadows(style));
            }
            return Bytes(reused);
        }
        var originalShadow = RenderShadow(shadow);
        for (int i = 0; i < 3; i++)
            Check(RenderShadow(shadow).AsSpan().SequenceEqual(originalShadow), "Reusing a box shadow changed its pixels.");
        shadow.Color = Colors.Orange;
        Check(!RenderShadow(shadow).AsSpan().SequenceEqual(originalShadow), "A cached box shadow ignored the new color.");

        using (var bitmap = factory.CreateRenderTargetBitmap(new(336,248), new(96,96)))
        foreach (bool inset in new[] { false, true })
        foreach (int corner in new[] { 0, 13 })
        {
            const int width = 336, height = 248;
            var rect = new RoundedRect(new Rect(24.25,21.5,280.5,200),corner);
            var style = new BoxShadow { OffsetX=-2.5, OffsetY=3.25, Spread=2, Blur=5, Color=Color.FromArgb(173,73,124,218), IsInset=inset };
            using (var draw = bitmap.CreateDrawingContext())
            {
                draw.Clear(Colors.Transparent);
                draw.DrawRectangle(null,null,rect,new BoxShadows(style));
            }
            // Independent full-size convolution, including the rows omitted by the strip renderer.
            using var fullMask = PixelBuffer.Rent(width,height,SimdSkia.PixelFormat.Alpha8);
            using var maskCanvas = new Cpu.RasterCanvas(fullMask);
            var cast = inset ? rect.Deflate(2,2) : rect.Inflate(2,2);
            if (inset) maskCanvas.Execute(DrawingCommand.Clear(new(255,255,255,255)));
            maskCanvas.Execute(new(CommandKind.FillPath,default,Matrix3.Identity with { M02=-2.5f,M12=3.25f },
                new(new(255,255,255,255),inset?CompositeMode.Clear:CompositeMode.SourceOver,true))
                {Path=SimdSkia.Avalonia.Geometry.Rounded(cast).Path,CurveTolerance=.015625f});
            float sigma=.288675f*5+.5f;
            using var dense = Cpu.ManagedFilters.Blur(fullMask,sigma,sigma);
            var box = maskCanvas.CreateClip(SimdSkia.Avalonia.Geometry.Rounded(rect).Path,Matrix3.Identity);
            var actual=Bytes(bitmap);
            for (int y=0;y<height;y++)
            {
                var alpha = dense.GetRowSpan(y);
                for (int x=0;x<width;x++)
                {
                    int cover=corner>0?box.At(x,y):x>=24&&x<305&&y>=22&&y<222?255:0;
                    static byte Mul(int a,int b)=>(byte)((a*b+127)/255);
                    byte a=Mul(Mul(alpha[x],173),inset?cover:255-cover);
                    int p=(y*width+x)*4;
                    if (actual[p]!=Mul(218,a)||actual[p+1]!=Mul(124,a)||actual[p+2]!=Mul(73,a)||actual[p+3]!=a)
                        throw new Exception($"Compressed box shadow differs at ({x},{y}), inset={inset}, radius={corner}.");
                }
            }
            checks++;
        }

        using var alphaPlane = new PixelBuffer(73, 19, SimdSkia.PixelFormat.Alpha8, SimdSkia.AlphaFormat.Premultiplied, 79);
        using var colorPlane = new PixelBuffer(73, 19, SimdSkia.PixelFormat.Bgra8888, SimdSkia.AlphaFormat.Premultiplied, 304);
        for (int y = 0; y < alphaPlane.Height; y++)
            for (int x = 0; x < alphaPlane.Width; x++)
            {
                byte alpha = (byte)((y * 43 + x * 31) & 255);
                alphaPlane.GetRowSpan(y)[x] = alpha;
                colorPlane.GetRowSpan(y)[x * 4 + 3] = alpha;
            }
        foreach (var region in new RectF?[] { null, new(5.25f, 2, 70.75f, 16) })
        {
            using var filteredAlpha = Cpu.ManagedFilters.Blur(alphaPlane, 2.15f, 1.1f, region);
            using var filteredColor = Cpu.ManagedFilters.Blur(colorPlane, 2.15f, 1.1f, region);
            for (int y = 0; y < alphaPlane.Height; y++)
                Check(Enumerable.Range(0, alphaPlane.Width).All(x =>
                    filteredAlpha.GetRowSpan(y)[x] == filteredColor.GetRowSpan(y)[x * 4 + 3]),
                    "Single-channel blur must match the premultiplied alpha channel, including edges and partial regions.");
        }
        return checks;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference,TileRasterCache.Key) CacheTransientSource()
    {
        var source=new object();var weak=new WeakReference(source);
        var key=new TileRasterCache.Key(TileRasterCache.Identity(source),0,default,default,Stretch.Fill,AlignmentX.Center,AlignmentY.Center,TileMode.Tile,new(2,2),new(96,96),default,default);
        using var pixels=new SimdSkia.PixelBuffer(2,2,SimdSkia.PixelFormat.Bgra8888,SimdSkia.AlphaFormat.Premultiplied);
        pixels.WritePixel(0,0,new(255,0,0,255));
        using var retained=TileRasterCache.Add(key,pixels);
        // A cached lease continues to own pixels independently of the source.
        if(retained.Pixels.ReadPixel(0,0).R!=255)throw new InvalidOperationException("Tile lease lost source pixels.");
        return (weak,key);
    }
}
