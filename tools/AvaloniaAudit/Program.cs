using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if(args.Length!=3) { Console.Error.WriteLine("AvaloniaAudit <Avalonia.Skia.dll> <SimdSkia.Compatibility.dll> <report.json>"); return 1; }
using var input=File.OpenRead(args[0]); using var pe=new PEReader(input);
using var replacement=File.OpenRead(args[1]); using var managedPe=new PEReader(replacement);
var reader=pe.GetMetadataReader(); var managed=managedPe.GetMetadataReader();
var signatures=new Names();
var available=new HashSet<string>(StringComparer.Ordinal);
var publicTypes=new HashSet<string>(StringComparer.Ordinal);
foreach(var handle in managed.TypeDefinitions)
{
    var type=managed.GetTypeDefinition(handle);string name=Names.Type(managed,handle);
    if(!name.StartsWith("SkiaSharp.",StringComparison.Ordinal))continue;
    if((type.Attributes&TypeAttributes.VisibilityMask)==TypeAttributes.Public)publicTypes.Add(name);
    foreach(var m in type.GetMethods())
    {
        var method=managed.GetMethodDefinition(m);
        if((method.Attributes&MethodAttributes.MemberAccessMask)!=MethodAttributes.Public)continue;
        available.Add(Key(name,managed.GetString(method.Name),method.DecodeSignature(signatures,null)));
    }
    foreach(var f in type.GetFields())
    {
        var field=managed.GetFieldDefinition(f);
        if((field.Attributes&FieldAttributes.FieldAccessMask)==FieldAttributes.Public)
            available.Add(name+"."+managed.GetString(field.Name)+":"+field.DecodeSignature(signatures,null));
    }
}
var opcodes=typeof(OpCodes).GetFields(BindingFlags.Static|BindingFlags.Public).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>unchecked((ushort)o.Value));
var callers=new Dictionary<int,HashSet<string>>();
foreach(var th in reader.TypeDefinitions)
{
    var t=reader.GetTypeDefinition(th);
    foreach(var mh in t.GetMethods())
    {
        var m=reader.GetMethodDefinition(mh);if(m.RelativeVirtualAddress==0)continue;
        string caller=Names.Type(reader,th)+"."+reader.GetString(m.Name);
        var il=pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!;
        for(int i=0;i<il.Length;)
        {
            ushort code=il[i++];if(code==0xfe)code=(ushort)(0xfe00|il[i++]);var op=opcodes[code];
            int size=op.OperandType switch {
                OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
                OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,
                OperandType.InlineSwitch=>checked(4+BitConverter.ToInt32(il,i)*4),_=>4};
            if(op.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok)
            {
                int token=BitConverter.ToInt32(il,i);var h=MetadataTokens.EntityHandle(token);
                if(h.Kind==HandleKind.MethodSpecification)h=reader.GetMethodSpecification((MethodSpecificationHandle)h).Method;
                if(h.Kind==HandleKind.MemberReference)
                {int member=MetadataTokens.GetToken(h);if(!callers.TryGetValue(member,out var set))callers.Add(member,set=[]);set.Add(caller);}
            }
            i+=size;
        }
    }
}
var rows=new List<object>();int matched=0,missing=0,harfbuzz=0;
foreach(var h in reader.MemberReferences)
{
    var member=reader.GetMemberReference(h);string type=Names.Type(reader,member.Parent);
    if(!type.StartsWith("SkiaSharp.",StringComparison.Ordinal)&&!type.StartsWith("HarfBuzzSharp.",StringComparison.Ordinal))continue;
    string key=member.GetKind()==MemberReferenceKind.Method?Key(type,reader.GetString(member.Name),member.DecodeMethodSignature(signatures,null)):
        type+"."+reader.GetString(member.Name)+":"+member.DecodeFieldSignature(signatures,null);
    bool exists=available.Contains(key);if(type.StartsWith("HarfBuzzSharp.",StringComparison.Ordinal))harfbuzz++;else if(exists)matched++;else missing++;
    rows.Add(new { Type=type,Signature=key,PresentInManagedMetadata=exists,Callers=callers.GetValueOrDefault(MetadataTokens.GetToken(h))?.Order().ToArray()??[] });
}
var usedTypes=reader.TypeReferences.Select(h=>Names.Type(reader,h)).Where(n=>n.StartsWith("SkiaSharp.",StringComparison.Ordinal)).Distinct().Order().ToArray();
var report=new { Date=DateTimeOffset.UtcNow,AvaloniaAssembly=reader.GetString(reader.GetAssemblyDefinition().Name),
    InputSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
    ReplacementSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))),
    UsedSkiaTypes=usedTypes.Length,PresentSkiaTypes=usedTypes.Count(publicTypes.Contains),MatchedSkiaMemberReferences=matched,MissingSkiaMemberReferences=missing,HarfBuzzMemberReferences=harfbuzz,
    Caveat="IL references, including properties as accessor methods. Metadata presence is not a behavior or integration pass. Inherited members outside the replacement assembly are not resolved. Constants inlined by the C# compiler are absent. No native library is loaded.",
    Types=usedTypes.Select(t=>new {Type=t,Present=publicTypes.Contains(t)}),Members=rows };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
