using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
Console.WriteLine($"SSE2={Sse2.IsSupported} AVX2={Avx2.IsSupported} AOT={!RuntimeFeature.IsDynamicCodeSupported}");
foreach(int size in new[]{4000,16000,32000,64000,100000,132000,256000,2880000})
{
    string[] methods=["Fill","Copy","Copy1024","Unroll"];
    var measured=methods.ToDictionary(m=>m,m=>new List<double>());
    for(int fork=0;fork<3;fork++)for(int m=0;m<methods.Length;m++)
    {
        var method=methods[(m+fork)%methods.Length];var data=new byte[size];uint color=0xfff0f8ff;
        for(int i=0;i<100;i++)Run(method,data,color);
        for(int i=0;i<size;i+=4)if(MemoryMarshal.Read<uint>(data.AsSpan(i))!=color)throw new Exception("Invalid fill");
        var timer=Stopwatch.StartNew();int count=0;
        while(timer.ElapsedMilliseconds<150){Run(method,data,color);count++;}
        measured[method].Add(timer.Elapsed.TotalMilliseconds/count);
    }
    foreach(var (method,times) in measured)
    {times.Sort();Console.WriteLine($"{size} {method}: {times[1]:F6} ms / fill (range {times[0]:F6}..{times[2]:F6})");}
}
static void Run(string method,Span<byte> data,uint color)
{
    if(method=="Fill"){MemoryMarshal.Cast<byte,uint>(data).Fill(color);return;}
    if(method is "Copy" or "Copy1024")
    {
        int filled=Math.Min(data.Length,method=="Copy"?256:1024);MemoryMarshal.Cast<byte,uint>(data[..filled]).Fill(color);
        while(filled<data.Length){int count=Math.Min(filled,data.Length-filled);data[..count].CopyTo(data[filled..]);filled+=count;}
        return;
    }
    ref byte first=ref MemoryMarshal.GetReference(data);var v=Vector128.Create(color).AsByte();int at=0;
    if(Sse2.IsSupported)
    {
        for(;at<=data.Length-128;at+=128)
        {
            v.StoreUnsafe(ref first,(nuint)at);v.StoreUnsafe(ref first,(nuint)(at+16));
            v.StoreUnsafe(ref first,(nuint)(at+32));v.StoreUnsafe(ref first,(nuint)(at+48));
            v.StoreUnsafe(ref first,(nuint)(at+64));v.StoreUnsafe(ref first,(nuint)(at+80));
            v.StoreUnsafe(ref first,(nuint)(at+96));v.StoreUnsafe(ref first,(nuint)(at+112));
        }
    }
    MemoryMarshal.Cast<byte,uint>(data[at..]).Fill(color);
}
