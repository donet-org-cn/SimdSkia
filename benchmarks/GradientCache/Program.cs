using SimdSkia;
using SimdSkia.Gpu;
using SimdSkia.Gpu.Direct2D;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This combined GPU benchmark uses Windows Vulkan/Direct2D.");
string backend=args[0],output=args[1];Directory.CreateDirectory(output);
using var pixels=new PixelBuffer(384,256,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
using ICanvasBackend canvas=backend=="vulkan"?new VulkanCanvas(pixels):backend=="direct2d"?new Direct2DCanvas(pixels):throw new ArgumentException("Backend");
if(canvas is VulkanCanvas {IsSoftwareDevice:true})throw new Exception("A physical GPU is required.");
string device=canvas is VulkanCanvas vk?vk.DeviceName:((Direct2DCanvas)canvas).DeviceName;
var shader=new GradientFixture();var commands=new DrawingCommand[64];
for(int i=0;i<64;i++)commands[i]=DrawingCommand.FillRectangle(new(i%8*48,i/8*32,i%8*48+48,i/8*32+32),Matrix3.Identity,new(new(255,255,255,219),CompositeMode.SourceOver,Shader:shader));
void Frame(){canvas.Execute(DrawingCommand.Clear(new(23,45,67,255)));foreach(var command in commands)canvas.Execute(command);canvas.Flush();}
var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<1)Frame();
GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();using var process=Process.GetCurrentProcess();long allocated=GC.GetTotalAllocatedBytes(true);double cpu=process.TotalProcessorTime.TotalMilliseconds;int frames=0;
watch.Restart();do{Frame();frames++;}while(watch.Elapsed.TotalSeconds<2);watch.Stop();process.Refresh();
long bytes=GC.GetTotalAllocatedBytes(true)-allocated;cpu=process.TotalProcessorTime.TotalMilliseconds-cpu;
byte[] raw=pixels.CopyBytes();File.WriteAllBytes(Path.Combine(output,"frame.bgra"),raw);
var result=new{backend,device,runtime=Environment.Version.ToString(),frames,msPerFrame=watch.Elapsed.TotalMilliseconds/frames,cpuMsPerFrame=cpu/frames,allocatedBytesPerFrame=(double)bytes/frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,pixelSha256=Convert.ToHexString(SHA256.HashData(raw)),scope="64 shared three-stop gradient tiles, completed fence/readback each frame; 384x256 BGRA premul; not a full Avalonia app"};
File.WriteAllText(Path.Combine(output,"metrics.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(result));

sealed class GradientFixture:IColorShader
{
    private readonly LinearGradientDescription description=new(new(0,0),new(384,256),[new(0,new(241,30,100,255)),new(.35f,new(20,210,120,255)),new(1,new(40,80,230,255))]);
    public bool IsOpaque=>true;
    public bool TryGetLinearGradient(out LinearGradientDescription? gradient){gradient=description;return true;}
    public Rgba32 Sample(float x,float y)
    {
        float t=Math.Clamp((x*384+y*256)/(384f*384+256f*256),0,1);var stops=description.Stops.Span;int index=t<.35f?0:1;
        var a=stops[index];var b=stops[index+1];float f=(t-a.Offset)/(b.Offset-a.Offset);
        byte Mix(byte u,byte v)=>(byte)Math.Clamp((int)MathF.Round(u+(v-u)*f),0,255);return new(Mix(a.Color.R,b.Color.R),Mix(a.Color.G,b.Color.G),Mix(a.Color.B,b.Color.B),255);
    }
}
