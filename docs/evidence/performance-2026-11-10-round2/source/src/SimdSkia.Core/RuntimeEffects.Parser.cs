using System.Globalization;
namespace SimdSkia;
public sealed partial class RuntimeEffectProgram
{
    private sealed class Parser
    {
        private readonly record struct Token(string Text,int Position);
        private sealed record Symbol(RuntimeType Type,int Slot=-1,RuntimeUniform? Uniform=null,Expression? Constant=null,bool ReadOnly=false,int Child=-1);
        private readonly string source;
        private readonly RuntimeEffectKind kind;
        private readonly List<Token> tokens=[];
        private readonly List<RuntimeUniform> uniforms=[];
        private readonly List<RuntimeChildDeclaration> children=[];
        private readonly Stack<Dictionary<string,Symbol>> scopes=new();
        private readonly HashSet<int> parameterSlots=[];
        private bool coordinateDependent;
        private int cursor,slotCount,depth;
        private string Current=>tokens[cursor].Text;
        private FormatException Error(string message)=>new($"{message} (source offset {tokens[cursor].Position}).");
        internal Parser(string source,RuntimeEffectKind kind){this.source=source;this.kind=kind;Lex();scopes.Push(new(StringComparer.Ordinal));}
        private void Lex()
        {
            int p=0;
            while(p<source.Length)
            {
                if(char.IsWhiteSpace(source[p])){p++;continue;}
                if(p+1<source.Length&&source[p]=='/'&&source[p+1]=='/') {p+=2;while(p<source.Length&&source[p]!='\n')p++;continue;}
                if(p+1<source.Length&&source[p]=='/'&&source[p+1]=='*')
                {int end=source.IndexOf("*/",p+2,StringComparison.Ordinal);if(end<0)throw new FormatException("Unclosed runtime comment.");p=end+2;continue;}
                int start=p;char c=source[p++];
                if(char.IsAsciiLetter(c)||c=='_'){while(p<source.Length&&(char.IsAsciiLetterOrDigit(source[p])||source[p]=='_'))p++;}
                else if(char.IsAsciiDigit(c)||c=='.'&&p<source.Length&&char.IsAsciiDigit(source[p]))
                {
                    while(p<source.Length&&(char.IsAsciiDigit(source[p])||source[p]=='.'))p++;
                    if(p<source.Length&&source[p] is 'e' or 'E'){p++;if(p<source.Length&&source[p] is '+' or '-')p++;while(p<source.Length&&char.IsAsciiDigit(source[p]))p++;}
                    if(p<source.Length&&source[p] is 'f' or 'h' or 'u')p++;
                }
                else if(p<source.Length&&new[]{"<=",">=","==","!=","&&","||","+=","-=","*=","/="}.Contains(source.Substring(start,2),StringComparer.Ordinal))p++;
                tokens.Add(new(source[start..p],start));if(tokens.Count>16384)throw new NotSupportedException("Runtime source exceeds 16384 tokens.");
            }
            tokens.Add(new("",source.Length));
        }
        private bool Eat(string text){if(Current!=text)return false;cursor++;return true;}
        private void Need(string text){if(!Eat(text))throw Error($"Expected '{text}', found '{Current}'");}
        private string Name()
        {string name=Current;if(name.Length==0||!char.IsAsciiLetter(name[0])&&name[0]!='_')throw Error("Expected an identifier");cursor++;return name;}
        private Symbol Lookup(string name)
        {foreach(var scope in scopes)if(scope.TryGetValue(name,out var symbol))return symbol;throw Error($"Unknown identifier '{name}'");}
        private void Add(string name,Symbol symbol)
        {if(!scopes.Peek().TryAdd(name,symbol))throw Error($"Duplicate identifier '{name}'");}
        private int NewSlot(){if(slotCount>=128)throw Error("Runtime program exceeds 128 local/parameter slots");return slotCount++;}
        private void Enter(){if(++depth>64)throw Error("Runtime syntax exceeds 64 nesting levels");}
        private static bool TryType(string name,out RuntimeType type)
        {
            type=default;string suffix;RuntimeScalarKind scalar;
            if(name.StartsWith("float",StringComparison.Ordinal)){suffix=name[5..];scalar=RuntimeScalarKind.Float;}
            else if(name.StartsWith("half",StringComparison.Ordinal)){suffix=name[4..];scalar=RuntimeScalarKind.Float;}
            else if(name.StartsWith("int",StringComparison.Ordinal)){suffix=name[3..];scalar=RuntimeScalarKind.Int;}
            else if(name.StartsWith("bool",StringComparison.Ordinal)){suffix=name[4..];scalar=RuntimeScalarKind.Bool;}
            else return false;
            if(suffix.Length==0){type=new(scalar);return true;}
            if(suffix.Length==1&&suffix[0] is >= '2' and <= '4'){type=new(scalar,suffix[0]-'0');return true;}
            if(scalar==RuntimeScalarKind.Float&&suffix.Length==3&&suffix[0] is >= '2' and <= '4'&&suffix[1]=='x'&&suffix[2] is >= '2' and <= '4')
            {type=new(scalar,suffix[2]-'0',suffix[0]-'0');return true;}
            return false;
        }
        private RuntimeType Type()
        {if(!TryType(Current,out var type))throw Error($"Unsupported runtime type '{Current}'");cursor++;return type;}
        private static RuntimeEffectKind ChildKind(string name)=>name switch{"shader"=>RuntimeEffectKind.Shader,"colorFilter"=>RuntimeEffectKind.ColorFilter,"blender"=>RuntimeEffectKind.Blender,_=>throw new FormatException("Unsupported runtime child type.")};
        internal RuntimeEffectProgram Parse()
        {
            while(Current.Length!=0)
            {
                bool color=false;
                if(Eat("layout")){Need("(");Need("color");Need(")");color=true;}
                if(Eat("uniform")){UniformDeclaration(color);continue;}
                if(color)throw Error("layout(color) requires a uniform declaration");
                if(Eat("const")){var type=Type();string name=Name();Need("=");var value=Expr();Need(";");if(!value.IsConstant)throw Error("Global const expressions cannot reference dynamic values");var context=new Evaluation(Span<Value>.Empty,ReadOnlySpan<byte>.Empty,[]);var folded=Coerce(value,type).Evaluate(ref context);Add(name,new(type,Constant:new Constant(folded),ReadOnly:true));continue;}
                var resultType=Type();string function=Name();
                if(function!="main")throw Error("User helper functions are not yet supported; inline the expression");
                if(resultType!=new RuntimeType(RuntimeScalarKind.Float,4))throw Error("Runtime main must return half4/float4");
                Need("(");scopes.Push(new(StringComparer.Ordinal));var parameters=new List<int>();
                if(!Eat(")"))
                {
                    do{var parameterType=Type();string name=Name();int slot=NewSlot();Add(name,new(parameterType,slot));parameters.Add(slot);parameterSlots.Add(slot);
                        var required=kind==RuntimeEffectKind.Shader?new RuntimeType(RuntimeScalarKind.Float,2):new(RuntimeScalarKind.Float,4);
                        if(parameterType!=required)throw Error("Runtime main has an incompatible parameter type");
                    }while(Eat(","));Need(")");
                }
                int expected=kind==RuntimeEffectKind.Blender?2:1;
                if(parameters.Count!=expected)throw Error("Runtime main has an incompatible parameter count");
                var body=ParseBlock();scopes.Pop();if(!body.AlwaysReturns)throw Error("Runtime main must return a color on every path");Eat(";");if(Current.Length!=0)throw Error("Additional runtime functions or declarations after main are not yet supported");
                return new(kind,body,slotCount,parameters.ToArray(),uniforms.ToArray(),children.ToArray(),coordinateDependent);
            }
            throw Error("Runtime main function was not found");
        }
        private void UniformDeclaration(bool color)
        {
            if(Current is "shader" or "colorFilter" or "blender")
            {if(color)throw Error("A child cannot have layout(color)");var childKind=ChildKind(Current);if(childKind==RuntimeEffectKind.Shader&&kind!=RuntimeEffectKind.Shader)throw Error("Shader children in color-filter/blender effects are not yet supported");cursor++;string name=Name();Need(";");int index=children.Count;if(index>=32)throw Error("Runtime program exceeds 32 children");children.Add(new(name,childKind));Add(name,new(default,Child:index));return;}
            var type=Type();if(type.Scalar==RuntimeScalarKind.Bool)throw Error("Boolean uniforms are not supported by the runtime ABI");if(color&&(type.Scalar!=RuntimeScalarKind.Float||type.Columns!=1||type.Rows is not (3 or 4)))throw Error("layout(color) requires float3 or float4");
            string variable=Name();bool array=Eat("[");int count=1;
            if(array){if(!int.TryParse(Current,NumberStyles.None,CultureInfo.InvariantCulture,out count)||count is <1 or >1024)throw Error("Uniform array size must be a constant from 1 to 1024");cursor++;Need("]");}
            Need(";");int offset=uniforms.Count==0?0:uniforms[^1].Offset+uniforms[^1].Size;var uniform=new RuntimeUniform(variable,offset,type,count,array,color);if(offset+uniform.Size>65536)throw Error("Uniform block exceeds 64 KiB");uniforms.Add(uniform);Add(variable,new(type,Uniform:uniform,ReadOnly:true));
        }
        private Statement ParseBlock()
        {
            Enter();Need("{");scopes.Push(new(StringComparer.Ordinal));var statements=new List<Statement>();
            while(!Eat("}")){if(Current.Length==0)throw Error("Unclosed runtime block");statements.Add(ParseStatement());if(statements.Count>1024)throw Error("Runtime block exceeds 1024 statements");}
            scopes.Pop();depth--;return new Block(statements.ToArray());
        }
        private Statement ParseStatement()
        {Enter();try{return ParseStatementCore();}finally{depth--;}}
        private Statement ParseStatementCore()
        {
            if(Current=="{")return ParseBlock();
            if(Eat("return")){var expression=Expr();if(expression.Type!=new RuntimeType(RuntimeScalarKind.Float,4))throw Error("Runtime return must be half4/float4");Need(";");return new Return(expression);}
            if(Eat("if")){Need("(");var condition=Expr();RequireBoolean(condition);Need(")");var yes=ParseStatement();var no=Eat("else")?ParseStatement():null;return new If(condition,yes,no);}
            bool constant=Eat("const");
            if(TryType(Current,out var type))
            {cursor++;string name=Name();Need("=");var expression=Coerce(Expr(),type);Need(";");int slot=NewSlot();Add(name,new(type,slot,ReadOnly:constant));return new Assign(slot,type,expression);}
            if(constant)throw Error("const requires a typed local declaration");
            if(Current is "for" or "while" or "do" or "switch" or "discard")throw Error($"Runtime statement '{Current}' is not implemented; no code is silently omitted");
            string targetName=Name();var target=Lookup(targetName);if(target.Slot<0||target.ReadOnly)throw Error("Assignment requires a mutable local variable");
            int[]? indices=null;var targetType=target.Type;
            if(Eat(".")){indices=SwizzleIndices(Name(),target.Type);if(indices.Distinct().Count()!=indices.Length)throw Error("Assignment swizzles cannot repeat components");targetType=new(target.Type.Scalar,indices.Length);}
            string op=Current;if(op is not ("=" or "+=" or "-=" or "*=" or "/="))throw Error("Expected a runtime assignment");cursor++;var right=Expr();Need(";");
            if(op!="="){Expression left=new Local(target.Slot,target.Type);if(indices is not null)left=new Swizzle(left,indices);right=MakeBinary(op[..1],left,right);}
            return new Assign(target.Slot,target.Type,Coerce(right,targetType),indices);
        }
        private static void RequireBoolean(Expression value)
        {if(value.Type!=new RuntimeType(RuntimeScalarKind.Bool))throw new FormatException("Runtime condition must be a scalar bool.");}
        private Expression Expr(int minimum=0)
        {
            Enter();var left=ParseUnary();
            while(Precedence(Current)>=minimum&&Precedence(Current)>=0)
            {string op=Current;int precedence=Precedence(op);cursor++;var right=Expr(precedence+1);left=MakeBinary(op,left,right);}
            if(minimum==0&&Eat("?")){RequireBoolean(left);var yes=Expr();Need(":");var no=Expr();var type=Common(yes.Type,no.Type,false);left=new Conditional(left,Coerce(yes,type),Coerce(no,type),type);}
            depth--;return left;
        }
        private static int Precedence(string op)=>op switch{"||"=>0,"&&"=>1,"==" or "!="=>2,"<" or "<=" or ">" or ">="=>3,"+" or "-"=>4,"*" or "/" or "%"=>5,_=>-1};
        private Expression ParseUnary()
        {Enter();try{return ParseUnaryCore();}finally{depth--;}}
        private Expression ParseUnaryCore()
        {
            if(Current=="-"&&tokens[cursor+1].Text=="2147483648"){cursor+=2;return new Constant(Value.Integer(int.MinValue));}
            if(Current is "+" or "-" or "!"){string op=Current;cursor++;var value=ParseUnary();if(op=="!")RequireBoolean(value);else if(value.Type.Scalar==RuntimeScalarKind.Bool)throw Error("Numeric unary operator cannot use bool");return new Unary(op,value,value.Type);}
            return ParsePrimary();
        }
        private Expression ParsePrimary()
        {
            Expression expression;
            if(Eat("(")){expression=Expr();Need(")");}
            else if(Current is "true" or "false"){expression=new Constant(Value.Scalar(Current=="true"?1:0,RuntimeScalarKind.Bool));cursor++;}
            else if(Current.Length>0&&(char.IsAsciiDigit(Current[0])||Current[0]=='.'))
            {
                string number=Current.TrimEnd('f','h','u');bool integer=!number.Contains('.')&&!number.Contains('e',StringComparison.OrdinalIgnoreCase)&&!Current.EndsWith('f')&&!Current.EndsWith('h');
                if(Current.EndsWith('u'))throw Error("Unsigned runtime integers are not yet supported");
                if(integer){if(!int.TryParse(number,NumberStyles.None,CultureInfo.InvariantCulture,out int value))throw Error("Runtime integer literal exceeds Int32");expression=new Constant(Value.Integer(value));}
                else{if(!float.TryParse(number,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)||!float.IsFinite(value))throw Error("Invalid finite runtime literal");expression=new Constant(Value.Scalar(value));}cursor++;
            }
            else
            {
                string name=Name();
                if(Eat("(")){var arguments=Arguments();expression=TryType(name,out var type)?Construct(type,arguments):Call(name,arguments);}
                else
                {
                    var symbol=Lookup(name);
                    if(symbol.Child>=0)
                    {Need(".");Need("eval");Need("(");var arguments=Arguments();var declaration=children[symbol.Child];int n=declaration.Kind==RuntimeEffectKind.Blender?2:1;if(arguments.Length!=n)throw Error("Child eval has the wrong argument count");var expected=new RuntimeType(RuntimeScalarKind.Float,declaration.Kind==RuntimeEffectKind.Shader?2:4);if(arguments.Any(a=>a.Type!=expected))throw Error("Child eval has incompatible argument types");expression=new ChildCall(symbol.Child,arguments);}
                    else if(symbol.Uniform is {} uniform)
                    {Expression? index=null;if(uniform.IsArray){Need("[");index=Expr();if(!index.Type.IsScalar||index.Type.Scalar!=RuntimeScalarKind.Int)throw Error("Uniform array index must be scalar int");Need("]");}expression=new Uniform(uniform,index);}
                    else{if(parameterSlots.Contains(symbol.Slot))coordinateDependent=true;expression=symbol.Constant??new Local(symbol.Slot,symbol.Type);}
                }
            }
            while(true)
            {
                if(Eat(".")){expression=new Swizzle(expression,SwizzleIndices(Name(),expression.Type));continue;}
                if(Eat("[")){var index=Expr();Need("]");if(!index.Type.IsScalar||index.Type.Scalar!=RuntimeScalarKind.Int)throw Error("Runtime index must be scalar int");if(expression.Type.IsScalar)throw Error("Cannot index a scalar");expression=new Index(expression,index,new(expression.Type.Scalar,expression.Type.IsMatrix?expression.Type.Rows:1));continue;}
                return expression;
            }
        }
        private Expression[] Arguments()
        {var arguments=new List<Expression>();if(!Eat(")")){do{if(arguments.Count>=16)throw Error("Runtime constructor exceeds 16 arguments");arguments.Add(Expr());}while(Eat(","));Need(")");}return arguments.ToArray();}
        private static int[] SwizzleIndices(string name,RuntimeType type)
        {
            if(type.IsMatrix||name.Length is <1 or >4)throw new FormatException("Invalid runtime swizzle.");string alphabet="xyzw".Contains(name[0])?"xyzw":"rgba".Contains(name[0])?"rgba":"stpq";
            var indices=new int[name.Length];for(int i=0;i<name.Length;i++){indices[i]=alphabet.IndexOf(name[i]);if(indices[i]<0||indices[i]>=type.Rows)throw new FormatException("Swizzle component is outside its vector.");}return indices;
        }
        private static Expression Coerce(Expression value,RuntimeType type)
        {
            if(value.Type.Rows!=type.Rows||value.Type.Columns!=type.Columns||value.Type.Scalar==RuntimeScalarKind.Bool&&type.Scalar!=RuntimeScalarKind.Bool)throw new FormatException("Runtime assignment dimensions/types do not match.");
            return value.Type==type?value:new Constructor(type,[value]);
        }
        private static Expression Construct(RuntimeType type,Expression[] arguments)
        {
            if(arguments.Length==0||arguments.Length!=1&&arguments.Sum(a=>a.Type.Components)!=type.Components||arguments.Length==1&&!arguments[0].Type.IsScalar&&arguments[0].Type.Components!=type.Components)throw new FormatException("Runtime constructor dimensions do not match.");
            return new Constructor(type,arguments);
        }
        private static RuntimeType Common(RuntimeType a,RuntimeType b,bool broadcast=true)
        {
            if(a.Scalar==RuntimeScalarKind.Bool||b.Scalar==RuntimeScalarKind.Bool){if(a!=b)throw new FormatException("Runtime bool operands do not match.");return a;}
            RuntimeScalarKind scalar=a.Scalar==RuntimeScalarKind.Float||b.Scalar==RuntimeScalarKind.Float?RuntimeScalarKind.Float:RuntimeScalarKind.Int;
            if(a.Rows==b.Rows&&a.Columns==b.Columns)return a with{Scalar=scalar};if(broadcast&&a.IsScalar)return b with{Scalar=scalar};if(broadcast&&b.IsScalar)return a with{Scalar=scalar};throw new FormatException("Runtime operand dimensions do not match.");
        }
        private static Expression MakeBinary(string op,Expression a,Expression b)
        {
            if(op is "&&" or "||"){RequireBoolean(a);RequireBoolean(b);return new Binary(op,a,b,new(RuntimeScalarKind.Bool));}
            if(op is "==" or "!="){_=Common(a.Type,b.Type,false);return new Binary(op,a,b,new(RuntimeScalarKind.Bool));}
            if(op is "<" or "<=" or ">" or ">=") {if(!a.Type.IsScalar||!b.Type.IsScalar||a.Type.Scalar==RuntimeScalarKind.Bool||b.Type.Scalar==RuntimeScalarKind.Bool)throw new FormatException("Runtime ordered comparisons require scalar numbers.");return new Binary(op,a,b,new(RuntimeScalarKind.Bool));}
            if(op=="*"&&!a.Type.IsScalar&&!b.Type.IsScalar&&(a.Type.IsMatrix||b.Type.IsMatrix))
            {
                int inner=a.Type.IsMatrix?a.Type.Columns:a.Type.Rows;if(inner!=b.Type.Rows)throw new FormatException("Runtime matrix multiply dimensions do not match.");
                var type=a.Type.IsMatrix&&b.Type.IsMatrix?new RuntimeType(RuntimeScalarKind.Float,a.Type.Rows,b.Type.Columns):new(RuntimeScalarKind.Float,a.Type.IsMatrix?a.Type.Rows:b.Type.Columns);return new Binary(op,a,b,type);
            }
            var result=Common(a.Type,b.Type);if(result.Scalar==RuntimeScalarKind.Bool)throw new FormatException("Runtime arithmetic requires numeric operands.");if(op=="%"&&result.Scalar!=RuntimeScalarKind.Int)throw new FormatException("Runtime '%' requires integer operands; use mod for float.");return new Binary(op,a,b,result);
        }
        private Expression Call(string name,Expression[] arguments)
        {
            int expected=name switch{"min" or "max" or "pow" or "mod" or "step" or "dot" or "distance" or "cross"=>2,"mix" or "clamp" or "smoothstep"=>3,"abs" or "sign" or "floor" or "ceil" or "round" or "trunc" or "fract" or "sin" or "cos" or "tan" or "asin" or "acos" or "sqrt" or "inversesqrt" or "exp" or "exp2" or "log" or "log2" or "length" or "normalize" or "saturate" or "radians" or "degrees"=>1,"atan"=>arguments.Length is 1 or 2?arguments.Length:0,_=>0};
            if(expected==0)throw Error($"Unsupported runtime function '{name}'");if(arguments.Length!=expected)throw Error($"Wrong argument count for runtime function '{name}'");
            if(arguments.Any(a=>a.Type.IsMatrix||a.Type.Scalar==RuntimeScalarKind.Bool))throw Error("Runtime builtins currently require scalar/vector numeric inputs");
            if(arguments.Any(a=>a.Type.Scalar==RuntimeScalarKind.Int))throw Error("This SkSL baseline's builtin overloads require floating-point arguments");
            if(name is "dot" or "length" or "distance" or "normalize" or "cross"&&arguments.Any(a=>a.Type.Scalar!=RuntimeScalarKind.Float))throw Error("Runtime vector geometry builtins require floating-point inputs");
            var type=arguments[0].Type;for(int i=1;i<arguments.Length;i++)type=Common(type,arguments[i].Type);
            if(name is "dot" or "distance" or "cross"){if(arguments[0].Type!=arguments[1].Type||arguments[0].Type.Columns!=1)throw Error("Runtime vector operands must match");}
            if(name=="cross"&&type.Rows!=3)throw Error("cross requires float3 arguments");
            if(name is "dot" or "length" or "distance")type=new(RuntimeScalarKind.Float);else if(name is not ("abs" or "sign" or "min" or "max" or "clamp"))type=type with{Scalar=RuntimeScalarKind.Float};
            return new Builtin(name,type,arguments);
        }
    }
}
