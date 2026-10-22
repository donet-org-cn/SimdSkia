using SimdSkia;
using SimdSkia.Cpu;
using SimdSkia.OptimizationReference;
using SkiaSharp;
using SimdSkia.Imaging;
using System.Text.Json;

var random=new Random(20261012);
int checks=0;
float Next(float min,float max)=>min+(float)random.NextDouble()*(max-min);
Rgba32 Color()=>new((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256));
void Equal(PixelBuffer expected,PixelBuffer actual,string label)
{
    var a=expected.GetPixelSpan();var b=actual.GetPixelSpan();
    if(!a.SequenceEqual(b))
    {int i=0;while(i<a.Length&&a[i]==b[i])i++;throw new Exception($"{label}: byte {i} expected {a[i]}, actual {b[i]}");}
    checks++;
}
// Native opaque alpha metadata overrides physical A bytes. Exercise direct
// row copies, mirrored mappings, fractional clips, padding and scalar tails.
foreach(bool enabled in new[]{false,true})
foreach(var format in Enum.GetValues<PixelFormat>())
foreach(var alpha in Enum.GetValues<AlphaFormat>())
foreach(int width in new[]{1,7,8,9,31,32,33,65})
foreach(var mode in Enum.GetValues<CompositeMode>())
{
    CpuOptions.EnableSimd=enabled;
    using var image=new PixelBuffer(width,9,format,AlphaFormat.Opaque,width*4+3);random.NextBytes(image.GetPixelSpan());
    using var expected=new PixelBuffer(97,29,format,alpha,97*4+5);using var actual=new PixelBuffer(97,29,format,alpha,97*4+5);
    using var baseline=new BaselineRasterCanvas(expected);using var canvas=new RasterCanvas(actual);
    foreach(float offset in new[]{.0f,.5f,-4.25f})
    {
        random.NextBytes(expected.GetPixelSpan());expected.GetPixelSpan().CopyTo(actual.GetPixelSpan());
        var command=new DrawingCommand(CommandKind.DrawImage,new RectF(0,0,width,9),new Matrix3(1,0,offset,0,1,7.5f,0,0,1),new(new(255,255,255,255),mode))
            {Image=image,Clip=new RectF(1,3,87,25)};
        baseline.Execute(command);canvas.Execute(command);Equal(expected,actual,$"Opaque image row copy {enabled}/{format}/{alpha}/{width}/{mode}/{offset}");
    }
}
foreach(bool simd in new[]{false,true})
foreach(var format in Enum.GetValues<PixelFormat>())
foreach(var alpha in Enum.GetValues<AlphaFormat>())
{
    CpuOptions.EnableSimd=simd;
    using var original=new PixelBuffer(97,63,format,alpha,97*4+7);
    using var optimized=new PixelBuffer(97,63,format,alpha,97*4+7);
    using var baseline=new BaselineRasterCanvas(original);using var canvas=new RasterCanvas(optimized);
    using var image=new PixelBuffer(11,7,format,alpha,11*4+5);random.NextBytes(image.GetPixelSpan());
    for(int iteration=0;iteration<70;iteration++)
    {
        random.NextBytes(original.GetPixelSpan());original.GetPixelSpan().CopyTo(optimized.GetPixelSpan());
        var matrix=(iteration%5) switch
        {
            0=>Matrix3.Identity,
            1=>new Matrix3(-1.3f,0,93.125f,0,.75f,5.375f,0,0,1),
            2=>new Matrix3(.85f,.27f,1.5f,-.13f,1.15f,4.125f,0,0,1),
            3=>new Matrix3(1.4f,0,-5.25f,0,-1.1f,66.25f,0,0,1),
            _=>new Matrix3(.5f,0,1,0,.7f,1,0,0,1)
        };
        RectF? clip=iteration%3==0?new(Next(-10,20),Next(-10,20),Next(75,100),Next(48,75)):null;
        var paint=new Paint(Color(),(CompositeMode)(iteration%3),iteration%2==0,
            iteration%4==0?new LinearGradient(new(0,0),new(iteration%8==0?97:41,iteration%8==0?0:63),Color(),Color()):null);
        float l=Next(-20,40),t=Next(-20,20);var rect=new RectF(l,t,l+Next(0,110),t+Next(0,85));
        var command=DrawingCommand.FillRectangle(rect,matrix,paint) with {Clip=clip};
        baseline.Execute(command);canvas.Execute(command);Equal(original,optimized,$"rectangle {simd} {format} {alpha} {iteration}");
        var geometry=new PathGeometry(new PathSegment[]{
            new(PathVerb.Move,new(Next(-20,30),Next(-20,30))),
            new(PathVerb.Quadratic,new(Next(0,120),Next(-20,80)),new(Next(40,130),Next(0,80))),
            new(PathVerb.Cubic,new(Next(0,100),Next(0,70)),new(Next(0,100),Next(0,70)),new(Next(-20,60),Next(40,100))),
            new(PathVerb.Close,default)},(FillRule)(iteration%4));
        command=new(CommandKind.FillPath,default,matrix,paint){Path=geometry,Clip=clip};
        baseline.Execute(command);canvas.Execute(command);Equal(original,optimized,$"path {simd} {format} {alpha} {iteration}");
        // Same mask, changed color and blend: cache keys must not capture paint or target pixels.
        command=command with {Paint=paint with {Color=Color(),Shader=null,Composite=(CompositeMode)((iteration+1)%3)}};
        baseline.Execute(command);canvas.Execute(command);Equal(original,optimized,$"cached path {iteration}");
        command=new(CommandKind.DrawImage,rect,matrix,paint with {Shader=null}){Image=image,Clip=clip};
        baseline.Execute(command);canvas.Execute(command);Equal(original,optimized,$"image {iteration}");
    }
    // Exact half-grid edges, reversed coordinates, collapsed clips and zero-area rectangles.
    for(int k=-8;k<24;k++)
    {
        var rect=new RectF(9+k/16f,7.5f,22.5f,19+k/16f);
        var paint=new Paint(new(211,17,87,123),CompositeMode.SourceOver,true);
        foreach(var r in new[]{rect,new RectF(rect.Right,rect.Bottom,rect.Left,rect.Top),new RectF(4,3,4,17)})
        {
            var command=DrawingCommand.FillRectangle(r,Matrix3.Identity,paint);
            baseline.Execute(command);canvas.Execute(command);Equal(original,optimized,$"sample-grid {k}");
        }
    }
}
// Dense small AA contours and adversarial edge order exercise both coverage representations,
// all blend/alpha/stride combinations and coherent-sort fallback against the frozen renderer.
foreach(bool simd in new[]{false,true})foreach(var format in Enum.GetValues<PixelFormat>())foreach(var alpha in Enum.GetValues<AlphaFormat>())
{
    CpuOptions.EnableSimd=simd;
    using var a=new PixelBuffer(97,63,format,alpha,397);using var b=new PixelBuffer(97,63,format,alpha,397);
    using var reference=new BaselineRasterCanvas(a);using var actual=new RasterCanvas(b);
    var segments=new List<PathSegment>();
    for(int i=0;i<160;i++)
    {
        float x=Next(-10,95),y=Next(-10,60);
        segments.AddRange(PathGeometry.Rectangle(new(x,y,x+Next(.03f,14),y+Next(.03f,15))).Segments.ToArray());
    }
    foreach(var rule in Enum.GetValues<FillRule>())
    {
        var geometry=new PathGeometry(segments,rule);
        foreach(var mode in Enum.GetValues<CompositeMode>())
        {
            var paint=new Paint(Color(),mode,true);
            var c=new DrawingCommand(CommandKind.FillPath,default,new(1,0,.03125f,0,1,.0625f,0,0,1),paint){Path=geometry,Clip=new(2.03125f,3.09375f,92.96875f,59.9375f)};
            reference.Execute(c);actual.Execute(c);Equal(a,b,"dense mask all layouts");
            c=c with {Paint=paint with {Shader=new LinearGradient(new(0,0),new(97,63),Color(),Color())}};
            reference.Execute(c);actual.Execute(c);Equal(a,b,"dense cache changed shader");
        }
    }
}
// Opaque horizontal gradient row caching: rectangle/clip/transform changes.
foreach(var format in Enum.GetValues<PixelFormat>())foreach(var alpha in Enum.GetValues<AlphaFormat>())
using(var a=new PixelBuffer(97,63,format,alpha,397))using(var b=new PixelBuffer(97,63,format,alpha,397))
using(var reference=new BaselineRasterCanvas(a))using(var actual=new RasterCanvas(b))
{
    var gradient=new LinearGradient(new(3,5),new(72,5),new(253,3,217,255),new(4,173,2,255));
    for(int k=0;k<12;k++)
    {
        var matrix=new Matrix3(k%2==0?1:-1.3f,0,k%2==0?0:95,0,.8f,2,0,0,1);
        var c=DrawingCommand.FillRectangle(new(2+k,2,85-k,51),matrix,new(new(255,255,255,255),(CompositeMode)(k%2),false,gradient)) with {Clip=new(7+k,3,82-k,48)};
        reference.Execute(c);actual.Execute(c);Equal(a,b,"gradient row cache");reference.Execute(c);actual.Execute(c);Equal(a,b,"gradient row reuse");
    }
}
// An extremely tall narrow target loses float subpixel precision: use the general reference grid.
using(var a=new PixelBuffer(2,524291,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))
using(var b=new PixelBuffer(2,524291,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))
using(var reference=new BaselineRasterCanvas(a))using(var actual=new RasterCanvas(b))
{
    var c=DrawingCommand.FillRectangle(new(.25f,524288.25f,1.75f,524289.75f),Matrix3.Identity,new(new(211,23,79,133),CompositeMode.SourceOver,true));
    reference.Execute(c);actual.Execute(c);Equal(a,b,"large float-grid precision");
}
// Sampling the same underlying buffer must preserve read/write order; mutating a source is visible.
using(var a=new PixelBuffer(97,63,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))
using(var b=new PixelBuffer(97,63,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))
using(var reference=new BaselineRasterCanvas(a))using(var actual=new RasterCanvas(b))
{
    random.NextBytes(a.GetPixelSpan());a.GetPixelSpan().CopyTo(b.GetPixelSpan());
    var c=new DrawingCommand(CommandKind.DrawImage,new(2,2,97,63),Matrix3.Identity,new(new(255,255,255,123),CompositeMode.SourceOver)){Image=a};
    reference.Execute(c);actual.Execute(c with {Image=b});Equal(a,b,"aliased sampling");
}
// LRU eviction: draw more unique immutable geometries than the coverage cache capacity.
using(var a=new PixelBuffer(97,63,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))
using(var b=new PixelBuffer(97,63,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))
using(var reference=new BaselineRasterCanvas(a))
using(var actual=new RasterCanvas(b))
{
    var paths=Enumerable.Range(0,100).Select(i=>PathGeometry.Rectangle(new(i%70+.25f,2.25f,i%70+15.75f,49.75f))).ToArray();
    foreach(var path in paths.Concat(paths.Reverse()))
    {var c=new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,new(Color(),CompositeMode.SourceOver,true)){Path=path};reference.Execute(c);actual.Execute(c);Equal(a,b,"eviction");}
}
// Compatibility mutation/lifetime tests for cached path snapshots, font parameters and bitmap data.
using(var bitmap=new SKBitmap(80,60))
using(var canvas=new SKCanvas(bitmap))
using(var paint=new SKPaint{Color=SKColors.Red})
using(var path=new SKPath())
{
    path.AddRect(new(2,2,10,10));canvas.DrawPath(path,paint);canvas.Clear();path.Reset();path.AddRect(new(30,30,40,40));canvas.DrawPath(path,paint);
    if(bitmap.GetPixel(3,3)!=SKColors.Empty||bitmap.GetPixel(35,35)!=SKColors.Red)throw new Exception("Path mutation reused a stale mask.");checks++;
}
using(var source=new SKBitmap(2,1))using(var bitmap=new SKBitmap(24,16))using(var canvas=new SKCanvas(bitmap))
{
    source.Erase(SKColors.Red);canvas.DrawBitmap(source,new SKRect(0,0,24,16));source.Erase(SKColors.Blue);canvas.DrawBitmap(source,new SKRect(0,0,24,16));
    if(bitmap.GetPixel(12,8)!=SKColors.Blue)throw new Exception("Mutable source bitmap was cached.");checks++;
}
using(var reused=new SKBitmap(97,63))using(var fresh=new SKBitmap(97,63))using(var actual=new SKCanvas(reused))using(var expected=new SKCanvas(fresh))
using(var paint=new SKPaint{Style=SKPaintStyle.Stroke,Color=SKColors.Red,IsAntialias=true})
{
    var rect=new SKRect(10,10,70,45);
    foreach(float width in new[]{2f,5f,0f,2f})foreach(float scale in new[]{1f,1.15f})
    {
        actual.ResetMatrix();expected.ResetMatrix();actual.Scale(scale);expected.Scale(scale);paint.StrokeWidth=width;
        actual.Clear();expected.Clear();actual.DrawRect(rect,paint);
        using(var path=new SKPath()){path.AddRect(rect);expected.DrawPath(path,paint);}
        if(!reused.GetPixelSpan().SequenceEqual(fresh.GetPixelSpan()))throw new Exception("Stroke parameter/matrix invalidation.");checks++;
    }
}
if(args.Length>0)
{
    using var face=SKTypeface.FromFile(args[0]);using var font=new SKFont(face,19);
    foreach(float size in new[]{19f,27f,19f})foreach(float scale in new[]{1f,.85f})foreach(float skew in new[]{0f,.2f})
    {
        font.Size=size;font.ScaleX=scale;font.SkewX=skew;
        using var fresh=new SKFont(face,size,scale,skew);
        using var a=new SKBitmap(240,100);using var b=new SKBitmap(240,100);using var ca=new SKCanvas(a);using var cb=new SKCanvas(b);using var paint=new SKPaint{Color=SKColors.White};
        for(int i=0;i<2;i++){ca.Clear();cb.Clear();ca.DrawText("Simd 中文",5,65,font,paint);cb.DrawText("Simd 中文",5,65,fresh,paint);}
        if(!a.GetPixelSpan().SequenceEqual(b.GetPixelSpan()))throw new Exception("Font parameter invalidation.");checks++;
    }
}
using(var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/codecs.json"))))
{
    foreach(var entry in fixture.RootElement.GetProperty("Deflate").EnumerateArray())
    {
        int length=entry.GetProperty("Length").GetInt32();string pattern=entry.GetProperty("Pattern").GetString()!;var expected=new byte[length];uint state=20261012;
        for(int i=0;i<length;i++)
        {
            state=unchecked(state*1664525+1013904223);
            expected[i]=pattern switch {"zero"=>0,"cycle"=>(byte)((i*17+i/7)%256),"runs"=>(byte)(i/1000),"high"=>255,"noise"=>(byte)(state>>24),_=>throw new Exception("Unknown fixture pattern.")};
        }
        var encoded=Convert.FromBase64String(entry.GetProperty("Encoded").GetString()!);
        var actual=ManagedZlib.Decode(encoded,length);
        if(!expected.AsSpan().SequenceEqual(actual))throw new Exception("Independent zlib fixture: "+entry.GetProperty("Name"));checks++;
        foreach(int truncated in new[]{1,Math.Max(1,encoded.Length/2)})
        {
            try{ManagedZlib.Decode(encoded.AsSpan(0,encoded.Length-truncated),length);throw new Exception("Accepted truncated zlib.");}
            catch(InvalidDataException){checks++;}
        }
        encoded[^1]^=1;
        try{ManagedZlib.Decode(encoded,length);throw new Exception("Accepted wrong Adler checksum.");}catch(InvalidDataException){checks++;}
    }
    foreach(var entry in fixture.RootElement.GetProperty("Png").EnumerateArray())
    {
        var encoded=Convert.FromBase64String(entry.GetProperty("Encoded").GetString()!);
        var expected=Convert.FromBase64String(entry.GetProperty("Expected").GetString()!);
        using var decoded=PngCodec.Decode(encoded);
        if(!decoded.GetPixelSpan().SequenceEqual(expected))throw new Exception("Independent PNG fixture: "+entry.GetProperty("Name"));checks++;
        encoded[encoded.Length/2]^=1;
        try{using var invalid=PngCodec.Decode(encoded);throw new Exception("Accepted invalid PNG CRC.");}catch(InvalidDataException){checks++;}
    }
    foreach(var entry in fixture.RootElement.GetProperty("InvalidPng").EnumerateArray())
    {
        var encoded=Convert.FromBase64String(entry.GetProperty("Encoded").GetString()!);
        try{using var invalid=PngCodec.Decode(encoded);throw new Exception("Accepted malformed PNG after output rental.");}catch(InvalidDataException){checks++;}
        var valid=fixture.RootElement.GetProperty("Png")[0];using var decoded=PngCodec.Decode(Convert.FromBase64String(valid.GetProperty("Encoded").GetString()!));
        if(!decoded.GetPixelSpan().SequenceEqual(Convert.FromBase64String(valid.GetProperty("Expected").GetString()!)))throw new Exception("Failed decode contaminated pooled output.");checks++;
    }
    // Exhaustive integer division identity used by the vector compositor.
    for(int n=0;n<=255*255+127;n++)if(n/255!=((n+1+(n>>8))>>8))throw new Exception("SIMD Divide255 identity.");checks++;
}
// A decoded buffer can have independently disposed aliases. Rental cannot be returned
// until the last alias/canvas is gone, and later decodes must initialize every byte.
using(var red=new SKBitmap(128,96))using(var blue=new SKBitmap(128,96))
{
    red.Erase(SKColors.Red);blue.Erase(SKColors.Blue);
    using var redData=red.Encode(SKEncodedImageFormat.Png,100);using var blueData=blue.Encode(SKEncodedImageFormat.Png,100);
    var redBytes=redData.ToArray();var blueBytes=blueData.ToArray();
    for(int i=0;i<12;i++)
    {
        var decoded=PngCodec.Decode(redBytes);var alias=decoded.Share();decoded.Dispose();decoded.Dispose();
        using(var another=PngCodec.Decode(blueBytes))
        {if(alias.ReadPixel(0,0)!=new Rgba32(255,0,0,255)||another.ReadPixel(0,0)!=new Rgba32(0,0,255,255))throw new Exception("Pixel rental returned while shared.");}
        alias.Dispose();alias.Dispose();checks++;
    }
    using(var decoded=SKBitmap.Decode(redBytes))using(var canvas=new SKCanvas(decoded))
    {
        var retained=decoded.GetPixelSpan();decoded.Dispose();
        using(var another=SKBitmap.Decode(blueBytes))
        {canvas.Clear(SKColors.Green);if(retained[1]!=128||retained[0]!=0||another.GetPixel(0,0)!=SKColors.Blue)throw new Exception("Decoded canvas alias storage lifetime.");}
        checks++;
    }
    Parallel.For(0,64,i=>
    {
        byte[] bytes=i%2==0?redBytes:blueBytes;
        using var image=PngCodec.Decode(bytes);using var alias=image.Share();image.Dispose();
        var expected=i%2==0?new Rgba32(255,0,0,255):new Rgba32(0,0,255,255);
        if(alias.ReadPixel(64,48)!=expected)throw new Exception("Concurrent independent rentals overlapped.");
    });checks++;
}
checks+=CoverageRunValidation.Run();
CpuOptions.EnableSimd=true;
Directory.CreateDirectory("artifacts");
File.WriteAllText($"artifacts/optimization-tests-{(System.Numerics.Vector.IsHardwareAccelerated?"hardware":"fallback")}.json",
    JsonSerializer.Serialize(new{Checks=checks,Passed=true,HardwareSimd=System.Numerics.Vector.IsHardwareAccelerated,Font=args.FirstOrDefault(),FixtureSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures/codecs.json"))))},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks} exact-byte optimization checks (frozen scalar renderer, stride, formats, alpha, clips, inverse fills, curves, cache eviction/mutation). Independent zlib/PNG fixtures and checksum rejection passed. SIMD hardware: {System.Numerics.Vector.IsHardwareAccelerated}");

