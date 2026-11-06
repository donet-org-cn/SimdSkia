using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
string output=args[0],scene=args[1];Directory.CreateDirectory(output);AppContext.SetSwitch("SimdSkia.Cpu.Alpha8Reference",args.Contains("--alpha-reference"));
using var process=Process.GetCurrentProcess();using var bitmap=new SKBitmap(new SKImageInfo(scene=="alpha8"?1024:256,scene=="alpha8"?768:192,scene=="alpha8"?SKColorType.Alpha8:SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);using var paint=new SKPaint{Color=new(240,80,40,170)};using var blender=SKBlender.CreateArithmetic(.4f,.3f,.2f,.15f,true);using var recorder=new SKPictureRecorder();var rc=recorder.BeginRecording(new(0,0,256,192));for(int i=0;i<24;i++)rc.DrawRect(i*5,i*3,50,30,paint);using var picture=recorder.EndRecording();
if(scene=="float-color")paint.ColorF=new(.13333f,.57777f,.89999f,.371f);if(scene=="arithmetic")paint.Blender=blender;
void Draw()
{
 switch(scene)
 {
  case "alpha8":canvas.Clear(new(0,0,0,70));canvas.DrawRect(50,40,800,600,paint);canvas.DrawRect(100,80,400,250,paint);break;
  case "float-color":case "arithmetic":canvas.Clear(new(30,60,100,180));canvas.DrawRect(10,8,230,170,paint);break;
  case "skp-write":using(var data=picture.Serialize()){if(data.Size<32)throw new Exception("SKP encoder failed.");}break;
  case "skp-read":throw new InvalidOperationException("The read action is initialized outside the timed path.");
  case "webp-lossless":using(var pixmap=bitmap.PeekPixels())using(var data=pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless,75))){if(data is null||data.Size<16)throw new Exception("WebP encoder failed.");}break;
  default:throw new ArgumentException("Unknown benchmark scene.");
 }
}
using var serialized=picture.Serialize();Action draw=scene=="skp-read"?()=>{using var read=SKPicture.Deserialize(serialized);if(read is null)throw new Exception("SKP decoding failed.");}:Draw;
if(scene=="webp-lossless"){using var g=SKShader.CreateLinearGradient(new(0,0),new(256,192),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);paint.Shader=g;canvas.DrawPaint(paint);paint.Shader=null;}
var clock=Stopwatch.StartNew();while(clock.Elapsed.TotalSeconds<.6)draw();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();long allocation=GC.GetAllocatedBytesForCurrentThread();var cpu=process.TotalProcessorTime;int frames=0;clock.Restart();while(clock.Elapsed.TotalSeconds<.8){draw();frames++;}clock.Stop();var used=process.TotalProcessorTime-cpu;allocation=GC.GetAllocatedBytesForCurrentThread()-allocation;process.Refresh();
var result=new{scene,frames,msPerFrame=clock.Elapsed.TotalMilliseconds/frames,cpuMsPerFrame=used.TotalMilliseconds/frames,cpuPercentOfMachine=used.TotalMilliseconds/clock.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=(double)allocation/frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,runtime=Environment.Version.ToString(),processorCount=Environment.ProcessorCount};File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result));File.WriteAllBytes(Path.Combine(output,"pixels.bin"),bitmap.GetPixelSpan().ToArray());Console.WriteLine(JsonSerializer.Serialize(result));
