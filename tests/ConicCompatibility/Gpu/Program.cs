using SimdSkia;
using SimdSkia.Cpu;
using SimdSkia.Gpu;
using SimdSkia.Gpu.Direct2D;
using System.Text.Json;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This combined Vulkan/Direct2D device test requires Windows.");
bool allPassed=true;int checks=0;var rows=new List<object>();
var path=new PathGeometry([new(PathVerb.Move,new(8.25f,60.75f)),PathSegment.Conic(new(38.25f,-7.5f),new(87.75f,50.25f),.70710677f),new(PathVerb.Line,new(75.5f,83.25f)),new(PathVerb.Close,default)]);
foreach(string name in new[]{"vulkan","direct2d"})
{
    using var expected=new PixelBuffer(128,128,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);using var actual=new PixelBuffer(128,128,PixelFormat.Bgra8888,AlphaFormat.Premultiplied,528);
    using var cpu=new RasterCanvas(expected);using ICanvasBackend gpu=name=="vulkan"?new VulkanCanvas(actual):new Direct2DCanvas(actual);
    foreach(float scale in new[]{.7f,1,1.3f})foreach(byte alpha in new byte[]{128,255})
    {
        var clear=DrawingCommand.Clear(new(30,40,50,255));cpu.Execute(clear);gpu.Execute(clear);
        var draw=new DrawingCommand(CommandKind.FillPath,default,new(scale,.03f,1.2f,-.02f,scale,2.3f,0,0,1),new(new(20,130,240,alpha),CompositeMode.SourceOver,true)){Path=path};
        cpu.Execute(draw);gpu.Execute(draw);gpu.Flush();int maximum=0;long squares=0;
        for(int y=0;y<128;y++)for(int x=0;x<512;x++){int d=Math.Abs(expected.GetRowSpan(y)[x]-actual.GetRowSpan(y)[x]);maximum=Math.Max(maximum,d);squares+=d*d;}
        double rmse=Math.Sqrt(squares/(128d*128*4));bool passed=maximum<=8&&rmse<=.75;rows.Add(new{name,scale,alpha,maximum,rmse,passed});
        allPassed&=passed;checks++;
        for(int y=0;y<128;y++)if(actual.GetRowSpan(y)[512..].IndexOfAnyExcept((byte)0)>=0)throw new Exception("GPU wrote padding");
    }
}
Directory.CreateDirectory(args[0]);
File.WriteAllText(Path.Combine(args[0],"results.json"),JsonSerializer.Serialize(new{checks,threshold=new{maximum=8,rmse=.75},passed=allPassed,rows},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine((allPassed?"PASS ":"FAIL ")+checks+" actual Vulkan/Direct2D conic completion/readback checks");
return allPassed?0:2;
