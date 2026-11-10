using System.Buffers.Binary;
using System.Numerics;

namespace SimdSkia;

public sealed partial class RuntimeEffectProgram
{
    // One lazily shared numeric plan removes Value copies and virtual AST visits
    // from straight-line floating-point programs. Binding snapshots uniforms
    // without rebuilding instructions. Other typed programs use the bounded
    // interpreter, including its exception paths.
    private sealed class FloatProgram(float[] constants,NumericInstruction[] instructions,int[] result,UniformRegister[] uniforms,bool vectorizable)
    {
        internal FloatProgram Bind(byte[] data)
        {
            if(uniforms.Length==0)return this;
            var bound=(float[])constants.Clone();
            foreach(var uniform in uniforms)bound[uniform.Register]=BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(uniform.Offset,4)));
            return new(bound,instructions,result,[],vectorizable);
        }
        internal Vector4 Evaluate(Vector4 first,Vector4 second)
        {
            Span<float> registers=stackalloc float[constants.Length];
            constants.CopyTo(registers);
            for(int i=0;i<4;i++){registers[i]=first[i];registers[i+4]=second[i];}
            foreach(var instruction in instructions)
                registers[instruction.Destination]=Calculate(instruction.Operation,registers[instruction.A],registers[instruction.B]);
            return new(registers[result[0]],registers[result[1]],registers[result[2]],registers[result[3]]);
        }

        internal void EvaluateBatch(Span<Vector4> samples)
        {
            if(!vectorizable||!Vector.IsHardwareAccelerated||samples.Length<Vector<float>.Count)
            {for(int i=0;i<samples.Length;i++)samples[i]=Evaluate(samples[i],default);return;}
            Span<Vector<float>> registers=stackalloc Vector<float>[constants.Length];
            for(int i=0;i<registers.Length;i++)registers[i]=new(constants[i]);
            Span<float> lanes=stackalloc float[Vector<float>.Count];
            int start=0;
            for(;start<=samples.Length-lanes.Length;start+=lanes.Length)
            {
                for(int component=0;component<4;component++)
                {
                    for(int lane=0;lane<lanes.Length;lane++)lanes[lane]=samples[start+lane][component];
                    registers[component]=new(lanes);
                }
                foreach(var instruction in instructions)
                {
                    var a=registers[instruction.A];var b=registers[instruction.B];
                    registers[instruction.Destination]=instruction.Operation switch
                    {
                        NumericOperation.Add=>a+b,NumericOperation.Subtract=>a-b,
                        NumericOperation.Multiply=>a*b,NumericOperation.Divide=>a/b,
                        NumericOperation.Negate=>-a,NumericOperation.Abs=>Vector.Abs(a),
                        NumericOperation.Sqrt=>Vector.SquareRoot(a),
                        _=>throw new InvalidOperationException("Non-vector instruction in numeric batch.")
                    };
                }
                for(int lane=0;lane<lanes.Length;lane++)
                    samples[start+lane]=new(registers[result[0]][lane],registers[result[1]][lane],registers[result[2]][lane],registers[result[3]][lane]);
            }
            for(;start<samples.Length;start++)samples[start]=Evaluate(samples[start],default);
        }

        private static float Calculate(NumericOperation operation,float a,float b)=>operation switch
        {
            NumericOperation.Add=>a+b,NumericOperation.Subtract=>a-b,NumericOperation.Multiply=>a*b,
            NumericOperation.Divide=>a/b,NumericOperation.Negate=>-a,NumericOperation.Abs=>MathF.Abs(a),
            NumericOperation.Sqrt=>MathF.Sqrt(a),NumericOperation.Min=>MathF.Min(a,b),NumericOperation.Max=>MathF.Max(a,b),
            NumericOperation.Floor=>MathF.Floor(a),NumericOperation.Ceiling=>MathF.Ceiling(a),
            NumericOperation.Sin=>MathF.Sin(a),NumericOperation.Cos=>MathF.Cos(a),
            _=>throw new InvalidOperationException("Unknown numeric instruction.")
        };

        internal static FloatProgram? TryCreate(RuntimeEffectProgram program)
        {
            try{return new NumericCompiler(program).Compile();}
            catch(NumericFallbackException){return null;}
        }
    }

    private enum NumericOperation {Add,Subtract,Multiply,Divide,Negate,Abs,Sqrt,Min,Max,Floor,Ceiling,Sin,Cos}
    private readonly record struct NumericInstruction(NumericOperation Operation,int Destination,int A,int B);
    private readonly record struct UniformRegister(int Offset,int Register);
    private sealed class NumericFallbackException:Exception;

    private sealed class NumericCompiler
    {
        private readonly RuntimeEffectProgram program;
        private readonly int[]?[] locals;
        private readonly List<float> constants=[0,0,0,0,0,0,0,0];
        private readonly Dictionary<int,int> literals=[];
        private readonly List<NumericInstruction> instructions=[];
        private readonly Dictionary<int,int> uniformRegisters=[];
        private int steps;

        internal NumericCompiler(RuntimeEffectProgram program)
        {this.program=program;locals=new int[program.slots][];}

        internal FloatProgram Compile()
        {
            if(program.parameters.Length>0)locals[program.parameters[0]]=program.Kind==RuntimeEffectKind.Shader?[0,1]:[0,1,2,3];
            if(program.parameters.Length>1)locals[program.parameters[1]]=[4,5,6,7];
            var result=Statement(program.body);
            if(result is not {Length:4})throw new NumericFallbackException();
            bool vectorizable=instructions.All(i=>i.Operation<=NumericOperation.Sqrt);
            return new(constants.ToArray(),instructions.ToArray(),result,uniformRegisters.Select(p=>new UniformRegister(p.Key,p.Value)).ToArray(),vectorizable);
        }

        private void Tick(){if(++steps>8192)throw new NumericFallbackException();}
        private int Register(float value=0)
        {if(constants.Count>=256)throw new NumericFallbackException();int index=constants.Count;constants.Add(value);return index;}
        private int Literal(float value)
        {
            int bits=BitConverter.SingleToInt32Bits(value);
            if(!literals.TryGetValue(bits,out int register)){register=Register(value);literals.Add(bits,register);}
            return register;
        }
        private int Emit(NumericOperation operation,int a,int b=0)
        {int target=Register();instructions.Add(new(operation,target,a,b));return target;}

        private int[]? Statement(Statement statement)
        {
            Tick();
            switch(statement)
            {
                case Block block:
                    foreach(var item in block.Statements)if(Statement(item) is {} returned)return returned;
                    return null;
                case Return returned:return Expression(returned.Value);
                case Assign assign when assign.TargetType.Scalar==RuntimeScalarKind.Float&&!assign.TargetType.IsMatrix:
                    var source=Expression(assign.Value);
                    if(assign.Components is null)locals[assign.Slot]=source;
                    else
                    {
                        if(locals[assign.Slot] is not {} previous)throw new NumericFallbackException();
                        var next=(int[])previous.Clone();
                        for(int i=0;i<source.Length;i++)next[assign.Components[i]]=source[i];
                        locals[assign.Slot]=next;
                    }
                    return null;
                default:throw new NumericFallbackException();
            }
        }

        private int[] Expression(Expression expression)
        {
            Tick();
            if(expression.Type.IsMatrix)throw new NumericFallbackException();
            if(expression is Constant constant)
            {
                var value=constant.Literal;var result=new int[value.Type.Components];
                for(int i=0;i<result.Length;i++)result[i]=Literal(value[i]);
                return result;
            }
            if(expression.Type.Scalar!=RuntimeScalarKind.Float)throw new NumericFallbackException();
            switch(expression)
            {
                case Local local:return locals[local.Slot]??throw new NumericFallbackException();
                case Uniform uniform when uniform.Element is null:
                    var values=new int[uniform.Type.Components];
                    for(int i=0;i<values.Length;i++)
                    {
                        int offset=uniform.Declaration.Offset+i*4;
                        if(!uniformRegisters.TryGetValue(offset,out int register)){register=Register();uniformRegisters.Add(offset,register);}
                        values[i]=register;
                    }
                    return values;
                case Swizzle swizzle:
                    var source=Expression(swizzle.Source);return swizzle.Indices.Select(i=>source[i]).ToArray();
                case Constructor constructor:
                    var arguments=constructor.Arguments.Select(Expression).ToArray();
                    return arguments.Length==1&&arguments[0].Length==1?Enumerable.Repeat(arguments[0][0],expression.Type.Components).ToArray():arguments.SelectMany(a=>a).ToArray();
                case Unary unary when unary.Operation is "+" or "-":
                    var operand=Expression(unary.Operand);
                    return unary.Operation=="+"?operand:operand.Select(a=>Emit(NumericOperation.Negate,a)).ToArray();
                case Binary binary when binary.Operation is "+" or "-" or "*" or "/":
                    var left=Expression(binary.Left);var right=Expression(binary.Right);
                    var operation=binary.Operation switch{"+"=>NumericOperation.Add,"-"=>NumericOperation.Subtract,"*"=>NumericOperation.Multiply,_=>NumericOperation.Divide};
                    return Enumerable.Range(0,expression.Type.Components).Select(i=>Emit(operation,left[left.Length==1?0:i],right[right.Length==1?0:i])).ToArray();
                case Builtin builtin:return Builtin(builtin);
                default:throw new NumericFallbackException();
            }
        }

        private int[] Builtin(Builtin builtin)
        {
            var arguments=builtin.Arguments.Select(Expression).ToArray();
            if(builtin.Name is "dot" or "length" or "distance" or "normalize")
            {
                int sum=Literal(0);
                for(int i=0;i<arguments[0].Length;i++)
                {
                    int x=arguments[0][i];
                    if(builtin.Name=="distance")x=Emit(NumericOperation.Subtract,x,arguments[1][i]);
                    sum=Emit(NumericOperation.Add,sum,Emit(NumericOperation.Multiply,x,builtin.Name=="dot"?arguments[1][i]:x));
                }
                if(builtin.Name=="dot")return [sum];
                int length=Emit(NumericOperation.Sqrt,sum);
                return builtin.Name=="normalize"?arguments[0].Select(a=>Emit(NumericOperation.Divide,a,length)).ToArray():[length];
            }
            NumericOperation operation=builtin.Name switch
            {
                "abs"=>NumericOperation.Abs,"sqrt"=>NumericOperation.Sqrt,"min"=>NumericOperation.Min,"max"=>NumericOperation.Max,
                "floor"=>NumericOperation.Floor,"ceil"=>NumericOperation.Ceiling,"sin"=>NumericOperation.Sin,"cos"=>NumericOperation.Cos,
                _=>throw new NumericFallbackException()
            };
            return Enumerable.Range(0,builtin.Type.Components).Select(i=>Emit(operation,arguments[0][arguments[0].Length==1?0:i],arguments.Length>1?arguments[1][arguments[1].Length==1?0:i]:0)).ToArray();
        }
    }
}
