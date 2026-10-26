using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SimdSkia;
using SimdSkia.Cpu;
using SkiaSharp;

string Value(string key,string fallback){int at=Array.IndexOf(args,key);return at<0?fallback:args[at+1];}
string engine=Value("--engine","managed"),output=Path.GetFullPath(Value("--output","artifacts/core-round8/probe"));
Directory.CreateDirectory(output);CpuOptions.EnableSimd=engine!="scalar";
if(args.Contains("--alpha-probe")){AlphaProbe.Run(output);return;}
if(args.Contains("--blend-variants")){BlendVariants.Run();return;}
if(engine is not ("managed" or "scalar" or "original"))throw new ArgumentException("Unknown engine");
var results=new List<object>();
foreach(string kind in new[]{"straight-opaque","straight-alpha","pma-alpha","opaque"})
foreach(var size in new[]{(Width:320,Height:240),(Width:1024,Height:768)})
{
    bool original=engine=="original",straight=kind.StartsWith("straight"),opaque=kind.EndsWith("opaque");
    var alpha=straight?AlphaFormat.Straight:opaque?AlphaFormat.Opaque:AlphaFormat.Premultiplied;
    using var source=new PixelBuffer(512,384,PixelFormat.Rgba8888,alpha);
    var bytes=source.GetPixelSpan();
    for(int y=0;y<384;y++)for(int x=0;x<512;x++){
        int i=(y*512+x)*4;byte a=opaque?(byte)255:(byte)(17+(x/8+y/8)%239);
        byte r=(byte)(x/2),g=(byte)(y*2/3),b=(byte)((x+y)/4);
        bytes[i]=straight?r:PixelBuffer.Multiply(r,a);bytes[i+1]=straight?g:PixelBuffer.Multiply(g,a);bytes[i+2]=straight?b:PixelBuffer.Multiply(b,a);bytes[i+3]=a;
    }
    using var target=new PixelBuffer(size.Width,size.Height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);
    using var renderer=new RasterCanvas(target);
    using var nativeSource=original?new SKBitmap(new SKImageInfo(512,384,SKColorType.Rgba8888,straight?SKAlphaType.Unpremul:opaque?SKAlphaType.Opaque:SKAlphaType.Premul)):null;
    if(nativeSource is not null)bytes.CopyTo(nativeSource.GetPixelSpan());
    using var nativeImage=nativeSource is null?null:SKImage.FromBitmap(nativeSource);
    using var nativeTarget=original?new SKBitmap(new SKImageInfo(size.Width,size.Height,SKColorType.Rgba8888,SKAlphaType.Premul)):null;
    using var nativeCanvas=nativeTarget is null?null:new SKCanvas(nativeTarget);
    var command=new DrawingCommand(CommandKind.DrawImage,new(0,0,size.Width,size.Height),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver))
        {Image=source,Sampling=ImageSampling.Bilinear};
    void Draw(){
        if(original){nativeCanvas!.Clear(new SKColor(10,20,30));nativeCanvas.DrawImage(nativeImage!,new SKRect(0,0,size.Width,size.Height),new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.None));}
        else{renderer.Execute(DrawingCommand.Clear(new(10,20,30,255)));renderer.Execute(command);}
    }
    var warm=Stopwatch.StartNew();while(warm.ElapsedMilliseconds<400)Draw();
    int iterations=1;double elapsed;
    do{var timer=Stopwatch.StartNew();for(int i=0;i<iterations;i++)Draw();elapsed=timer.Elapsed.TotalMilliseconds;if(elapsed<40)iterations*=2;}while(elapsed<40);
    iterations=Math.Clamp((int)(iterations*120/elapsed),1,20000);
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    using var process=Process.GetCurrentProcess();var samples=new List<object>();
    for(int sample=0;sample<7;sample++){
        long allocation=GC.GetAllocatedBytesForCurrentThread();double cpu=process.TotalProcessorTime.TotalMilliseconds;
        var timer=Stopwatch.StartNew();for(int i=0;i<iterations;i++)Draw();timer.Stop();
        samples.Add(new{millisecondsPerFrame=timer.Elapsed.TotalMilliseconds/iterations,cpuMillisecondsPerFrame=(process.TotalProcessorTime.TotalMilliseconds-cpu)/iterations,allocatedBytesPerFrame=(GC.GetAllocatedBytesForCurrentThread()-allocation)/(double)iterations});
    }
    process.Refresh();byte[] pixels=(original?nativeTarget!.GetPixelSpan():target.GetPixelSpan()).ToArray();
    string name=$"{kind}-{size.Width}x{size.Height}.rgba";File.WriteAllBytes(Path.Combine(output,name),pixels);
    results.Add(new{kind,width=size.Width,height=size.Height,samples,iterations,workingSetBytes=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,pixels=name,sha256=Convert.ToHexString(SHA256.HashData(pixels))});
    Console.WriteLine($"{engine} {kind} {size.Width}x{size.Height}: done");
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{engine,runtime=Environment.Version.ToString(),cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),processors=Environment.ProcessorCount,simd=CpuOptions.EnableSimd,results},new JsonSerializerOptions{WriteIndented=true}));
