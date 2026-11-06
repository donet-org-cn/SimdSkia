using SkiaSharp;
using SimdSkia;
using SimdSkia.Cpu;
using SimdSkia.Gpu.Direct2D;
using System.Text.Json;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This Vulkan/Direct2D device suite requires Windows.");
var rows=new List<object>();int checks=0;bool passed=true;Directory.CreateDirectory(args[0]);
void Compare(PixelBuffer a,PixelBuffer b,string key,int maximumAllowed)
{
    int maximum=0;long squares=0;for(int y=0;y<128;y++)for(int x=0;x<512;x++){int d=Math.Abs(a.GetRowSpan(y)[x]-b.GetRowSpan(y)[x]);maximum=Math.Max(maximum,d);squares+=d*d;}
    double rmse=Math.Sqrt(squares/(128d*128*4));bool ok=maximum<=maximumAllowed&&rmse<=2;passed&=ok;checks++;rows.Add(new{key,maximum,rmse,maximumAllowed,passed=ok});
}
using(var surface=VulkanSurface.Create(128,128))using(var bitmap=new SKBitmap(128,128,SKColorType.Rgba8888,SKAlphaType.Premul))using(var cpu=new SKCanvas(bitmap))
{
    using var p=new SKPath();p.MoveTo(13.25f,94.75f);p.LineTo(50.5f,18.25f);p.LineTo(100.75f,90.5f);
    foreach(var cap in Enum.GetValues<SKStrokeCap>())foreach(var join in Enum.GetValues<SKStrokeJoin>())
    {
        using var paint=new SKPaint{Color=new(20,150,220,160),Style=SKPaintStyle.Stroke,StrokeWidth=9,IsAntialias=true,StrokeCap=cap,StrokeJoin=join};
        foreach(var c in new[]{cpu,surface.Canvas}){c.Clear(new SKColor(30,40,50));c.DrawPath(p,paint);}
        using var image=surface.Snapshot();using var actual=SKBitmap.FromImage(image);
        // Both are tight RGBA buffers in this API-level device check.
        var x=bitmap.GetPixelSpan();var y=actual.GetPixelSpan();int maximum=0;long squares=0;for(int i=0;i<x.Length;i++){int d=Math.Abs(x[i]-y[i]);maximum=Math.Max(maximum,d);squares+=d*d;}
        double rmse=Math.Sqrt(squares/(double)x.Length);bool ok=maximum<=1&&rmse<=.1;passed&=ok;checks++;rows.Add(new{key=$"vulkan-{cap}-{join}",maximum,rmse,passed=ok});
    }
}
using(var expected=new PixelBuffer(128,128,PixelFormat.Bgra8888,AlphaFormat.Premultiplied))using(var actual=new PixelBuffer(128,128,PixelFormat.Bgra8888,AlphaFormat.Premultiplied))
using(var cpu=new RasterCanvas(expected))using(var gpu=new Direct2DCanvas(actual))
{
    foreach(var cap in Enum.GetValues<StrokeCap>())foreach(var join in Enum.GetValues<StrokeJoin>())
    {
        var outline=StrokeGeometry.Create([ [new(13.25f,94.75f),new(50.5f,18.25f),new(100.75f,90.5f)] ],9,4,cap,join);
        var clear=DrawingCommand.Clear(new(30,40,50,255));cpu.Execute(clear);gpu.Execute(clear);
        var draw=new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,new(new(20,150,220,160),CompositeMode.SourceOver,true)){Path=outline};cpu.Execute(draw);gpu.Execute(draw);gpu.Flush();Compare(expected,actual,$"direct2d-{cap}-{join}",32);
    }
}
File.WriteAllText(Path.Combine(args[0],"results.json"),JsonSerializer.Serialize(new{checks,passed,rows},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((passed?"PASS ":"FAIL ")+checks+" actual GPU stroke completion/readback checks");return passed?0:2;
