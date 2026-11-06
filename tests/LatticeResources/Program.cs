using SkiaSharp;
using System.Text.Json;
using System.Runtime.InteropServices;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("Lattice/resource assertion "+checks);}
var defaults=new GRContextOptions();values["options-default"]=new object[]{defaults.AvoidStencilBuffers,defaults.RuntimeProgramCacheSize,defaults.GlyphCacheTextureMaximumBytes,defaults.AllowPathMaskCaching,defaults.DoManualMipmapping,defaults.BufferMapThreshold};
defaults.AvoidStencilBuffers=true;defaults.RuntimeProgramCacheSize=99;defaults.GlyphCacheTextureMaximumBytes=123;defaults.AllowPathMaskCaching=false;defaults.DoManualMipmapping=true;defaults.BufferMapThreshold=55;
values["options-mutated"]=new object[]{defaults.AvoidStencilBuffers,defaults.RuntimeProgramCacheSize,defaults.GlyphCacheTextureMaximumBytes,defaults.AllowPathMaskCaching,defaults.DoManualMipmapping,defaults.BufferMapThreshold};
using(var info=new GRD3DTextureResourceInfo{Resource=(IntPtr)123,ResourceState=45,Format=67,SampleCount=8,LevelCount=4,SampleQualityPattern=13,Protected=true}){values["d3d-info"]=new object[]{(long)info.Resource,info.ResourceState,info.Format,info.SampleCount,info.LevelCount,info.SampleQualityPattern,info.Protected};info.Dispose();Check(info.Resource==(IntPtr)123);}
using(var context=new GRD3DBackendContext{Adapter=(IntPtr)123,Device=(IntPtr)234,Queue=(IntPtr)345,ProtectedContext=true}){values["d3d-context"]=new object[]{(long)context.Adapter,(long)context.Device,(long)context.Queue,context.ProtectedContext};context.Dispose();Check(context.Queue==(IntPtr)345);}
using(var context=new GRMtlBackendContext{DeviceHandle=(IntPtr)444,QueueHandle=(IntPtr)555}){values["metal-context"]=new object[]{(long)context.DeviceHandle,(long)context.QueueHandle};context.Dispose();Check(context.QueueHandle==(IntPtr)555);}
var mtl=new GRMtlTextureInfo((IntPtr)456);var mtl2=mtl;Check(mtl==mtl2);Check(mtl.Equals((object)mtl2));Check(mtl.GetHashCode()==mtl2.GetHashCode());mtl2.TextureHandle=(IntPtr)789;Check(mtl!=mtl2);values["metal-value"]=new object[]{Marshal.SizeOf<GRMtlTextureInfo>(),(long)mtl.TextureHandle,(long)mtl2.TextureHandle};
GRVkGetProcedureAddressDelegate vk=(name,instance,device)=>(IntPtr)(name.Length+(long)instance+(long)device);GRGlGetProcedureAddressDelegate gl=name=>(IntPtr)name.Length;SKImageTextureReleaseDelegate release=ctx=>Check((string)ctx=="released");release("released");Check(vk("vkTest",(IntPtr)2,(IntPtr)3)==(IntPtr)11);Check(gl("glTest")== (IntPtr)6);
using(var context=new GRVkBackendContext{VkInstance=(IntPtr)1,VkPhysicalDevice=(IntPtr)2,VkDevice=(IntPtr)3,VkQueue=(IntPtr)4,GraphicsQueueIndex=5,MaxAPIVersion=6,VkPhysicalDeviceFeatures=(IntPtr)7,VkPhysicalDeviceFeatures2=(IntPtr)8,GetProcedureAddress=vk,ProtectedContext=true})values["vk-context"]=new object[]{(long)context.VkInstance,(long)context.VkPhysicalDevice,(long)context.VkDevice,(long)context.VkQueue,context.GraphicsQueueIndex,context.MaxAPIVersion,(long)context.VkPhysicalDeviceFeatures,(long)context.VkPhysicalDeviceFeatures2,context.GetProcedureAddress("vk",(IntPtr)2,(IntPtr)3).ToInt64(),context.ProtectedContext};
var latticeValue=new SKLattice{XDivs=[2,6],YDivs=[3,7],Bounds=new(0,0,10,10),Colors=[SKColors.Red],RectTypes=[SKLatticeRectType.FixedColor]};var copy=latticeValue;var equalContent=latticeValue;equalContent.XDivs=[2,6];values["lattice-equality"]=new object[]{latticeValue.Equals(copy),latticeValue==copy,latticeValue!=equalContent,latticeValue.Equals((object)copy),copy.GetHashCode()==latticeValue.GetHashCode()};
using var source=new SKBitmap(new SKImageInfo(19,17,SKColorType.Rgba8888,SKAlphaType.Premul));
for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)source.SetPixel(x,y,new SKColor((byte)(x*13),(byte)(y*15),(byte)((x*23+y*11)%256),(byte)(90+(x*9+y*13)%166)));
using var image=SKImage.FromBitmap(source);
for(int scene=0;scene<7;scene++)for(int size=0;size<4;size++)for(int filter=0;filter<2;filter++)
{
 using var surface=SKSurface.Create(new SKImageInfo(88,76,SKColorType.Rgba8888,SKAlphaType.Premul))!;var canvas=surface.Canvas;canvas.Clear(new SKColor(30,40,50));using var paint=new SKPaint{Color=new SKColor(255,255,255,190)};
 SKRect dst=size switch{0=>new(3,4,78,65),1=>new(3,4,14,13),2=>new(3.25f,4.5f,63.75f,64.25f),_=>new(4,5,23,22)};
 var lattice=new SKLattice{XDivs=[3,12],YDivs=[4,11]};
 if(scene==1){lattice.XDivs=[0,4,8,13];lattice.YDivs=[0,5,9,13];}
 if(scene==2)lattice.Bounds=new(2,3,17,15);
 if(scene==3){lattice.RectTypes=new SKLatticeRectType[9];lattice.RectTypes[1]=SKLatticeRectType.Transparent;lattice.RectTypes[4]=SKLatticeRectType.FixedColor;lattice.Colors=new SKColor[9];lattice.Colors[4]=new(230,60,90,140);
#if NATIVE_ORACLE
 // The native Skia lattice wire format uses byte rect types, but the 3.119.4
 // managed wrapper forwards int enums without conversion. Keep the ordinary
 // wrapper's failures in lattice-resources-1; this separate oracle packs the
 // exact same semantic types for the C entry point, without modifying Skia.
 if(args.Contains("--packed-lattice")){byte[] packed=[0,1,0,0,2,0,0,0,0];var raw=MemoryMarshal.AsBytes(lattice.RectTypes.AsSpan());raw.Clear();packed.CopyTo(raw);}
#endif
 }
 if(scene==4){lattice.XDivs=[3,2];}
 if(scene==5){canvas.DrawImageNinePatch(image,new(3,4,12,11),dst,(SKFilterMode)filter,paint);}
 else if(scene==6){canvas.DrawBitmapLattice(source,lattice,dst,(SKFilterMode)filter,paint);}
 else canvas.DrawImageLattice(image,lattice,dst,(SKFilterMode)filter,paint);
 using var result=surface.Snapshot();using var read=new SKBitmap(new SKImageInfo(88,76,SKColorType.Rgba8888,SKAlphaType.Premul));Check(result.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));byte[] bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind="lattice",scene,transform=size*2+filter,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} lattice images, {values.Count} metadata entries and {checks} resource/validation checks");
