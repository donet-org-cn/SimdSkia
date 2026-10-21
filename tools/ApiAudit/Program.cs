using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length==3 && args[0]=="extract")
{
    var inventory=Extract(args[1]);
    Write(args[2],inventory);
    Console.WriteLine($"{inventory.Types} public types; {inventory.Entries.Length} API entries; {inventory.AssemblyIdentity}");
    return 0;
}
if (args.Length==4 && args[0]=="compare")
{
    var baseline=Read(args[1]); var replacement=Read(args[2]);
    var baselineSet=baseline.Entries.ToHashSet(StringComparer.Ordinal);
    var replacementSet=replacement.Entries.ToHashSet(StringComparer.Ordinal);
    var missing=baselineSet.Except(replacementSet).Order(StringComparer.Ordinal).ToArray();
    var extra=replacementSet.Except(baselineSet).Order(StringComparer.Ordinal).ToArray();
    var report=new { Baseline=baseline.AssemblyIdentity, Replacement=replacement.AssemblyIdentity,
        BaselineHash=baseline.Sha256, ReplacementHash=replacement.Sha256,
        BaselineEntries=baselineSet.Count, MatchingEntries=baselineSet.Intersect(replacementSet).Count(),
        MissingOrDifferent=missing, AddedOrDifferent=extra,
        BinaryCompatibility="Not established: assembly identity and strong-name signing differ; signature matches alone do not prove binary compatibility.",
        Limitations="Public metadata only; protected APIs, attributes not listed by this tool, nullable annotations, and behavioral contracts require additional review." };
    Write(args[3],report);
    Console.WriteLine($"Matching: {report.MatchingEntries}/{report.BaselineEntries}; missing or different: {missing.Length}; added or different: {extra.Length}");
    return missing.Length==0 ? 0 : 2;
}
Console.Error.WriteLine("ApiAudit extract <assembly.dll> <inventory.json> | compare <baseline.json> <replacement.json> <report.json>\ncompare exits 2 for compatibility gaps, 1 for tool errors. No native functions or static field initializers are invoked.");
return 1;

