using SimdSkia;
using System.Numerics;
namespace SkiaSharp;
public class SKRuntimeEffect : SKObject
{
    private RuntimeEffectProgram? program;
    private readonly IReadOnlyList<string> uniforms,children;
    private SKRuntimeEffect(RuntimeEffectProgram program){this.program=program;uniforms=Array.AsReadOnly(program.Uniforms.Select(v=>v.Name).ToArray());children=Array.AsReadOnly(program.Children.Select(v=>v.Name).ToArray());}
    internal RuntimeEffectProgram Capture(){ThrowIfDisposed();return program!;}
    public int UniformSize=>Capture().UniformSize;
    public IReadOnlyList<string> Uniforms {get{_=Capture();return uniforms;}}
    public IReadOnlyList<string> Children {get{_=Capture();return children;}}
    public static SKRuntimeEffect CreateShader(string sksl,out string errors)=>Create(sksl,RuntimeEffectKind.Shader,out errors);
    public static SKRuntimeEffect CreateColorFilter(string sksl,out string errors)=>Create(sksl,RuntimeEffectKind.ColorFilter,out errors);
    public static SKRuntimeEffect CreateBlender(string sksl,out string errors)=>Create(sksl,RuntimeEffectKind.Blender,out errors);
    private static SKRuntimeEffect Create(string source,RuntimeEffectKind kind,out string errors)
    {
        ArgumentNullException.ThrowIfNull(source);try{var result=new SKRuntimeEffect(RuntimeEffectProgram.Compile(source,kind));errors=null!;return result;}
        catch(Exception e)when(e is FormatException or NotSupportedException){errors=e.Message;return null!;}
    }
    public static SKRuntimeShaderBuilder BuildShader(string sksl)=>new(Require(CreateShader(sksl,out var errors),errors));
    public static SKRuntimeColorFilterBuilder BuildColorFilter(string sksl)=>new(Require(CreateColorFilter(sksl,out var errors),errors));
    public static SKRuntimeBlenderBuilder BuildBlender(string sksl)=>new(Require(CreateBlender(sksl,out var errors),errors));
    private static SKRuntimeEffect Require(SKRuntimeEffect? effect,string? errors)=>effect??throw new SKRuntimeEffectBuilderException("Failed to compile the runtime effect. There was an error: "+errors);
    public SKShader ToShader()=>Shader(null,null,SKMatrix.Identity);
    public SKShader ToShader(SKRuntimeEffectUniforms uniforms)=>Shader(uniforms,null,SKMatrix.Identity);
    public SKShader ToShader(SKRuntimeEffectUniforms uniforms,SKRuntimeEffectChildren children)=>Shader(uniforms,children,SKMatrix.Identity);
    public SKShader ToShader(SKRuntimeEffectUniforms uniforms,SKRuntimeEffectChildren children,SKMatrix localMatrix)=>Shader(uniforms,children,localMatrix);
    private SKShader Shader(SKRuntimeEffectUniforms? values,SKRuntimeEffectChildren? inputs,SKMatrix matrix)
    {
        var p=Capture();if(p.Kind!=RuntimeEffectKind.Shader||!matrix.ToCore().TryInvert(out var inverse))return null!;
        ReadOnlySpan<byte> bytes=values is null?ReadOnlySpan<byte>.Empty:values.Storage;if(bytes.Length!=p.UniformSize)return null!;
        var captured=CaptureChildren(p,inputs);try{return SKShader.Runtime(new RuntimeColorShader(p.Bind(bytes,captured.Children),inverse),captured.Resources);}catch{captured.Resources?.Dispose();throw;}
    }
    public SKColorFilter ToColorFilter()=>Filter(null,null);
    public SKColorFilter ToColorFilter(SKRuntimeEffectUniforms uniforms)=>Filter(uniforms,null);
    public SKColorFilter ToColorFilter(SKRuntimeEffectUniforms uniforms,SKRuntimeEffectChildren children)=>Filter(uniforms,children);
    private SKColorFilter Filter(SKRuntimeEffectUniforms? values,SKRuntimeEffectChildren? inputs)
    {
        var p=Capture();if(p.Kind!=RuntimeEffectKind.ColorFilter)return null!;ReadOnlySpan<byte> bytes=values is null?ReadOnlySpan<byte>.Empty:values.Storage;if(bytes.Length!=p.UniformSize)return null!;
        var children=CaptureChildren(p,inputs);if(children.Resources is not null){children.Resources.Dispose();throw new NotSupportedException("Color-filter runtime graphs cannot own shader resources in this implementation.");}
        var bound=p.Bind(bytes,children.Children);return SKColorFilter.Runtime(c=>bound.Evaluate(c));
    }
    public SKBlender ToBlender()=>Blender(null,null);
    public SKBlender ToBlender(SKRuntimeEffectUniforms uniforms)=>Blender(uniforms,null);
    public SKBlender ToBlender(SKRuntimeEffectUniforms uniforms,SKRuntimeEffectChildren children)=>Blender(uniforms,children);
    private SKBlender Blender(SKRuntimeEffectUniforms? values,SKRuntimeEffectChildren? inputs)
    {
        var p=Capture();if(p.Kind!=RuntimeEffectKind.Blender)return null!;ReadOnlySpan<byte> bytes=values is null?ReadOnlySpan<byte>.Empty:values.Storage;if(bytes.Length!=p.UniformSize)return null!;
        var children=CaptureChildren(p,inputs);if(children.Resources is not null){children.Resources.Dispose();throw new NotSupportedException("Blender runtime graphs cannot own shader resources in this implementation.");}
        var bound=p.Bind(bytes,children.Children);return SKBlender.Runtime((s,d)=>bound.Evaluate(s,d));
    }
    private static (IRuntimeEffectChild?[] Children,ShaderResources? Resources) CaptureChildren(RuntimeEffectProgram program,SKRuntimeEffectChildren? inputs)
    {
        var values=inputs?.ToArray()??[];if(values.Length!=program.Children.Count)throw new ArgumentException("Runtime child count does not match the effect.",nameof(inputs));var children=new IRuntimeEffectChild?[values.Length];var owners=new List<IDisposable>();
        try
        {
            for(int i=0;i<values.Length;i++)
            {
                if(values[i] is null)throw new NotSupportedException("Implicit paint-color runtime children are not implemented; supply each declared child.");values[i].ThrowIfDisposed();
                switch(values[i])
                {
                    case SKShader shader:
                        var source=shader.ToCore();if(shader.RetainResources() is {} owner)owners.Add(owner);
                        children[i]=new Child(RuntimeEffectKind.Shader,(p,_)=>{if(source is IPremultipliedColorShader pm)return pm.SamplePremultiplied(p.X,p.Y);var c=source.Sample(p.X,p.Y);float a=c.A/255f;return new(c.R/255f*a,c.G/255f*a,c.B/255f*a,a);});break;
                    case SKColorFilter filter:var operation=filter.Capture();children[i]=new Child(RuntimeEffectKind.ColorFilter,(c,_)=>operation(c));break;
                    case SKBlender blender:children[i]=new Child(RuntimeEffectKind.Blender,blender.Capture());break;
                    default:throw new NotSupportedException("Unknown runtime child kind.");
                }
            }
            return(children,owners.Count==0?null:new ShaderResources(owners.ToArray()));
        }
        catch{foreach(var owner in owners)owner.Dispose();throw;}
    }
    private sealed record Child(RuntimeEffectKind Kind,Func<Vector4,Vector4,Vector4> Operation):IRuntimeEffectChild
    {public Vector4 Evaluate(Vector4 first,Vector4 second)=>Operation(first,second);}
    protected override void Dispose(bool disposing)=>program=null;
}

