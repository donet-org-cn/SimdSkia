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
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{checks,seed=1033194,avx2=System.Runtime.Intrinsics.X86.Avx2.IsSupported}));Console.WriteLine($"PASS {checks} exact scalar/SIMD float-paint buffers, all blends/formats/alpha/tails/stride");
