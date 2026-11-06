using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text.Json;
using SimdSkia;
using SimdSkia.Gpu.Direct2D;
using var host=new D3DHost();using var sourceDevice=new D3D12Device(host.Device,host.Queue);return Run(host,sourceDevice,args[0]);
static int Run(D3DHost host,D3D12Device sourceDevice,string output)
{
 string renderer=host.Name;
 Directory.CreateDirectory(output);using var binary=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new Dictionary<string,object?>();int checks=0,releases=0;var token=new object();
 void Check(bool value,string label){checks++;if(!value)throw new Exception($"D3D12 interop assertion {checks}: {label}");}
 void Capture(SKImage image,string kind,int scene,int transform){using var bitmap=new SKBitmap(new SKImageInfo(image.Width,image.Height,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(bitmap.Info,bitmap.GetPixels(),bitmap.RowBytes,0,0),kind+" read");var pixels=bitmap.GetPixelSpan().ToArray();cases.Add(new{kind,scene,transform,offset=binary.Position,length=pixels.Length});binary.Write(pixels);}
 void DeviceCapture(Resource resource,string kind,int scene,int transform){using var buffer=resource.Image.ReadPixels();var pixels=buffer.CopyBytes();cases.Add(new{kind,scene,transform,offset=binary.Position,length=pixels.Length});binary.Write(pixels);}
 using var descriptor=new GRD3DBackendContext{Adapter=host.Adapter,Device=host.Device,Queue=host.Queue};using var context=GRContext.CreateDirect3D(descriptor)??throw new Exception("No D3D12 context");using var space=SKColorSpace.CreateSrgb();using var props=new SKSurfaceProperties(SKPixelGeometry.Unknown);
 for(int index=0;index<Factories.ImageCases.Length;index++)for(int flip=0;flip<2;flip++)
 {
  var factory=Factories.ImageCases[index];using var source=Resource.Create(sourceDevice);nint name=source.Handle;using var backend=new GRBackendTexture(32,24,source.Info);if(factory.Adopted)D2DNative.Retain(name);
  int prior=releases;var image=Factories.Image(index,context,backend,(GRSurfaceOrigin)flip,SKColorType.Rgba8888,SKAlphaType.Premul,space,ctx=>{Check(ReferenceEquals(ctx,token)||ctx is null,"callback context");releases++;},token)??throw new Exception("Import null: "+factory.Name);
  values[$"image-{index}-{flip}"]=new{image.IsTextureBacked,image.IsLazyGenerated,image.ColorType,image.AlphaType,peek=image.PeekPixels() is null};Check(image.IsTextureBacked,"texture backing");
  using(var surface=SKSurface.Create(context,true,new SKImageInfo(40,32,SKColorType.Rgba8888,SKAlphaType.Premul))!)
  {surface.Canvas.Clear(SKColors.Black);surface.Canvas.DrawImage(image,4,3);using var snapshot=surface.Snapshot();Capture(snapshot,"draw-image",index,flip);}
  Capture(image,"import-image",index,flip);using(var raster=image.ToRasterImage()){Check(!raster.IsTextureBacked,"raster image");Capture(raster,"to-raster",index,flip);}
  image.Dispose();context.Flush(true,true);context.PurgeResources();using(var actual=source.Image.ReadPixels())Check(actual.ReadPixel(0,0).A==255,"caller resource remains valid after image disposal");
  if(factory.Callback)Check(releases==prior+1,"release exactly once");else Check(releases==prior,"no unsolicited release");
 }
 for(int index=0;index<Factories.SurfaceCases.Length;index++)for(int flip=0;flip<2;flip++)
 {
  using var source=Resource.Create(sourceDevice);using var texture=new GRBackendTexture(32,24,source.Info);using var target=new GRBackendRenderTarget(32,24,source.Info);
  var surface=Factories.Surface(index,context,texture,target,(GRSurfaceOrigin)flip,0,SKColorType.Rgba8888,space,props)??throw new Exception("Surface import null");
  using(var before=surface.Snapshot())Capture(before,"surface-before",index,flip);
  using(var paint=new SKPaint{Color=new SKColor(200,80,40,170)}){surface.Canvas.ClipRect(new SKRect(1,2,31,22));surface.Canvas.DrawRect(new SKRect(3,4,20,16),paint);paint.BlendMode=SKBlendMode.Src;paint.Color=new(30,80,170,120);surface.Canvas.DrawRect(new SKRect(15,10,29,20),paint);}
  using(var frozen=surface.Snapshot())
  {values[$"surface-{index}-{flip}-texture-snapshot"]=frozen.IsTextureBacked;Capture(frozen,"surface-after",index,flip);surface.Canvas.Clear(SKColors.Green);Capture(frozen,"snapshot-stable",index,flip);}
  surface.Flush(true,true);context.Submit(true);DeviceCapture(source,"external-target",index,flip);surface.Dispose();using(var actual=source.Image.ReadPixels())Check(actual.ReadPixel(0,0).A>0,"borrowed D3D12 resource retained");
 }
 using(var bitmap=new SKBitmap(32,24)){bitmap.Erase(new SKColor(90,130,190,175));using var raster=SKImage.FromBitmap(bitmap);for(int variant=0;variant<3;variant++){if(variant==1)continue;using var image=variant switch{0=>raster.ToTextureImage(context),1=>raster.ToTextureImage(context,true),_=>raster.ToTextureImage(context,false,true)};Check(image.IsTextureBacked,"uploaded texture");Capture(image,"upload",variant,0);var alias=image.ToTextureImage(context);values[$"upload-{variant}-same-wrapper"]=ReferenceEquals(alias,image);if(!ReferenceEquals(alias,image))alias.Dispose();}}
 int managedLifecycleChecks=0;
#if SIMDSKIA
 managedLifecycleChecks=Validation.Run();
#endif
 File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks,releases,renderer,managedLifecycleChecks}));Console.WriteLine($"D3D12 interop: {cases.Count} images, {values.Count} metadata, {checks} checks, {managedLifecycleChecks} managed lifecycle checks.");return 0;
}
internal sealed class Resource:IDisposable
{
 internal nint Handle {get;}
 internal D3D12Image Image {get;}
 internal GRD3DTextureResourceInfo Info=>new(){Resource=Handle,ResourceState=0,Format=28,SampleCount=1,LevelCount=1};
 private Resource(nint handle,D3D12Image image){Handle=handle;Image=image;}
 internal static Resource Create(D3D12Device device)
 {
  nint handle=device.CreateTexture(32,24,28);D3D12Image? image=null;
  try{image=new(device,handle,0,28,32,24,false,AlphaFormat.Premultiplied,true,false);handle=0;using var pixels=PixelBuffer.Rent(32,24,PixelFormat.Rgba8888);for(int y=0;y<24;y++)for(int x=0;x<32;x++)pixels.WritePixel(x,y,new((byte)(20+x*5),(byte)(30+y*7),(byte)(40+x+y),255));image.Write(pixels);return new(imageHandle(image),image);}
  catch{image?.Dispose();throw;}finally{if(handle!=0)D2DNative.Release(ref handle);}
 }
 private static nint imageHandle(D3D12Image image)=>image.ResourceHandle;
 public void Dispose()=>Image.Dispose();
}
