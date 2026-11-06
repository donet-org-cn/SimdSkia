using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);using var stream=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new SortedDictionary<string,object?>();int offset=0,checks=0;
void Capture(SKBitmap bitmap,object metadata){var bytes=bitmap.GetPixelSpan();stream.Write(bytes);cases.Add(new{metadata,offset,length=bytes.Length});offset+=bytes.Length;}
float[] identity=[1,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,1,0];var factories=new List<(string Name,Func<SKColorFilter?> Create)>();factories.Add(("identity",()=>SKColorFilter.CreateColorMatrix(identity)));
for(int i=0;i<8;i++){int id=i;factories.Add(($"matrix-{i}",()=>{var m=(float[])identity.Clone();if(id==0)m[4]=.25f;else if(id==1){m[0]=.4f;m[6]=1.3f;m[12]=.7f;}else if(id==2){m[15]=.3f;m[18]=.5f;m[19]=.1f;}else if(id==3){m[0]=0;m[2]=1;m[10]=1;m[12]=0;}else if(id==4){m[0]=-.5f;m[4]=.5f;}else if(id==5){m[0]=1.2f;m[1]=.3f;m[2]=-.4f;}else if(id==6){m[18]=0;m[19]=.3f;}else {m[3]=.4f;m[9]=-.1f;}return SKColorFilter.CreateColorMatrix(m);}));}
foreach(var mode in Enum.GetValues<SKBlendMode>()){var captured=mode;factories.Add(($"blend-{(int)mode}",()=>SKColorFilter.CreateBlendMode(new SKColor(231,58,19,171),captured)));}
factories.Add(("lighting",()=>SKColorFilter.CreateLighting(new SKColor(140,210,40),new SKColor(10,39,2))));factories.Add(("luma",SKColorFilter.CreateLumaColor));factories.Add(("srgb-linear",SKColorFilter.CreateSrgbToLinearGamma));factories.Add(("linear-srgb",SKColorFilter.CreateLinearToSrgbGamma));
byte[] reverse=Enumerable.Range(0,256).Select(i=>(byte)(255-i)).ToArray(),table=Enumerable.Range(0,256).Select(i=>(byte)(i/2)).ToArray();factories.Add(("table-rgb",()=>SKColorFilter.CreateTable(reverse)));factories.Add(("table-argb",()=>SKColorFilter.CreateTable(table,reverse,table,reverse)));
for(int i=0;i<4;i++){int id=i;factories.Add(($"hsla-{i}",()=>{var m=(float[])identity.Clone();m[4]=id*.25f;m[6]=.8f;m[14]=.05f;return SKColorFilter.CreateHslaColorMatrix(m);}));}
foreach(bool gray in new[]{false,true})foreach(var invert in Enum.GetValues<SKHighContrastConfigInvertStyle>())foreach(float contrast in new[]{-.7f,0,.6f}){var g=gray;var inv=invert;var con=contrast;factories.Add(($"contrast-{g}-{(int)inv}-{con}",()=>SKColorFilter.CreateHighContrast(g,inv,con)));}
factories.Add(("compose",()=>{using var a=SKColorFilter.CreateLighting(new SKColor(128,230,191),SKColors.Black);using var b=SKColorFilter.CreateSrgbToLinearGamma();return SKColorFilter.CreateCompose(a,b);}));factories.Add(("lerp",()=>{using var a=SKColorFilter.CreateLumaColor();using var b=SKColorFilter.CreateLighting(new SKColor(110,238,28),new SKColor(19,2,73));return SKColorFilter.CreateLerp(.3f,a,b);}));
var colors=new[]{SKColors.Transparent,SKColors.White,SKColors.Black,SKColors.Red,SKColors.Blue,new SKColor(30,100,211,255),new SKColor(219,91,3,193),new SKColor(1,250,111,9),new SKColor(111,82,160,128),new SKColor(90,159,193,1),new SKColor(253,1,129,254),new SKColor(127,129,128,73)};
foreach(var (name,create) in factories)
{
 using var filter=create();values[name+"-null"]=filter is null;
 for(int color=0;color<colors.Length;color++)foreach(int pipeline in new[]{0,1,2})
 {
  using var bitmap=new SKBitmap(new SKImageInfo(2,2,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(91,131,29,171));using var paint=new SKPaint{Color=colors[color],BlendMode=SKBlendMode.Src};
  if(pipeline==0){paint.ColorFilter=filter;canvas.DrawRect(0,0,2,2,paint);}
  else if(pipeline==1){using var shader=SKShader.CreateColor(colors[color]);using var filtered=filter is null?shader.WithLocalMatrix(SKMatrix.Identity):shader.WithColorFilter(filter);paint.Color=SKColors.White;paint.Shader=filtered;canvas.DrawRect(0,0,2,2,paint);}
  else {using var source=new SKBitmap(new SKImageInfo(2,2,SKColorType.Rgba8888,SKAlphaType.Premul));source.Erase(colors[color]);paint.Color=new SKColor(0,0,0,193);paint.ColorFilter=filter;canvas.DrawBitmap(source,0,0,paint);}
  Capture(bitmap,new{name,color,pipeline});
 }
}
using(var filter=SKColorFilter.CreateSrgbToLinearGamma()){filter.Dispose();using var retained=SKShader.CreateColor(SKColors.Gray).WithColorFilter(filter);checks++;}
values["local-checks"]=checks;File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {cases.Count} filter images, {checks} lifetime checks");
