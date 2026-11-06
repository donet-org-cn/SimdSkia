using SkiaSharp;
using System.Text.Json;
using System.Text.Json.Serialization;
string output=args[0];Directory.CreateDirectory(output);using var binary=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<Frame>();var values=new Dictionary<string,string>();int checks=0;
void Check(bool condition){checks++;if(!condition)throw new Exception("WGL assertion "+checks);}
for(int profile=0;profile<2;profile++)
{
 int currentProfile=profile;
 WglContext.Run(profile==1,(api,renderer)=>
 {
  values[$"renderer-{currentProfile}"]=renderer;
  for(int factory=0;factory<4;factory++)
  {
   using var context=factory switch{0=>GRContext.CreateGl(),1=>GRContext.CreateGl(new GRContextOptions()),2=>GRContext.CreateGl(api),_=>GRContext.CreateGl(api,new GRContextOptions())};Check(context is not null);
#if SIMDSKIA
   if(factory==0)WglContext.WithoutCurrent(()=>{bool failed=false;try{context!.Flush();}catch(InvalidOperationException){failed=true;}Check(failed);});
#endif
   using var surface=SKSurface.Create(context!,true,new SKImageInfo(48,36,SKColorType.Rgba8888,SKAlphaType.Premul))!;surface.Canvas.Clear(new SKColor(20,30,50));using var paint=new SKPaint{Color=new SKColor(210,80,50,180)};surface.Canvas.DrawRect(new SKRect(3,5,28,30),paint);using var gradient=SKShader.CreateLinearGradient(new(0,0),new(48,36),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);paint.Shader=gradient;paint.Color=SKColors.White;surface.Canvas.DrawRect(new SKRect(24,7,45,33),paint);
   using var bitmap=new SKBitmap(new SKImageInfo(48,36,SKColorType.Rgba8888,SKAlphaType.Premul));Check(surface.ReadPixels(bitmap.Info,bitmap.GetPixels(),bitmap.RowBytes,0,0));var pixels=bitmap.GetPixelSpan().ToArray();cases.Add(new("system-gl",factory,currentProfile,binary.Position,pixels.Length));binary.Write(pixels);
  }
 });
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,AotJson.Default.Values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases,AotJson.Default.Frames));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new Local(checks,!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported,Environment.Version.ToString()),AotJson.Default.Local));Console.WriteLine($"WGL: {cases.Count} images, {checks} checks.");
internal sealed record Frame(string kind,int scene,int transform,long offset,int length);
internal sealed record Local(int checks,bool nativeAot,string runtime);
[JsonSerializable(typeof(List<Frame>),TypeInfoPropertyName="Frames")]
[JsonSerializable(typeof(Dictionary<string,string>),TypeInfoPropertyName="Values")]
[JsonSerializable(typeof(Local),TypeInfoPropertyName="Local")]
internal partial class AotJson:JsonSerializerContext { }
