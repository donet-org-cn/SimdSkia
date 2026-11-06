using SkiaSharp;
using System.Text.Json;

string output=args[0];Directory.CreateDirectory(output);var data=new SortedDictionary<string,object?>();
void Probe(string key,Func<SKShader?> create){try{using var shader=create();data[key]=shader is null?"null":"shader";}catch(Exception e){data[key]=new{exception=e.GetType().Name,parameter=(e as ArgumentException)?.ParamName};}}
SKColor[] palette=[new(237,30,60),new(15,210,120),new(60,80,220)];
SKColor[] alpha=[new(230,12,40,20),new(35,200,140,180),new(60,90,210,240)];
var start=new SKPoint(24,32);var end=new SKPoint(100,80);
Probe("null-colors",()=>SKShader.CreateLinearGradient(start,end,null!,SKShaderTileMode.Clamp));
Probe("length-mismatch",()=>SKShader.CreateLinearGradient(start,end,palette,[0,1],SKShaderTileMode.Clamp));
foreach(int count in new[]{0,1,2})Probe("count-"+count,()=>SKShader.CreateLinearGradient(start,end,palette[..count],SKShaderTileMode.Clamp));
Probe("singular",()=>SKShader.CreateLinearGradient(start,end,palette,null,SKShaderTileMode.Clamp,SKMatrix.CreateScale(0,0)));
foreach(float radius in new[]{-1f,0,1})Probe("radius-"+radius,()=>SKShader.CreateRadialGradient(start,radius,palette,SKShaderTileMode.Clamp));
Probe("sweep-reversed",()=>SKShader.CreateSweepGradient(start,palette,SKShaderTileMode.Clamp,180,90));
Probe("sweep-equal",()=>SKShader.CreateSweepGradient(start,palette,SKShaderTileMode.Clamp,90,90));
Probe("null-local",()=>SKShader.CreateLocalMatrix(null!,SKMatrix.Identity));
Probe("nonfinite-point",()=>SKShader.CreateLinearGradient(new(float.NaN,0),end,palette,SKShaderTileMode.Clamp));
Probe("empty-bitmap-erase",()=>{using var b=new SKBitmap();b.Erase(SKColors.Red);b.Erase(SKColors.Red,new(1,1,4,4));return SKShader.CreateEmpty();});
void Scene(string key,Func<SKShader?> create,bool retain=false,SKMatrix? canvasMatrix=null)
{
    using var bitmap=new SKBitmap(128,128,SKColorType.Bgra8888,SKAlphaType.Premul);using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(40,45,50));
    using var shader=create();using var paint=new SKPaint{Shader=shader,Color=new SKColor(255,255,255,211)};
    if(retain)shader?.Dispose();if(canvasMatrix is {} m)canvas.SetMatrix(m);
    canvas.DrawRect(new(-64,-64,256,256),paint);File.WriteAllBytes(Path.Combine(output,key+".bgra"),bitmap.Bytes);
}
foreach(var mode in Enum.GetValues<SKShaderTileMode>())
{
    Scene("linear-"+mode,()=>SKShader.CreateLinearGradient(start,end,palette,[0,.3f,1],mode));
    Scene("alpha-"+mode,()=>SKShader.CreateLinearGradient(start,end,alpha,[0,.3f,1],mode));
    Scene("radial-"+mode,()=>SKShader.CreateRadialGradient(new(61,67),43,palette,[0,.4f,1],mode));
    Scene("sweep-"+mode,()=>SKShader.CreateSweepGradient(new(63,64),palette,[0,.2f,1],mode,45,240));
}
Scene("linear-uniform",()=>SKShader.CreateLinearGradient(start,end,palette,SKShaderTileMode.Clamp));
Scene("linear-hard-stops",()=>SKShader.CreateLinearGradient(new(20,0),new(100,0),new[]{palette[0],palette[1],palette[2],SKColors.White},[0,.5f,.5f,1],SKShaderTileMode.Clamp));
Scene("linear-partial-stops",()=>SKShader.CreateLinearGradient(start,end,palette,[.2f,.4f,.7f],SKShaderTileMode.Clamp));
Scene("linear-clamped-stops",()=>SKShader.CreateLinearGradient(start,end,palette,[-.2f,1.5f,.7f],SKShaderTileMode.Clamp));
Scene("one-color",()=>SKShader.CreateLinearGradient(start,end,[palette[0]],SKShaderTileMode.Decal));
Scene("local-matrix",()=>SKShader.CreateLinearGradient(start,end,palette,null,SKShaderTileMode.Mirror,SKMatrix.CreateScale(1.4f,.8f)));
Scene("local-nested",()=>{using var s=SKShader.CreateLinearGradient(start,end,palette,SKShaderTileMode.Repeat);using var s2=s.WithLocalMatrix(SKMatrix.CreateScale(1.5f,.7f));return s2.WithLocalMatrix(SKMatrix.CreateTranslation(12,9));});
Scene("local-canvas",()=>{using var s=SKShader.CreateLinearGradient(start,end,palette,SKShaderTileMode.Repeat);return SKShader.CreateLocalMatrix(s,SKMatrix.CreateTranslation(12,9));},canvasMatrix:SKMatrix.CreateScale(1.3f,.7f));
Scene("radial-alpha",()=>SKShader.CreateRadialGradient(new(60,68),52,alpha,SKShaderTileMode.Clamp));
Scene("radial-local",()=>SKShader.CreateRadialGradient(new(60,68),52,palette,null,SKShaderTileMode.Clamp,SKMatrix.CreateScale(1.3f,.7f)));
Scene("sweep-full",()=>SKShader.CreateSweepGradient(new(64,64),palette));
Scene("sweep-negative-start",()=>SKShader.CreateSweepGradient(new(64,64),palette,SKShaderTileMode.Clamp,-90,180));
Scene("sweep-local",()=>SKShader.CreateSweepGradient(new(64,64),palette,null,SKMatrix.CreateRotationDegrees(32,64,64)));
Scene("solid-retained",()=>SKShader.CreateColor(new(70,140,210,120)),true);
Scene("empty-shader",SKShader.CreateEmpty);
// Caller arrays and shader wrappers can be changed/disposed after a paint retains its immutable payload.
Scene("retained-multi",()=>{SKColor[] c=[..palette];float[] positions=[0,.3f,1];var s=SKShader.CreateLinearGradient(start,end,c,positions,SKShaderTileMode.Clamp);c[0]=SKColors.Black;positions[1]=.9f;return s;},true);
foreach(var mode in Enum.GetValues<SKShaderTileMode>())
{
    Scene("degenerate-linear-"+mode,()=>SKShader.CreateLinearGradient(start,start,palette,mode));
    Scene("degenerate-radial-"+mode,()=>SKShader.CreateRadialGradient(start,0,palette,mode));
    Scene("degenerate-alpha-"+mode,()=>SKShader.CreateLinearGradient(start,start,alpha,[.2f,.4f,.7f],mode));
    Scene("degenerate-sweep-"+mode,()=>SKShader.CreateSweepGradient(new(64,64),palette,mode,90,90));
}
Scene("local-perspective",()=>SKShader.CreateLinearGradient(start,end,palette,null,SKShaderTileMode.Clamp,new(1,0,0,0,1,0,.002f,.001f,1)));
Scene("two-local",()=>{using var s=SKShader.CreateLinearGradient(start,end,[palette[0],palette[1]],SKShaderTileMode.Clamp);return s.WithLocalMatrix(SKMatrix.CreateTranslation(5,9));});
foreach(var type in new[]{SKColorType.Bgra8888,SKColorType.Rgba8888})foreach(var a in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul,SKAlphaType.Opaque})foreach(bool subset in new[]{false,true})
{
    using var b=new SKBitmap(new SKImageInfo(13,9,type,a),64);b.GetPixelSpan().Fill(0xa7);
    if(subset)b.Erase(new SKColor(33,81,174,127),new(-3,2,9,20));else b.Erase(new SKColor(33,81,174,127));
    File.WriteAllBytes(Path.Combine(output,$"erase-{type}-{a}-{subset}.bytes"),b.Bytes);
}
using(var b=new SKBitmap(8,8))
{
    b.Erase(SKColors.Red);byte[] before=b.Bytes;
    b.Erase(SKColors.Blue,new(6,6,2,2));data["erase-reversed"]=before.SequenceEqual(b.Bytes);
    b.Erase(SKColors.Blue,new(-20,-20,-1,-1));data["erase-outside"]=before.SequenceEqual(b.Bytes);
}
foreach(var type in new[]{SKColorType.Bgra8888,SKColorType.Rgba8888})foreach(var a in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul,SKAlphaType.Opaque})foreach(byte value in new byte[]{0,1,255})
{
    using var b=new SKBitmap(new SKImageInfo(13,9,type,a),64);b.GetPixelSpan().Fill(0xa7);b.Erase(new SKColor(74,139,218,value),new(1,2,12,8));
    File.WriteAllBytes(Path.Combine(output,$"erase-alpha-{type}-{a}-{value}.bytes"),b.Bytes);
}
#if MANAGED
// SIMD and scalar paths must agree over the complete strided storage, including padding.
int scalarChecks=0;
foreach(var type in new[]{SKColorType.Bgra8888,SKColorType.Rgba8888})foreach(var a in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul,SKAlphaType.Opaque})
{
    var random=new Random(14711);
    using var simd=new SKBitmap(new SKImageInfo(31,17,type,a),160);using var scalar=new SKBitmap(simd.Info,160);
    for(int i=0;i<80;i++)
    {
        simd.GetPixelSpan().Fill(0xa7);scalar.GetPixelSpan().Fill(0xa7);
        var color=new SKColor((uint)random.NextInt64(0,1L<<32));var rect=new SKRectI(random.Next(-10,40),random.Next(-10,25),random.Next(-10,40),random.Next(-10,25));
        SimdSkia.Cpu.CpuOptions.EnableSimd=true;simd.Erase(color,rect);
        SimdSkia.Cpu.CpuOptions.EnableSimd=false;scalar.Erase(color,rect);
        if(!simd.GetPixelSpan().SequenceEqual(scalar.GetPixelSpan()))throw new Exception("Scalar/SIMD erase mismatch");scalarChecks++;
    }
}
SimdSkia.Cpu.CpuOptions.EnableSimd=true;
File.WriteAllText(Path.Combine(output,"managed-checks.json"),JsonSerializer.Serialize(new{scalarChecks}));
#endif
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(output);
