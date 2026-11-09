using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 2)
    throw new ArgumentException("Supply the official Avalonia.Android 12.1.5 DLL and output path.");

string source = Path.GetFullPath(args[0]);
string destination = Path.GetFullPath(args[1]);
const string expectedHash = "AC7D14C5BB6A9C4BC476E2AE7A88D4FE4E9B9E54BD3A8D2286DBF6FB71B64056";
if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))) != expectedHash)
    throw new InvalidDataException("Unrecognized Android host. Review the upstream change before updating its hash.");

using var module = ModuleDefinition.ReadModule(source);
var entry = module.GetType("Avalonia.AndroidApplicationExtensions").Methods
    .Single(method => method.Name == "UseAndroid");
int removed = 0;
foreach (var instruction in entry.Body.Instructions)
{
    if (instruction.OpCode != OpCodes.Call || instruction.Operand is not MethodReference method)
        continue;
    if (method.DeclaringType.Scope.Name is not ("Avalonia.Skia" or "Avalonia.HarfBuzz"))
        continue;
    if (method.Name is not ("UseSkia" or "UseHarfBuzz") || method.Parameters.Count != 1)
        throw new InvalidDataException("Unexpected default renderer registration signature.");
    // Both extensions consume and return the same AppBuilder. Removing the call
    // preserves the stack while the caller registers SimdSkia's rendering/text services.
    instruction.OpCode = OpCodes.Nop;
    instruction.Operand = null;
    removed++;
}
if (removed != 2)
    throw new InvalidDataException("Expected exactly two default renderer registrations.");

foreach (var reference in module.AssemblyReferences.ToArray())
    if (reference.Name is "Avalonia.Skia" or "Avalonia.HarfBuzz")
        module.AssemblyReferences.Remove(reference);

Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
try
{
    module.Write(temporary);
    using (var verified = ModuleDefinition.ReadModule(temporary))
    {
        if (verified.AssemblyReferences.Any(reference => reference.Name.Contains("Skia", StringComparison.OrdinalIgnoreCase)
            || reference.Name.Contains("HarfBuzz", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Unexpected native renderer dependency remains in the Android host.");
    }
    File.Move(temporary, destination, overwrite: true);
}
finally
{
    File.Delete(temporary);
}
Console.WriteLine("Prepared pinned Android window/input host for managed SimdSkia rendering.");
