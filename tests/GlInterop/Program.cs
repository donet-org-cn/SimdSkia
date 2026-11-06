using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text.Json;
return AngleContext.Run((api,renderer)=>args.Contains("--pending")?PendingProbe(api,args[0]):Run(api,renderer,args[0]));
static int PendingProbe(GRGlInterface api,string output)
{
 Directory.CreateDirectory(output);using var context=GRContext.CreateGl(api)!;uint texture=Gl.Texture();int releases=0;var values=new Dictionary<string,int>();
 using var backend=new GRBackendTexture(32,24,false,new GRGlTextureInfo(0x0DE1,texture,0x8058));
 using var surface=SKSurface.Create(context,true,new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul))!;
 var image=SKImage.FromTexture(context,backend,GRSurfaceOrigin.TopLeft,SKColorType.Rgba8888,SKAlphaType.Premul,null!,_=>releases++,null!)!;
 surface.Canvas.Clear(SKColors.Black);surface.Canvas.DrawImage(image,0,0);values["after-draw"]=releases;
 image.Dispose();values["after-image-dispose"]=releases;surface.Flush();values["after-surface-flush"]=releases;
 using var bitmap=new SKBitmap(new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul));if(!surface.ReadPixels(bitmap.Info,bitmap.GetPixels(),bitmap.RowBytes,0,0))throw new Exception("Pending draw readback failed.");values["after-readback"]=releases;
 surface.Dispose();context.Flush(true,true);context.PurgeResources();values["after-last-consumer"]=releases;
 if(releases!=1)throw new Exception("Pending draw callback did not release exactly once.");
 File.WriteAllBytes(Path.Combine(output,"pixels.bin"),bitmap.GetPixelSpan().ToArray());File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));Gl.Delete(texture);return 0;
}
static int Run(GRGlInterface api,string renderer,string output)
{
 Directory.CreateDirectory(output);using var binary=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new Dictionary<string,object?>();int checks=0,releases=0;var token=new object();
 void Check(bool value,string label){checks++;if(!value)throw new Exception($"GL interop assertion {checks}: {label}");}
 void Capture(SKImage image,string kind,int scene,int transform){using var bitmap=new SKBitmap(new SKImageInfo(image.Width,image.Height,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(bitmap.Info,bitmap.GetPixels(),bitmap.RowBytes,0,0),kind+" read");var pixels=bitmap.GetPixelSpan().ToArray();cases.Add(new{kind,scene,transform,offset=binary.Position,length=pixels.Length});binary.Write(pixels);}
 void DeviceCapture(uint texture,string kind,int scene,int transform){var pixels=Gl.Read(texture);cases.Add(new{kind,scene,transform,offset=binary.Position,length=pixels.Length});binary.Write(pixels);}
 using var context=GRContext.CreateGl(api)??throw new Exception("No GL context");using var space=SKColorSpace.CreateSrgb();using var props=new SKSurfaceProperties(SKPixelGeometry.Unknown);
 for(int index=0;index<Factories.ImageCases.Length;index++)for(int flip=0;flip<2;flip++)
 {
  var descriptor=Factories.ImageCases[index];uint name=Gl.Texture();using var backend=new GRBackendTexture(32,24,false,new GRGlTextureInfo(0x0DE1,name,0x8058));
  int prior=releases;var image=Factories.Image(index,context,backend,(GRSurfaceOrigin)flip,SKColorType.Rgba8888,SKAlphaType.Premul,space,ctx=>{Check(ReferenceEquals(ctx,token)||ctx is null,"callback context");releases++;},token)??throw new Exception("Import null: "+descriptor.Name);
  values[$"image-{index}-{flip}"]=new{image.IsTextureBacked,image.IsLazyGenerated,image.ColorType,image.AlphaType,peek=image.PeekPixels() is null};Check(image.IsTextureBacked,"texture backing");
  using(var surface=SKSurface.Create(context,true,new SKImageInfo(40,32,SKColorType.Rgba8888,SKAlphaType.Premul))!)
  {surface.Canvas.Clear(SKColors.Black);surface.Canvas.DrawImage(image,4,3);using var snapshot=surface.Snapshot();Capture(snapshot,"draw-image",index,flip);}
  Capture(image,"import-image",index,flip);using(var raster=image.ToRasterImage()){Check(!raster.IsTextureBacked,"raster image");Capture(raster,"to-raster",index,flip);}
  image.Dispose();context.Flush(true,true);context.PurgeResources();Check(Gl.Exists(name)!=descriptor.Adopted,"texture adoption ownership");if(!descriptor.Adopted)Gl.Delete(name);
  if(descriptor.Callback)Check(releases==prior+1,"release exactly once");else Check(releases==prior,"no unsolicited release");
 }
 for(int index=0;index<Factories.SurfaceCases.Length;index++)for(int flip=0;flip<2;flip++)
 {
  uint name=Gl.Texture(),fbo=Gl.Framebuffer(name);using var texture=new GRBackendTexture(32,24,false,new GRGlTextureInfo(0x0DE1,name,0x8058));using var target=new GRBackendRenderTarget(32,24,0,0,new GRGlFramebufferInfo(fbo,0x8058));
  var surface=Factories.Surface(index,context,texture,target,(GRSurfaceOrigin)flip,0,SKColorType.Rgba8888,space,props)??throw new Exception("Surface import null");
  using(var before=surface.Snapshot())Capture(before,"surface-before",index,flip);
  using(var paint=new SKPaint{Color=new SKColor(200,80,40,170)}){surface.Canvas.ClipRect(new SKRect(1,2,31,22));surface.Canvas.DrawRect(new SKRect(3,4,20,16),paint);paint.BlendMode=SKBlendMode.Src;paint.Color=new(30,80,170,120);surface.Canvas.DrawRect(new SKRect(15,10,29,20),paint);}
  using(var frozen=surface.Snapshot())
  {values[$"surface-{index}-{flip}-texture-snapshot"]=frozen.IsTextureBacked;Capture(frozen,"surface-after",index,flip);surface.Canvas.Clear(SKColors.Green);Capture(frozen,"snapshot-stable",index,flip);}
  surface.Flush();DeviceCapture(name,"external-target",index,flip);surface.Dispose();Check(Gl.Exists(name),"borrowed surface texture retained");Check(Gl.FramebufferExists(fbo),"borrowed framebuffer retained");Gl.DeleteFramebuffer(fbo);Gl.Delete(name);
 }
 for(int format=0;format<2;format++)for(int alpha=0;alpha<3;alpha++)for(int origin=0;origin<2;origin++)
 {
  uint name=Gl.Texture(alpha);using var backend=new GRBackendTexture(32,24,false,new GRGlTextureInfo(0x0DE1,name,0x8058));var type=format==0?SKColorType.Rgba8888:SKColorType.Bgra8888;var alphaType=alpha==0?SKAlphaType.Opaque:alpha==1?SKAlphaType.Premul:SKAlphaType.Unpremul;
  try
  {
   using var image=SKImage.FromTexture(context,backend,(GRSurfaceOrigin)origin,type,alphaType);int scene=format*3+alpha;values[$"alpha-import-{scene}-{origin}"]=image is not null;if(image is null)continue;Capture(image,"alpha-import",scene,origin);
   for(int sampling=0;sampling<2;sampling++)
   {using var surface=SKSurface.Create(context,true,new SKImageInfo(40,32,SKColorType.Rgba8888,SKAlphaType.Premul))!;surface.Canvas.Clear(new SKColor(20,40,60,120));using var paint=new SKPaint{Color=new SKColor(255,255,255,145),IsAntialias=true};surface.Canvas.DrawImage(image,new SKRect(2,3,29,21),new SKRect(1.5f,2.25f,35.75f,27.5f),new SKSamplingOptions((SKFilterMode)sampling),paint);using var snapshot=surface.Snapshot();Capture(snapshot,"alpha-draw-"+sampling,scene,origin);}
  }
  finally{Gl.Delete(name);}
 }
 using(var bitmap=new SKBitmap(32,24)){bitmap.Erase(new SKColor(90,130,190,175));using var raster=SKImage.FromBitmap(bitmap);for(int variant=0;variant<3;variant++){using var image=variant switch{0=>raster.ToTextureImage(context),1=>raster.ToTextureImage(context,true),_=>raster.ToTextureImage(context,false,true)};Check(image.IsTextureBacked,"uploaded texture");Capture(image,"upload",variant,0);var alias=image.ToTextureImage(context);values[$"upload-{variant}-same-wrapper"]=ReferenceEquals(alias,image);if(!ReferenceEquals(alias,image))alias.Dispose();}}
 // Texture callbacks belong to the last consumer, including shaders and
 // recorded pictures, rather than only the first SKImage wrapper.
 for(int consumer=0;consumer<2;consumer++)
 {
  uint name=Gl.Texture();int callbacks=0;using var backend=new GRBackendTexture(32,24,false,new GRGlTextureInfo(0x0DE1,name,0x8058));
  var image=SKImage.FromTexture(context,backend,GRSurfaceOrigin.TopLeft,SKColorType.Rgba8888,SKAlphaType.Premul,null!,_=>callbacks++,null!)!;
  SKShader? shader=null;SKPicture? picture=null;
  if(consumer==0)shader=image.ToShader();
  else{using var recorder=new SKPictureRecorder();recorder.BeginRecording(new SKRect(0,0,32,24)).DrawImage(image,0,0);picture=recorder.EndRecording();}
  image.Dispose();context.Flush(true,true);context.PurgeResources();values[$"consumer-{consumer}-after-image"]=callbacks;
  if(picture is not null){using var surface=SKSurface.Create(context,true,new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul))!;surface.Canvas.Clear(SKColors.Black);surface.Canvas.DrawPicture(picture);using var snapshot=surface.Snapshot();Capture(snapshot,"retained-picture",consumer,0);}
  shader?.Dispose();picture?.Dispose();context.Flush(true,true);context.PurgeResources();values[$"consumer-{consumer}-after-consumer"]=callbacks;Check(callbacks==1,"last consumer releases texture exactly once");Gl.Delete(name);
 }
 int managedLifecycleChecks=0;
#if SIMDSKIA
 managedLifecycleChecks=Validation.Run();
#endif
 File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks,releases,renderer,managedLifecycleChecks}));Console.WriteLine($"GL interop: {cases.Count} images, {values.Count} metadata, {checks} checks, {managedLifecycleChecks} managed lifecycle checks.");return 0;
}
internal static unsafe class Gl
{
 private static nint P(string name)=>AngleContext.GetProcedure(name);
 internal static uint Texture(int alpha=0){uint texture=0;((delegate* unmanaged[Stdcall]<int,uint*,void>)P("glGenTextures"))(1,&texture);((delegate* unmanaged[Stdcall]<uint,uint,void>)P("glBindTexture"))(0x0DE1,texture);foreach(uint p in new uint[]{0x2800,0x2801})((delegate* unmanaged[Stdcall]<uint,uint,int,void>)P("glTexParameteri"))(0x0DE1,p,0x2600);var bytes=new byte[32*24*4];for(int y=0;y<24;y++)for(int x=0;x<32;x++){int i=(y*32+x)*4;bytes[i]=(byte)(20+x*5);bytes[i+1]=(byte)(30+y*7);bytes[i+2]=(byte)(40+x+y);byte a=alpha==0?(byte)255:(byte)((x+y)%4*70);bytes[i+3]=a;if(alpha==1)for(int channel=0;channel<3;channel++)bytes[i+channel]=(byte)((bytes[i+channel]*a+127)/255);}fixed(byte* data=bytes)((delegate* unmanaged[Stdcall]<uint,int,int,int,int,int,uint,uint,byte*,void>)P("glTexImage2D"))(0x0DE1,0,0x1908,32,24,0,0x1908,0x1401,data);return texture;}
 internal static uint Framebuffer(uint texture){uint fbo=0;((delegate* unmanaged[Stdcall]<int,uint*,void>)P("glGenFramebuffers"))(1,&fbo);((delegate* unmanaged[Stdcall]<uint,uint,void>)P("glBindFramebuffer"))(0x8D40,fbo);((delegate* unmanaged[Stdcall]<uint,uint,uint,uint,int,void>)P("glFramebufferTexture2D"))(0x8D40,0x8CE0,0x0DE1,texture,0);return fbo;}
 internal static byte[] Read(uint texture){uint fbo=Framebuffer(texture);try{var pixels=new byte[32*24*4];fixed(byte* data=pixels)((delegate* unmanaged[Stdcall]<int,int,int,int,uint,uint,byte*,void>)P("glReadPixels"))(0,0,32,24,0x1908,0x1401,data);return pixels;}finally{DeleteFramebuffer(fbo);}}
 internal static bool Exists(uint texture)=>((delegate* unmanaged[Stdcall]<uint,byte>)P("glIsTexture"))(texture)!=0;
 internal static bool FramebufferExists(uint fbo)=>((delegate* unmanaged[Stdcall]<uint,byte>)P("glIsFramebuffer"))(fbo)!=0;
 internal static void Delete(uint texture)=>((delegate* unmanaged[Stdcall]<int,uint*,void>)P("glDeleteTextures"))(1,&texture);
 internal static void DeleteFramebuffer(uint fbo)=>((delegate* unmanaged[Stdcall]<int,uint*,void>)P("glDeleteFramebuffers"))(1,&fbo);
}
