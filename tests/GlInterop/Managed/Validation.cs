using SkiaSharp;
internal static unsafe class Validation
{
 internal static int Run()
 {
  int checks=0;void Check(bool condition){checks++;if(!condition)throw new Exception("Managed GL lifecycle assertion "+checks);}
  using(var api=GRGlInterface.CreateGles(AngleContext.GetProcedure))
  {
   var context=GRContext.CreateGl(api)!;using var a=SKSurface.Create(context,true,new SKImageInfo(32,24));using var b=SKSurface.Create(context,true,new SKImageInfo(32,24));
   a!.Canvas.Clear(SKColors.Red);b!.Canvas.Clear(SKColors.Blue);
   var error=Task.Run(()=>{try{context.Flush();return false;}catch(InvalidOperationException){return true;}}).GetAwaiter().GetResult();Check(error);
   int callbacks=0;uint texture=Gl.Texture();using var description=new GRBackendTexture(32,24,false,new GRGlTextureInfo(0x0DE1,texture,0x8058));
   var image=SKImage.FromTexture(context,description,GRSurfaceOrigin.TopLeft,SKColorType.Rgba8888,SKAlphaType.Premul,null!,_=>callbacks++);
   context.Dispose();Check(callbacks==0);image.Dispose();Check(callbacks==1);image.Dispose();Check(callbacks==1);Check(Gl.Exists(texture));Gl.Delete(texture);
  }
  using(var api=GRGlInterface.CreateGles(AngleContext.GetProcedure))
  using(var context=GRContext.CreateGl(api))
  {
   using var a=SKSurface.Create(context,true,new SKImageInfo(32,24));using var b=SKSurface.Create(context,true,new SKImageInfo(32,24));a!.Canvas.Clear(SKColors.Red);b!.Canvas.Clear(SKColors.Blue);using var image=a.Snapshot();
   context.AbandonContext(false);Check(context.IsAbandoned);try{context.Flush();Check(false);}catch(InvalidOperationException){Check(true);}
  }
  using(var api=GRGlInterface.CreateGles(AngleContext.GetProcedure))
  using(var context=GRContext.CreateGl(api))
  {
   context.SetResourceCacheLimit(0);using var surface=SKSurface.Create(context,true,new SKImageInfo(32,24));using var bitmap=new SKBitmap(32,24);bitmap.Erase(SKColors.Red);surface!.Canvas.DrawBitmap(bitmap,0,0);surface.Flush();context.GetResourceCacheUsage(out _,out long before);context.PurgeResources();context.GetResourceCacheUsage(out _,out long after);Check(before==after);
  }
  using(var api=GRGlInterface.CreateGles(AngleContext.GetProcedure))
  using(var context=GRContext.CreateGl(api))
  using(var surface=SKSurface.Create(context,true,new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul)))
  using(var bitmap=new SKBitmap(new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul)))
  {
   surface!.Canvas.Clear(SKColors.Red);uint pbo=0;
   ((delegate* unmanaged[Stdcall]<int,uint*,void>)AngleContext.GetProcedure("glGenBuffers"))(1,&pbo);
   try
   {
    ((delegate* unmanaged[Stdcall]<uint,uint,void>)AngleContext.GetProcedure("glBindBuffer"))(0x88EB,pbo);
    ((delegate* unmanaged[Stdcall]<uint,nint,nint,uint,void>)AngleContext.GetProcedure("glBufferData"))(0x88EB,65536,0,0x88E8);
    foreach(uint parameter in new uint[]{0x0D02,0x0D03,0x0D04})((delegate* unmanaged[Stdcall]<uint,int,void>)AngleContext.GetProcedure("glPixelStorei"))(parameter,7);
    Check(surface.ReadPixels(bitmap.Info,bitmap.GetPixels(),bitmap.RowBytes,0,0));Check(bitmap.GetPixel(0,0)==SKColors.Red&&bitmap.GetPixel(31,23)==SKColors.Red);
   }
   finally{((delegate* unmanaged[Stdcall]<int,uint*,void>)AngleContext.GetProcedure("glDeleteBuffers"))(1,&pbo);}
  }
  return checks;
 }
}
