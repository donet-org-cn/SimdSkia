using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var results=new List<object>();using var process=Process.GetCurrentProcess();using var bitmap=new SKBitmap(new SKImageInfo(256,192,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);using var paint=new SKPaint{Color=SKColors.White};
void Run(string scene,Action draw)
{
 var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<.5)draw();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();long allocated=GC.GetAllocatedBytesForCurrentThread();var cpu=process.TotalProcessorTime;int frames=0;timer.Restart();while(timer.Elapsed.TotalSeconds<.8){draw();frames++;}timer.Stop();var used=process.TotalProcessorTime-cpu;allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;process.Refresh();
 results.Add(new{scene,frames,msPerFrame=timer.Elapsed.TotalMilliseconds/frames,cpuMsPerFrame=used.TotalMilliseconds/frames,cpuPercentOfMachine=used.TotalMilliseconds/timer.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=(double)allocated/frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d});File.WriteAllBytes(Path.Combine(output,scene+".rgba"),bitmap.GetPixelSpan().ToArray());
}
using var gradient=SKShader.CreateLinearGradient(new(0,0),new(256,192),[new SKColor(230,30,60,180),new SKColor(30,90,220,240)],SKShaderTileMode.Clamp);paint.Shader=gradient;
SKPoint[] positions=[new(4,4),new(240,12),new(14,174),new(245,181)];SKColor[] colors=[SKColors.Red,SKColors.Green,SKColors.Blue,SKColors.White];using var vertices=SKVertices.CreateCopy(SKVertexMode.TriangleStrip,positions,positions,colors);
Run("mesh",()=>{canvas.Clear(SKColors.Black);canvas.DrawVertices(vertices,SKBlendMode.Modulate,paint);});
SKPoint[] patch=[new(8,8),new(70,-12),new(160,30),new(240,8),new(260,70),new(210,130),new(240,176),new(160,190),new(70,155),new(8,176),new(-12,120),new(35,70)];
Run("patch",()=>{canvas.Clear(SKColors.Black);canvas.DrawPatch(patch,colors,positions,paint);});
using var atlasBitmap=new SKBitmap(16,16);atlasBitmap.Erase(new SKColor(40,150,230,190));using var atlas=SKImage.FromBitmap(atlasBitmap);SKRect[] rects=Enumerable.Repeat(new SKRect(0,0,16,16),32).ToArray();var transforms=Enumerable.Range(0,32).Select(i=>SKRotationScaleMatrix.CreateTranslation(i%8*30+2,i/8*45+2)).ToArray();paint.Shader=null;
Run("atlas32",()=>{canvas.Clear(SKColors.Black);canvas.DrawAtlas(atlas,rects,transforms,paint);});
using var builder=SKRuntimeEffect.BuildShader("uniform float2 size;half4 main(float2 p){return half4(p/size,.5,1);}");builder.Uniforms["size"]=new SKPoint(256,192);using var runtime=builder.Build();paint.Shader=runtime;
Run("runtime",()=>{canvas.Clear(SKColors.Black);canvas.DrawPaint(paint);});
using var noise=SKShader.CreatePerlinNoiseFractalNoise(.035f,.04f,3,7);paint.Shader=noise;Run("perlin",()=>{canvas.Clear(SKColors.Black);canvas.DrawPaint(paint);});paint.Shader=null;
using var face=SKTypeface.FromFile("baseline/fonts/ZenOldMincho-Regular.ttf");using var font=new SKFont(face,18);Run("bilingual",()=>{canvas.Clear(SKColors.Black);for(int i=0;i<6;i++)canvas.DrawText("SimdSkia 中文控件 123",4,24+i*28,font,paint);});
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results));Console.WriteLine($"Measured {results.Count} CPU API scenes");
