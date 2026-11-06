using SkiaSharp;
using SimdSkia;
using SimdSkia.Gpu;
using System.Text.Json;
using System.Runtime.InteropServices;
string output=args[0];Directory.CreateDirectory(output);using var binary=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new Dictionary<string,object?>();int checks=0,releases=0;
AppContext.SetSwitch("SimdSkia.Vulkan.ExternalCpuBridge",args.Contains("--reference"));
AppContext.SetSwitch("SimdSkia.Vulkan.DisableRectangleBatches",args.Contains("--no-batch"));
void Check(bool condition,string label){checks++;if(!condition)throw new Exception($"Vulkan interop assertion {checks}: {label}");}
void Capture(SKImage image,string kind,int scene,int transform){using var bitmap=new SKBitmap(new SKImageInfo(image.Width,image.Height,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(bitmap.Info,bitmap.GetPixels(),bitmap.RowBytes,0,0),"read image");var bytes=bitmap.GetPixelSpan().ToArray();cases.Add(new{kind,scene,transform,offset=binary.Position,length=bytes.Length});binary.Write(bytes);}
using var hostPixels=new PixelBuffer(1,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);using var host=new VulkanCanvas(hostPixels);var handles=host.ContextHandles;using var transfer=new VulkanTransfer(handles);
using var loader=new Loader();using var descriptor=new GRVkBackendContext{VkInstance=handles.Instance,VkPhysicalDevice=handles.PhysicalDevice,VkDevice=handles.Device,VkQueue=handles.Queue,GraphicsQueueIndex=handles.QueueFamily,GetProcedureAddress=loader.Get};using var context=GRContext.CreateVulkan(descriptor)??throw new Exception("The original or managed Vulkan context could not be created.");using var space=SKColorSpace.CreateSrgb();using var props=new SKSurfaceProperties(SKPixelGeometry.Unknown);
VulkanImage Create(){using var pixels=PixelBuffer.Rent(32,24,PixelFormat.Rgba8888);for(int y=0;y<24;y++)for(int x=0;x<32;x++)pixels.WritePixel(x,y,new((byte)(20+x*5),(byte)(30+y*7),(byte)(40+x+y),255));return transfer.Create(pixels);}
GRVkImageInfo Info(VulkanImage image,bool adopted=false){var d=image.Description;return new(){Image=d.Image,Alloc=adopted?default:new GRVkAlloc{Memory=d.Memory,Size=d.AllocationSize},Format=d.Format,ImageLayout=d.Layout,ImageUsageFlags=d.Usage,LevelCount=1,SampleCount=1,CurrentQueueFamily=handles.QueueFamily};}
for(int index=0;index<Factories.ImageCases.Length;index++)for(int origin=0;origin<2;origin++)
{
 var factory=Factories.ImageCases[index];using var source=Create();var original=source.Description;using var texture=new GRBackendTexture(32,24,Info(source,factory.Adopted));int before=releases;
 var image=Factories.Image(index,context,texture,(GRSurfaceOrigin)origin,SKColorType.Rgba8888,SKAlphaType.Premul,space,_=>releases++,null!);values[$"image-{index}-{origin}-available"]=image is not null;
 if(image is null)continue;
 if(factory.Adopted)source.Invalidate();
 try
 {
  values[$"image-{index}-{origin}"]=new{image.IsTextureBacked,image.IsLazyGenerated,peek=image.PeekPixels() is null};Capture(image,"image",index,origin);
  using(var raster=image.ToRasterImage()){Check(!raster.IsTextureBacked,"raster conversion");Capture(raster,"raster",index,origin);}
  using(var surface=SKSurface.Create(context,true,new SKImageInfo(40,32,SKColorType.Rgba8888,SKAlphaType.Premul))!){surface.Canvas.Clear(SKColors.Black);surface.Canvas.DrawImage(image,4,3);using var snapshot=surface.Snapshot();Capture(snapshot,"draw",index,origin);}
 }
 finally{image.Dispose();context.Flush(true,true);context.PurgeResources();if(factory.Adopted)unsafe{Vk.vkFreeMemory(handles.Device,original.Memory,null);}}
 Check(releases==before+(factory.Callback?1:0),"release callback count");
}
for(int index=0;index<Factories.SurfaceCases.Length;index++)for(int origin=0;origin<2;origin++)
{
 using var source=Create();var info=Info(source);using var texture=new GRBackendTexture(32,24,info);using var target=new GRBackendRenderTarget(32,24,info);using var surface=Factories.Surface(index,context,texture,target,(GRSurfaceOrigin)origin,0,SKColorType.Rgba8888,space,props);values[$"surface-{index}-{origin}-available"]=surface is not null;if(surface is null)continue;
 using(var initial=surface.Snapshot())Capture(initial,"surface-before",index,origin);
 surface.Canvas.Clear(new SKColor(20,30,50));using var paint=new SKPaint{Color=new(200,80,40,170)};surface.Canvas.DrawRect(new SKRect(3,4,20,16),paint);surface.Flush();
#if SIMDSKIA
 using(var actual=source.ReadPixels())using(var expected=new SKBitmap(new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul))){Check(surface.ReadPixels(expected.Info,expected.GetPixels(),expected.RowBytes,0,0),"surface read for physical comparison");bool flipped=!Factories.SurfaceCases[index].Origin||origin==1;var color=expected.GetPixel(4,5);Check(actual.ReadPixel(4,flipped?18:5)==new Rgba32(color.Red,color.Green,color.Blue,color.Alpha),"external Vulkan partial update/orientation");}
#endif
 using(var snapshot=surface.Snapshot()){values[$"surface-{index}-{origin}-texture"]=snapshot.IsTextureBacked;Capture(snapshot,"surface-after",index,origin);surface.Canvas.Clear(SKColors.Green);Capture(snapshot,"snapshot-stable",index,origin);}
 surface.Flush(true,true);context.Submit(true);
#if SIMDSKIA
 // Native SkiaSharp exposes no Vulkan layout getter. Its own surface readback
 // above is the independent reference. Our bridge promises to restore the
 // supplied layout, so additionally verify its actual external device bytes.
 using var devicePixels=source.ReadPixels();Check(devicePixels.ReadPixel(0,0)==new Rgba32(0,128,0,255),"actual external Vulkan image updated");
#endif
}
using(var bitmap=new SKBitmap(32,24)){bitmap.Erase(SKColors.Red);using var raster=SKImage.FromBitmap(bitmap);for(int variant=0;variant<2;variant++){using var image=variant==0?raster.ToTextureImage(context):raster.ToTextureImage(context,false,true);values[$"uploaded-{variant}"]=image.IsTextureBacked;Capture(image,"upload",variant,0);}}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks,releases,host.DeviceName}));Console.WriteLine($"Vulkan interop: {cases.Count} images, {values.Count} metadata, {checks} checks.");
internal sealed unsafe class Loader:IDisposable
{
 private readonly nint library=NativeLibrary.Load(OperatingSystem.IsWindows()?"vulkan-1.dll":"libvulkan.so.1");
 internal nint Get(string name,nint instance,nint device)
 {var bytes=System.Text.Encoding.UTF8.GetBytes(name+'\0');fixed(byte* text=bytes){if(device!=0){var proc=(delegate* unmanaged[Stdcall]<nint,byte*,nint>)NativeLibrary.GetExport(library,"vkGetDeviceProcAddr");nint address=proc(device,text);if(address!=0)return address;}var get=(delegate* unmanaged[Stdcall]<nint,byte*,nint>)NativeLibrary.GetExport(library,"vkGetInstanceProcAddr");return get(instance,text);}}
 public void Dispose()=>NativeLibrary.Free(library);
}
