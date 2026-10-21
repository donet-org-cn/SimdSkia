using SkiaSharp;
using SimdSkia.Imaging;
using SimdSkia;
using SimdSkia.Cpu;
using System.Text.Json;

var results=new List<object>();int failures=0;
void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);results.Add(new {Name=name,Passed=true,Error=(string?)null});}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);results.Add(new {Name=name,Passed=false,Error=e.ToString()});}}
Test("Winding and even-odd holes",()=>
{
    using var b=new SKBitmap(16,16);using var c=new SKCanvas(b);using var p=new SKPaint {Color=SKColors.Red};using var path=new SKPath();
    path.AddRect(SKRect.Create(1,1,14,14));path.AddRect(SKRect.Create(5,5,6,6));c.DrawPath(path,p);Equal(SKColors.Red,b.GetPixel(8,8));
    c.Clear();path.FillType=SKPathFillType.EvenOdd;c.DrawPath(path,p);Equal(SKColor.Empty,b.GetPixel(8,8));Equal(SKColors.Red,b.GetPixel(2,2));
    c.Clear();path.FillType=SKPathFillType.InverseEvenOdd;c.DrawPath(path,p);Equal(SKColors.Red,b.GetPixel(0,0));Equal(SKColor.Empty,b.GetPixel(2,2));
});
Test("Curve fill, implicit close, path snapshots",()=>
{
    using var b=new SKBitmap(32,32);using var c=new SKCanvas(b);using var p=new SKPaint {Color=SKColors.Red,IsAntialias=true};using var path=new SKPath();
    path.MoveTo(2,16);path.QuadTo(16,0,30,16);path.CubicTo(30,30,2,30,2,16);c.DrawPath(path,p);Equal(SKColors.Red,b.GetPixel(16,16));Equal(SKColor.Empty,b.GetPixel(0,0));
    path.Reset();True(path.IsEmpty);Equal(SKColors.Red,b.GetPixel(16,16));
});
Test("AA fractional coverage",()=>
{
    using var b=new SKBitmap(4,4);using var c=new SKCanvas(b);using var p=new SKPaint {Color=SKColors.Red,IsAntialias=true};c.DrawRect(new SKRect(.25f,.25f,2.25f,2.25f),p);
    Equal((byte)143,b.GetPixel(0,0).Alpha);Equal((byte)255,b.GetPixel(1,1).Alpha);Equal((byte)16,b.GetPixel(2,2).Alpha);
});
Test("Curve AA agrees with independent area integration",()=>
{
    using var b=new SKBitmap(64,48);using var c=new SKCanvas(b);using var p=new SKPaint {Color=SKColors.Red,IsAntialias=true};using var path=new SKPath();
    path.MoveTo(3,24);path.QuadTo(32,1,60,24);path.CubicTo(55,44,8,44,3,24);path.Close();c.DrawPath(path,p);
    double area=0;for(int i=0;i<4096;i++){double x=14+(i+.5)/4096,t=(58-Math.Sqrt(3364-4*(x-3)))/2,y=24-46*t+46*t*t;area+=Math.Clamp(17-y,0,1)/4096;}
    True(Math.Abs(b.GetPixel(14,16).Alpha-area*255)<=8);
});
Test("Paint retains shader data and applies shader opacity",()=>
{
    using var b=new SKBitmap(4,4);using var c=new SKCanvas(b);var shader=SKShader.CreateLinearGradient(new(0,0),new(4,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);
    using var p=new SKPaint {Shader=shader,Color=SKColors.White.WithAlpha(128)};shader.Dispose();c.DrawRect(SKRect.Create(4,4),p);Equal((byte)128,b.GetPixel(1,1).Alpha);
    using var transformed=new SKBitmap(16,16);using var drawing=new SKCanvas(transformed);drawing.Translate(4,0);drawing.Scale(2,1);p.Color=SKColors.White;p.IsAntialias=true;
    drawing.DrawLine(0,8,4,8,p);True(transformed.GetPixel(5,7).Red>transformed.GetPixel(5,7).Blue);True(transformed.GetPixel(10,7).Blue>transformed.GetPixel(10,7).Red);
});
Test("Miter stroke and butt line",()=>
{
    using var b=new SKBitmap(32,32);using var c=new SKCanvas(b);using var p=new SKPaint {Color=SKColors.Red,Style=SKPaintStyle.Stroke,StrokeWidth=4};
    c.DrawRect(SKRect.Create(8,8,16,16),p);Equal(SKColors.Red,b.GetPixel(8,8));Equal(SKColor.Empty,b.GetPixel(16,16));
    c.DrawLine(4,16,28,16,p);Equal(SKColors.Red,b.GetPixel(16,16));
});
Test("Saved clip and shader",()=>
{
    using var b=new SKBitmap(16,16);using var c=new SKCanvas(b);using var shader=SKShader.CreateLinearGradient(new(0,0),new(16,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);
    using var p=new SKPaint {Shader=shader};int save=c.Save();c.ClipRect(SKRect.Create(4,4,8,8));c.DrawRect(SKRect.Create(16,16),p);
    Equal(SKColor.Empty,b.GetPixel(0,0));True(b.GetPixel(4,8).Red>b.GetPixel(11,8).Red);c.RestoreToCount(save);p.Shader=null;p.Color=SKColors.White;c.DrawRect(SKRect.Create(1,1),p);Equal(SKColors.White,b.GetPixel(0,0));
});
Test("Bitmap nearest sampling and opacity",()=>
{
    using var source=new SKBitmap(2,1);source.SetPixel(0,0,SKColors.Red);source.SetPixel(1,0,SKColors.Blue);
    using var b=new SKBitmap(8,4);using var c=new SKCanvas(b);using var p=new SKPaint {Color=SKColors.White.WithAlpha(128)};c.DrawBitmap(source,new SKRect(0,0,8,4),p);
    Equal((byte)128,b.GetPixel(1,1).Alpha);Equal((byte)255,b.GetPixel(1,1).Red);Equal((byte)255,b.GetPixel(6,1).Blue);
    using var opaque=new SKBitmap(1,1,true);opaque.Erase(SKColor.Empty);c.Clear();c.DrawBitmap(opaque,0,0);Equal(SKColors.Black,b.GetPixel(0,0));
});
Test("PNG roundtrip and lifecycle",()=>
{
    using var b=new SKBitmap(new SKImageInfo(9,7,SKColorType.Rgba8888,SKAlphaType.Unpremul),44);
    for(int y=0;y<7;y++)for(int x=0;x<9;x++)b.SetPixel(x,y,new SKColor((byte)(x*20),(byte)(y*30),(byte)(x+y),(byte)(x*20+y)));
    using var data=b.Encode(SKEncodedImageFormat.Png,100);using var decoded=SKBitmap.Decode(data.ToArray());
    for(int y=0;y<7;y++)for(int x=0;x<9;x++)Equal(b.GetPixel(x,y),decoded.GetPixel(x,y));
    var corrupt=data.ToArray();corrupt[20]^=1;Throws<InvalidDataException>(()=>SKBitmap.Decode(corrupt));Throws<NotSupportedException>(()=>b.Encode(SKEncodedImageFormat.Webp,100));
    using var jpeg=b.Encode(SKEncodedImageFormat.Jpeg,95);using var jpegDecoded=SKBitmap.Decode(jpeg.ToArray());Equal(jpegDecoded.Width,b.Width);Equal(jpegDecoded.Height,b.Height);
    using var image=SKImage.FromBitmap(b);using var imageData=image.Encode(SKEncodedImageFormat.Png,100);True(imageData.Size>0);
});
Test("Managed DEFLATE stored blocks and checksums",()=>
{
    var input=Enumerable.Range(0,150000).Select(i=>(byte)i).ToArray();var encoded=ManagedZlib.EncodeStored(input);True(input.AsSpan().SequenceEqual(ManagedZlib.Decode(encoded,input.Length)));
    encoded[^1]^=1;Throws<InvalidDataException>(()=>ManagedZlib.Decode(encoded,input.Length));
});
Test("Independent fixed-Huffman DEFLATE fixture",()=>
{
    var encoded=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures/fixed.zlib"));var expected=Enumerable.Range(0,32768).Select(i=>(byte)((i*17+i/7)%256)).ToArray();
    True(expected.AsSpan().SequenceEqual(ManagedZlib.Decode(encoded,expected.Length)));
});
Test("SIMD clear agrees with scalar for formats, stride, alpha, and tails",()=>
{
    foreach(int width in new[]{1,3,7,31,128})foreach(var format in new[]{PixelFormat.Rgba8888,PixelFormat.Bgra8888})foreach(var alpha in new[]{AlphaFormat.Premultiplied,AlphaFormat.Straight,AlphaFormat.Opaque})foreach(byte a in new byte[]{0,1,128,254,255})
    {
        using var scalar=new PixelBuffer(width,3,format,alpha,width*4+3);using var simd=new PixelBuffer(width,3,format,alpha,width*4+3);
        scalar.GetPixelSpan().Fill(0xa5);simd.GetPixelSpan().Fill(0xa5);var color=new Rgba32(32,123,240,a);
        ClearKernel.Fill(scalar,color,false);ClearKernel.Fill(simd,color,true);True(scalar.GetPixelSpan().SequenceEqual(simd.GetPixelSpan()));
        for(int y=0;y<2;y++)for(int x=width*4;x<width*4+3;x++)Equal((byte)0xa5,simd.GetRowSpan(y)[x]);
    }
});
string? fontPath=args.FirstOrDefault(a=>a.EndsWith(".ttf",StringComparison.OrdinalIgnoreCase))??Environment.GetEnvironmentVariable("SIMDSKIA_CJK_FONT");
if(fontPath is null&&OperatingSystem.IsWindows())fontPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"simhei.ttf");
if(fontPath is not null&&File.Exists(fontPath))
{
    Test("Managed Chinese/Latin TrueType glyphs and text",()=>
    {
        using var face=SKTypeface.FromFile(fontPath);using var font=new SKFont(face,28);True(face.GetGlyph('中')!=0);True(face.GetGlyph('A')!=0);
        using var glyph=font.GetGlyphPath(face.GetGlyph('中'));True(!glyph.IsEmpty);True(font.MeasureText("中文 ABC")>100);
        using var b=new SKBitmap(240,80);using var c=new SKCanvas(b);c.Clear(SKColors.White);using var p=new SKPaint {Color=SKColors.Black};c.DrawText("中文 ABC",8,48,font,p);
        int dark=0;for(int y=0;y<80;y++)for(int x=0;x<240;x++)if(b.GetPixel(x,y).Red<128)dark++;True(dark>300);
        using var data=b.Encode(SKEncodedImageFormat.Png,100);Directory.CreateDirectory("artifacts");using var file=File.Create("artifacts/chinese.png");data.SaveTo(file);
        var layout=TextRendering.DrawTextBlock(c,"中文 ABC\r\n中文 English words wrap",font,p,new SKRect(0,0,120,80));True(layout.Lines.Length>=3);True(layout.Height>0);
        foreach(var line in layout.Lines)True(line.Width<=120);
        Equal(1,c.SaveCount);
        face.Dispose();True(font.MeasureText("中文")>0);
        Throws<NotSupportedException>(()=>font.MeasureText("العربية"));Throws<NotSupportedException>(()=>font.Hinting=SKFontHinting.Normal);
    });
}
else Console.WriteLine("NOT RUN: Chinese font test requires a real glyf CJK font path or SIMDSKIA_CJK_FONT; no font is fabricated.");
if(args.Contains("--reference-pngs"))Test("Decode native-encoded compressed PNG fixtures",()=>
{
    foreach(string name in new[]{"rectangle","blend","path-aa","gradient","image","chinese"})
    {
        string stem=Path.Combine("artifacts/reference",name+"-Rgba8888-Premul");using var decoded=SKBitmap.Decode(stem+".png");var expected=File.ReadAllBytes(stem+".rgba");
        for(int y=0;y<decoded.Height;y++)for(int x=0;x<decoded.Width;x++)
        {var c=decoded.GetPixel(x,y);int i=(y*decoded.Width+x)*4;True(Math.Abs(c.Red-expected[i])<=1);True(Math.Abs(c.Green-expected[i+1])<=1);True(Math.Abs(c.Blue-expected[i+2])<=1);Equal(c.Alpha,expected[i+3]);}
    }
});
Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/features.json",JsonSerializer.Serialize(new {Total=results.Count,Failures=failures,CjkFont=fontPath,Results=results},new JsonSerializerOptions {WriteIndented=true}));return failures==0?0:1;
static void True(bool value){if(!value)throw new Exception("Expected true.");}
static void Equal<T>(T a,T b){if(!EqualityComparer<T>.Default.Equals(a,b))throw new Exception($"Expected {a}, got {b}.");}
static void Throws<T>(Action a)where T:Exception{try{a();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
