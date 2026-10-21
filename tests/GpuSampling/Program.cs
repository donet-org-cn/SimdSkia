using System.Text.Json;
using SimdSkia;
using SimdSkia.Cpu;
using SimdSkia.Gpu;

const int width=67,height=43,stride=width*4+11;int checks=0,maximumError=0;double maximumRmse=0;
using var expected=new PixelBuffer(width,height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied,stride);
using var actual=new PixelBuffer(width,height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied,stride);
using var cpu=new RasterCanvas(expected);using var gpu=new VulkanCanvas(actual);
var random=new Random(821);
var coverage=new byte[39*29];random.NextBytes(coverage);var clip=new CoverageClip(7,5,39,29,coverage);
void Compare(DrawingCommand draw,int tolerance=2)
{
    var clear=DrawingCommand.Clear(new(15,35,75,191));cpu.Execute(clear);gpu.Execute(clear);cpu.Execute(draw);gpu.Execute(draw);gpu.Flush();
    int max=0;long squares=0;
    for(int y=0;y<height;y++)for(int x=0;x<width*4;x++){int d=Math.Abs(expected.GetRowSpan(y)[x]-actual.GetRowSpan(y)[x]);max=Math.Max(max,d);squares+=d*d;}
    double rmse=Math.Sqrt(squares/(double)(width*height*4));maximumError=Math.Max(maximumError,max);maximumRmse=Math.Max(maximumRmse,rmse);
    if(max>tolerance||rmse>.5)throw new Exception($"GPU sample/clip: {draw.Sampling}/{draw.Paint.Composite}, max={max}, RMSE={rmse:F4}, rect={draw.ImageSource}, matrix={draw.Transform}");checks++;
}
foreach(var format in Enum.GetValues<PixelFormat>())foreach(var alpha in Enum.GetValues<AlphaFormat>())
{
    using var source=new PixelBuffer(13,9,format,alpha,13*4+7);
    for(int y=0;y<9;y++)for(int x=0;x<13;x++)source.WritePixel(x,y,new((byte)(x*17),(byte)(y*25),(byte)(x*9+y*7),(byte)(32+(x*11+y*17)%224)));
    foreach(var sampling in Enum.GetValues<ImageSampling>())foreach(var blend in Enum.GetValues<CompositeMode>())foreach(bool aa in new[]{false,true})foreach(bool masked in new[]{false,true})foreach(bool crop in new[]{false,true})
    {
        var transform=aa?new Matrix3(.91f,.13f,1.25f,-.07f,1.03f,2.75f,0,0,1):new(-1,0,65,0,1,0,0,0,1);
        Compare(new(CommandKind.DrawImage,new(3.25f,4.125f,57.5f,37.25f),transform,new(new(255,255,255,137),blend,aa))
        {Image=source,ImageSource=crop?new(1.25f,.5f,11.75f,8.5f):null,Sampling=sampling,CoverageClip=masked?clip:null,Clip=new(1.25f,3.5f,63.75f,39.5f)});
    }
}
var path=new PathGeometry([new(PathVerb.Move,new(2,20)),new(PathVerb.Cubic,new(15,-3),new(60,54),new(64,17)),new(PathVerb.Line,new(33,41)),new(PathVerb.Close,default)]);
foreach(float tolerance in new[]{.125f,.015625f})foreach(var blend in Enum.GetValues<CompositeMode>())
{
    Compare(new(CommandKind.FillPath,default,Matrix3.Identity,new(new(41,191,92,147),blend,true)){Path=path,CoverageClip=clip,CurveTolerance=tolerance});
    Compare(DrawingCommand.FillRectangle(new(5.25f,6.75f,62.25f,37.25f),Matrix3.Identity,new(new(41,191,92,147),blend,true)) with{CoverageClip=clip},0);
}
foreach(var target in new[]{expected,actual})for(int y=0;y<height;y++)if(target.GetRowSpan(y)[(width*4)..].IndexOfAnyExcept((byte)0)>=0)throw new Exception("GPU sampling wrote stride padding.");
bool rejected=false;using(var source=new PixelBuffer(2,2,PixelFormat.Rgba8888,AlphaFormat.Premultiplied))
{try{gpu.Execute(new(CommandKind.DrawImage,new(0,0,5,5),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver)){Image=source,ImageSource=new(-1,0,2,2)});}catch(ArgumentOutOfRangeException){rejected=true;}}
if(!rejected)throw new Exception("GPU accepted invalid source crop.");checks++;
checks+=TextureCacheValidation.Run(gpu,actual,()=>gpu.UploadedTextures,()=>gpu.TextureCacheHits);
var report=new{checks,maximumError,maximumRmse,device=gpu.DeviceName,passed=true,scope="Actual Vulkan device filtering/coverage + fence/readback; independent CPU scalar math; two formats, three alpha/composite modes, AA, rotation/mirror, fractional crops, stride and fine curves; content cache reuse/mutation/eviction/disposed sources"};
Directory.CreateDirectory("artifacts/completion");File.WriteAllText("artifacts/completion/gpu-sampling-tests.json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));
