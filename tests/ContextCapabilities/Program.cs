using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();int checks=0;
void Check(bool b){checks++;if(!b)throw new Exception("Context capability assertion "+checks);}
AngleContext.Run((api,renderer)=>{Check(api.Validate());values["validated"]=api.Validate();values["angle"]=api.HasExtension("GL_ANGLE_instanced_arrays");values["missing"]=api.HasExtension("GL_SimdSkia_nonexistent");values["renderer"]=renderer;return 0;});
Check(GRGlInterface.CreateGles(_=>IntPtr.Zero) is null);Check(GRGlInterface.CreateEvas(IntPtr.Zero) is null);
Exception? failure=null;var thread=new Thread(()=>{try{using var a=new SKAutoCoInitialize();using var b=new SKAutoCoInitialize();Check(a.Initialized&&b.Initialized);b.Uninitialize();b.Uninitialize();Check(!b.Initialized&&a.Initialized);a.Uninitialize();Check(!a.Initialized);}catch(Exception e){failure=e;}});if(OperatingSystem.IsWindows())thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure is not null)throw failure;
using var hostPixels=new SimdSkia.PixelBuffer(1,1,SimdSkia.PixelFormat.Rgba8888,SimdSkia.AlphaFormat.Premultiplied);using var host=new SimdSkia.Gpu.VulkanCanvas(hostPixels);var handles=host.ContextHandles;var vk=NativeLibrary.Load("vulkan-1.dll");try{int calls=0;IntPtr Get(string name,IntPtr instance,IntPtr device){calls++;return NativeLibrary.GetExport(vk,name);}using var extensions=GRVkExtensions.Create(Get,handles.Instance,handles.PhysicalDevice,["VK_KHR_surface"],[]);Check(calls>0);extensions.HasExtension("VK_KHR_surface",1);extensions.HasExtension("VK_SimdSkia_nonexistent",1);values["enumerated"]=calls>0;}finally{NativeLibrary.Free(vk);}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"PASS {checks} live ANGLE/Vulkan/COM checks");