public class SKRuntimeEffectUniforms : IEnumerable<string>,IDisposable
{
    private readonly string[] names;
    private readonly IReadOnlyList<string> view;
    private readonly Dictionary<string,RuntimeUniform> variables;
    private SKData data;
    private bool disposed;
    public SKRuntimeEffectUniforms(SKRuntimeEffect effect)
    {ArgumentNullException.ThrowIfNull(effect);var p=effect.Capture();names=p.Uniforms.Select(v=>v.Name).ToArray();view=Array.AsReadOnly(names);variables=p.Uniforms.ToDictionary(v=>v.Name,StringComparer.Ordinal);data=SKData.Create(p.UniformSize);}
    private void Check()=>ObjectDisposedException.ThrowIf(disposed,this);
    internal ReadOnlySpan<byte> Storage {get{Check();return data.AsSpan();}}
    public IReadOnlyList<string> Names=>view;
    public int Count=>names.Length;
    public int Size {get{Check();return(int)data.Size;}}
    public bool Contains(string name)=>Array.IndexOf(names,name)>=0;
    public void Reset(){Check();var next=SKData.Create(Size);data.Dispose();data=next;}
    public SKRuntimeEffectUniform this[string name] {set=>Add(name,value);}
    public void Add(string name,SKRuntimeEffectUniform value)
    {
        Check();if(!variables.TryGetValue(name,out var variable))throw new ArgumentOutOfRangeException(name,$"Variable was not found for name: '{name}'.");
        bool integer=variable.Type.Scalar==RuntimeScalarKind.Int;int count=variable.Type.Components;
        bool valid=value.Type switch
        {
            SKRuntimeEffectUniform.DataType.Float=>!integer&&count==1&&!variable.IsArray,
            SKRuntimeEffectUniform.DataType.FloatArray=>!integer&&(count>1||variable.IsArray),
            SKRuntimeEffectUniform.DataType.Int32=>integer&&count==1&&!variable.IsArray,
            SKRuntimeEffectUniform.DataType.Int32Array=>integer&&(count>1||variable.IsArray),
            SKRuntimeEffectUniform.DataType.Color=>!integer&&variable.Type.Columns==1&&variable.Type.Rows is 3 or 4,_=>false
        };
        if(!valid)throw new ArgumentOutOfRangeException(nameof(value),"Uniform value type does not match its declaration.");value.WriteTo(data.Span.Slice(variable.Offset,variable.Size));
    }
    public SKData ToData(){Check();return data.Size==0?SKData.Empty:SKData.CreateCopy(data.AsSpan());}
    public IEnumerator<string> GetEnumerator()=>((IEnumerable<string>)names).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    public void Dispose(){if(disposed)return;disposed=true;data.Dispose();}
}
public class SKRuntimeEffectChildren : IEnumerable<string>,IDisposable
{
    private readonly string[] names;
    private readonly IReadOnlyList<string> view;
    private readonly SKObject[] children;
    private bool disposed;
    public SKRuntimeEffectChildren(SKRuntimeEffect effect){ArgumentNullException.ThrowIfNull(effect);names=effect.Children.ToArray();view=Array.AsReadOnly(names);children=new SKObject[names.Length];}
    private void Check()=>ObjectDisposedException.ThrowIf(disposed,this);
    public IReadOnlyList<string> Names=>view;
    public int Count=>names.Length;
    public bool Contains(string name)=>Array.IndexOf(names,name)>=0;
    public SKRuntimeEffectChild? this[string name] {set=>Add(name,value);}
    public void Reset(){Check();Array.Clear(children);}
    public void Add(string name,SKRuntimeEffectChild? value){Check();int index=Array.IndexOf(names,name);if(index<0)throw new ArgumentOutOfRangeException(name,$"Variable was not found for name: '{name}'.");children[index]=value?.Value!;}
    public SKObject[] ToArray(){Check();return(SKObject[])children.Clone();}
    public IEnumerator<string> GetEnumerator()=>((IEnumerable<string>)names).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    public void Dispose(){if(disposed)return;disposed=true;Array.Clear(children);}
}
public class SKRuntimeEffectBuilderException(string message):ApplicationException(message);
public class SKRuntimeEffectBuilder : IDisposable
{
    public SKRuntimeEffectBuilder(SKRuntimeEffect effect){Effect=effect??throw new ArgumentNullException(nameof(effect));Uniforms=new(effect);Children=new(effect);}
    public SKRuntimeEffect Effect {get;}
    public SKRuntimeEffectUniforms Uniforms {get;}
    public SKRuntimeEffectChildren Children {get;}
    public void Dispose(){Uniforms.Dispose();Children.Dispose();Effect.Dispose();}
}
public class SKRuntimeShaderBuilder(SKRuntimeEffect effect):SKRuntimeEffectBuilder(effect)
{public SKShader Build()=>Effect.ToShader(Uniforms,Children);public SKShader Build(SKMatrix localMatrix)=>Effect.ToShader(Uniforms,Children,localMatrix);}
public class SKRuntimeColorFilterBuilder(SKRuntimeEffect effect):SKRuntimeEffectBuilder(effect)
{public SKColorFilter Build()=>Effect.ToColorFilter(Uniforms,Children);}
public class SKRuntimeBlenderBuilder(SKRuntimeEffect effect):SKRuntimeEffectBuilder(effect)
{public SKBlender Build()=>Effect.ToBlender(Uniforms,Children);}
