using SkiaSharp;
using System.Text.Json;
using System.Runtime.InteropServices;
string output=args[0];Directory.CreateDirectory(output);var values=new SortedDictionary<string,object?>();int local=0;
void Check(bool value){if(!value)throw new Exception("Utility check "+local);local++;}
foreach(var enc in new[]{SKTextEncoding.Utf8,SKTextEncoding.Utf16,SKTextEncoding.Utf32})foreach(string text in new[]{"","A","中文","A\0中","😀","\ud800","\udc00","é"})
{
    string key=$"{enc}-{string.Join('-',text.Select(c=>((int)c).ToString("X4")))}";var b=StringUtilities.GetEncodedText(text,enc);values[key+"-bytes"]=b;values[key+"-span"]=StringUtilities.GetEncodedText(text.AsSpan(),enc);values[key+"-string"]=StringUtilities.GetString(b,enc);values[key+"-slice"]=StringUtilities.GetString(new byte[4].Concat(b).Concat(new byte[4]).ToArray().AsSpan(),4,b.Length,enc);
    var storage=b.Length==0?new byte[1]:b;var pin=GCHandle.Alloc(storage,GCHandleType.Pinned);try{values[key+"-ptr"]=StringUtilities.GetString(pin.AddrOfPinnedObject(),b.Length,enc);}finally{pin.Free();}
    try{values[key+"-character"]=StringUtilities.GetUnicodeCharacterCode(text,enc);}catch(Exception e){values[key+"-character"]=e.GetType().Name;}
}
foreach(int count in new[]{0,1,2,3,4,7,8,9,15,16,17,31,32,33,255,256,257})
{
    var input=Enumerable.Range(0,count*4+32).Select(x=>(byte)(x*131+17)).ToArray();var outputPixels=Enumerable.Repeat((byte)215,input.Length).ToArray();
    SKSwizzle.SwapRedBlue(outputPixels.AsSpan(),input.AsSpan(),count);values["swizzle-dest-"+count]=outputPixels;
    var inPlace=input.ToArray();SKSwizzle.SwapRedBlue((ReadOnlySpan<byte>)inPlace,count);values["swizzle-inplace-"+count]=inPlace;
    var pin=GCHandle.Alloc(inPlace,GCHandleType.Pinned);try{SKSwizzle.SwapRedBlue(pin.AddrOfPinnedObject(),count);values["swizzle-ptr-"+count]=inPlace;}finally{pin.Free();}
    Check(inPlace.AsSpan().SequenceEqual(input));
    #if MANAGED
    var sized=input.AsSpan(0,count*4).ToArray();SKSwizzle.SwapRedBlue(sized.AsSpan());Check(sized.AsSpan().SequenceEqual(outputPixels.AsSpan(0,count*4)));
    foreach(int shift in new[]{-4,4})
    {
        var alias=input.ToArray();int from=shift<0?4:0,to=shift<0?0:4;var expected=alias.AsSpan(from,count*4).ToArray();SKSwizzle.SwapRedBlue(expected.AsSpan());SKSwizzle.SwapRedBlue(alias.AsSpan(to,count*4),alias.AsSpan(from,count*4),count);Check(alias.AsSpan(to,count*4).SequenceEqual(expected));
    }
    #endif
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {values.Count} utility cases; {local} local safety checks");
