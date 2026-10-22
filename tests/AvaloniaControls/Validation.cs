using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.Media.TextFormatting;
using System.Runtime.InteropServices;
using SimdSkia.Cpu;
using System.Text.Json;
using System.Security.Cryptography;
using CorePixel=SimdSkia.PixelBuffer;
using AAlpha=Avalonia.Platform.AlphaFormat;

namespace SimdSkia.ControlValidation;

internal static class Validation
{
    private static int assertions;
    private static void Assert(bool success,string message){assertions++;if(!success)throw new InvalidOperationException(message);}
    private static void Throws<T>(Action action,string message) where T:Exception
    {try{action();}catch(T){assertions++;return;}throw new InvalidOperationException(message);}
    internal static void Run(Window host,Control scene,RenderTargetBitmap bitmap,string font,string output)
    {
        RunDrawingChecks();RunCompositorOracle();RunGeometry();RunShaping(font);RunLifetime();assertions+=GlyphValidation.Run();assertions+=CompletionValidation.Run();assertions+=CollectionValidation.Run(font);assertions+=FeatureProbe.ValidateTileFixtures(output);assertions+=MemoryValidation.Run();assertions+=TileRowValidation.Run();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);using var frame=host.CaptureRenderedFrame();Assert(frame is not null&&frame.PixelSize.Width>0,"Real window compositor did not present a framebuffer.");
        var button=scene.GetVisualDescendants().OfType<Button>().First();int clicked=0;button.Click+=(_,_)=>clicked++;
        var point=button.TranslatePoint(new Point(button.Bounds.Width/2,button.Bounds.Height/2),host)??throw new InvalidOperationException("Button coordinate mapping failed.");
        host.MouseDown(point,MouseButton.Left);host.MouseUp(point,MouseButton.Left);Assert(clicked==1,"Real pointer input did not click the button.");
        var text=scene.GetVisualDescendants().OfType<TextBox>().First();text.Focus();text.SelectAll();host.KeyTextInput("中文 OCR 123");Assert(text.Text=="中文 OCR 123","Chinese text input did not update TextBox.");
        using(var rendered=host.CaptureRenderedFrame()){Assert(rendered is not null,"Window stopped presenting after input.");using var stream=File.Create(Path.Combine(output,"window.png"));rendered!.Save(stream,PngBitmapEncoderOptions.Default);}
        host.SetRenderScaling(1.5);using(var scaled=host.CaptureRenderedFrame())Assert(scaled is not null&&scaled.PixelSize.Width==(int)(host.ClientSize.Width*1.5),"Compositor did not handle DPI changes.");
        host.Width=900;using(var resized=host.CaptureRenderedFrame())Assert(resized is not null&&resized.PixelSize.Width==(int)(host.ClientSize.Width*1.5),"Compositor did not rebuild a resized framebuffer.");host.SetRenderScaling(1);host.Width=1000;
        Console.WriteLine($"PASS: {assertions} real Avalonia, pixel, geometry, shaping, SIMD and lifetime assertions.");
    }
    private static IPlatformRenderInterface Factory=>AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();
    private static IRenderTargetBitmapImpl Target(int width=32,int height=24)=>Factory.CreateRenderTargetBitmap(new(width,height),new(96,96));
    private static byte[] Bytes(IReadableBitmapImpl image)
    {using var locked=image.Lock();var bytes=new byte[locked.RowBytes*locked.Size.Height];Marshal.Copy(locked.Address,bytes,0,bytes.Length);return bytes;}
    private static Color Pixel(IReadableBitmapImpl bitmap,int x,int y)
    {using var locked=bitmap.Lock();var values=new byte[4];Marshal.Copy(locked.Address+y*locked.RowBytes+x*4,values,0,4);return locked.Format==PixelFormats.Bgra8888?Color.FromArgb(values[3],values[2],values[1],values[0]):Color.FromArgb(values[3],values[0],values[1],values[2]);}
    private static void RunDrawingChecks()
    {
        using(var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","clips-native-3.119.4.json"))))
        foreach(var sample in fixture.RootElement.EnumerateArray())
        {
            double edge=sample.GetProperty("edge").GetDouble();bool left=sample.GetProperty("left").GetBoolean();
            using var probe=Target(8,1);using(var c=probe.CreateDrawingContext())
            {c.Clear(Colors.Transparent);c.PushClip(left?new Rect(edge,0,8-edge,1):new Rect(0,0,edge+4,1));c.DrawRectangle(Brushes.Red,null,new Rect(0,0,8,1));c.PopClip();}
            var expected=sample.GetProperty("alpha").GetBytesFromBase64();
            Assert(Enumerable.Range(0,8).All(i=>Pixel(probe,i,0).A==expected[i]),$"Native hard-clip rounding differs at {edge}, left={left}.");
        }
        using var image=Target();
        using(var context=image.CreateDrawingContext())
        {
            context.Clear(Colors.Transparent);context.PushOpacity(.5,null);
            context.DrawRectangle(Brushes.Red,null,new Rect(2,2,12,12));context.DrawRectangle(Brushes.Red,null,new Rect(8,2,12,12));context.PopOpacity();
        }
        Assert(Pixel(image,3,5).A==128&&Pixel(image,10,5).A==128,"Group opacity was incorrectly applied per draw at overlapping pixels.");
        using(var context=image.CreateDrawingContext())
        {
            context.Clear(Colors.Transparent);context.PushClip(new RoundedRect(new Rect(4,4,20,16),6));
            context.DrawRectangle(Brushes.Blue,null,new Rect(0,0,32,24));context.PopClip();
        }
        Assert(Pixel(image,4,4).A==0&&Pixel(image,12,10).B==255&&Pixel(image,0,0).A==0,"Rounded clip leaked or erased its interior.");
        Assert(Bytes(image).Where((_,i)=>i%4==3).Any(a=>a is >0 and <255),"Rounded clipping has no antialias coverage.");
        using(var context=image.CreateDrawingContext())
        {
            context.Clear(Colors.Transparent);context.PushOpacityMask(new SolidColorBrush(Color.FromArgb(128,255,255,255)),new Rect(0,0,16,24));
            context.DrawRectangle(Brushes.Green,null,new Rect(0,0,32,24));context.PopOpacityMask();
        }
        Assert(Pixel(image,4,8).A==128&&Pixel(image,20,8).A==0,"Opacity mask did not use alpha and bounds.");
        using var source=Factory.CreateWriteableBitmap(new(2,1),new(96,96),PixelFormats.Rgba8888,AAlpha.Unpremul);
        using(var locked=source.Lock())Marshal.Copy(new byte[]{255,0,0,255,0,0,255,255},0,locked.Address,8);
        using(var context=image.CreateDrawingContext())
        {
            context.Clear(Colors.Transparent);context.PushRenderOptions(new RenderOptions{BitmapInterpolationMode=BitmapInterpolationMode.None});
            context.DrawBitmap(source,1,new Rect(1,0,1,1),new Rect(0,0,8,8));context.PopRenderOptions();
        }
        Assert(Pixel(image,1,1).B==255&&Pixel(image,1,1).R==0,"Source image crop was ignored.");
        using(var locked=source.Lock())Marshal.Copy(new byte[]{255,0,0,0,0,0,255,255},0,locked.Address,8);
        using(var resized=Factory.ResizeBitmap(source,new(3,1),BitmapInterpolationMode.LowQuality))
        {var middle=Pixel((IReadableBitmapImpl)resized,1,0);Assert(middle.R==0&&middle.B==255&&middle.A is >=127 and <=128,"Bilinear sampling mixed straight RGB instead of premultiplied colors.");}
        foreach(double scale in new[]{1d,1.5,2})
        {
            using var scaled=Factory.CreateRenderTargetBitmap(new(32,24),new(96*scale,96*scale));using(var context=scaled.CreateDrawingContext()){context.Clear(Colors.Transparent);context.DrawRectangle(Brushes.Red,null,new Rect(2,2,4,4));}
            Assert(Pixel(scaled,(int)(3*scale),(int)(3*scale)).R==255&&Pixel(scaled,0,0).A==0,"DPI scaling was not applied exactly once.");
        }
        var before=Bytes(image);using(var context=image.CreateDrawingContext())Throws<ArgumentOutOfRangeException>(()=>context.DrawRectangle(Brushes.Red,null,new Rect(0,0,8,8),new BoxShadows(new BoxShadow{Blur=double.NaN,Color=Colors.Black})),"Invalid shadow parameters silently succeeded.");
        Assert(before.AsSpan().SequenceEqual(Bytes(image)),"A rejected draw modified destination pixels.");
        using(var context=image.CreateDrawingContext())
        {Assert(context is IDrawingContextImplWithEffects,"Missing effect interface would let Avalonia silently ignore visual effects.");Throws<ArgumentOutOfRangeException>(()=>((IDrawingContextImplWithEffects)context).PushEffect(new Rect(0,0,8,8),new BlurEffect{Radius=double.NaN}),"Invalid effect was ignored.");Throws<NotSupportedException>(()=>context.Transform=new Matrix(1,0,.1,0,1,0,0,0,1),"Perspective transform was silently flattened.");}
        var region=Factory.CreateRegion();region.AddRect(new(){Left=1,Top=1,Right=10,Bottom=10});region.AddRect(new(){Left=5,Top=5,Right=15,Bottom=15});
        Assert(region.Contains(new(12,12))&&!region.Contains(new(20,20))&&region.Rects.Sum(r=>(r.Right-r.Left)*(r.Bottom-r.Top))==156,"Region union double-counted its intersection.");region.Dispose();Throws<ObjectDisposedException>(()=>region.Reset(),"Disposed region remained usable.");
    }
    private static void RunCompositorOracle()
    {
        var random=new Random(416);
        foreach(var format in Enum.GetValues<SimdSkia.PixelFormat>())foreach(int width in new[]{1,7,8,9,16,17,31,65})foreach(byte opacity in new byte[]{0,1,63,127,128,254,255})
        {
            using var source=new CorePixel(width,3,format,SimdSkia.AlphaFormat.Premultiplied);using var destination=new CorePixel(width+4,5,format,SimdSkia.AlphaFormat.Premultiplied,(width+6)*4);
            random.NextBytes(source.GetPixelSpan());random.NextBytes(destination.GetPixelSpan());
            foreach(var buffer in new[]{source,destination})for(int y=0;y<buffer.Height;y++)for(int x=0;x<buffer.Width;x++){var p=buffer.GetRowSpan(y).Slice(x*4,4);for(int c=0;c<3;c++)p[c]=(byte)Math.Min(p[c],p[3]);}
            var expected=destination.CopyBytes();
            static byte Mul(byte a,byte b)=>(byte)Math.Floor(a*b/255d+.5);
            for(int y=0;y<3;y++)for(int x=0;x<width;x++)
            {var p=source.GetRowSpan(y).Slice(x*4,4);int offset=(y+1)*destination.RowBytes+(x+2)*4;byte alpha=Mul(p[3],opacity);for(int c=0;c<4;c++)expected[offset+c]=(byte)Math.Min(255,Mul(p[c],opacity)+Mul(expected[offset+c],(byte)(255-alpha)));}
            using(var canvas=new RasterCanvas(destination))canvas.Execute(new(CommandKind.DrawImage,new(2,1,width+2,4),Matrix3.Identity,new(new(255,255,255,opacity),CompositeMode.SourceOver)){Image=source,ImageSource=new(0,0,width,3),Sampling=ImageSampling.Nearest});
            Assert(expected.AsSpan().SequenceEqual(destination.GetPixelSpan()),$"Porter-Duff image oracle failed: {format}/{width}/{opacity} (including stride padding).");
        }
        using var clipped=new CorePixel(2,1,SimdSkia.PixelFormat.Rgba8888,SimdSkia.AlphaFormat.Premultiplied);using var raster=new RasterCanvas(clipped);
        raster.Execute(DrawingCommand.FillRectangle(new(0,0,2,1),Matrix3.Identity,new(new(255,0,0,255),CompositeMode.SourceOver)) with{CoverageClip=new(0,0,2,1,new byte[]{128,0})});
        Assert(clipped.ReadPixel(0,0).A==128&&clipped.ReadPixel(1,0).A==0,"Coverage clip was ignored by a rectangle.");
        foreach(var format in Enum.GetValues<SimdSkia.PixelFormat>())foreach(var alpha in Enum.GetValues<SimdSkia.AlphaFormat>())foreach(bool aa in new[]{false,true})foreach(bool mirror in new[]{false,true})
        {
            using var source=new CorePixel(7,5,format,alpha);using var axis=new CorePixel(19,13,format,SimdSkia.AlphaFormat.Premultiplied,19*4+9);using var general=axis.Clone();
            for(int y=0;y<5;y++)for(int x=0;x<7;x++)source.WritePixel(x,y,new((byte)(x*31),(byte)(y*53),(byte)(x*y*8),(byte)(x*27+y*15)));
            var m=new Matrix3(mirror?-1.25f:1.25f,0,mirror?17:.125f,0,1.5f,.375f,0,0,1);
            var command=new DrawingCommand(CommandKind.DrawImage,new(0,0,11,7),m,new(new(255,255,255,173),CompositeMode.SourceOver,aa)){Image=source,ImageSource=new(.25f,.125f,6.75f,4.875f),Sampling=ImageSampling.Bilinear,Clip=new(1.25f,.75f,16.75f,11.5f)};
            using(var fast=new RasterCanvas(axis))fast.Execute(command);
            // An infinitesimal shear bypasses the axis kernel without changing any float32 sample or 1/16 coverage grid.
            using(var reference=new RasterCanvas(general))reference.Execute(command with {Transform=m with {M01=1e-20f}});
            Assert(axis.GetPixelSpan().SequenceEqual(general.GetPixelSpan()),$"Axis/generic bilinear mismatch: {format}/{alpha}/{aa}/{mirror}.");
        }
    }
    private static void RunGeometry()
    {
        var a=Factory.CreateRectangleGeometry(new(0,0,20,20));var b=Factory.CreateEllipseGeometry(new(5,5,20,20));
        var cut=Factory.CreateCombinedGeometry(GeometryCombineMode.Exclude,a,b);
        Assert(cut.FillContains(new(1,1))&&!cut.FillContains(new(15,15)),"Geometry difference did not preserve its hole.");
        var stroke=a.GetWidenedGeometry(new Pen(Brushes.Black,2));Assert(stroke.FillContains(new(0,10))&&!stroke.FillContains(new(10,10)),"Closed stroke filled the entire rectangle.");
        var mutable=new Pen(Brushes.Black,2);a.GetWidenedGeometry(mutable);mutable.Thickness=8;Assert(a.GetWidenedGeometry(mutable).FillContains(new(-3,10)),"Mutable pen width returned stale cached geometry.");
        var dotted=Factory.CreateLineGeometry(new(2,8),new(26,8)).GetWidenedGeometry(new Pen(Brushes.Black,2,new DashStyle(new double[]{0,2},0),PenLineCap.Round));
        Assert(dotted.FillContains(new(2,8))&&dotted.FillContains(new(6,8))&&!dotted.FillContains(new(4,8)),"Zero-length round dash did not render actual dots.");
        var line=Factory.CreateLineGeometry(new(0,0),new(10,0));Assert(Math.Abs(line.ContourLength-10)<1e-5&&line.TryGetPointAtDistance(5,out var midpoint)&&midpoint==new Point(5,0),"Geometry distance evaluation failed.");
        Assert(line.TryGetSegment(2,6,true,out var segment)&&Math.Abs(segment.ContourLength-4)<1e-5,"Geometry trim did not preserve requested distances.");
        var geometry=Factory.CreateStreamGeometry();using(var context=geometry.Open()){context.BeginFigure(new(0,10),false);context.ArcTo(new(20,10),new(10,10),0,false,SweepDirection.Clockwise);context.EndFigure(false);}
        Assert(geometry.ContourLength>30&&geometry.ContourLength<32&&geometry.Bounds.Width>19,"Unfilled SVG elliptical arc lost its curve or bounds.");
        var clone=geometry.Clone();using(var edit=clone.Open()){edit.BeginFigure(new(3,3));edit.LineTo(new(5,3));edit.LineTo(new(5,5));edit.EndFigure(true);}Assert(clone.FillContains(new(4.5,3.5))&&geometry.ContourLength>30,"Editing cloned geometry changed the original.");
        clone.GetWidenedGeometry(new Pen(Brushes.Black,1));using(var edit=clone.Open()){edit.BeginFigure(new(20,20));edit.LineTo(new(25,20));edit.EndFigure(false);}Assert(clone.GetWidenedGeometry(new Pen(Brushes.Black,1)).FillContains(new(22,20)),"Editing a geometry did not invalidate its stroke cache.");
    }
    private static void RunShaping(string font)
    {
        var collection=new global::Avalonia.Media.Fonts.EmbeddedFontCollection(new Uri("fonts:ShapingValidation"),new Uri(font));FontManager.Current.AddFontCollection(collection);
        var face=new Typeface(collection[0]).GlyphTypeface;var shaper=AvaloniaLocator.Current.GetRequiredService<ITextShaperImpl>();
        ushort aGlyph=face.CharacterToGlyphMap['A'];
        using(var normal=Factory.CreateGlyphRun(face,18,new[]{new GlyphInfo(aGlyph,0,18)},default))using(var shifted=Factory.CreateGlyphRun(face,18,new[]{new GlyphInfo(aGlyph,0,18,new Vector(2,5))},default))
            Assert(Math.Abs(shifted.Bounds.Top-normal.Bounds.Top-5)<1e-5&&Math.Abs(shifted.Bounds.Left-normal.Bounds.Left-2)<1e-5,"GlyphInfo offsets did not follow Avalonia's screen coordinate convention.");
        byte[] GlyphImage(TextRenderingMode mode,EdgeMode edge)
        {
            using var image=Target();using var run=Factory.CreateGlyphRun(face,18,new[]{new GlyphInfo(aGlyph,0,18)},new(3.125,20.375));
            using(var c=image.CreateDrawingContext())
            {c.Clear(Colors.Transparent);c.PushRenderOptions(new(){EdgeMode=edge});c.PushTextOptions(new(){TextRenderingMode=mode,TextHintingMode=TextHintingMode.None,BaselinePixelAlignment=BaselinePixelAlignment.Unaligned});c.DrawGlyphRun(Brushes.Red,run);c.PopTextOptions();c.PopRenderOptions();}
            return Bytes(image);
        }
        var antialiased=GlyphImage(TextRenderingMode.Antialias,EdgeMode.Unspecified);var aliased=GlyphImage(TextRenderingMode.Alias,EdgeMode.Unspecified);
        Assert(antialiased.AsSpan().SequenceEqual(GlyphImage(TextRenderingMode.Antialias,EdgeMode.Aliased)),"Explicit text AA was overridden by shape EdgeMode.");
        Assert(aliased.AsSpan().SequenceEqual(GlyphImage(TextRenderingMode.Unspecified,EdgeMode.Aliased)),"Unspecified text mode did not inherit aliased EdgeMode.");
        Assert(antialiased.Where((_,i)=>i%4==3).Any(a=>a is >0 and <255)&&aliased.Where((_,i)=>i%4==3).All(a=>a is 0 or 255),"Text AA/alias modes did not produce their documented coverage.");
        using(var shaped=shaper.ShapeText("中文 ABC".AsMemory(),new(face,18))){Assert(shaped.Length==6&&shaped[0].GlyphIndex!=0&&shaped[1].GlyphIndex!=0,"Chinese text was not mapped to actual TrueType glyphs.");for(int i=0;i<shaped.Length;i++)Assert(shaped[i].GlyphCluster==i&&shaped[i].GlyphAdvance>=0,"UTF-16 clusters or advances were invalid.");}
        using(var shaped=shaper.ShapeText("A\U00020000B".AsMemory(),new(face,18)))Assert(shaped.Length==3&&shaped[2].GlyphCluster==3,"Supplementary-plane UTF-16 cluster accounting failed.");
        using(var shaped=shaper.ShapeText("A\tB\r\nC".AsMemory(),new(face,18)))Assert(shaped[1].GlyphIndex==face.CharacterToGlyphMap[' ']&&shaped[3].GlyphAdvance==0&&shaped[4].GlyphAdvance==0,"Tabs and line breaks were shaped as visible .notdef boxes.");
        if(Path.GetFileName(font)=="ZCOOLXiaoWei-Regular.ttf")using(var shaped=shaper.ShapeText("AVATAR To WA".AsMemory(),new(face,12)))
        {double[] reference=[6.792,6.672,6.888,6.12,7.62,7.14,3,5.964,6.36,3,9.6,7.62];for(int i=0;i<reference.Length;i++)Assert(Math.Abs(shaped[i].GlyphAdvance-reference[i])<1e-6,"GPOS class/pair kerning differs from the isolated native HarfBuzz fixture.");}
        if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(font)))=="A42B620140F493DB42F741351DFBF343C0936D58588EE8004B8B2A218D997FF1")
        {
            using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","shaping-zcool-native.json")));
            foreach(var sample in fixture.RootElement.GetProperty("cases").EnumerateArray())
            {
                using var actual=shaper.ShapeText(sample.GetProperty("text").GetString()!.AsMemory(),new(face,sample.GetProperty("size").GetDouble()));
                var expected=sample.GetProperty("glyphs");Assert(actual.Length==expected.GetArrayLength(),"Native shaping glyph count differs.");int index=0;
                foreach(var value in expected.EnumerateArray())
                {
                    var glyph=actual[index++];Assert(glyph.GlyphIndex==value.GetProperty("GlyphIndex").GetUInt16()&&glyph.GlyphCluster==value.GetProperty("GlyphCluster").GetInt32(),"Native shaping glyph or UTF-16 cluster differs.");
                    Assert(Math.Abs(glyph.GlyphAdvance-value.GetProperty("GlyphAdvance").GetDouble())<=1e-6&&Math.Abs(glyph.GlyphOffset.X-value.GetProperty("x").GetDouble())<=1e-6&&Math.Abs(glyph.GlyphOffset.Y-value.GetProperty("y").GetDouble())<=1e-6,"Native shaping advance or offset differs.");
                }
            }
        }
        Throws<NotSupportedException>(()=>shaper.ShapeText("العربية".AsMemory(),new(face,18)),"Unsupported Arabic shaping silently returned unshaped text.");
        using(var stream=File.OpenRead(font))
        {
            var manager=AvaloniaLocator.Current.GetRequiredService<IFontManagerImpl>();if(!manager.TryCreateGlyphTypeface(stream,FontSimulations.None,out var platform))throw new InvalidOperationException("Test font registration failed.");
            var synthetic=new GlyphTypeface(platform,FontSimulations.Bold);
            try
            {
                Throws<NotSupportedException>(()=>shaper.ShapeText("A".AsMemory(),new(synthetic,18)),"Synthetic bold was silently shaped with the regular face.");
                Throws<NotSupportedException>(()=>Factory.CreateGlyphRun(synthetic,18,new[]{new GlyphInfo(aGlyph,0,18)},default),"Synthetic bold was silently drawn as regular glyphs.");
            }
            finally{synthetic.Dispose();}
        }
    }
    private static void RunLifetime()
    {
        var bitmap=(IReadableBitmapImpl)Factory.CreateWriteableBitmap(new(8,8),new(96,96),PixelFormats.Bgra8888,AAlpha.Premul);var locked=bitmap.Lock();bitmap.Dispose();Marshal.WriteByte(locked.Address,77);Assert(Marshal.ReadByte(locked.Address)==77,"Pinned bitmap lease did not retain its storage after bitmap disposal.");locked.Dispose();Throws<ObjectDisposedException>(()=>{_=locked.Address;},"Disposed framebuffer pin remained usable.");
        using var target=Target();var context=target.CreateDrawingContext();context.Dispose();context.Dispose();Throws<ObjectDisposedException>(()=>context.Clear(Colors.Red),"Disposed drawing context remained usable.");
        var unbalanced=target.CreateDrawingContext();unbalanced.PushOpacity(.5,null);Throws<InvalidOperationException>(()=>unbalanced.Dispose(),"Unbalanced opacity layer silently disappeared on disposal.");
    }
}
