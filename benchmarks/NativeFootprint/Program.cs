using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using SimdSkia;
#if VULKAN
using SimdSkia.Gpu;
#elif DIRECT2D
using SimdSkia.Gpu.Direct2D;
#else
using SimdSkia.Cpu;
#endif

using var pixels=new PixelBuffer(640,360,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
var commands=new DrawingCommand[17];
commands[0]=DrawingCommand.Clear(new(20,26,31,255));
for(int i=1;i<commands.Length;i++)
{
    int x=i%4*136,y=i/4*60;
    commands[i]=DrawingCommand.FillRectangle(new(x,y,x+125,y+58),Matrix3.Identity,
        new(new((byte)(12*i),133,213,173),CompositeMode.SourceOver));
}
using var process=Process.GetCurrentProcess();
long heapBefore=GC.GetTotalMemory(true),allocatedBefore=GC.GetTotalAllocatedBytes(true);
var start=Stopwatch.GetTimestamp();
#if VULKAN
using var canvas=new VulkanCanvas(pixels);
if(canvas.IsSoftwareDevice)throw new InvalidOperationException("A physical Vulkan device is required.");
string backend=canvas.DeviceName;
#elif DIRECT2D
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Direct2D requires Windows.");
using var direct2d=new Direct2DCanvas(pixels);
ICanvasBackend canvas=direct2d;
string backend=direct2d.DeviceName;
#else
using var canvas=new RasterCanvas(pixels);
string backend="CPU";
#endif
void Frame(){foreach(var command in commands)canvas.Execute(command);canvas.Flush();}
Frame();
double firstMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
long firstBytes=GC.GetTotalAllocatedBytes(true)-allocatedBefore;
for(int i=0;i<16;i++)Frame();
long heap=GC.GetTotalMemory(true);
process.Refresh();
long working=process.WorkingSet64,privateBytes=process.PrivateMemorySize64;
const int frames=400;
long before=GC.GetTotalAllocatedBytes(true);
var cpu=process.TotalProcessorTime;
start=Stopwatch.GetTimestamp();
for(int i=0;i<frames;i++)Frame();
double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
double cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;
long allocated=GC.GetTotalAllocatedBytes(true)-before;
string hash=Convert.ToHexString(SHA256.HashData(pixels.GetPixelSpan()));
Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"native={!RuntimeFeature.IsDynamicCodeSupported} frames={frames} firstMs={firstMs:F4} firstBytes={firstBytes} liveDelta={heap-heapBefore} live={heap} working={working} private={privateBytes} elapsedMs={ms:F4} cpuMs={cpuMs:F4} bytes={allocated} pixels={hash}"));
Console.WriteLine(backend);
#if VULKAN
Console.WriteLine($"rectangleBatches={canvas.RectangleBatchDispatches}");
#endif
