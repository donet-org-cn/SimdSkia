using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

string output=args[0];Directory.CreateDirectory(output);var values=new SortedDictionary<string,object?>();int assertions=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);assertions++;}
var rng=new Random(40951);
var curves=new[]{SKColorSpaceTransferFn.Srgb,SKColorSpaceTransferFn.TwoDotTwo,SKColorSpaceTransferFn.Linear,SKColorSpaceTransferFn.Rec2020,SKColorSpaceTransferFn.Pq,SKColorSpaceTransferFn.Hlg};
var colorData=new{curves=curves.Select(fn=>new{fn.Values,inverse=fn.Invert().Values,samples=new[]{-1f,-.1f,0,.003f,.04f,.18f,.5f,1,1.5f}.Select(x=>fn.Transform(x)).ToArray()}).ToArray(),matrices=new[]{SKColorSpaceXyz.Srgb,SKColorSpaceXyz.AdobeRgb,SKColorSpaceXyz.DisplayP3,SKColorSpaceXyz.Rec2020,SKColorSpaceXyz.Xyz}.Select(x=>x.Values).ToArray()};
File.WriteAllText(Path.Combine(output,"colors.json"),JsonSerializer.Serialize(colorData,new JsonSerializerOptions{WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals}));
var primary=new SKColorSpacePrimaries(.64f,.33f,.30f,.60f,.15f,.06f,.3127f,.3290f);values["primaries-valid"]=primary.ToColorSpaceXyz(out var primaryMatrix);values["primaries-matrix"]=primaryMatrix.Values;
values["xyz-inverse"]=SKColorSpaceXyz.Srgb.Invert().Values;values["xyz-concat"]=SKColorSpaceXyz.Concat(SKColorSpaceXyz.Srgb,SKColorSpaceXyz.DisplayP3).Values;
using(var pmOutput=new MemoryStream())
{
    for(int alpha=0;alpha<=255;alpha++)for(int channel=0;channel<=alpha;channel++)
    {
        var color=new SKColor((byte)channel,(byte)channel,(byte)channel,(byte)alpha);uint pre=(uint)SKPMColor.PreMultiply(color),post=(uint)SKPMColor.UnPreMultiply(new SKPMColor((uint)((alpha<<24)|(channel<<16)|(channel<<8)|channel)));
        pmOutput.Write(BitConverter.GetBytes(pre));pmOutput.Write(BitConverter.GetBytes(post));
    }
    File.WriteAllBytes(Path.Combine(output,"premultiplied.bin"),pmOutput.ToArray());
}
using(var input=new MemoryStream(Enumerable.Range(0,30).Select(i=>(byte)i).ToArray()))
using(var front=new SKFrontBufferedManagedStream(input,8))
{
    byte[] bytes=new byte[6];values["front-read"]=front.Read(bytes,6);values["front-bytes"]=bytes;values["front-rewind"]=front.Rewind();values["front-read-again"]=front.Read(bytes,6);values["front-bytes-again"]=bytes.ToArray();
    values["front-state"]=new{front.HasPosition,front.HasLength,front.Length,front.Position,front.IsAtEnd};front.Read(bytes,6);values["front-beyond-rewind"]=front.Rewind();
}
for(int i=0;i<160;i++)
{
    var a=SKMatrix44.CreateScale(1+i*.01f,2+i*.03f,3+i*.007f)*SKMatrix44.CreateTranslation(i/2f,7-i/3f,i/7f);
    var b=SKMatrix44.CreateRotation(1,2,3,i*.033f);var c=a*b;bool invert=c.TryInvert(out var inverse);
    var p=c.MapPoint(1.5f,2.5f,3.5f);var r=SKMatrix44.FromColumnMajor(c.ToColumnMajor());
    values[$"matrix-{i}"]=new{c=c.ToRowMajor(),det=c.Determinant(),invert,inverse=inverse.ToRowMajor(),p=new[]{p.X,p.Y,p.Z},r=r.ToRowMajor(),pre=a.PreConcat(b).ToRowMajor(),post=a.PostConcat(b).ToRowMajor(),transpose=c.Transpose().ToRowMajor(),map2=c.MapPoint(new SKPoint(7,9)).ToString(),isInvertible=c.IsInvertible};
    var transform=SKRotationScaleMatrix.Create(1+i*.013f,i*.02f,i,2*i,3,4);values[$"rotation-{i}"]=transform.ToMatrix().Values;
}
var singular=new SKMatrix44();values["matrix-singular"]=singular.TryInvert(out _);
using(var data=SKData.CreateCopy(new byte[]{1,2,3,4,5,6,7,8}))
{
    using var subset=data.Subset(2,4);subset.Span[0]=99;values["subset-shares"]=data.ToArray();
    using var stream=data.AsStream();stream.Position=5;stream.WriteByte(55);values["stream-shares"]=data.ToArray();
    Marshal.WriteByte(data.Data,6,77);GC.Collect();GC.WaitForPendingFinalizers();Check(Marshal.ReadByte(data.Data,6)==77,"data pin across GC");
    values["subset-invalid"]=data.Subset(7,4) is null;values["subset-end-empty"]=data.Subset(8,0)?.Size;
}
SKData.Empty.Dispose();Check(SKData.Empty.Size==0,"static empty survives Dispose");
foreach(int length in new[]{0,1,7,128,4097})
{
    byte[] bytes=new byte[length];rng.NextBytes(bytes);using var managed=new MemoryStream(bytes);
    using var data=SKData.Create(managed,length);values[$"create-stream-{length}"]=data.ToArray();Check(managed.CanRead,"default stream retained");
    using var native=new SKMemoryStream(bytes);values[$"memory-{length}"]=new{native.Length,native.Position,native.HasLength,native.HasPosition,native.IsAtEnd,address=native.GetMemoryBase()!=IntPtr.Zero};
    byte[] target=new byte[length+3];int count=native.Read(target,target.Length);values[$"read-{length}"]=new{count,target,native.Position,native.IsAtEnd};
    native.Rewind();values[$"move-{length}"]=new{seek=native.Seek(length+10),native.Position,move=native.Move(-2),position2=native.Position};
}
using(var writer=new SKDynamicMemoryWStream())
{
    Check(writer.Write8(123)&&writer.Write16(0xABCD)&&writer.Write32(0x12345678)&&writer.WriteBool(true)&&writer.WriteBool(false)&&writer.WriteScalar(1.25f),"primitives written");
    foreach(uint value in new uint[]{0,1,253,254,65535,65536,uint.MaxValue}){writer.WritePackedUInt32(value);values[$"packed-size-{value}"]=SKWStream.GetSizeOfPackedUInt32(value);}
    writer.WriteText("abc\0def");writer.NewLine();writer.WriteHexAsText(0xABCDEF,8);writer.NewLine();writer.WriteDecimalAsTest(-123);writer.NewLine();writer.WriteBigDecimalAsText(-123,8);writer.NewLine();writer.WriteScalarAsText(1.25e-7f);
    using var snapshot=writer.CopyToData();values["writer-bytes"]=snapshot.ToArray();values["writer-length"]=writer.BytesWritten;
    using var data=writer.DetachAsData();Check(writer.BytesWritten==0,"detach clears writer");writer.Write8(9);Check(data.Size>1&&data.Span[0]==123,"detach owns data");
    using var reader=new SKMemoryStream(data);values["primitive-read"]=new{a=reader.ReadByte(),b=reader.ReadUInt16(),c=reader.ReadUInt32(),d=reader.ReadBool(),e=reader.ReadBool(),f=reader.ReadUInt32()};
    using var copy=new MemoryStream();writer.CopyTo(copy);values["writer-copy"]=copy.ToArray();
}
var pMemory=Marshal.AllocHGlobal(8);var releases=new int[1];
using(var root=SKData.Create(pMemory,8,(pointer,context)=>{((int[])context)[0]++;Marshal.FreeHGlobal(pointer);},releases))
{
    root.Span.Fill(17);using var child=root.Subset(2,3);root.Dispose();Check(releases[0]==0,"subset retains external owner");Check(child.Span[0]==17,"external subset valid");child.Dispose();Check(releases[0]==1,"one release callback");
}
values["release-count"]=releases[0];
using(var source=new MemoryStream(new byte[]{4,5,6,7}))using(var native=new SKManagedStream(source))
{
    using var destination=new SKDynamicMemoryWStream();values["managed-copy-count"]=native.CopyTo(destination);using var result=destination.DetachAsData();values["managed-copy-bytes"]=result.ToArray();
}
using(var shortStream=new MemoryStream(new byte[]{1,2}))values["short-data"]=SKData.Create(shortStream,3) is null;
string path=Path.Combine(output,"stream.bin");using(var file=new SKFileWStream(path)){Check(file.IsValid,"file writer");file.Write32(0x12345678);}
using(var file=SKFileStream.OpenStream(path)){Check(file is not null,"file reader");values["file-read"]=file!.ReadUInt32();}
values["missing-file"]=SKFileStream.OpenStream(Path.Combine(output,"nonexistent.bin")) is null;
foreach(var type in Enum.GetValues<SKColorType>())
{
    var imageInfo=new SKImageInfo(7,9,type);values["format-"+type]=new[]{type.GetBytesPerPixel(),type.GetBitShiftPerPixel(),imageInfo.BytesSize,imageInfo.BitsPerPixel,(int)type.GetAlphaType(SKAlphaType.Opaque),(int)type.GetAlphaType(SKAlphaType.Premul),(int)type.GetAlphaType(SKAlphaType.Unpremul),(int)type.ToGlSizedFormat()};
}
foreach(var pg in Enum.GetValues<SKPixelGeometry>())values["pixel-geometry-"+pg]=new[]{pg.IsRgb(),pg.IsBgr(),pg.IsHorizontal(),pg.IsVertical()};
var affine=SKMatrix.CreateScaleTranslation(2,3,4,5);var affineValues=new float[9];affine.GetValues(affineValues);values["matrix-affine"]=new SKMatrix(affineValues).Invert().Values;
var options=new JsonSerializerOptions{WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,options));Console.WriteLine($"PASS {assertions} foundation checks, {values.Count} comparison cases");
