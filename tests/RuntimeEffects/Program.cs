using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool v){checks++;if(!v)throw new Exception("Runtime assertion "+checks);}
string[] shaders=[
"half4 main(float2 p){return half4(.2,.4,.6,.75);}",
"uniform float2 size;uniform float time;half4 main(float2 p){float2 uv=p/size;return half4(uv,.25+.25*sin(time),1);}",
"uniform float3x3 matrix;half4 main(float2 p){float3 q=matrix*float3(p,1);return half4(fract(q.xy/40),.5,1);}",
"uniform int edge;uniform int3 values;half4 main(float2 p){if(edge==2147483647&&values.x+1==values.y){return half4(.8,.2,.1,1);}else{return half4(.1,.2,.8,1);}}",
"uniform float gains[3];half4 main(float2 p){return half4(gains[0],gains[1],gains[2],1);}",
"layout(color) uniform half4 tint;uniform float alpha;half4 main(float2 p){return tint*alpha;}",
"half4 main(float2 p){return p.x>p.y?half4(.8,.1,.2,1):half4(.1,.8,.2,1);}",
"uniform float2 size;half4 main(float2 p){float2 v=p/size-float2(.5);float shade=1-smoothstep(.05,.45,length(v));return half4(shade*.2,shade*.4,shade*.8,shade);}",
"half4 main(float2 p){float4 c=half4(p.x/64,p.y/48,.2,1);c.rgb=c.bgr;c.r+=.1;return c;}",
"uniform shader image;uniform float2 shift;half4 main(float2 p){return image.eval(p+shift).bgra;}",
"uniform colorFilter filter;uniform float2 size;half4 main(float2 p){return filter.eval(half4(p/size,.3,1));}",
"uniform blender blend;uniform half4 first;uniform half4 second;half4 main(float2 p){return blend.eval(first,second);}",
"const float pi=3.14159265;half4 main(float2 p){float x=cos(pi);return half4(abs(x),.2,.4,1);}",
"uniform float2x2 matrix;half4 main(float2 p){float2 q=matrix*(p/64);return half4(q,.2,1);}",
"uniform int edge;half4 main(float2 p){int a=-2147483648;int b=a+edge;return half4(b==-1?1.0:0.0,.2,.4,1);}",
"half4 main(float2 p){float3 x=normalize(float3(p/64,.5));float d=dot(x,float3(.3,.4,.5));return half4(x*d,1);}"];
for(int scene=0;scene<shaders.Length;scene++)for(int transform=0;transform<2;transform++)
{
    using var effect=SKRuntimeEffect.CreateShader(shaders[scene],out var errors);values[$"shader-{scene}-compiled"]=new object?[]{effect is not null,string.IsNullOrEmpty(errors)};if(effect is null){values[$"shader-{scene}-error"]=errors;continue;}
    using var uniforms=new SKRuntimeEffectUniforms(effect);using var children=new SKRuntimeEffectChildren(effect);values[$"shader-{scene}-layout"]=new object[]{effect.UniformSize,effect.Uniforms,effect.Children,uniforms.Size,uniforms.Count,children.Count};
    if(uniforms.Contains("size"))uniforms["size"]=new SKPoint(64,48);if(uniforms.Contains("time"))uniforms.Add("time",.7f);if(uniforms.Contains("shift"))uniforms["shift"]=new SKPoint(4,2);
    if(uniforms.Contains("edge"))uniforms["edge"]=int.MaxValue;if(uniforms.Contains("values"))uniforms["values"]=new[]{16777216,16777217,7};if(uniforms.Contains("gains"))uniforms["gains"]=new[]{.2f,.4f,.6f};if(uniforms.Contains("tint"))uniforms["tint"]=new SKColorF(.6f,.3f,.2f,.8f);if(uniforms.Contains("alpha"))uniforms["alpha"]=.6f;
    if(uniforms.Contains("matrix")) {if(scene==13)uniforms["matrix"]=new float[]{1,.2f,.1f,1};else uniforms["matrix"]=new SKMatrix(1,.1f,2,.2f,1,3,0,0,1);}
    if(uniforms.Contains("first"))uniforms["first"]=new SKColorF(.3f,.2f,.1f,.6f);if(uniforms.Contains("second"))uniforms["second"]=new SKColorF(.1f,.2f,.4f,.7f);
    using var gradient=SKShader.CreateLinearGradient(new SKPoint(0,0),new SKPoint(64,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);using var cf=SKColorFilter.CreateLighting(new SKColor(128,200,255),new SKColor(10,20,30));using var blend=SKBlender.CreateArithmetic(0,.3f,.7f,0,true);
    if(children.Contains("image"))children["image"]=gradient;if(children.Contains("filter"))children["filter"]=cf;if(children.Contains("blend"))children["blend"]=blend;
    using(var snapshot=uniforms.ToData()){values[$"shader-{scene}-bytes"]=Convert.ToHexString(snapshot.AsSpan());uniforms.Reset();Check(uniforms.Size==snapshot.Size);foreach(string name in effect.Uniforms)Check(uniforms.Contains(name));}
    // Reset was exercised without assuming native uninitialized bytes. Supply
    // every value again before building either renderer's real shader.
    if(uniforms.Contains("size"))uniforms["size"]=new SKPoint(64,48);if(uniforms.Contains("time"))uniforms["time"]=.7f;if(uniforms.Contains("shift"))uniforms["shift"]=new SKPoint(4,2);if(uniforms.Contains("edge"))uniforms["edge"]=int.MaxValue;if(uniforms.Contains("values"))uniforms["values"]=new[]{16777216,16777217,7};if(uniforms.Contains("gains"))uniforms["gains"]=new[]{.2f,.4f,.6f};if(uniforms.Contains("tint"))uniforms["tint"]=new SKColorF(.6f,.3f,.2f,.8f);if(uniforms.Contains("alpha"))uniforms["alpha"]=.6f;if(uniforms.Contains("matrix")){if(scene==13)uniforms["matrix"]=new float[]{1,.2f,.1f,1};else uniforms["matrix"]=new SKMatrix(1,.1f,2,.2f,1,3,0,0,1);}if(uniforms.Contains("first"))uniforms["first"]=new SKColorF(.3f,.2f,.1f,.6f);if(uniforms.Contains("second"))uniforms["second"]=new SKColorF(.1f,.2f,.4f,.7f);
    var matrix=transform==0?SKMatrix.Identity:SKMatrix.CreateScale(.85f,1.1f);using var shader=effect.ToShader(uniforms,children,matrix);Check(shader is not null);effect.Dispose();uniforms.Dispose();children.Dispose();gradient.Dispose();cf.Dispose();blend.Dispose();
    using var target=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(target);using var paint=new SKPaint{Shader=shader,Color=new SKColor(255,255,255,210)};shader!.Dispose();canvas.Clear(new SKColor(240,245,250));canvas.DrawRect(new SKRect(0,0,64,48),paint);Record("shader",scene,transform,target);
}
string[] filters=["half4 main(half4 color){return half4(color.bgr,color.a);}","uniform float gain;half4 main(half4 color){return half4(color.rgb*gain,color.a);}","uniform colorFilter child;half4 main(half4 color){return child.eval(color)*half4(.5,.75,1,1);}"];
for(int scene=0;scene<filters.Length;scene++)
{
    using var builder=SKRuntimeEffect.BuildColorFilter(filters[scene]);if(builder.Uniforms.Contains("gain"))builder.Uniforms["gain"]=.6f;using var child=SKColorFilter.CreateLighting(new SKColor(200,150,100),SKColors.Black);if(builder.Children.Contains("child"))builder.Children["child"]=child;using var filter=builder.Build();Check(filter is not null);builder.Dispose();child.Dispose();
    using var target=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(target);using var paint=new SKPaint{Color=new SKColor(40,100,200,173),ColorFilter=filter};filter!.Dispose();canvas.Clear(SKColors.White);canvas.DrawRect(new SKRect(4,3,60,45),paint);Record("filter",scene,0,target);
}
string[] blenders=["half4 main(half4 src,half4 dst){return src*.3+dst*.7;}","uniform float amount;half4 main(half4 src,half4 dst){return mix(dst,src,amount);}"];
for(int scene=0;scene<blenders.Length;scene++)
{
    using var builder=SKRuntimeEffect.BuildBlender(blenders[scene]);if(builder.Uniforms.Contains("amount"))builder.Uniforms["amount"]=.4f;using var blender=builder.Build();using var src=SKShader.CreateColor(new SKColor(180,40,80,180));using var dst=SKShader.CreateColor(new SKColor(20,110,200,170));using var shader=SKShader.CreateBlend(blender,dst,src);builder.Dispose();blender.Dispose();
    using var target=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(target);using var paint=new SKPaint{Shader=shader};canvas.Clear(SKColors.White);canvas.DrawPaint(paint);Record("blender",scene,0,target);
}
foreach(var source in new[]{"half4 main(float2 p){return missing;}","float main(float2 p){return 1;}","half4 main(float2 p){float x=1;}"}){using var e=SKRuntimeEffect.CreateShader(source,out var errors);Check(e is null&&!string.IsNullOrEmpty(errors));}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} runtime-effect images, {values.Count} metadata entries and {checks} lifetime/validation checks");
void Record(string kind,int scene,int transform,SKBitmap bitmap){int offset=(int)pixels.Position;var bytes=bitmap.GetPixelSpan();pixels.Write(bytes);cases.Add(new{kind,scene,transform,offset,length=bytes.Length});}
