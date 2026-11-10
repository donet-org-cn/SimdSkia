using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimdSkia;
using SimdSkia.Cpu;

var output=args[0];Directory.CreateDirectory(output);
int checks=0;var random=new Random(119403);
string[] expressions=[
    "return half4(p/size,.5,1);",
    "float2 q=p/size;return half4(q.yx*gain,.5,1);",
    "float4 c=half4(p/size,.5,1);c.rgb=c.bgr;c.r+=gain;return c;",
    "float3 q=normalize(float3(p/size,gain));return half4(q*dot(q,float3(.2,.4,.5)),1);",
    "float d=distance(p,size);return half4(sqrt(abs(d)),sin(d),cos(d),1);",
    "float2 q=min(p/size,float2(gain));return half4(max(q,float2(-.5)),floor(gain),1);",
    "return half4(clamp(p/size,float2(0),float2(1)),gain,1);",
    "if(p.x>p.y){return half4(p/size,.2,1);}return half4(gain);",
    "return half4(p.x>p.y?gain:.25,p/size,1);"
];
RuntimeEffectProgram.BoundRuntimeEffect Bind(RuntimeEffectProgram program,byte[] bytes,bool interpreter)
{
    AppContext.SetSwitch("SimdSkia.DisableRuntimeNumericProgram",interpreter);
    return program.Bind(bytes);
}
void Equal(Vector4 expected,Vector4 actual)
{
    for(int i=0;i<4;i++)
        if(BitConverter.SingleToInt32Bits(expected[i])!=BitConverter.SingleToInt32Bits(actual[i])&&!(float.IsNaN(expected[i])&&float.IsNaN(actual[i])))
            throw new Exception($"Numeric result changed {expected}/{actual} component {i}");
    checks++;
}
foreach(string expression in expressions)
{
    var program=RuntimeEffectProgram.Compile("uniform float2 size;uniform float gain;half4 main(float2 p){"+expression+"}",RuntimeEffectKind.Shader);
    for(int iteration=0;iteration<40;iteration++)
    {
        float[] values=[iteration%9==0?0:random.NextSingle()*300-150,random.NextSingle()*300-150,iteration%11==0?float.NaN:random.NextSingle()*4-2];
        byte[] data=new byte[12];for(int i=0;i<3;i++)BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i*4),values[i]);
        var original=Bind(program,data,true);var optimized=Bind(program,data,false);
        for(int sample=0;sample<100;sample++)
        {var point=new Vector4(random.NextSingle()*300-100,random.NextSingle()*200-100,0,0);Equal(original.Evaluate(point),optimized.Evaluate(point));}
        foreach(int width in new[]{1,7,8,9,31,128,129})foreach(bool simd in new[]{false,true})
        {
            CpuOptions.EnableSimd=simd;
            var format=iteration%2==0?PixelFormat.Rgba8888:PixelFormat.Bgra8888;
            using var expected=new PixelBuffer(width,3,format,AlphaFormat.Premultiplied,width*4+7);
            using var actual=new PixelBuffer(width,3,format,AlphaFormat.Premultiplied,width*4+7);
            random.NextBytes(expected.GetPixelSpan());expected.GetPixelSpan().CopyTo(actual.GetPixelSpan());
            var matrix=iteration%3==0?new Matrix3(.75f,.125f,2,-.25f,1.25f,3,.003f,-.002f,1):new Matrix3(.75f,.125f,2,-.25f,1.25f,3,0,0,1);
            var command=DrawingCommand.FillRectangle(new(-100,-100,width+100,100),Matrix3.Identity,new(new(255,255,255,137),CompositeMode.SourceOver,false,new RuntimeColorShader(original,matrix)));
            using(var canvas=new RasterCanvas(expected))canvas.Execute(command);
            using(var canvas=new RasterCanvas(actual))canvas.Execute(command with{Paint=command.Paint with{Shader=new RuntimeColorShader(optimized,matrix)}});
            if(!expected.GetPixelSpan().SequenceEqual(actual.GetPixelSpan()))throw new Exception($"Numeric span mismatch {expression}/{iteration}/{width}/{simd}");
            checks++;
        }
    }
}
foreach(var kind in new[]{RuntimeEffectKind.ColorFilter,RuntimeEffectKind.Blender})
{
    var program=RuntimeEffectProgram.Compile(kind==RuntimeEffectKind.ColorFilter?
        "half4 main(half4 c){return half4(c.bgr*.75,c.a);}":
        "half4 main(half4 s,half4 d){return s*.25+d*.75;}",kind);
    var original=Bind(program,[],true);var optimized=Bind(program,[],false);
    for(int i=0;i<1000;i++)
    {var a=new Vector4(random.NextSingle(),random.NextSingle(),random.NextSingle(),random.NextSingle());var b=new Vector4(random.NextSingle());Equal(original.Evaluate(a,b),optimized.Evaluate(a,b));}
}
AppContext.SetSwitch("SimdSkia.DisableRuntimeNumericProgram",false);CpuOptions.EnableSimd=true;
for(int iteration=0;iteration<200000;iteration++)
{
    float value=BitConverter.Int32BitsToSingle((int)random.NextInt64(int.MinValue,(long)int.MaxValue+1));
    float expected=MathF.Floor(MathF.BitDecrement(value)),actual=ImageColorShader.Nearest(value);
    Equal(new(expected),new(actual));
}
float[] special=[0,-0f,float.NaN,float.PositiveInfinity,float.NegativeInfinity,float.Epsilon,-float.Epsilon,float.MaxValue,-float.MaxValue];
foreach(var format in Enum.GetValues<PixelFormat>())foreach(var alpha in Enum.GetValues<AlphaFormat>())
foreach(int tileX in new[]{0,1,2,3})foreach(int tileY in new[]{0,1,2,3})
{
    using var image=new PixelBuffer(7,5,format,alpha,format==PixelFormat.Alpha8?11:33);random.NextBytes(image.GetPixelSpan());
    var shader=new ImageColorShader(image,tileX,tileY,ImageSampling.Nearest);
    var samples=new Vector4[129];
    for(int i=0;i<samples.Length;i++)samples[i]=new(i%11==0?special[i%special.Length]:i%7-.5f,i%13==0?special[i%special.Length]:i%5,0,0);
    var expected=samples.Select(p=>shader.SamplePremultiplied(p.X,p.Y)).ToArray();
    ((IPremultipliedBatchShader)shader).SampleBatch(samples);
    for(int i=0;i<samples.Length;i++)Equal(expected[i],samples[i]);
}
var boundProgram=RuntimeEffectProgram.Compile("uniform float gain;half4 main(float2 p){return half4(p*gain,0,1);}",RuntimeEffectKind.Shader);
byte[] uniformBytes=new byte[4];BinaryPrimitives.WriteSingleLittleEndian(uniformBytes,3);
var retainedOriginal=Bind(boundProgram,uniformBytes,true);var retainedNumeric=Bind(boundProgram,uniformBytes,false);
BinaryPrimitives.WriteSingleLittleEndian(uniformBytes,4);var rebound=Bind(boundProgram,uniformBytes,false);
Equal(new(6,9,0,1),retainedNumeric.Evaluate(new(2,3,0,0)));Equal(new(8,12,0,1),rebound.Evaluate(new(2,3,0,0)));
long bindStart=GC.GetAllocatedBytesForCurrentThread();
for(int i=0;i<1000;i++)
{BinaryPrimitives.WriteSingleLittleEndian(uniformBytes,i);var changing=boundProgram.Bind(uniformBytes);Equal(new(i*2,i*3,0,1),changing.Evaluate(new(2,3,0,0)));}
long bindBytes=GC.GetAllocatedBytesForCurrentThread()-bindStart;
if(bindBytes>512000)throw new Exception($"Repeated uniform binding rebuilt numeric instructions ({bindBytes} bytes).");checks++;
var concurrent=Enumerable.Range(0,8).Select(worker=>Task.Run(()=>Enumerable.Range(0,1000).Select(i=>retainedNumeric.Evaluate(new(i*.25f,worker*.125f,0,0))).ToArray())).ToArray();
var batches=await Task.WhenAll(concurrent);
for(int worker=0;worker<batches.Length;worker++)for(int i=0;i<batches[worker].Length;i++)Equal(retainedOriginal.Evaluate(new(i*.25f,worker*.125f,0,0)),batches[worker][i]);
var large=RuntimeEffectProgram.Compile("half4 main(float2 p){float x=p.x;"+string.Concat(Enumerable.Repeat("x+=.001;",300))+"return half4(x,.2,.3,1);}",RuntimeEffectKind.Shader);
var largeOriginal=Bind(large,[],true);var largeFallback=Bind(large,[],false);
for(int i=0;i<100;i++)Equal(largeOriginal.Evaluate(new(i*.25f)),largeFallback.Evaluate(new(i*.25f)));
AppContext.SetSwitch("SimdSkia.DisableRuntimeNumericProgram",false);
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new NumericTestResult(checks,bindBytes/1000d,!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported),NumericTestJson.Default.NumericTestResult));
Console.WriteLine($"Passed {checks} numeric interpreter/batch/stride/typed fallback checks.");

internal sealed record NumericTestResult(int checks,double allocatedBytesPerBind,bool nativeAot);
[JsonSerializable(typeof(NumericTestResult))]
internal partial class NumericTestJson:JsonSerializerContext;
