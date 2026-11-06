using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
string output=args[0],scene=args[1];Directory.CreateDirectory(output);
bool paced=args.Contains("--paced");AppContext.SetSwitch("SimdSkia.Vulkan.ExternalCpuBridge",args.Contains("--cpu-bridge"));
AppContext.SetSwitch("SimdSkia.Vulkan.DisableRectangleBatches",args.Contains("--no-batch"));
GpuHost.Run((context,surface,device)=>
{
 var canvas=surface.Canvas;
 using var readback=new SKBitmap(new SKImageInfo(640,480,SKColorType.Rgba8888,SKAlphaType.Premul));using var paint=new SKPaint{Color=new(180,80,40,190),IsAntialias=true};using var path=new SKPath();path.MoveTo(4,4);path.CubicTo(80,0,4,74,76,70);path.LineTo(4,70);path.Close();
 using var gradient=SKShader.CreateLinearGradient(new(0,0),new(640,480),[new SKColor(240,80,40,210),new SKColor(40,100,220,170)],SKShaderTileMode.Clamp);
 using var bitmap=new SKBitmap(new SKImageInfo(64,64,SKColorType.Rgba8888,SKAlphaType.Premul));for(int y=0;y<64;y++)for(int x=0;x<64;x++)bitmap.SetPixel(x,y,new((byte)(x*3),(byte)(y*3),120,(byte)(90+(x+y)%165)));using var raster=SKImage.FromBitmap(bitmap);using var texture=raster.ToTextureImage(context);
 var rectangles=Enumerable.Range(0,24).Select(i=>new SKRect((i%6)*100+4,(i/6)*110+4,(i%6)*100+92,(i/6)*110+96)).ToArray();
 void Draw()
 {
  canvas.Clear(new SKColor(20,30,50));
  if(scene=="gradient")paint.Shader=gradient;
  else paint.Shader=null;
  foreach(var rectangle in rectangles)
  {
   if(scene=="paths"){canvas.Save();canvas.Translate(rectangle.Left,rectangle.Top);canvas.DrawPath(path,paint);canvas.Restore();}
   else if(scene=="image")canvas.DrawImage(texture,rectangle,new SKSamplingOptions(SKFilterMode.Linear),paint);
   else if(scene is "rectangles" or "gradient")canvas.DrawRect(rectangle,paint);
   else throw new ArgumentException("Unknown GPU benchmark scene.");
  }
 }
 void Read(){if(!surface.ReadPixels(readback.Info,readback.GetPixels(),readback.RowBytes,0,0))throw new Exception("GPU readback failed.");}
 var clock=Stopwatch.StartNew();while(clock.Elapsed.TotalSeconds<1){Draw();Read();}GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();long allocated=GC.GetAllocatedBytesForCurrentThread();TimeSpan cpu=process.TotalProcessorTime;int frames=0;long drawTicks=0,completeTicks=0;File.WriteAllText(Path.Combine(output,"measuring.marker"),"ready");clock.Restart();
 while(clock.Elapsed.TotalSeconds<2){long start=Stopwatch.GetTimestamp();Draw();long submitted=Stopwatch.GetTimestamp();Read();long completed=Stopwatch.GetTimestamp();drawTicks+=submitted-start;completeTicks+=completed-submitted;frames++;if(paced){double due=frames/60d;while(clock.Elapsed.TotalSeconds<due)Thread.Sleep(1);}}
 clock.Stop();var cpuUsed=process.TotalProcessorTime-cpu;allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;File.Delete(Path.Combine(output,"measuring.marker"));process.Refresh();
 var result=new{scene,device,frames,width=640,height=480,mode=paced?"paced60":"throughput",runtime=Environment.Version.ToString(),processorCount=Environment.ProcessorCount,msPerFrame=clock.Elapsed.TotalMilliseconds/frames,activeMsPerFrame=(drawTicks+completeTicks)*1000d/Stopwatch.Frequency/frames,framesPerSecond=frames/clock.Elapsed.TotalSeconds,drawCallsMsPerFrame=drawTicks*1000d/Stopwatch.Frequency/frames,completeReadbackMsPerFrame=completeTicks*1000d/Stopwatch.Frequency/frames,cpuMsPerFrame=cpuUsed.TotalMilliseconds/frames,cpuPercentOfMachine=cpuUsed.TotalMilliseconds/clock.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=(double)allocated/frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,completedGpuReadback=true};
 File.WriteAllBytes(Path.Combine(output,"pixels.bin"),readback.GetPixelSpan().ToArray());File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result));Console.WriteLine(JsonSerializer.Serialize(result));
});
