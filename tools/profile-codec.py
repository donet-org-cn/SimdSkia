"""Instrument copies of the managed decoder; production sources stay untouched."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("images", nargs="+")
parser.add_argument("--output", required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
output = (root / args.output).resolve()
output.mkdir(parents=True, exist_ok=True)
build = output / "build"
build.mkdir(exist_ok=True)
hashes = {}
for name in ["PngCodec.cs", "ManagedZlib.cs", "PngFilters.cs"]:
    path = root / "src/SimdSkia.Imaging" / name
    hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    source = path.read_text(encoding="utf-8-sig")
    if name == "PngCodec.cs":
        source = source.replace("int position=8,width=0", "long stage=System.Diagnostics.Stopwatch.GetTimestamp();\n        int position=8,width=0")
        source = source.replace("ManagedZlib.Decode(compressed", "Profile.Parse+=System.Diagnostics.Stopwatch.GetTimestamp()-stage;stage=System.Diagnostics.Stopwatch.GetTimestamp();\n        ManagedZlib.Decode(compressed")
        source = source.replace("byte[]? gammaTable=null;", "Profile.Inflate+=System.Diagnostics.Stopwatch.GetTimestamp()-stage;stage=System.Diagnostics.Stopwatch.GetTimestamp();\n        byte[]? gammaTable=null;")
        source = source.replace("return output;\n        }\n        catch", "Profile.Filter+=System.Diagnostics.Stopwatch.GetTimestamp()-stage;\n            return output;\n        }\n        catch")
    else:
        source = source.replace("if(Adler(output)!=", "long adlerStart=System.Diagnostics.Stopwatch.GetTimestamp();uint checksum=Adler(output);Profile.Adler+=System.Diagnostics.Stopwatch.GetTimestamp()-adlerStart;\n        if(checksum!=")
    (build / name).write_text(source, encoding="utf-8")
project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>SimdSkia.Imaging</AssemblyName><EnableDefaultCompileItems>true</EnableDefaultCompileItems></PropertyGroup><ItemGroup><ProjectReference Include="' + str(root / 'src/SimdSkia.Core/SimdSkia.Core.csproj') + '" /></ItemGroup></Project>'
(build / "Profile.csproj").write_text(project, encoding="utf-8")
(build / "Program.cs").write_text('''using System.Diagnostics;
using System.Text.Json;
using SimdSkia.Imaging;
var results=new List<object>();
foreach(string path in args){
    var data=File.ReadAllBytes(path);
    for(int i=0;i<8;i++){using var image=PngCodec.Decode(data);}
    Profile.Parse=Profile.Inflate=Profile.Filter=Profile.Adler=0;
    const int count=40;var watch=Stopwatch.StartNew();
    long allocation=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<count;i++){using var image=PngCodec.Decode(data);}
    allocation=GC.GetAllocatedBytesForCurrentThread()-allocation;watch.Stop();
    double Ms(long ticks)=>ticks*1000.0/Stopwatch.Frequency/count;
    results.Add(new{Path=path,TotalMs=watch.Elapsed.TotalMilliseconds/count,ParseCrcMs=Ms(Profile.Parse),InflateMs=Ms(Profile.Inflate),AdlerMs=Ms(Profile.Adler),FilterConvertMs=Ms(Profile.Filter),AllocatedBytes=allocation/count});
}
Console.WriteLine(JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
internal static class Profile {public static long Parse,Inflate,Filter,Adler;}
''', encoding="utf-8")
subprocess.run(["dotnet", "build", str(build / "Profile.csproj"), "-c", "Release", "--nologo"], check=True)
env = dict(os.environ, DOTNET_TieredCompilation="0", DOTNET_ReadyToRun="0", DOTNET_gcServer="0")
result = subprocess.run(["dotnet", str(build / "bin/Release/net8.0/SimdSkia.Imaging.dll"), *[str(Path(p).resolve()) for p in args.images]], env=env, check=True, capture_output=True, text=True)
(output / "profile.json").write_text(result.stdout, encoding="utf-8")
(output / "sources.json").write_text(json.dumps(hashes, indent=2), encoding="utf-8")
print(result.stdout)
