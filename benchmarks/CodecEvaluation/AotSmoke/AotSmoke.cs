// Deliberately uses Utf8JsonWriter so the smoke harness also needs no reflection.
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Sdcb.SimdCodecs.Jpeg;
using Sdcb.SimdCodecs.Png;

if(RuntimeFeature.IsDynamicCodeSupported)throw new Exception("Expected actual Native AOT execution");
using var fixtures=JsonDocument.Parse(File.ReadAllText(args[0]));
using var stream=File.Create(args[1]);using var output=new Utf8JsonWriter(stream,new JsonWriterOptions{Indented=true});
output.WriteStartObject();output.WriteBoolean("nativeAot",true);output.WriteStartArray("rows");
int count=0;
foreach(var f in fixtures.RootElement.GetProperty("cases").EnumerateArray())
{
    string name=f.GetProperty("name").GetString()!;
    int width=f.GetProperty("width").GetInt32(),height=f.GetProperty("height").GetInt32();
    byte[] encoded=Convert.FromBase64String(f.GetProperty("encoded").GetString()!);
    byte[] rgba=new byte[checked(width*height*4)];
    if(name.StartsWith("jpeg"))JpegDecoder.DecodeTo(encoded,rgba);
    else if(!PngImageDecoder.TryDecodeIntoRgba8(encoded,rgba))throw new Exception("PNG rejected: "+name);
    output.WriteStartObject();output.WriteString("name",name);output.WriteString("sha256",Convert.ToHexString(SHA256.HashData(rgba)));output.WriteEndObject();count++;
}
output.WriteEndArray();output.WriteNumber("cases",count);output.WriteEndObject();
Console.WriteLine($"PASS real Native AOT codec execution: {count} fixture hashes captured for comparison against JIT");
