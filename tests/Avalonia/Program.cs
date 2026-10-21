using SkiaSharp;
using System.Text.Json;
#if !ORIGINAL
using SimdSkia;
using SimdSkia.Cpu;
#endif

string fontPath=args.Length>0?args[0]:"baseline/fonts/ZenOldMincho-Regular.ttf";
string output=args.Length>1?args[1]:"artifacts/avalonia/tests-managed";Directory.CreateDirectory(output);
int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
void Throws<T>(Action action,string label)where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception(label);}
using var face=SKTypeface.FromFile(fontPath);using var font=new SKFont(face,16){Hinting=SKFontHinting.None,Subpixel=true,Edging=SKFontEdging.Antialias,BaselineSnap=false,LinearMetrics=true,Embolden=false,ForceAutoHinting=false};
using var defaultFont=new SKFont(face);
var defaults=new{Hinting=defaultFont.Hinting.ToString(),defaultFont.Subpixel,defaultFont.BaselineSnap,defaultFont.LinearMetrics,defaultFont.Embolden,defaultFont.ForceAutoHinting,Edging=defaultFont.Edging.ToString()};
var codepoints=new[]{0x4e2d,0x6587,0x41,0x67,0x20,0x0039};var ids=font.GetGlyphs(codepoints);
var widths=new float[ids.Length];var bounds=new SKRect[ids.Length];font.GetGlyphWidths(ids,widths,bounds);
Check(ids.Length==6&&ids.All(g=>g>0),"Actual CJK/Latin glyphs");
var positions=new SKPoint[ids.Length];for(int i=1;i<positions.Length;i++)positions[i]=new(positions[i-1].X+widths[i-1],0);
using var builder=new SKTextBlobBuilder();Check(builder.Build() is null,"Empty builder");
var run=builder.AllocatePositionedRun(font,ids.Length);Check(run.Size==ids.Length,"Run size");
run.SetGlyphs(ids);run.SetPositions(positions);
run.SetGlyphs(ids.AsSpan(0,1));run.SetPositions(positions.AsSpan(0,1));checks++;
Throws<ArgumentException>(()=>run.SetGlyphs(new ushort[ids.Length+1]),"Excess glyph input rejected");
Throws<ArgumentException>(()=>run.SetPositions(new SKPoint[ids.Length+1]),"Excess positions rejected");
var retainedGlyphs=run.Glyphs;var retainedPositions=run.Positions;
using var blob=builder.Build()!;Check(blob is not null&&blob.UniqueId!=0,"Immutable blob");
var blobBounds=blob!.Bounds;Check(builder.Build() is null,"Build resets builder");
using var pixels=new SKBitmap(new SKImageInfo(160,70,SKColorType.Rgba8888,SKAlphaType.Premul));
using var paint=new SKPaint{Color=new SKColor(230,210,180,181),IsAntialias=true};
void Draw(){using var canvas=new SKCanvas(pixels);canvas.Clear(new SKColor(20,40,60,170));canvas.ClipRect(new SKRect(9,5,150,65));canvas.DrawText(blob,8.25f,37.5f,paint);canvas.Flush();}
Draw();var snapshot=pixels.GetPixelSpan().ToArray();Check(snapshot.Any(b=>b!=0),"Positioned text drawn");
// A retained Span is invalid after Build in Skia. Only exercise our guaranteed
// managed snapshot safety; never write through a stale native pointer.
#if !ORIGINAL
retainedGlyphs.Fill(0);retainedPositions.Fill(new SKPoint(999,999));Draw();Check(snapshot.AsSpan().SequenceEqual(pixels.GetPixelSpan()),"Blob detached from caller buffers");
Throws<ObjectDisposedException>(()=>{var _=run.Size;},"Run buffer invalidated after Build");
Throws<NotSupportedException>(()=>font.Hinting=SKFontHinting.Full,"Hinting is explicit");
Throws<NotSupportedException>(()=>font.Edging=SKFontEdging.SubpixelAntialias,"LCD is explicit");
var temporaryFace=SKTypeface.FromFile(fontPath);var temporaryFont=new SKFont(temporaryFace,16);
using(var delayed=new SKTextBlobBuilder())
{
    var buffer=delayed.AllocatePositionedRun(temporaryFont,ids.Length);buffer.SetGlyphs(ids);buffer.SetPositions(positions);
    temporaryFont.Dispose();temporaryFace.Dispose();using var saved=delayed.Build()!;
    using var canvas=new SKCanvas(pixels);canvas.DrawText(saved,0,25,paint);checks++;
}
Throws<ArgumentException>(()=>{using var b=new SKTextBlobBuilder();var r=b.AllocatePositionedRun(font,1);r.SetGlyphs([ids[0]]);r.SetPositions([new(float.NaN,0)]);using var invalid=b.Build();},"Nonfinite positions");
// A producer-supplied bounds hint must not hide real glyphs during culling.
using(var b=new SKTextBlobBuilder())
{
    var r=b.AllocatePositionedRun(font,ids.Length,new SKRect(-200,-200,-180,-180));r.SetGlyphs(ids);r.SetPositions(positions);using var hinted=b.Build()!;
    using var canvas=new SKCanvas(pixels);canvas.Clear();canvas.DrawText(hinted,10,35,paint);Check(pixels.GetPixelSpan().ContainsAnyExcept((byte)0),"Bounds hint cannot cull real coverage");
}
var shape=new PathGeometry([
    new(PathVerb.Move,new(-3,-8)),new(PathVerb.Quadratic,new(11,-17),new(23,-4)),
    new(PathVerb.Cubic,new(29,11),new(2,14),new(-3,-8)),new(PathVerb.Close,default)]);