static Inventory Read(string path) => JsonSerializer.Deserialize<Inventory>(File.ReadAllText(path)) ?? throw new InvalidDataException(path);
static void Write(string path,object value)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    File.WriteAllText(path,JsonSerializer.Serialize(value,new JsonSerializerOptions { WriteIndented=true })+Environment.NewLine);
}
static Inventory Extract(string path)
{
    path=Path.GetFullPath(path);
    var context=new AuditContext(path);
    try
    {
        var assembly=context.LoadFromAssemblyPath(path);
        var entries=new List<string>();
        var types=assembly.GetExportedTypes().Where(t=>t.Namespace=="SkiaSharp").OrderBy(t=>t.FullName,StringComparer.Ordinal).ToArray();
        const BindingFlags flags=BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
        foreach (var type in types)
        {
            string prefix=Name(type);
            string kind=type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : "class";
            string interfaces=string.Join(",",type.GetInterfaces().Where(i=>i.IsVisible).Select(Name).Order(StringComparer.Ordinal));
            string layout=type.StructLayoutAttribute is { } l ? $";layout={l.Value},size={l.Size},pack={l.Pack}" : "";
            entries.Add($"type {prefix};kind={kind};base={Name(type.BaseType)};abstract={type.IsAbstract};sealed={type.IsSealed};interfaces={interfaces}{layout};constraints={Constraints(type.GetGenericArguments())}{Attributes(type.GetCustomAttributesData())}");
            foreach (var field in type.GetFields(flags))
            {
                string value=field.IsLiteral ? ";constant="+Literal(field.GetRawConstantValue()) : "";
                entries.Add($"field {prefix}.{field.Name}:{Name(field.FieldType)};static={field.IsStatic};readonly={field.IsInitOnly}{value}{Attributes(field.GetCustomAttributesData())}");
            }
            foreach (var ctor in type.GetConstructors(flags))
                entries.Add($"ctor {prefix}({Parameters(ctor.GetParameters())}){Attributes(ctor.GetCustomAttributesData())}");
            foreach (var method in type.GetMethods(flags).Where(m=>!m.IsSpecialName || m.Name.StartsWith("op_",StringComparison.Ordinal)))
                entries.Add($"method {prefix}.{method.Name}<{string.Join(",",method.GetGenericArguments().Select(Name))}>({Parameters(method.GetParameters())}):{Name(method.ReturnType)};static={method.IsStatic};virtual={method.IsVirtual};abstract={method.IsAbstract};constraints={Constraints(method.GetGenericArguments())}{Attributes(method.GetCustomAttributesData())}");
            foreach (var prop in type.GetProperties(flags))
                entries.Add($"property {prefix}.{prop.Name}[{Parameters(prop.GetIndexParameters())}]:{Name(prop.PropertyType)};get={Accessor(prop.GetMethod)};set={Accessor(prop.SetMethod)}{Attributes(prop.GetCustomAttributesData())}");
            foreach (var evt in type.GetEvents(flags))
                entries.Add($"event {prefix}.{evt.Name}:{Name(evt.EventHandlerType)};add={Accessor(evt.AddMethod)};remove={Accessor(evt.RemoveMethod)}{Attributes(evt.GetCustomAttributesData())}");
        }
        string? informational=assembly.GetCustomAttributesData().FirstOrDefault(a=>a.AttributeType==typeof(AssemblyInformationalVersionAttribute))?.ConstructorArguments[0].Value?.ToString();
        return new(assembly.FullName!,informational,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),types.Length,entries.Order(StringComparer.Ordinal).ToArray());
    }
    finally { context.Unload(); }
}
static string Name(Type? t)
{
    if (t is null) return "-";
    if (t.IsByRef) return Name(t.GetElementType())+"&";
    if (t.IsPointer) return Name(t.GetElementType())+"*";
    if (t.IsArray) return Name(t.GetElementType())+"["+new string(',',t.GetArrayRank()-1)+"]";
    if (t.IsGenericParameter) return (t.DeclaringMethod is null ? "!" : "!!")+t.GenericParameterPosition;
    if (t.IsGenericType) return t.GetGenericTypeDefinition().FullName+"<"+string.Join(",",t.GetGenericArguments().Select(Name))+">";
    return t.FullName ?? t.Name;
}
static string Constraints(Type[] args) => string.Join("|",args.Where(t=>t.IsGenericParameter).Select(t=>$"{Name(t)}:{t.GenericParameterAttributes}:{string.Join(",",t.GetGenericParameterConstraints().Select(Name).Order(StringComparer.Ordinal))}"));
static string Parameters(ParameterInfo[] args) => string.Join(",",args.Select(p=>$"{p.Name}:{Name(p.ParameterType)}:in={p.IsIn}:out={p.IsOut}:optional={p.IsOptional}:default={(p.HasDefaultValue ? Literal(p.RawDefaultValue) : "-")}:requiredmods={string.Join("+",p.GetRequiredCustomModifiers().Select(Name))}{Attributes(p.GetCustomAttributesData())}"));
static string Accessor(MethodInfo? m) => m is null ? "-" : $"{(m.IsPublic ? "public" : m.IsFamily ? "protected" : "nonpublic")},static={m.IsStatic},virtual={m.IsVirtual}";
static string Literal(object? value) => value switch
{
    null => "null", string s => JsonSerializer.Serialize(s), char c => ((int)c).ToString(CultureInfo.InvariantCulture),
    IFormattable f => f.ToString(null,CultureInfo.InvariantCulture), _ => value.ToString() ?? "null"
};
static string Attributes(IList<CustomAttributeData> data) => string.Concat(data
    .Where(a=>a.AttributeType.FullName is "System.ObsoleteAttribute" or "System.FlagsAttribute" or "System.ParamArrayAttribute" or "System.Runtime.InteropServices.MarshalAsAttribute")
    .Select(a=>$";attribute={a.AttributeType.FullName}({string.Join(",",a.ConstructorArguments.Select(v=>Literal(v.Value)))})[{string.Join(",",a.NamedArguments.Select(v=>v.MemberName+"="+Literal(v.TypedValue.Value)))}]").Order(StringComparer.Ordinal));

sealed record Inventory(string AssemblyIdentity,string? InformationalVersion,string Sha256,int Types,string[] Entries);
sealed class AuditContext(string assemblyPath) : AssemblyLoadContext(isCollectible:true)
{
    protected override Assembly? Load(AssemblyName name)
    {
        string candidate=Path.Combine(Path.GetDirectoryName(assemblyPath)!,name.Name+".dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}
