using SkiaSharp;
#if VULKAN
using SimdSkia;
using SimdSkia.Gpu;
using System.Runtime.InteropServices;
#endif
internal static class GpuHost
{
 internal static void Run(Action<GRContext,SKSurface,string> action)
 {
#if VULKAN
  using var hostPixels=new PixelBuffer(1,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);using var host=new VulkanCanvas(hostPixels);var handles=host.ContextHandles;using var transfer=new VulkanTransfer(handles);using var initial=PixelBuffer.Rent(640,480,PixelFormat.Rgba8888);using var resource=transfer.Create(initial);var native=resource.Description;using var loader=new Loader();
  using var descriptor=new GRVkBackendContext{VkInstance=handles.Instance,VkPhysicalDevice=handles.PhysicalDevice,VkDevice=handles.Device,VkQueue=handles.Queue,GraphicsQueueIndex=handles.QueueFamily,GetProcedureAddress=loader.Get};using var context=GRContext.CreateVulkan(descriptor)??throw new Exception("Vulkan context unavailable.");
  using var texture=new GRBackendTexture(640,480,new GRVkImageInfo{Image=native.Image,Alloc=new(){Memory=native.Memory,Size=native.AllocationSize},Format=native.Format,ImageLayout=native.Layout,ImageUsageFlags=native.Usage,SampleCount=1,LevelCount=1,CurrentQueueFamily=handles.QueueFamily});using var surface=SKSurface.Create(context,texture,GRSurfaceOrigin.TopLeft,SKColorType.Rgba8888)??throw new Exception("External Vulkan target unavailable.");
  action(context,surface,host.DeviceName);surface.Flush(true,true);context.Submit(true);
#elif DIRECT3D
  using var host=new D3DHost();using var descriptor=new GRD3DBackendContext{Adapter=host.Adapter,Device=host.Device,Queue=host.Queue};using var context=GRContext.CreateDirect3D(descriptor)??throw new Exception("D3D12 context unavailable.");
  using var surface=SKSurface.Create(context,true,new SKImageInfo(640,480,SKColorType.Rgba8888,SKAlphaType.Premul))??throw new Exception("D3D12 surface unavailable.");action(context,surface,host.Name);surface.Flush(true,true);context.Submit(true);
#else
  WglContext.Run(true,(api,name)=>{using var context=GRContext.CreateGl(api);using var surface=SKSurface.Create(context,true,new SKImageInfo(640,480,SKColorType.Rgba8888,SKAlphaType.Premul))!;action(context,surface,name);});
#endif
 }
#if VULKAN
 private sealed unsafe class Loader:IDisposable
 {
  private readonly nint library=NativeLibrary.Load(OperatingSystem.IsWindows()?"vulkan-1.dll":"libvulkan.so.1");
  internal nint Get(string name,nint instance,nint device){var bytes=System.Text.Encoding.UTF8.GetBytes(name+'\0');fixed(byte* text=bytes){if(device!=0){nint result=((delegate* unmanaged[Stdcall]<nint,byte*,nint>)NativeLibrary.GetExport(library,"vkGetDeviceProcAddr"))(device,text);if(result!=0)return result;}return ((delegate* unmanaged[Stdcall]<nint,byte*,nint>)NativeLibrary.GetExport(library,"vkGetInstanceProcAddr"))(instance,text);}}
  public void Dispose()=>NativeLibrary.Free(library);
 }
#endif
}
