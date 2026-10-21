using System.Text.Json;
using SimdSkia;
using SimdSkia.Imaging;
using SkiaSharp;
using System.IO.Compression;

if(args.Length==2&&args[0]=="--encode-probe"){EncodeProbe.Run(args[1]);return;}
int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
using var fixtures=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures.json")));
var results=new List<object>();
foreach(var fixture in fixtures.RootElement.GetProperty("cases").EnumerateArray())
{
    string name=fixture.GetProperty("name").GetString()!;var encoded=Convert.FromBase64String(fixture.GetProperty("encoded").GetString()!);var expected=Convert.FromBase64String(fixture.GetProperty("rgba").GetString()!);
    if(fixture.TryGetProperty("unsupported",out var unsupported)&&unsupported.GetBoolean())
    {bool rejected=false;try{using var invalid=ImageCodec.Decode(encoded);}catch(NotSupportedException){rejected=true;}Check(rejected,name+" explicit rejection; NOT a supported feature");results.Add(new{name,status="UNSUPPORTED - rejected"});continue;}
    using var image=ImageCodec.Decode(encoded);
    Check(image.Width==fixture.GetProperty("width").GetInt32()&&image.Height==fixture.GetProperty("height").GetInt32(),name+" size");
    int maximum=0;double square=0;var actual=image.GetPixelSpan();
    for(int i=0;i<expected.Length;i++){int error=Math.Abs(actual[i]-expected[i]);maximum=Math.Max(maximum,error);square+=error*error;}
    double rmse=Math.Sqrt(square/expected.Length);results.Add(new{name,maximum,rmse});
    Check(maximum<=fixture.GetProperty("max").GetInt32()&&rmse<=fixture.GetProperty("rmse").GetDouble(),$"{name} pixel gate max {maximum}, RMSE {rmse:F3}; expected {Convert.ToHexString(expected.AsSpan(0,16))}, actual {Convert.ToHexString(actual[..16])}");
    using var bitmap=SKBitmap.Decode(encoded);Check(bitmap.Width==image.Width&&bitmap.GetPixel(7,3).Red==image.ReadPixel(7,3).R,name+" compatibility decode");
    foreach(int length in new[]{0,2,encoded.Length/2,encoded.Length-4})
    {
        bool rejected=false;try{using var truncated=ImageCodec.Decode(encoded.AsSpan(0,length));}catch(Exception e)when(e is InvalidDataException or NotSupportedException or ArgumentException){rejected=true;}
        Check(rejected,name+" truncated "+length);
    }
}
using(var source=new PixelBuffer(35,21,PixelFormat.Bgra8888,AlphaFormat.Premultiplied,35*4+7))
{
    for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)source.WritePixel(x,y,new((byte)(x*5),(byte)(y*9),(byte)(x+y),255));
    foreach(int quality in new[]{0,50,95,100})
    {
        byte[] encoded=JpegCodec.Encode(source,quality);using var decoded=ImageCodec.Decode(encoded);Check(decoded.Width==35&&decoded.Height==21,"JPEG encode size "+quality);
        if(quality>=95){double square=0;for(int y=0;y<21;y++)for(int x=0;x<35;x++){var a=source.ReadPixel(x,y);var b=decoded.ReadPixel(x,y);square+=Math.Pow(a.R-b.R,2)+Math.Pow(a.G-b.G,2)+Math.Pow(a.B-b.B,2);}Check(Math.Sqrt(square/(35*21*3))<3,"JPEG high quality error");}
    }
    source.Dispose();bool disposed=false;try{JpegCodec.Encode(source);}catch(ObjectDisposedException){disposed=true;}Check(disposed,"JPEG disposed source");
}
foreach(int quality in new[]{-1,101}){using var source=new PixelBuffer(1,1,PixelFormat.Rgba8888,AlphaFormat.Straight);bool rejected=false;try{JpegCodec.Encode(source,quality);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"JPEG invalid quality");}
var random=new Random(1671);
var encodedFixtures=new List<object>();
foreach(int length in new[]{0,1,2,3,7,258,259,32768,65535,65536,100003})foreach(int pattern in new[]{0,1,2})
{
    var data=new byte[length];if(pattern==0)random.NextBytes(data);else for(int i=0;i<length;i++)data[i]=(byte)(pattern==1?17:i%71);
    var compressed=ManagedZlib.Encode(data);Check(ManagedZlib.Decode(compressed,length).AsSpan().SequenceEqual(data),"LZ77 roundtrip, window/tails/overlap "+length+"/"+pattern);
    encodedFixtures.Add(new{length,pattern,raw=Convert.ToBase64String(data),encoded=Convert.ToBase64String(compressed)});
    Check(compressed.Length<=ManagedZlib.EncodeStored(data).Length,"Compression expanded beyond stored blocks");
}
Directory.CreateDirectory("artifacts/completion");File.WriteAllText("artifacts/completion/managed-zlib-encoded.json",JsonSerializer.Serialize(encodedFixtures));
using(var source=new PixelBuffer(128,96,PixelFormat.Rgba8888,AlphaFormat.Straight))
{
    for(int y=0;y<96;y++)for(int x=0;x<128;x++)source.WritePixel(x,y,new((byte)(x/16*20),(byte)(y/16*30),17,255));
    int stored=PngCodec.Encode(source,CompressionLevel.NoCompression).Length;
    foreach(var level in Enum.GetValues<CompressionLevel>())
    {byte[] encoded=PngCodec.Encode(source,level);using var decoded=PngCodec.Decode(encoded);Check(source.GetPixelSpan().SequenceEqual(decoded.GetPixelSpan()),"PNG compression level changes pixels");if(level!=CompressionLevel.NoCompression)Check(encoded.Length<stored/10,"PNG compression option silently ignored");}
}
using(var native=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"jpeg-encode-native.json"))))
using(var managed=JsonDocument.Parse(EncodeProbe.Capture()))
{
    Check(native.RootElement.GetArrayLength()==managed.RootElement.GetArrayLength(),"Native JPEG probe count");int i=0;
    foreach(var row in managed.RootElement.EnumerateArray())
    {var expected=native.RootElement[i++];Check(row.GetProperty("alpha").GetString()==expected.GetProperty("alpha").GetString()&&row.GetProperty("a").GetInt32()==expected.GetProperty("a").GetInt32()&&row.GetProperty("storage").GetString()==expected.GetProperty("storage").GetString(),"JPEG native case identity");foreach(string channel in new[]{"r","g","b","outAlpha"})Check(Math.Abs(row.GetProperty(channel).GetInt32()-expected.GetProperty(channel).GetInt32())<=1,"JPEG default physical RGB/alpha differs from original "+row);}
}
Console.WriteLine(JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS: {checks} independent PNG/Adam7/16-bit/ICC/JPEG/compatibility/corruption assertions.");
