using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
namespace SimdSkia;

public enum RuntimeEffectKind { Shader,ColorFilter,Blender }
public enum RuntimeScalarKind { Float,Int,Bool }
public readonly record struct RuntimeType(RuntimeScalarKind Scalar,int Rows=1,int Columns=1)
{
    public int Components=>Rows*Columns;
    public bool IsScalar=>Rows==1&&Columns==1;
    public bool IsMatrix=>Columns>1;
}
public sealed record RuntimeUniform(string Name,int Offset,RuntimeType Type,int Count,bool IsArray,bool IsColor)
{public int Size=>Type.Components*Count*4;}
public sealed record RuntimeChildDeclaration(string Name,RuntimeEffectKind Kind);
public interface IRuntimeEffectChild
{
    RuntimeEffectKind Kind {get;}
    Vector4 Evaluate(Vector4 first,Vector4 second);
}

/// <summary>Managed, typed, bounded SkSL interpreter. Compilation never generates machine code or requires dynamic code.</summary>
public sealed partial class RuntimeEffectProgram
{
    private readonly Statement body;
    private readonly int slots;
    private readonly int[] parameters;
    private readonly bool coordinateDependent;
    private readonly IReadOnlyList<RuntimeUniform> uniforms;
    private readonly IReadOnlyList<RuntimeChildDeclaration> children;
    public RuntimeEffectKind Kind {get;}
    public int UniformSize {get;}
    public IReadOnlyList<RuntimeUniform> Uniforms=>uniforms;
    public IReadOnlyList<RuntimeChildDeclaration> Children=>children;
    private RuntimeEffectProgram(RuntimeEffectKind kind,Statement body,int slots,int[] parameters,RuntimeUniform[] uniforms,RuntimeChildDeclaration[] children,bool coordinateDependent)
    {Kind=kind;this.body=body;this.slots=slots;this.parameters=parameters;this.coordinateDependent=coordinateDependent;this.uniforms=Array.AsReadOnly(uniforms);this.children=Array.AsReadOnly(children);UniformSize=uniforms.Length==0?0:uniforms[^1].Offset+uniforms[^1].Size;}
    public static RuntimeEffectProgram Compile(string source,RuntimeEffectKind kind)
    {ArgumentNullException.ThrowIfNull(source);if(source.Length>65536)throw new NotSupportedException("Runtime source exceeds 64 KiB.");return new Parser(source,kind).Parse();}
    public BoundRuntimeEffect Bind(ReadOnlySpan<byte> data,IReadOnlyList<IRuntimeEffectChild?>? inputs=null)
    {
        if(data.Length!=UniformSize)throw new ArgumentException("Uniform data size does not match the compiled program.",nameof(data));
        if((inputs?.Count??0)!=children.Count)throw new ArgumentException("Child count does not match the compiled program.",nameof(inputs));
        var retained=inputs?.ToArray()??[];for(int i=0;i<retained.Length;i++)if(retained[i] is null||retained[i]!.Kind!=children[i].Kind)throw new NotSupportedException("Every declared child must be supplied with its declared kind; implicit paint-color children are not implemented.");
        return new(this,data.ToArray(),retained);
    }
    internal Vector4 Evaluate(byte[] data,IRuntimeEffectChild?[] children,Vector4 first,Vector4 second)
    {
        Span<Value> locals=stackalloc Value[slots];var context=new Evaluation(locals,data,children);
        if(parameters.Length>0)locals[parameters[0]]=Kind==RuntimeEffectKind.Shader?Value.Vector(first,2):Value.Vector(first,4);
        if(parameters.Length>1)locals[parameters[1]]=Value.Vector(second,4);
        if(!body.Execute(ref context,out var result))throw new InvalidOperationException("Runtime program did not return a color.");
        return new(result[0],result[1],result[2],result[3]);
    }
    [InlineArray(16)]
    private struct Components {private float first;}
    private struct Value
    {
        private Components components;
        internal RuntimeType Type;
        internal float this[int index] {readonly get=>Type.Scalar==RuntimeScalarKind.Int?BitConverter.SingleToInt32Bits(components[index]):components[index];set=>components[index]=Type.Scalar==RuntimeScalarKind.Int?BitConverter.Int32BitsToSingle((int)value):Type.Scalar==RuntimeScalarKind.Bool?value==0?0:1:value;}
        internal readonly int Int(int index)=>BitConverter.SingleToInt32Bits(components[index]);
        internal void SetInt(int index,int value)=>components[index]=BitConverter.Int32BitsToSingle(value);
        internal static Value Integer(int x){var v=new Value{Type=new(RuntimeScalarKind.Int)};v.SetInt(0,x);return v;}
        internal static Value Scalar(float x,RuntimeScalarKind kind=RuntimeScalarKind.Float){var v=new Value{Type=new(kind)};v[0]=kind==RuntimeScalarKind.Int?(int)x:kind==RuntimeScalarKind.Bool?x==0?0:1:x;return v;}
        internal static Value Vector(Vector4 x,int count){var v=new Value{Type=new(RuntimeScalarKind.Float,count)};for(int i=0;i<count;i++)v[i]=x[i];return v;}
        internal readonly bool Truth=>this[0]!=0;
        internal readonly Value Convert(RuntimeType type)
        {
            if(Type.Components!=type.Components)throw new InvalidOperationException("Runtime value dimensions changed after compilation.");
            if(Type==type)return this;
            var result=new Value{Type=type};for(int i=0;i<type.Components;i++)result[i]=type.Scalar switch{RuntimeScalarKind.Int=>(int)this[i],RuntimeScalarKind.Bool=>this[i]==0?0:1,_=>this[i]};return result;
        }
    }
    private ref struct Evaluation(Span<Value> locals,ReadOnlySpan<byte> uniforms,IRuntimeEffectChild?[] children)
    {
        internal Span<Value> Locals=locals;
        internal ReadOnlySpan<byte> Uniforms=uniforms;
        internal IRuntimeEffectChild?[] Children=children;
        private int remaining=8192;
        internal void Tick(){if(--remaining<0)throw new InvalidOperationException("Runtime expression exceeded 8192 evaluation steps per sample.");}
    }
    private abstract class Expression(RuntimeType type)
    {
        internal RuntimeType Type {get;}=type;
        internal virtual bool IsConstant=>false;
        internal abstract Value Evaluate(ref Evaluation context);
    }
    private sealed class Constant(Value value):Expression(value.Type)
    {internal override bool IsConstant=>true;internal override Value Evaluate(ref Evaluation context){context.Tick();return value;}}
    private sealed class Local(int slot,RuntimeType type):Expression(type)
    {
        internal int Slot=>slot;
        internal override Value Evaluate(ref Evaluation context){context.Tick();return context.Locals[slot];}
    }
    private sealed class Uniform(RuntimeUniform uniform,Expression? index=null):Expression(uniform.Type)
    {
        internal override Value Evaluate(ref Evaluation context)
        {
            context.Tick();int element=index is null?0:(int)index.Evaluate(ref context)[0];if((uint)element>=(uint)uniform.Count)throw new InvalidOperationException("Runtime uniform array index is out of bounds.");
            int offset=uniform.Offset+element*uniform.Type.Components*4;var result=new Value{Type=Type};
            for(int i=0;i<Type.Components;i++){int bits=BinaryPrimitives.ReadInt32LittleEndian(context.Uniforms.Slice(offset+i*4,4));if(Type.Scalar==RuntimeScalarKind.Int)result.SetInt(i,bits);else result[i]=BitConverter.Int32BitsToSingle(bits);}return result;
        }
    }
    private sealed class Swizzle(Expression source,int[] indices):Expression(new(source.Type.Scalar,indices.Length))
    {
        internal override bool IsConstant=>source.IsConstant;
        internal override Value Evaluate(ref Evaluation context)
        {context.Tick();var input=source.Evaluate(ref context);var result=new Value{Type=Type};for(int i=0;i<indices.Length;i++){if(Type.Scalar==RuntimeScalarKind.Int)result.SetInt(i,input.Int(indices[i]));else result[i]=input[indices[i]];}return result;}
    }
    private sealed class Index(Expression source,Expression index,RuntimeType type):Expression(type)
    {
        internal override bool IsConstant=>source.IsConstant&&index.IsConstant;
        internal override Value Evaluate(ref Evaluation context)
        {
            context.Tick();var value=source.Evaluate(ref context);int at=(int)index.Evaluate(ref context)[0],count=source.Type.IsMatrix?source.Type.Columns:source.Type.Rows;
            if((uint)at>=(uint)count)throw new InvalidOperationException("Runtime vector or matrix index is out of bounds.");var result=new Value{Type=Type};for(int i=0;i<Type.Components;i++){if(Type.Scalar==RuntimeScalarKind.Int)result.SetInt(i,value.Int(at*Type.Components+i));else result[i]=value[at*Type.Components+i];}return result;
        }
    }
    private sealed class Unary(string operation,Expression value,RuntimeType type):Expression(type)
    {
        internal override bool IsConstant=>value.IsConstant;
        internal override Value Evaluate(ref Evaluation context)
        {context.Tick();var v=value.Evaluate(ref context);if(operation=="!")return Value.Scalar(v.Truth?0:1,RuntimeScalarKind.Bool);if(operation=="-")for(int i=0;i<Type.Components;i++){if(Type.Scalar==RuntimeScalarKind.Int)v.SetInt(i,unchecked(-v.Int(i)));else v[i]=-v[i];}return v;}
    }
    private sealed class Binary(string operation,Expression left,Expression right,RuntimeType type):Expression(type)
    {
        internal override bool IsConstant=>left.IsConstant&&right.IsConstant;
        internal override Value Evaluate(ref Evaluation context)
        {
            context.Tick();var a=left.Evaluate(ref context);if(operation=="&&"&&!a.Truth||operation=="||"&&a.Truth)return Value.Scalar(a.Truth?1:0,RuntimeScalarKind.Bool);var b=right.Evaluate(ref context);var result=new Value{Type=Type};
            if(operation=="*"&&(a.Type.IsMatrix||b.Type.IsMatrix)&&!a.Type.IsScalar&&!b.Type.IsScalar)
            {
                int rows=a.Type.Rows,columns=b.Type.IsMatrix?b.Type.Columns:1,inner=a.Type.Columns;
                if(!a.Type.IsMatrix){rows=1;inner=a.Type.Rows;}
                for(int c=0;c<columns;c++)for(int r=0;r<rows;r++){float sum=0;for(int k=0;k<inner;k++)sum+=a[a.Type.IsMatrix?k*a.Type.Rows+r:k]*b[b.Type.IsMatrix?c*b.Type.Rows+k:k];result[c*rows+r]=sum;}return result;
            }
            if(operation is "==" or "!=")
            {bool same=true;for(int i=0;i<a.Type.Components;i++)same&=a.Type.Scalar==RuntimeScalarKind.Int&&b.Type.Scalar==RuntimeScalarKind.Int?a.Int(i)==b.Int(i):a[i]==b[i];return Value.Scalar(operation=="=="?same?1:0:same?0:1,RuntimeScalarKind.Bool);}
            if(a.Type.Scalar==RuntimeScalarKind.Int&&b.Type.Scalar==RuntimeScalarKind.Int)
            {
                for(int i=0;i<Type.Components;i++)
                {
                    int x=a.Int(a.Type.IsScalar?0:i),y=b.Int(b.Type.IsScalar?0:i);
                    if(Type.Scalar==RuntimeScalarKind.Bool){result[i]=operation switch{"<"=>x<y?1:0,"<="=>x<=y?1:0,">"=>x>y?1:0,">="=>x>=y?1:0,_=>throw new InvalidOperationException("Unknown integer comparison.")};continue;}
                    int value=operation switch{"+"=>unchecked(x+y),"-"=>unchecked(x-y),"*"=>unchecked(x*y),"/"=>y==0?throw new InvalidOperationException("Runtime integer division by zero."):x==int.MinValue&&y==-1?int.MinValue:x/y,"%"=>y==0?throw new InvalidOperationException("Runtime integer remainder by zero."):x==int.MinValue&&y==-1?0:x%y,_=>throw new InvalidOperationException("Unknown integer arithmetic.")};result.SetInt(i,value);
                }
                return result;
            }
            for(int i=0;i<Type.Components;i++)
            {
                float x=a[a.Type.IsScalar?0:i],y=b[b.Type.IsScalar?0:i];result[i]=operation switch{"+"=>x+y,"-"=>x-y,"*"=>x*y,"/"=>x/y,"%"=>x%y,"<"=>x<y?1:0,"<="=>x<=y?1:0,">"=>x>y?1:0,">="=>x>=y?1:0,"&&"=>x!=0&&y!=0?1:0,"||"=>x!=0||y!=0?1:0,_=>throw new InvalidOperationException("Unknown runtime operator.")};
            }
            return Type.Scalar==RuntimeScalarKind.Int?result.Convert(Type):result;
        }
    }
    private sealed class Conditional(Expression condition,Expression yes,Expression no,RuntimeType type):Expression(type)
    {internal override bool IsConstant=>condition.IsConstant&&yes.IsConstant&&no.IsConstant;internal override Value Evaluate(ref Evaluation context){context.Tick();return(condition.Evaluate(ref context).Truth?yes:no).Evaluate(ref context).Convert(Type);}}
    private sealed class Constructor(RuntimeType type,Expression[] arguments):Expression(type)
    {
        internal override bool IsConstant=>arguments.All(a=>a.IsConstant);
        internal override Value Evaluate(ref Evaluation context)
        {
            context.Tick();var result=new Value{Type=Type};int offset=0;
            if(arguments.Length==1&&arguments[0].Type.IsScalar)
            {var input=arguments[0].Evaluate(ref context);float value=input[0];for(int c=0;c<Type.Columns;c++)for(int r=0;r<Type.Rows;r++){if(Type.Scalar==RuntimeScalarKind.Int&&input.Type.Scalar==RuntimeScalarKind.Int)result.SetInt(c*Type.Rows+r,input.Int(0));else result[c*Type.Rows+r]=Type.IsMatrix?(c==r?value:0):value;}return result.Convert(Type);}
            foreach(var argument in arguments){var value=argument.Evaluate(ref context);for(int i=0;i<value.Type.Components;i++){if(Type.Scalar==RuntimeScalarKind.Int&&value.Type.Scalar==RuntimeScalarKind.Int)result.SetInt(offset++,value.Int(i));else result[offset++]=value[i];}}return result.Convert(Type);
        }
    }
    private sealed class ChildCall(int child,Expression[] arguments):Expression(new(RuntimeScalarKind.Float,4))
    {
        internal override Value Evaluate(ref Evaluation context)
        {
            context.Tick();var a=arguments[0].Evaluate(ref context);Value b=arguments.Length>1?arguments[1].Evaluate(ref context):default;
            var first=new Vector4(a[0],a.Type.Rows>1?a[1]:0,a.Type.Rows>2?a[2]:0,a.Type.Rows>3?a[3]:0);var second=arguments.Length>1?new Vector4(b[0],b[1],b[2],b[3]):default;
            return Value.Vector(context.Children[child]!.Evaluate(first,second),4);
        }
    }
    private sealed class Builtin(string name,RuntimeType type,Expression[] arguments):Expression(type)
    {
        internal override bool IsConstant=>arguments.All(a=>a.IsConstant);
        internal override Value Evaluate(ref Evaluation context)
        {
            context.Tick();Span<Value> values=stackalloc Value[3];for(int i=0;i<arguments.Length;i++)values[i]=arguments[i].Evaluate(ref context);var result=new Value{Type=Type};
            if(Type.Scalar==RuntimeScalarKind.Int)
            {
                for(int i=0;i<Type.Components;i++){int x=values[0].Int(values[0].Type.IsScalar?0:i),y=arguments.Length>1?values[1].Int(values[1].Type.IsScalar?0:i):0,z=arguments.Length>2?values[2].Int(values[2].Type.IsScalar?0:i):0;int value=name switch{"abs"=>x<0?unchecked(-x):x,"sign"=>Math.Sign(x),"min"=>Math.Min(x,y),"max"=>Math.Max(x,y),"clamp"=>Math.Clamp(x,y,z),_=>throw new InvalidOperationException("Unknown integer builtin.")};result.SetInt(i,value);}return result;
            }
            if(name is "dot" or "length" or "distance" or "normalize")
            {
                float sum=0;for(int i=0;i<values[0].Type.Components;i++){float x=name=="distance"?values[0][i]-values[1][i]:values[0][i];sum+=name=="dot"?x*values[1][i]:x*x;}
                if(name=="normalize"){float length=MathF.Sqrt(sum);for(int i=0;i<Type.Components;i++)result[i]=values[0][i]/length;return result;}return Value.Scalar(name=="dot"?sum:MathF.Sqrt(sum));
            }
            if(name=="cross")return Value.Vector(new Vector4(Vector3.Cross(new(values[0][0],values[0][1],values[0][2]),new(values[1][0],values[1][1],values[1][2])),0),3);
            for(int i=0;i<Type.Components;i++)
            {
                float x=values[0][values[0].Type.IsScalar?0:i],y=arguments.Length>1?values[1][values[1].Type.IsScalar?0:i]:0,z=arguments.Length>2?values[2][values[2].Type.IsScalar?0:i]:0;
                result[i]=name switch
                {
                    "abs"=>MathF.Abs(x),"sign"=>x<0?-1:x>0?1:0,"floor"=>MathF.Floor(x),"ceil"=>MathF.Ceiling(x),"round"=>MathF.Round(x),"trunc"=>MathF.Truncate(x),"fract"=>x-MathF.Floor(x),
                    "sin"=>MathF.Sin(x),"cos"=>MathF.Cos(x),"tan"=>MathF.Tan(x),"asin"=>MathF.Asin(x),"acos"=>MathF.Acos(x),"atan"=>arguments.Length==1?MathF.Atan(x):MathF.Atan2(x,y),
                    "sqrt"=>MathF.Sqrt(x),"inversesqrt"=>1/MathF.Sqrt(x),"exp"=>MathF.Exp(x),"exp2"=>MathF.Pow(2,x),"log"=>MathF.Log(x),"log2"=>MathF.Log2(x),"pow"=>MathF.Pow(x,y),
                    "min"=>MathF.Min(x,y),"max"=>MathF.Max(x,y),"clamp"=>Math.Clamp(x,y,z),"saturate"=>Math.Clamp(x,0,1),"mix"=>x+(y-x)*z,"step"=>y<x?0:1,"smoothstep"=>Smooth(x,y,z),
                    "mod"=>x-y*MathF.Floor(x/y),"radians"=>x*(MathF.PI/180),"degrees"=>x*(180/MathF.PI),_=>throw new InvalidOperationException("Unknown runtime builtin.")
                };
            }
            return result;
        }
        private static float Smooth(float edge0,float edge1,float x){float t=Math.Clamp((x-edge0)/(edge1-edge0),0,1);return t*t*(3-2*t);}
    }
    private abstract class Statement
    {
        internal virtual bool AlwaysReturns=>false;
        internal abstract bool Execute(ref Evaluation context,out Value result);
    }
    private sealed class Block(Statement[] statements):Statement
    {
        internal override bool AlwaysReturns=>statements.Any(s=>s.AlwaysReturns);
        internal override bool Execute(ref Evaluation context,out Value result)
        {context.Tick();foreach(var statement in statements)if(statement.Execute(ref context,out result))return true;result=default;return false;}
    }
    private sealed class Return(Expression value):Statement
    {internal override bool AlwaysReturns=>true;internal override bool Execute(ref Evaluation context,out Value result){context.Tick();result=value.Evaluate(ref context);return true;}}
    private sealed class Assign(int slot,RuntimeType type,Expression value,int[]? components=null):Statement
    {
        internal override bool Execute(ref Evaluation context,out Value result)
        {context.Tick();var v=value.Evaluate(ref context);if(components is null)context.Locals[slot]=v.Convert(type);else{var target=context.Locals[slot];for(int i=0;i<components.Length;i++){if(type.Scalar==RuntimeScalarKind.Int)target.SetInt(components[i],v.Int(i));else target[components[i]]=v[i];}context.Locals[slot]=target;}result=default;return false;}
    }
    private sealed class If(Expression condition,Statement yes,Statement? no):Statement
    {
        internal override bool AlwaysReturns=>yes.AlwaysReturns&&no is {AlwaysReturns:true};
        internal override bool Execute(ref Evaluation context,out Value result)
        {context.Tick();var chosen=condition.Evaluate(ref context).Truth?yes:no;if(chosen is not null)return chosen.Execute(ref context,out result);result=default;return false;}
    }
    public sealed class BoundRuntimeEffect
    {
        private readonly RuntimeEffectProgram program;
        private readonly byte[] data;
        private readonly IRuntimeEffectChild?[] children;
        private readonly Vector4? constant;
        internal BoundRuntimeEffect(RuntimeEffectProgram program,byte[] data,IRuntimeEffectChild?[] children)
        {this.program=program;this.data=data;this.children=children;if(program.Kind==RuntimeEffectKind.Shader&&!program.coordinateDependent&&children.All(c=>c?.Kind!=RuntimeEffectKind.Shader))constant=program.Evaluate(data,children,default,default);}
        public bool TryGetConstant(out Vector4 value){value=constant??default;return constant.HasValue;}
        public Vector4 Evaluate(Vector4 first,Vector4 second=default)=>constant??program.Evaluate(data,children,first,second);
    }
}
