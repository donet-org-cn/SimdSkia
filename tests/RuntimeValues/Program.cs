using SkiaSharp;
using System.Text.Json;
using System.Runtime.InteropServices;
var output=args[0];Directory.CreateDirectory(output);var rows=new Dictionary<string,object?>();int checks=0;
void Capture(string name,SKRuntimeEffectUniform uniform,int bytes=-1)
{
    if(bytes<0)bytes=uniform.Size;byte[] memory=Enumerable.Repeat((byte)0xa7,bytes+8).ToArray();string? failure=null;
    try{uniform.WriteTo(memory.AsSpan(4,bytes));}catch(Exception e){failure=e.GetType().FullName+":"+(e as ArgumentException)?.ParamName;}
    if(memory.AsSpan(0,4).IndexOfAnyExcept((byte)0xa7)>=0||memory.AsSpan(bytes+4,4).IndexOfAnyExcept((byte)0xa7)>=0)throw new Exception("Uniform writer changed guard bytes.");checks++;
    rows[name]=new{uniform.Size,uniform.IsEmpty,bytes,failure,data=Convert.ToHexString(memory)};
}
Capture("float",.125f);Capture("float-zero",0f);Capture("float-nan",float.NaN);Capture("float-positive-infinity",float.PositiveInfinity);Capture("float-mismatch",1f,8);Capture("float-short",1f,3);
Capture("int",42);Capture("int-negative",int.MinValue);Capture("int-mismatch",7,8);
Capture("float-array",new float[]{1,2.5f,-3.25f});Capture("float-array-empty",Array.Empty<float>());Capture("float-array-null",(float[]?)null!);Capture("float-array-mismatch",new float[]{1,2},4);
Span<float> f=stackalloc float[]{3,5,7,11};Capture("float-span",f);ReadOnlySpan<float> rf=f;Capture("float-readonly-span",rf);SKRuntimeEffectUniform retained=f;f[1]=17;Capture("float-span-live",retained);
Capture("int-array",new[]{3,-5,17,int.MaxValue});Capture("int-array-null",(int[]?)null!);Span<int> i=stackalloc int[]{4,7,13};Capture("int-span",i);ReadOnlySpan<int> ri=i;Capture("int-readonly-span",ri);
Capture("point",new SKPoint(1.25f,-9));Capture("size",new SKSize(15,27));Capture("point3",new SKPoint3(1,2,3));Capture("point-i",new SKPointI(4,-3));Capture("size-i",new SKSizeI(40,60));
Capture("color",new SKColor(30,80,170,123));Capture("color-f",new SKColorF(.125f,.25f,.75f,.5f));Capture("color-three",new SKColorF(.125f,.25f,.75f,.5f),12);Capture("color-invalid",new SKColorF(.125f,.25f,.75f,.5f),8);
Capture("matrix",new SKMatrix(1,2,3,4,5,6,7,8,9));Capture("matrix-array",new float[][]{[1,2,3],[4,5,6],[7,8,9]});Capture("empty-default",default(SKRuntimeEffectUniform),20);Capture("empty-named",SKRuntimeEffectUniform.Empty,0);
using var shader=SKShader.CreateColor(SKColors.Red);using var filter=SKColorFilter.CreateLighting(SKColors.White,SKColors.Black);using var blender=SKBlender.CreateBlendMode(SKBlendMode.Multiply);
var children=new SKRuntimeEffectChild[]{new(shader),new(filter),new(blender),shader,filter,blender,default,new((SKShader)null!)};
rows["children"]=children.Select(c=>new{type=c.Value is SKShader?nameof(SKShader):c.Value is SKColorFilter?nameof(SKColorFilter):c.Value is SKBlender?nameof(SKBlender):null,shader=ReferenceEquals(c.Shader,shader),filter=ReferenceEquals(c.ColorFilter,filter),blender=ReferenceEquals(c.Blender,blender)}).ToArray();
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks,privateImplementationTypes=children.Select(c=>c.Value?.GetType().FullName).ToArray()}));Console.WriteLine($"Captured {rows.Count} uniform/child cases, {checks} writer-guard checks");
