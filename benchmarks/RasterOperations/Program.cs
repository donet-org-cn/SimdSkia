using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var rows=new List<object>();
void Bench(string scene,Action work,SKBitmap pixels)
{
 var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<.4)work();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long allocation=GC.GetTotalAllocatedBytes(true);int frames=0;watch.Restart();while(watch.Elapsed.TotalSeconds<.8){work();frames++;}watch.Stop();var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;long bytes=GC.GetTotalAllocatedBytes(true)-allocation;process.Refresh();double n=frames;rows.Add(new{scene,msPerFrame=watch.Elapsed.TotalMilliseconds/n,cpuMsPerFrame=cpuMs/n,cpuPercentOfMachine=cpuMs/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=bytes/n,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,frames});File.WriteAllBytes(Path.Combine(output,scene+".rgba"),pixels.GetPixelSpan().ToArray());
}
using var bitmap=new SKBitmap(new SKImageInfo(1024,512,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);using var paint=new SKPaint{Color=new SKColor(39,113,201,255),IsAntialias=true};
Bench("clear-1024x512",()=>{canvas.Clear(new SKColor(9,30,70,255));},bitmap);
Bench("opaque-rectangle",()=>{canvas.DrawRect(0,0,1024,512,paint);},bitmap);
paint.Color=new SKColor(39,113,201,123);Bench("alpha-rectangle",()=>{canvas.DrawRect(0,0,1024,512,paint);},bitmap);
paint.Color=new SKColor(39,113,201,255);using var gradient=SKShader.CreateLinearGradient(new(0,0),new(1024,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);paint.Shader=gradient;Bench("linear-gradient",()=>{canvas.DrawRect(0,0,1024,512,paint);},bitmap);paint.Shader=null;
using var input=new SKBitmap(new SKImageInfo(13,9,SKColorType.Rgba8888,SKAlphaType.Premul));for(int y=0;y<9;y++)for(int x=0;x<13;x++)input.SetPixel(x,y,new SKColor((byte)(x*19),(byte)(y*29),(byte)(250-x*7),(byte)(80+(x*7+y*13)%176)));using var image=SKImage.FromBitmap(input);paint.IsAntialias=false;paint.BlendMode=SKBlendMode.Src;
Bench("nearest-image",()=>{canvas.DrawImage(image,new SKRect(0,0,512,256),new SKSamplingOptions(SKFilterMode.Nearest),paint);},bitmap);
Bench("linear-image",()=>{canvas.DrawImage(image,new SKRect(0,0,512,256),new SKSamplingOptions(SKFilterMode.Linear),paint);},bitmap);
paint.Color=new SKColor(153,211,77,191);paint.BlendMode=SKBlendMode.Multiply;Bench("multiply-rectangle",()=>{canvas.DrawRect(0,0,1024,512,paint);},bitmap);
paint.BlendMode=SKBlendMode.Src;paint.Color=SKColors.White;using var shader=input.ToShader(SKShaderTileMode.Repeat,SKShaderTileMode.Mirror,new SKSamplingOptions(SKFilterMode.Nearest),SKMatrix.CreateScale(3.7f,2.3f));paint.Shader=shader;Bench("repeat-image-shader",()=>{canvas.DrawRect(0,0,512,256,paint);},bitmap);paint.Shader=null;
paint.Color=new SKColor(211,131,73,191);using var filter=SKColorFilter.CreateLighting(new SKColor(128,211,77),new SKColor(12,9,3));paint.ColorFilter=filter;Bench("lighting-color-filter",()=>{canvas.DrawRect(0,0,1024,512,paint);},bitmap);
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{rows.Count} raster CPU benchmarks");
