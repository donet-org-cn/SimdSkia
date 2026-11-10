using System.Numerics;
using System.Text.Json;
using SimdSkia;
using SimdSkia.Cpu;
var output=args[0];Directory.CreateDirectory(output);var random=new Random(1033194);int checks=0;
foreach(var format in new[]{PixelFormat.Rgba8888,PixelFormat.Bgra8888})foreach(var alpha in Enum.GetValues<AlphaFormat>())foreach(var mode in Enum.GetValues<CompositeMode>())for(int iteration=0;iteration<32;iteration++)
{
 int width=iteration+1;using var scalar=new PixelBuffer(width,3,format,alpha,width*4+7);using var simd=new PixelBuffer(width,3,format,alpha,width*4+7);random.NextBytes(scalar.GetPixelSpan());scalar.GetPixelSpan().CopyTo(simd.GetPixelSpan());var f=new Vector4((float)random.NextDouble()*1.5f-.25f,(float)random.NextDouble()*1.5f-.25f,(float)random.NextDouble()*1.5f-.25f,(float)random.NextDouble());var paint=new Paint(default,mode){FloatingColor=f};var command=DrawingCommand.FillRectangle(new(0,0,width,3),Matrix3.Identity,paint);
 CpuOptions.EnableSimd=false;using(var c=new RasterCanvas(scalar))c.Execute(command);CpuOptions.EnableSimd=true;using(var c=new RasterCanvas(simd))c.Execute(command);
 var a=scalar.GetPixelSpan();var b=simd.GetPixelSpan();if(!a.SequenceEqual(b)){int at=0;while(a[at]==b[at])at++;throw new Exception($"Float SIMD mismatch {format}/{alpha}/{mode}/{width}, byte {at}: {a[at]}/{b[at]}");}checks++;
}
Vector4[] samples=[new(.1f,.2f,.3f,.4f),new(1.25f,-.25f,.5f,.125f),new(1,1,1,1),Vector4.Zero,new(float.NaN,.5f,float.PositiveInfinity,1),new(float.NegativeInfinity,.1f,.2f,float.NaN)];
foreach(var format in new[]{PixelFormat.Rgba8888,PixelFormat.Bgra8888})foreach(var alpha in new[]{AlphaFormat.Premultiplied,AlphaFormat.Opaque})
foreach(int width in new[]{1,2,3,7,8,9,127,128,129,257})foreach(byte opacity in new byte[]{0,1,137,255})foreach(bool path in new[]{false,true})
{
    using var scalar=new PixelBuffer(width,9,format,alpha,width*4+7);using var simd=new PixelBuffer(width,9,format,alpha,width*4+7);
    random.NextBytes(scalar.GetPixelSpan());scalar.GetPixelSpan().CopyTo(simd.GetPixelSpan());
    var paint=new Paint(new(255,255,255,opacity),CompositeMode.SourceOver,true,new VaryingShader(samples));
    var command=path?new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,paint){Path=new PathGeometry([new(PathVerb.Move,new(-1,.25f)),new(PathVerb.Line,new(width+1,1.25f)),new(PathVerb.Line,new(width-1,8.75f)),new(PathVerb.Line,new(0,7.25f)),new(PathVerb.Close,default)])}:
        DrawingCommand.FillRectangle(new(-1,-1,width+1,10),new(1,0,.125f,0,1,-.25f,0,0,1),paint);
    CpuOptions.EnableSimd=false;using(var canvas=new RasterCanvas(scalar))canvas.Execute(command);
    CpuOptions.EnableSimd=true;using(var canvas=new RasterCanvas(simd))canvas.Execute(command);
    if(!scalar.GetPixelSpan().SequenceEqual(simd.GetPixelSpan()))throw new Exception($"Varying shader SIMD mismatch {format}/{alpha}/{width}/{opacity}/{path}");
    checks++;
}
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{checks,seed=1033194,avx2=System.Runtime.Intrinsics.X86.Avx2.IsSupported}));Console.WriteLine($"PASS {checks} exact scalar/SIMD float-paint and varying-shader buffers, all blends/formats/alpha/tails/stride");

sealed class VaryingShader(Vector4[] samples):IPremultipliedColorShader
{
    public Vector4 SamplePremultiplied(float x,float y)=>samples[(int)Math.Abs(MathF.Floor(x)+MathF.Floor(y)*13)%samples.Length];
    public Rgba32 Sample(float x,float y)=>throw new InvalidOperationException("PMA sampling is required.");
}
