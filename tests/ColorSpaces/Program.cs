using SkiaSharp;
using System.Text.Json;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
var directory=args[0];Directory.CreateDirectory(directory);var rows=new Dictionary<string,object?>();
var transfers=new[]{SKColorSpaceTransferFn.Srgb,SKColorSpaceTransferFn.Linear,SKColorSpaceTransferFn.TwoDotTwo,SKColorSpaceTransferFn.Rec2020,SKColorSpaceTransferFn.Pq,SKColorSpaceTransferFn.Hlg,new SKColorSpaceTransferFn(-1,1,0,0,0,0,0)};
var matrices=new[]{SKColorSpaceXyz.Srgb,SKColorSpaceXyz.DisplayP3,SKColorSpaceXyz.AdobeRgb,SKColorSpaceXyz.Rec2020,SKColorSpaceXyz.Identity,SKColorSpaceXyz.Empty};
for(int t=0;t<transfers.Length;t++)for(int m=0;m<matrices.Length;m++)
{
    using var cs=SKColorSpace.CreateRgb(transfers[t],matrices[m]);string name=$"rgb-{t}-{m}";rows[name+"-created"]=cs is not null;if(cs is null)continue;
    rows[name+"-flags"]=new[]{cs.IsSrgb,cs.GammaIsCloseToSrgb,cs.GammaIsLinear,cs.IsNumericalTransferFunction};rows[name+"-fn"]=cs.GetNumericalTransferFunction().Values;rows[name+"-matrix"]=cs.ToColorSpaceXyz().Values;
    rows[name+"-fn-out"]=cs.GetNumericalTransferFunction(out var fn);rows[name+"-matrix-out"]=cs.ToColorSpaceXyz(out var xyz);
    using var profile=cs.ToProfile();using var restored=SKColorSpace.CreateIcc(profile);rows[name+"-profile"]=new object[]{profile.Size,profile.Buffer==IntPtr.Zero,profile.ToColorSpaceXyz(out var px),px.Values,restored is not null&&SKColorSpace.Equal(cs,restored)};
    using var lin=cs.ToLinearGamma();using var gamma=lin.ToSrgbGamma();rows[name+"-gamma"]=new object[]{lin.GammaIsLinear,gamma.GammaIsCloseToSrgb,lin.ToColorSpaceXyz().Values};
}
using(var srgb=SKColorSpace.CreateSrgb()){srgb.Dispose();rows["static-survives-dispose"]=srgb.IsSrgb;rows["static-identity"]=ReferenceEquals(srgb,SKColorSpace.CreateSrgb());}
using(var empty=new SKColorSpaceIccProfile()){rows["empty-profile"]=new object[]{empty.Size,empty.Buffer==IntPtr.Zero,empty.ToColorSpaceXyz(out _)};}
for(int kind=0;kind<3;kind++)
{
    byte[] bytes=Icc(kind);using var data=SKData.CreateCopy(bytes);using var profile=SKColorSpaceIccProfile.Create(bytes);rows[$"icc-{kind}-valid"]=profile is not null;
    if(profile is null)continue;rows[$"icc-{kind}-size"]=profile.Size;rows[$"icc-{kind}-xyz"]=profile.ToColorSpaceXyz().Values;
    var copied=new byte[bytes.Length];Marshal.Copy(profile.Buffer,copied,0,copied.Length);rows[$"icc-{kind}-bytes"]=copied.SequenceEqual(bytes);
    using var a=SKColorSpace.CreateIcc(data);using var b=SKColorSpace.CreateIcc(bytes,bytes.Length);using var c=SKColorSpace.CreateIcc(bytes.AsSpan());using var d=SKColorSpace.CreateIcc(data.Data,data.Size);using var p2=SKColorSpaceIccProfile.Create(data);using var p3=SKColorSpaceIccProfile.Create(data.Data,data.Size);
    data.Dispose();rows[$"icc-{kind}-all"]=new[]{SKColorSpace.Equal(a,b),SKColorSpace.Equal(a,c),SKColorSpace.Equal(a,d),p2.Size==bytes.Length,p3.Size==bytes.Length,a.GammaIsLinear};
}
foreach(var invalid in new[]{Array.Empty<byte>(),new byte[4],new byte[132],Icc(1)[..^1]})rows["invalid-"+invalid.Length]=SKColorSpaceIccProfile.Create(invalid) is null;
using var output=File.Create(Path.Combine(directory,"pixels.bin"));var cases=new List<object>();
foreach(var from in new[]{SKColorSpace.CreateSrgb(),SKColorSpace.CreateSrgbLinear(),SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb,SKColorSpaceXyz.DisplayP3),SKColorSpace.CreateRgb(SKColorSpaceTransferFn.TwoDotTwo,SKColorSpaceXyz.AdobeRgb)})
foreach(var to in new[]{SKColorSpace.CreateSrgb(),SKColorSpace.CreateSrgbLinear(),SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb,SKColorSpaceXyz.DisplayP3)})
foreach(var alpha in new[]{SKAlphaType.Unpremul,SKAlphaType.Premul})
{
    using var source=new SKBitmap(new SKImageInfo(64,4,SKColorType.Rgba8888,alpha,from));var pixels=source.GetPixelSpan();
    for(int y=0;y<4;y++)for(int x=0;x<64;x++){int at=y*source.RowBytes+x*4;byte a=(byte)(y*85);pixels[at]=(byte)(x*4);pixels[at+1]=(byte)(255-x*4);pixels[at+2]=(byte)(x*37%256);pixels[at+3]=a;if(alpha==SKAlphaType.Premul)for(int c=0;c<3;c++)pixels[at+c]=(byte)((pixels[at+c]*a+127)/255);}
    using var dst=new SKBitmap(new SKImageInfo(64,4,SKColorType.Bgra8888,alpha,to),272);using var image=SKImage.FromBitmap(source);bool success=image.ReadPixels(dst.Info,dst.GetPixels(),dst.RowBytes,0,0);rows["transfer-"+cases.Count]=success;
    int offset=(int)output.Position;for(int y=0;y<4;y++)output.Write(dst.GetPixelSpan().Slice(y*dst.RowBytes,256));cases.Add(new{offset,length=1024,from=from.ToColorSpaceXyz().Values,sourceLinear=from.GammaIsLinear,to=to.ToColorSpaceXyz().Values,targetLinear=to.GammaIsLinear,alpha=(int)alpha});
    using var map=source.PeekPixels();var cf=map.GetPixelColorF(21,2);rows["get-colors-"+cases.Count]=new object[]{(uint)source.GetPixel(21,2),(uint)map.GetPixelColor(21,2),new[]{cf.Red,cf.Green,cf.Blue,cf.Alpha}};
    source.SetPixel(0,0,new SKColor(90,170,240,128));rows["set-pixel-"+cases.Count]=source.GetPixelSpan()[..4].ToArray();
    source.Erase(new SKColor(90,170,240,128));rows["erase-pixel-"+cases.Count]=source.GetPixelSpan()[..4].ToArray();
    map.Erase(new SKColor(23,92,201,170));rows["erase-map-"+cases.Count]=source.GetPixelSpan()[..4].ToArray();
}
File.WriteAllText(Path.Combine(directory,"values.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(directory,"cases.json"),JsonSerializer.Serialize(cases));Console.WriteLine($"Captured {rows.Count} values, {cases.Count} pixel transfers");
static byte[] Icc(int kind)
{
    var bytes=new byte[132+6*12+3*20+3*40];void U32(int p,int v)=>BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(p,4),v);void Fixed(int p,float v)=>U32(p,(int)MathF.Round(v*65536));void Tag(int p,string s)=>System.Text.Encoding.ASCII.GetBytes(s).CopyTo(bytes,p);
    U32(0,bytes.Length);U32(8,0x04300000);Tag(12,"mntr");Tag(16,"RGB ");Tag(20,"XYZ ");Tag(36,"acsp");Fixed(68,.9642f);Fixed(72,1);Fixed(76,.8249f);U32(128,6);var matrix=SKColorSpaceXyz.DisplayP3;
    for(int c=0;c<3;c++)
    {
        string channel=new[]{"r","g","b"}[c];int dir=132+c*12,p=204+c*20;Tag(dir,channel+"XYZ");U32(dir+4,p);U32(dir+8,20);Tag(p,"XYZ ");for(int r=0;r<3;r++)Fixed(p+8+r*4,matrix[c,r]);
        dir=168+c*12;p=264+c*40;Tag(dir,channel+"TRC");U32(dir+4,p);U32(dir+8,kind==0?12:kind==1?14:40);Tag(p,kind==2?"para":"curv");
        if(kind==1){U32(p+8,1);BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(p+12,2),563);}
        if(kind==2){BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(p+8,2),4);var f=SKColorSpaceTransferFn.Srgb.Values;for(int n=0;n<7;n++)Fixed(p+12+n*4,f[n]);}
    }
    return bytes;
}