Console.WriteLine($"Avalonia.Skia: {usedTypes.Length} Skia types ({report.PresentSkiaTypes} present); {matched}/{matched+missing} member references present; {harfbuzz} HarfBuzz references. This does not establish integration compatibility.");
return 0;

static string Key(string type,string name,MethodSignature<string> signature)=>$"{type}.{name}`{signature.GenericParameterCount}({string.Join(",",signature.ParameterTypes)}):{signature.ReturnType};instance={signature.Header.IsInstance}";

sealed class Names : ISignatureTypeProvider<string,object?>
{
    public static string Type(MetadataReader r,EntityHandle h)=>h.Kind switch {
        HandleKind.TypeReference=>Ref(r,(TypeReferenceHandle)h),HandleKind.TypeDefinition=>Def(r,(TypeDefinitionHandle)h),
        HandleKind.TypeSpecification=>r.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(new Names(),null),_=>"<"+h.Kind+">"};
    private static string Ref(MetadataReader r,TypeReferenceHandle h){var t=r.GetTypeReference(h);return t.ResolutionScope.Kind==HandleKind.TypeReference?Type(r,t.ResolutionScope)+"+"+r.GetString(t.Name):r.GetString(t.Namespace)+"."+r.GetString(t.Name);}
    private static string Def(MetadataReader r,TypeDefinitionHandle h){var t=r.GetTypeDefinition(h);return t.IsNested?Type(r,t.GetDeclaringType())+"+"+r.GetString(t.Name):r.GetString(t.Namespace)+"."+r.GetString(t.Name);}
    public string GetArrayType(string e,ArrayShape s)=>e+"["+new string(',',s.Rank-1)+"]";
    public string GetByReferenceType(string e)=>e+"&";
    public string GetFunctionPointerType(MethodSignature<string> s)=>"fn("+string.Join(",",s.ParameterTypes)+")";
    public string GetGenericInstantiation(string t,ImmutableArray<string> a)=>t+"<"+string.Join(",",a)+">";
    public string GetGenericMethodParameter(object? c,int i)=>"!!"+i;
    public string GetGenericTypeParameter(object? c,int i)=>"!"+i;
    public string GetModifiedType(string m,string t,bool required)=>t;
    public string GetPinnedType(string t)=>t;
    public string GetPointerType(string t)=>t+"*";
    public string GetPrimitiveType(PrimitiveTypeCode c)=>"System."+(c==PrimitiveTypeCode.TypedReference?"TypedReference":c.ToString());
    public string GetSZArrayType(string t)=>t+"[]";
    public string GetTypeFromDefinition(MetadataReader r,TypeDefinitionHandle h,byte k)=>Type(r,h);
    public string GetTypeFromReference(MetadataReader r,TypeReferenceHandle h,byte k)=>Type(r,h);
    public string GetTypeFromSpecification(MetadataReader r,object? c,TypeSpecificationHandle h,byte k)=>Type(r,h);
}