var random=new Random(20261021);
foreach(bool simd in new[]{false,true})
foreach(var format in Enum.GetValues<PixelFormat>())
foreach(var alpha in Enum.GetValues<AlphaFormat>())
{
    CpuOptions.EnableSimd=simd;TextCoverageCache.Clear();
    for(int iteration=0;iteration<48;iteration++)
    {
        var transform=(iteration%6) switch{
            0=>new Matrix3(1,0,30+iteration*.25f,0,1,25,0,0,1),
            1=>new Matrix3(1.5f,0,iteration*.375f,0,1.5f,12.5f,0,0,1),
            2=>new Matrix3(-1,0,70,0,1,42.25f,0,0,1),
            3=>new Matrix3(.9f,.27f,-5.25f,-.17f,1.2f,30.5f,0,0,1),
            4=>new Matrix3(1,0,65538,0,1,35,0,0,1),
            _=>new Matrix3(1,0,-14.75f,0,1,2.5f,0,0,1)};
        using var expected=new PixelBuffer(97,61,format,alpha,97*4+7);using var actual=new PixelBuffer(97,61,format,alpha,97*4+7);
        random.NextBytes(expected.GetPixelSpan());expected.GetPixelSpan().CopyTo(actual.GetPixelSpan());
        var color=new Rgba32((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256));
        var p=new Paint(color,(CompositeMode)(iteration%3),iteration%5!=0,iteration%4==0?new LinearGradient(new(0,0),new(40,40),new(0,40,80,190),new(90,80,10,240)):null);
        var command=new DrawingCommand(CommandKind.FillPath,default,transform,p){Path=shape,CacheTextCoverage=true,Clip=iteration%3==0?new RectF(9,8,67,54):null};
        TextCoverageCache.Enabled=false;using(var canvas=new RasterCanvas(expected))canvas.Execute(command);
        TextCoverageCache.Enabled=true;
        // Warm it, then replay against fresh target pixels using a fresh canvas.
        using(var warm=new PixelBuffer(97,61,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))using(var canvas=new RasterCanvas(warm)){canvas.Execute(command);canvas.Execute(command);}
        using(var canvas=new RasterCanvas(actual))canvas.Execute(command);
        Check(expected.GetPixelSpan().SequenceEqual(actual.GetPixelSpan()),$"Cached text {simd}/{format}/{alpha}/{iteration}");
    }
}
CpuOptions.EnableSimd=true;TextCoverageCache.Clear();
for(int i=0;i<600;i++)
{
    var geometry=new PathGeometry([new(PathVerb.Move,new(0,0)),new(PathVerb.Line,new(7,0)),new(PathVerb.Line,new(3,9)),new(PathVerb.Close,default)]);
    var command=new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver,true)){Path=geometry,CacheTextCoverage=true};
    using var buffer=new PixelBuffer(13,13,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);using var canvas=new RasterCanvas(buffer);canvas.Execute(command);canvas.Execute(command);
}
var stats=TextCoverageCache.Statistics;Check(stats.Count<=TextCoverageCache.MaximumEntries&&stats.Bytes<=TextCoverageCache.MaximumBytes,"Global text cache bounded");
var results=new byte[8][];
Parallel.For(0,8,i=>{
    using var buffer=new PixelBuffer(75,49,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);using var canvas=new RasterCanvas(buffer);
    canvas.Execute(new(CommandKind.FillPath,default,new Matrix3(1,0,15.25f,0,1,23.5f,0,0,1),new(new(100,200,50,190),CompositeMode.SourceOver,true)){Path=shape,CacheTextCoverage=true});
    results[i]=buffer.GetPixelSpan().ToArray();
});Check(results.All(r=>r.AsSpan().SequenceEqual(results[0])),"Concurrent independent canvases");
var fontResults=new byte[8][];
Parallel.For(0,8,i=>{
    using var sharedFaceFont=new SKFont(face,18);var w=new float[ids.Length];var b=new SKRect[ids.Length];sharedFaceFont.GetGlyphWidths(ids,w,b);
    using var maker=new SKTextBlobBuilder();var r=maker.AllocatePositionedRun(sharedFaceFont,ids.Length);r.SetGlyphs(ids);r.SetPositions(positions);using var text=maker.Build()!;
    using var bitmap=new SKBitmap(new SKImageInfo(160,70,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);using var ink=new SKPaint{Color=new SKColor(200,210,220,170)};
    canvas.DrawText(text,9.25f,38.5f,ink);fontResults[i]=bitmap.GetPixelSpan().ToArray();
});Check(fontResults.All(r=>r.AsSpan().SequenceEqual(fontResults[0])),"Concurrent shared-face metrics, blob building and rendering");
TextCoverageCache.Clear();TextCoverageCache.Enabled=true;
if(args.Contains("--gpu"))
{
    using var surface=VulkanSurface.Create(160,70);
    foreach(float phase in new[]{0f,.25f,.5f,.75f})
    {
        foreach(var canvas in new[]{new SKCanvas(pixels),surface.Canvas})
        {
            canvas.Clear(new SKColor(24,28,38));int saved=canvas.Save();canvas.ClipRect(new SKRect(9,5,150,65));canvas.Translate(phase,0);canvas.DrawText(blob,8.25f,37.5f,paint);canvas.RestoreToCount(saved);
            if(!ReferenceEquals(canvas,surface.Canvas))canvas.Dispose();
        }
        using var actual=surface.CopyBitmap();var a=pixels.GetPixelSpan();var b=actual.GetPixelSpan();int max=0;for(int i=0;i<a.Length;i++)max=Math.Max(max,Math.Abs(a[i]-b[i]));
        Check(max<=2,$"CPU/Vulkan positioned text phase {phase}, maximum error {max}");
    }
}
#endif
var report=new{Checks=checks,FontDefaults=defaults,Glyphs=ids,Widths=widths,Bounds=bounds.Select(b=>new[]{b.Left,b.Top,b.Right,b.Bottom}),BlobBounds=new[]{blobBounds.Left,blobBounds.Top,blobBounds.Right,blobBounds.Bottom}};
File.WriteAllText(Path.Combine(output,"behavior.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllBytes(Path.Combine(output,"positioned-text.rgba"),snapshot);
Console.WriteLine($"PASS {checks} Avalonia text-chain / cache / lifecycle checks. Behavior snapshot: {output}");
