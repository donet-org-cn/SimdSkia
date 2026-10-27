using SkiaSharp;
using SimdSkia;
using SimdSkia.Cpu;
using System.Text.Json;

var tests=new (string Name,Action Run)[]
{
    ("Bitmap -> canvas -> clear -> rect -> every pixel", () =>
    {
        using var bitmap=new SKBitmap(20,16); using var canvas=new SKCanvas(bitmap);
        canvas.Clear(SKColors.White); using var paint=new SKPaint { Color=SKColors.Red };
        canvas.DrawRect(SKRect.Create(2,3,8,5),paint);
        for(int y=0;y<16;y++) for(int x=0;x<20;x++) Equal(x>=2&&x<10&&y>=3&&y<8 ? SKColors.Red : SKColors.White,bitmap.GetPixel(x,y));
    }),
    ("Rgba8888 and Bgra8888 byte order", () =>
    {
        foreach(var format in new[] {SKColorType.Rgba8888,SKColorType.Bgra8888})
        {
            using var bitmap=new SKBitmap(new SKImageInfo(1,1,format,SKAlphaType.Unpremul));
            bitmap.SetPixel(0,0,new SKColor(10,20,30,40)); var b=bitmap.Bytes;
            Equal(format==SKColorType.Rgba8888 ? (byte)10 : (byte)30,b[0]); Equal((byte)20,b[1]);
            Equal(format==SKColorType.Rgba8888 ? (byte)30 : (byte)10,b[2]); Equal((byte)40,b[3]);
        }
    }),
    ("Padded rows and last row byte count", () =>
    {
        using var bitmap=new SKBitmap(new SKImageInfo(3,2,SKColorType.Rgba8888),20);
        bitmap.GetPixelSpan().Fill(0xa5); using var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.Red);
        Equal(20,bitmap.RowBytes); Equal(32,bitmap.ByteCount); Equal(32,bitmap.Bytes.Length);
        for(int i=12;i<20;i++) Equal((byte)0xa5,bitmap.Bytes[i]); Equal(SKColors.Red,bitmap.GetPixel(2,1));
    }),
    ("Half-pixel edges follow baseline rounding", () =>
    {
        using var bitmap=new SKBitmap(4,4); using var canvas=new SKCanvas(bitmap); using var paint=new SKPaint { Color=SKColors.Red };
        canvas.DrawRect(new SKRect(0.5f,0.5f,2.5f,2.5f),paint);
        Equal(SKColor.Empty,bitmap.GetPixel(0,0)); Equal(SKColors.Red,bitmap.GetPixel(2,2)); Equal(SKColor.Empty,bitmap.GetPixel(3,3));
    }),
    ("Opaque metadata preserves Src and Clear physical alpha", () =>
    {
        using var bitmap=new SKBitmap(1,1,true); using var canvas=new SKCanvas(bitmap);
        using var paint=new SKPaint { Color=new SKColor(200,100,50,128),BlendMode=SKBlendMode.Src };
        canvas.DrawRect(SKRect.Create(1,1),paint); Equal(new SKColor(100,50,25,128),bitmap.GetPixel(0,0));
        paint.BlendMode=SKBlendMode.Clear; canvas.DrawRect(SKRect.Create(1,1),paint); Equal(SKColor.Empty,bitmap.GetPixel(0,0));
        paint.BlendMode=SKBlendMode.SrcOver; canvas.DrawRect(SKRect.Create(1,1),paint); Equal(new SKColor(100,50,25,128),bitmap.GetPixel(0,0));
    }),
    ("Premultiplied storage and unpremultiply", () =>
    {
        using var bitmap=new SKBitmap(new SKImageInfo(1,1,SKColorType.Rgba8888,SKAlphaType.Premul));
        bitmap.SetPixel(0,0,new SKColor(200,100,50,128)); var b=bitmap.Bytes;
        Equal((byte)100,b[0]); Equal((byte)50,b[1]); Equal((byte)25,b[2]); Equal((byte)128,b[3]);
        var c=bitmap.GetPixel(0,0); Near(200,c.Red,1); Near(100,c.Green,1); Near(50,c.Blue,1);
    }),
    ("Alpha zero storage is canonical black", () =>
    {
        using var bitmap=new SKBitmap(1,1); bitmap.SetPixel(0,0,new SKColor(200,100,50,0)); Equal(SKColor.Empty,bitmap.GetPixel(0,0));
        foreach(var b in bitmap.Bytes) Equal((byte)0,b);
    }),
    ("SourceOver on opaque destination", () =>
    {
        using var bitmap=new SKBitmap(1,1); using var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.Blue);
        using var paint=new SKPaint { Color=new SKColor(255,0,0,128) }; canvas.DrawRect(SKRect.Create(1,1),paint);
        Equal(new SKColor(128,0,127,255),bitmap.GetPixel(0,0));
    }),
    ("Source replaces alpha", () =>
    {
        using var bitmap=new SKBitmap(1,1); using var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.Blue);
        using var paint=new SKPaint { Color=new SKColor(255,0,0,128),BlendMode=SKBlendMode.Src }; canvas.DrawRect(SKRect.Create(1,1),paint);
        Equal(new SKColor(255,0,0,128),bitmap.GetPixel(0,0));
    }),
    ("Clear blend replaces pixels with transparent", () =>
    {
        using var bitmap=new SKBitmap(3,3); using var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.Blue);
        using var paint=new SKPaint { BlendMode=SKBlendMode.Clear }; canvas.DrawRect(SKRect.Create(1,1,1,1),paint);
        Equal(SKColor.Empty,bitmap.GetPixel(1,1)); Equal(SKColors.Blue,bitmap.GetPixel(0,0));
    }),
    ("Transparent source leaves destination", () =>
    {
        using var bitmap=new SKBitmap(1,1); using var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.Blue);
        using var paint=new SKPaint { Color=SKColors.Transparent }; canvas.DrawRect(SKRect.Create(1,1),paint); Equal(SKColors.Blue,bitmap.GetPixel(0,0));
    }),
    ("Straight alpha SourceOver", () =>
    {
        using var bitmap=new SKBitmap(new SKImageInfo(1,1,SKColorType.Rgba8888,SKAlphaType.Unpremul)); using var canvas=new SKCanvas(bitmap);
        canvas.Clear(new SKColor(0,0,255,128)); using var paint=new SKPaint { Color=new SKColor(255,0,0,128) };
        canvas.DrawRect(SKRect.Create(1,1),paint); var c=bitmap.GetPixel(0,0); Near(170,c.Red,1); Near(85,c.Blue,1); Equal((byte)192,c.Alpha);
    }),
    ("Device bounds clip rectangle", () =>
    {
        using var bitmap=new SKBitmap(3,3); using var canvas=new SKCanvas(bitmap); canvas.Clear(); using var paint=new SKPaint { Color=SKColors.Red };
        canvas.DrawRect(SKRect.Create(-100,-100,102,102),paint); Equal(SKColors.Red,bitmap.GetPixel(1,1)); Equal(SKColor.Empty,bitmap.GetPixel(2,2));
    }),
    ("Huge offscreen coordinates safely clipped", () =>
    {
        using var bitmap=new SKBitmap(2,2); using var canvas=new SKCanvas(bitmap); using var paint=new SKPaint { Color=SKColors.Red };
        canvas.DrawRect(new SKRect(-1e20f,-1e20f,1e20f,1e20f),paint); Equal(SKColors.Red,bitmap.GetPixel(1,1));
    }),
    ("Empty geometry has no coverage", () =>
    {
        using var bitmap=new SKBitmap(2,2); using var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.Blue); using var paint=new SKPaint();
        canvas.DrawRect(new SKRect(1,0,1,2),paint); Equal(SKColors.Blue,bitmap.GetPixel(0,0));
    }),
    ("Save returns previous count and restore restores matrix", () =>
    {
        using var bitmap=new SKBitmap(8,8); using var canvas=new SKCanvas(bitmap); Equal(1,canvas.Save()); Equal(2,canvas.SaveCount);
        canvas.Translate(2,3); canvas.Scale(2,2); Equal(new SKPoint(4,5),canvas.TotalMatrix.MapPoint(1,1));
        canvas.RestoreToCount(1); Equal(SKMatrix.Identity,canvas.TotalMatrix); canvas.Restore(); Equal(1,canvas.SaveCount);
    }),
    ("Clear ignores transform", () =>
    {
        using var bitmap=new SKBitmap(2,2); using var canvas=new SKCanvas(bitmap); canvas.Translate(100,100); canvas.Clear(SKColors.Red); Equal(SKColors.Red,bitmap.GetPixel(0,0));
    }),
    ("Affine translate-scale renders", () =>
    {
        using var bitmap=new SKBitmap(8,8); using var canvas=new SKCanvas(bitmap); canvas.Translate(2,1); canvas.Scale(2,3); using var paint=new SKPaint { Color=SKColors.Red };
        canvas.DrawRect(SKRect.Create(1,1),paint); Equal(SKColors.Red,bitmap.GetPixel(3,3)); Equal(SKColor.Empty,bitmap.GetPixel(4,3));
    }),
    ("Singular affine matrix has empty coverage", () =>
    {
        using var bitmap=new SKBitmap(2,2); using var canvas=new SKCanvas(bitmap); canvas.Scale(0); using var paint=new SKPaint(); canvas.DrawRect(SKRect.Create(2,2),paint); Equal(SKColor.Empty,bitmap.GetPixel(0,0));
    }),
    ("Matrix inverse round trip", () =>
    {
        var m=new SKMatrix(2,0.25f,12,-0.5f,3,8,0,0,1); True(m.TryInvert(out var inverse)); var p=inverse.MapPoint(m.MapPoint(4,5));
        Near(4,p.X,0.00001f); Near(5,p.Y,0.00001f); True(!SKMatrix.CreateScale(0,1).TryInvert(out _));
    }),
    ("Bitmap survives canvas disposal", () =>
    {
        using var bitmap=new SKBitmap(2,2); var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.Red); canvas.Dispose(); canvas.Dispose();
        Equal(SKColors.Red,bitmap.GetPixel(0,0)); Throws<ObjectDisposedException>(()=>canvas.Clear());
    }),
    ("Canvas retains pixel storage after bitmap disposal", () =>
    {
        var bitmap=new SKBitmap(2,2); using var canvas=new SKCanvas(bitmap); var storage=bitmap.GetPixelSpan(); bitmap.Dispose(); bitmap.Dispose();
        canvas.Clear(SKColors.Red); Equal(1,canvas.Save()); Equal((byte)255,storage[2]); Throws<ObjectDisposedException>(()=>bitmap.GetPixel(0,0));
    }),
    ("Surface owns canvas lifetime", () =>
    {
        var surface=SKSurface.Create(new SKImageInfo(4,4)) ?? throw new Exception("Surface allocation failed."); var canvas=surface.Canvas;
        canvas.Clear(SKColors.Red); surface.Dispose(); surface.Dispose(); Throws<ObjectDisposedException>(()=>canvas.Clear()); Throws<ObjectDisposedException>(()=>_ = surface.Canvas);
    }),
    ("Surface snapshot owns independent pixels after disposal", () =>
    {
        var surface=SKSurface.Create(new SKImageInfo(2,2))!; surface.Canvas.Clear(SKColors.Red); using var image=surface.Snapshot();
        surface.Canvas.Clear(SKColors.Blue); surface.Dispose(); using var bitmap=SKBitmap.FromImage(image); Equal(SKColors.Red,bitmap.GetPixel(1,1));
        bitmap.Erase(SKColors.White); using var second=SKBitmap.FromImage(image); Equal(SKColors.Red,second.GetPixel(0,0));
    }),
    ("Image conversion rejects disposed images and null", () =>
    {
        using var bitmap=new SKBitmap(1,1); var image=SKImage.FromBitmap(bitmap); image.Dispose();
        Throws<ObjectDisposedException>(()=>SKBitmap.FromImage(image)); Throws<ArgumentNullException>(()=>SKBitmap.FromImage(null!));
    }),
    ("Bitmap copy preserves stride and independent bytes", () =>
    {
        using var bitmap=new SKBitmap(new SKImageInfo(2,2),12); bitmap.Erase(SKColors.Red); using var copy=bitmap.Copy();
        Equal(12,copy.RowBytes); bitmap.Erase(SKColors.Blue); Equal(SKColors.Red,copy.GetPixel(1,1));
    }),
    ("Disposed paint rejects drawing", () =>
    {
        using var bitmap=new SKBitmap(1,1); using var canvas=new SKCanvas(bitmap); var paint=new SKPaint(); paint.Dispose(); Throws<ObjectDisposedException>(()=>canvas.DrawRect(SKRect.Create(1,1),paint));
    }),
    ("Coordinate validation", () =>
    {
        using var bitmap=new SKBitmap(1,1); Throws<ArgumentOutOfRangeException>(()=>bitmap.GetPixel(-1,0)); Throws<ArgumentOutOfRangeException>(()=>bitmap.SetPixel(0,1,SKColors.Red));
        using var canvas=new SKCanvas(bitmap); using var paint=new SKPaint(); Throws<ArgumentException>(()=>canvas.DrawRect(new SKRect(float.NaN,0,1,1),paint));
        Throws<ArgumentNullException>(()=>canvas.DrawRect(SKRect.Create(1,1),null!)); Throws<ArgumentNullException>(()=>new SKCanvas(null!));
    }),
    ("Invalid allocation and checked overflow", () =>
    {
        using var bitmap=new SKBitmap(); True(!bitmap.TryAllocPixels(new SKImageInfo(-1,4))); True(!bitmap.TryAllocPixels(new SKImageInfo(3,3),11));
        True(!bitmap.TryAllocPixels(new SKImageInfo(int.MaxValue,2),0)); Equal(0,bitmap.ByteCount);
        True(SKSurface.Create(new SKImageInfo(0,4)) is null);
    }),
    ("Unsupported features throw before mutation", () =>
    {
        using var bitmap=new SKBitmap(2,2); using var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.Blue); using var paint=new SKPaint { Style=SKPaintStyle.StrokeAndFill };
        Throws<NotSupportedException>(()=>canvas.DrawRect(SKRect.Create(2,2),paint)); paint.Style=SKPaintStyle.Stroke;paint.StrokeCap=SKStrokeCap.Round;
        Throws<NotSupportedException>(()=>canvas.DrawRect(SKRect.Create(2,2),paint)); paint.Style=SKPaintStyle.Fill; paint.BlendMode=SKBlendMode.Multiply;
        Throws<NotSupportedException>(()=>canvas.DrawRect(SKRect.Create(2,2),paint)); Equal(SKColors.Blue,bitmap.GetPixel(0,0));
    }),
    ("Unknown formats and native handles fail explicitly", () =>
    {
        Throws<NotSupportedException>(()=>new SKBitmap(new SKImageInfo(1,1,SKColorType.RgbaF16)));
        Throws<NotSupportedException>(()=>SKColorSpace.CreateSrgbLinear()); using var bitmap=new SKBitmap(1,1);
        if(bitmap.GetPixels()==IntPtr.Zero)throw new Exception("Allocated bitmap must expose pinned pixels."); Throws<NotSupportedException>(()=>_ = bitmap.Handle);
        Throws<NotSupportedException>(()=>SKSurface.Create(new SKImageInfo(1,1),IntPtr.Zero));
    }),
    ("Perspective and nonfinite matrices rejected atomically", () =>
    {
        using var bitmap=new SKBitmap(1,1); using var canvas=new SKCanvas(bitmap); var m=SKMatrix.Identity; m.Persp0=0.1f;
        Throws<NotSupportedException>(()=>canvas.SetMatrix(in m)); Equal(SKMatrix.Identity,canvas.TotalMatrix);
        Throws<ArgumentException>(()=>canvas.Translate(float.PositiveInfinity,0)); Equal(SKMatrix.Identity,canvas.TotalMatrix);
    }),
    ("sRGB metadata supported; disposed profiles rejected", () =>
    {
        using var srgb=SKColorSpace.CreateSrgb(); using var bitmap=new SKBitmap(new SKImageInfo(1,1,SKColorType.Rgba8888,SKAlphaType.Premul,srgb));
        Equal(srgb,bitmap.ColorSpace!); srgb.Dispose(); Throws<ObjectDisposedException>(()=>new SKBitmap(bitmap.Info));
    }),
    ("Color packing and value equality", () =>
    {
        var c=new SKColor(0x80402010); Equal((byte)128,c.Alpha); Equal((byte)64,c.Red); Equal((byte)32,c.Green); Equal((byte)16,c.Blue);
        Equal("#80402010",c.ToString()); Equal(0x80402010u,(uint)c); Equal(new SKColor(64,32,16,128),c); Equal((byte)255,c.WithAlpha(255).Alpha);
    }),
    ("Backend unknown command fails explicitly", () =>
    {
        using var buffer=new PixelBuffer(1,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied); using var backend=new RasterCanvas(buffer);
        var c=new DrawingCommand((CommandKind)999,default,Matrix3.Identity,default); Throws<NotSupportedException>(()=>backend.Execute(c));
    }),
    ("Warm drawing loop has zero thread allocations", () =>
    {
        using var bitmap=new SKBitmap(8,8); using var canvas=new SKCanvas(bitmap); using var paint=new SKPaint { Color=SKColors.Red };
        var rect=SKRect.Create(8,8); for(int i=0;i<20;i++) canvas.DrawRect(rect,paint);
        long start=GC.GetAllocatedBytesForCurrentThread(); for(int i=0;i<100;i++) { canvas.Clear(); canvas.DrawRect(rect,paint); }
        Equal(0L,GC.GetAllocatedBytesForCurrentThread()-start);
    })
};
var results=new List<object>(); int failures=0;
foreach(var test in tests)
{
    try { test.Run(); Console.WriteLine("PASS "+test.Name); results.Add(new { test.Name,Passed=true,Error=(string?)null }); }
    catch(Exception e) { failures++; Console.WriteLine("FAIL "+test.Name+": "+e); results.Add(new { test.Name,Passed=false,Error=e.ToString() }); }
}
Directory.CreateDirectory("artifacts");
File.WriteAllText("artifacts/tests.json",JsonSerializer.Serialize(new { Total=tests.Length,Passed=tests.Length-failures,Failed=failures,Tests=results },new JsonSerializerOptions { WriteIndented=true }));
Console.WriteLine($"{tests.Length-failures}/{tests.Length} tests passed.");
return failures==0 ? 0 : 1;

static void True(bool value) { if(!value) throw new Exception("Expected true."); }
static void Equal<T>(T expected,T actual) { if(!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}, got {actual}."); }
static void Near(float expected,float actual,float tolerance) { if(!float.IsFinite(actual)||MathF.Abs(expected-actual)>tolerance) throw new Exception($"Expected {expected} ± {tolerance}, got {actual}."); }
static void Throws<T>(Action action) where T:Exception
{
    try { action(); } catch(T) { return; } throw new Exception("Expected "+typeof(T).Name);
}
