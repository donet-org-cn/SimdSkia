using SkiaSharp;
using SimdSkia.Gpu;
using System.Text.Json;
using System.Runtime.InteropServices;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("GPU context assertion "+checks);}
using var hostPixels=new SimdSkia.PixelBuffer(1,1,SimdSkia.PixelFormat.Rgba8888,SimdSkia.AlphaFormat.Premultiplied);using var host=new VulkanCanvas(hostPixels);var handles=host.ContextHandles;
IntPtr loader=NativeLibrary.Load(OperatingSystem.IsWindows()?"vulkan-1.dll":"libvulkan.so.1");
unsafe IntPtr Proc(string name,IntPtr instance,IntPtr device)
{
 var utf8=System.Text.Encoding.UTF8.GetBytes(name+"\0");fixed(byte* text=utf8)
 {if(device!=IntPtr.Zero){var p=(delegate* unmanaged[Stdcall]<IntPtr,byte*,IntPtr>)NativeLibrary.GetExport(loader,"vkGetDeviceProcAddr");var result=p(device,text);if(result!=IntPtr.Zero)return result;}var get=(delegate* unmanaged[Stdcall]<IntPtr,byte*,IntPtr>)NativeLibrary.GetExport(loader,"vkGetInstanceProcAddr");return get(instance,text);}
}
using var descriptor=new GRVkBackendContext{VkInstance=handles.Instance,VkPhysicalDevice=handles.PhysicalDevice,VkDevice=handles.Device,VkQueue=handles.Queue,GraphicsQueueIndex=handles.QueueFamily,GetProcedureAddress=Proc};
using var context=GRContext.CreateVulkan(descriptor)??throw new Exception("Unable to create native Vulkan context.");values["context-backend"]=context.Backend.ToString();values["initial-abandoned"]=context.IsAbandoned;
context.SetResourceCacheLimit(2*1024*1024);Check(context.GetResourceCacheLimit()==2*1024*1024);GRRecordingContext recording=context;Check(recording.GetMaxSurfaceSampleCount(SKColorType.Rgba8888)>=1);Check(context.MaxTextureSize>=1024);Check(context.MaxRenderTargetSize>=1024);
descriptor.Dispose();
for(int scene=0;scene<5;scene++)for(int transform=0;transform<2;transform++)
{
 using var surface=SKSurface.Create(context,true,new SKImageInfo(96,72,SKColorType.Rgba8888,SKAlphaType.Premul),0,(GRSurfaceOrigin)transform)!;Check(surface.Context is not null);Check(surface.Canvas.Context is not null);using(var peek=surface.PeekPixels())Check(peek is null);
 var canvas=surface.Canvas;canvas.Clear(new SKColor(20,30,50));using var paint=new SKPaint{Color=new SKColor(200,70,90,180)};
 if(scene==0)canvas.DrawRect(new SKRect(8,7,64,49),paint);
 if(scene==1){canvas.ClipRect(new SKRect(12,13,70,54));canvas.DrawRect(new SKRect(4,5,80,66),paint);}
 if(scene==2){using var shader=SKShader.CreateLinearGradient(new(0,0),new(96,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);paint.Shader=shader;paint.Color=SKColors.White;canvas.DrawRect(new SKRect(6,7,84,60),paint);}
 if(scene==3){using var path=new SKPath();path.MoveTo(5,9);path.LineTo(64,9);path.LineTo(29,59);path.Close();canvas.DrawPath(path,paint);}
 if(scene==4){using var bitmap=new SKBitmap(16,12);bitmap.Erase(new SKColor(70,150,220));using var image=SKImage.FromBitmap(bitmap);canvas.DrawImage(image,new SKRect(8,9,72,57));}
 surface.Flush(false);context.Submit(true);using var read=new SKBitmap(new SKImageInfo(96,72,SKColorType.Rgba8888,SKAlphaType.Premul));Check(surface.ReadPixels(read.Info,read.GetPixels(),read.RowBytes,0,0));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind="gpu-context",scene,transform,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);
 using var snapshot=surface.Snapshot();Check(snapshot.Width==96);using var props=surface.SurfaceProperties;Check(props is not null);
}
context.Flush(true,true);context.GetResourceCacheUsage(out var resources,out var before);Check(resources>=0&&before>=0);context.PurgeUnlockedResources(1,true);context.PurgeUnusedResources(100000);context.PurgeResources();context.GetResourceCacheUsage(out _,out var after);Check(after<=before);context.ResetContext();
using(var dump=new Dump()){context.DumpMemoryStatistics(dump);Check(dump.Values>0);}
context.AbandonContext(true);values["final-abandoned"]=context.IsAbandoned;
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks,host.DeviceName,resources,before,after}));Console.WriteLine($"Captured {cases.Count} Vulkan context images, {values.Count} metadata entries and {checks} real-device checks");
sealed class Dump : SKTraceMemoryDump
{
 public int Values;
 public Dump():base(true,true) { }
 protected override void OnDumpNumericValue(string dumpName,string valueName,string units,ulong value)=>Values++;
}
