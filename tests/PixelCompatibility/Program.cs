using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;

string output=args.Length>0?args[0]:"artifacts/pixel-check";Directory.CreateDirectory(output);
var values=new SortedDictionary<string,object?>();var transfers=new List<object>();int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
float[] Floats(SKColorF c)=>[c.Red,c.Green,c.Blue,c.Alpha];
object Error(Action action){try{action();return "none";}catch(Exception e){return new{type=e.GetType().Name,parameter=(e as ArgumentException)?.ParamName};}}
var random=new Random(48291);
for(int i=0;i<320;i++)
{
    var c=new SKColorF((float)random.NextDouble()*1.5f-.25f,(float)random.NextDouble(),(float)random.NextDouble(),(float)random.NextDouble());
    c.ToHsl(out var h,out var s,out var l);c.ToHsv(out var hh,out var ss,out var v);
    values[$"color-{i}"]=new {rgba=Floats(c),clamp=Floats(c.Clamp()),packed=(uint)(SKColor)c,h,s,l,hh,ss,v,hsl=Floats(SKColorF.FromHsl(h,s,l,c.Alpha)),hsv=Floats(SKColorF.FromHsv(hh,ss,v,c.Alpha)),converted=Floats((SKColorF)(SKColor)c),with=Floats(c.WithRed(.2f).WithGreen(.3f).WithBlue(.4f).WithAlpha(.5f)),equal=c==new SKColorF(c.Red,c.Green,c.Blue,c.Alpha)};
}
using(var bitmap=new SKBitmap(8,5))
using(var canvas=new SKCanvas(bitmap))
{
    using(var auto=new SKAutoCanvasRestore(canvas)){canvas.Translate(3,4);canvas.Save();Check(canvas.SaveCount==3,"save helper");auto.Restore();auto.Restore();Check(canvas.SaveCount==1,"restore once");}
    using(var auto=new SKAutoCanvasRestore(canvas,false)){canvas.Save();}Check(canvas.SaveCount==1,"doSave false");
    using var none=new SKAutoCanvasRestore(null!);
    values["bitmap-empty-state"]=new{bitmap.ReadyToDraw,bitmap.DrawsNothing,bitmap.IsNull,bitmap.IsImmutable};
    bitmap.SetImmutable();values["immutable"]=bitmap.IsImmutable;
    bitmap.Reset();values["bitmap-reset"]=new{bitmap.ReadyToDraw,bitmap.DrawsNothing,bitmap.IsNull,bitmap.IsImmutable,bitmap.Width,bitmap.Height};
}
SKColorType[] formats=[SKColorType.Rgba8888,SKColorType.Bgra8888];SKAlphaType[] alphas=[SKAlphaType.Premul,SKAlphaType.Unpremul,SKAlphaType.Opaque];
foreach(var sf in formats)foreach(var sa in alphas)
{
    var info=new SKImageInfo(11,7,sf,sa);const int stride=56,prefix=32;int length=(info.Height-1)*stride+info.RowBytes;
    byte[] data=Enumerable.Repeat((byte)0xAD,prefix+length+32).ToArray();Fill(data.AsSpan(prefix),info,stride);
    var handle=GCHandle.Alloc(data,GCHandleType.Pinned);
    try
    {
        using var src=new SKPixmap(info,handle.AddrOfPinnedObject()+prefix,stride);
        values[$"info-{sf}-{sa}"]=new{src.Width,src.Height,src.RowBytes,src.BytesSize,src.BytesSize64,src.BytesPerPixel,src.BitShiftPerPixel,opaque=src.ComputeIsOpaque()};
        values[$"span-padding-{sf}-{sa}"]=new{bytes=src.GetPixelSpan().Length,typed=src.GetPixelSpan<uint>().Length,offset=src.GetPixelSpan(2,1).Length};
        for(int i=0;i<10;i++)values[$"read-color-{sf}-{sa}-{i}"]=new{packed=(uint)src.GetPixelColor(i,3),rgba=Floats(src.GetPixelColorF(i,3)),alpha=src.GetPixelAlpha(i,3)};
        using(var subset=src.ExtractSubset(new(-2,1,6,10)))
        {
            if(subset is null)throw new Exception("subset success");checks++;values[$"subset-{sf}-{sa}"]=new{subset.Width,subset.Height,subset.RowBytes,address=(long)(subset.GetPixels()-src.GetPixels()),pixel=(uint)subset.GetPixelColor(2,1)};
            using var retag=subset.WithColorType(sf==SKColorType.Rgba8888?SKColorType.Bgra8888:SKColorType.Rgba8888);values[$"retag-{sf}-{sa}"]=(uint)retag.GetPixelColor(2,1);
        }
        foreach(var df in formats)foreach(var da in alphas)
        foreach(var offset in new (int X,int Y)[]{(0,0),(2,1),(-2,-1),(10,6),(11,0),(int.MinValue,0)})
        {
            var di=new SKImageInfo(9,5,df,da);const int ds=48;int dl=(di.Height-1)*ds+di.RowBytes;
            byte[] dest=Enumerable.Repeat((byte)0xB3,prefix+dl+32).ToArray();var dh=GCHandle.Alloc(dest,GCHandleType.Pinned);
            try
            {
                using var dst=new SKPixmap(di,dh.AddrOfPinnedObject()+prefix,ds);bool ok=src.ReadPixels(dst,offset.X,offset.Y);
                string name=$"read-{sf}-{sa}-{df}-{da}-{offset.X}-{offset.Y}";
                File.WriteAllBytes(Path.Combine(output,name+".bin"),dest);transfers.Add(new{name,ok,kind="read",threshold=1});
                dest.AsSpan().Fill(0xB3);ok=src.ReadPixels(di,dh.AddrOfPinnedObject()+prefix,ds,offset.X,offset.Y);name="pointer-"+name;
                File.WriteAllBytes(Path.Combine(output,name+".bin"),dest);transfers.Add(new{name,ok,kind="pointer-read",threshold=1});
            }
            finally{dh.Free();}
        }
        foreach(var mode in new[]{"nearest","linear","mitchell","catmull"})
        {
            using var dst=new SKBitmap(new SKImageInfo(17,4,sf,sa),80);using var view=dst.PeekPixels();
            var sampling=mode switch{"nearest"=>SKSamplingOptions.Default,"linear"=>new SKSamplingOptions(SKFilterMode.Linear),"mitchell"=>new SKSamplingOptions(SKCubicResampler.Mitchell),_=>new SKSamplingOptions(SKCubicResampler.CatmullRom)};
            bool ok=src.ScalePixels(view,sampling);string name=$"scale-{sf}-{sa}-{mode}";
            File.WriteAllBytes(Path.Combine(output,name+".bin"),dst.Bytes);transfers.Add(new{name,ok,kind="scale",threshold=mode=="nearest"?1:3});
        }
        byte[] before=(byte[])data.Clone();Check(src.Erase(new SKColor(100,60,20,123),new(2,1,8,5)),"erase intersect");
        values[$"erased-color-{sf}-{sa}"]=new{packed=(uint)src.GetPixelColor(3,2),rgba=Floats(src.GetPixelColorF(3,2)),alpha=src.GetPixelAlpha(3,2),opaque=src.ComputeIsOpaque()};
        string erase=$"erase-{sf}-{sa}";File.WriteAllBytes(Path.Combine(output,erase+".bin"),data);transfers.Add(new{name=erase,ok=true,kind="erase",threshold=1});
        for(int i=0;i<prefix;i++)Check(data[i]==before[i],"leading guard");for(int i=prefix+length;i<data.Length;i++)Check(data[i]==before[i],"trailing guard");
        Check(!src.Erase(SKColors.Red,new(100,100,110,110)),"empty erase");
    }
    finally{handle.Free();}
}
using(var src=new SKBitmap(new SKImageInfo(8,5,SKColorType.Bgra8888,SKAlphaType.Unpremul),40))
{
    src.Pixels=Enumerable.Range(0,40).Select(i=>new SKColor((byte)(i*6),30,80,(byte)(50+i*5))).ToArray();
    values["bitmap-pixels"]=src.Pixels.Select(c=>(uint)c).ToArray();
    using var copy=src.Copy(SKColorType.Rgba8888);values["copy-colors"]=copy.Pixels.Select(c=>(uint)c).ToArray();
    using var defaultCopy=src.Copy();values["default-copy-stride"]=defaultCopy.RowBytes;
    using var dst=new SKBitmap();Check(src.CopyTo(dst),"copy dst");Check(src.CopyTo(src),"self copy");
    values["copy-state"]=new{dst.Width,dst.Height,dst.RowBytes,dst.IsImmutable};
    using var resized=src.Resize(new SKSizeI(16,10),SKSamplingOptions.Default);Check(resized.Width==16&&resized.Height==10,"resize");
    using var image=SKImage.FromBitmap(src);using var view=dst.PeekPixels();Check(image.ReadPixels(view),"image read");
    values["image-state"]=new{image.Width,image.Height,image.IsAlphaOnly,image.AlphaType};
    using var surface=SKSurface.Create(new SKImageInfo(8,5))!;surface.Canvas.Clear(SKColors.Lime);using var sm=surface.PeekPixels();
    Check(surface.ReadPixels(dst.Info,dst.GetPixels(),dst.RowBytes,0,0),"surface read");values["surface-pixel"]=(uint)dst.GetPixel(2,2);
    using var encoded=sm.Encode(SKEncodedImageFormat.Png,100);using var decoded=SKBitmap.Decode(encoded);values["png-pixel"]=(uint)decoded.GetPixel(2,2);
}
using(var empty=new SKPixmap())values["empty-map"]=new{empty.Width,empty.Height,empty.RowBytes,empty.BytesSize,opaque=empty.ComputeIsOpaque(),empty=empty.GetPixels()==IntPtr.Zero};
using(var b=new SKBitmap(3,2))
using(var map=b.PeekPixels())
using(var image=SKImage.FromBitmap(b))
{
    values["error-copy-null"]=Error(()=>b.CopyTo(null!));values["error-pixels-null"]=Error(()=>b.Pixels=null!);
    values["error-pixels-count"]=Error(()=>b.Pixels=[]);values["error-scale-null"]=Error(()=>map.ScalePixels(null!,SKSamplingOptions.Default));
    values["error-subset-null"]=Error(()=>map.ExtractSubset(null!,new(0,0,1,1)));
    values["error-typed-span"]=Error(()=>{_=map.GetPixelSpan<short>();});
    values["error-peek-null"]=Error(()=>b.PeekPixels(null!));values["error-image-peek-null"]=Error(()=>image.PeekPixels(null!));
    values["span-typed-offset-compact"]=map.GetPixelSpan<uint>(1,1).Length;
    b.Pixels=[new(120,40,200,0),new(120,40,200,1),new(120,40,200,128),new(120,40,200,255),SKColors.Red,SKColors.Blue];values["pixels-alpha-edges"]=b.Pixels.Select(c=>(uint)c).ToArray();
}
#if MANAGED
using(var src=new SKBitmap(19,12))
using(var dst=new SKBitmap(17,13))
using(var from=src.PeekPixels())
using(var to=dst.PeekPixels())
using(var image=SKImage.FromBitmap(src))
using(var surface=SKSurface.Create(src.Info)!)
{
    var address=to.GetPixels();Func<bool>[] operations=[()=>from.ReadPixels(to),()=>from.ReadPixels(to.Info,address,to.RowBytes),
        ()=>from.ScalePixels(to,SKSamplingOptions.Default),()=>from.ScalePixels(to,new SKSamplingOptions(SKFilterMode.Linear)),
        ()=>image.ReadPixels(to.Info,address,to.RowBytes),()=>surface.ReadPixels(to.Info,address,to.RowBytes,0,0)];
    foreach(var operation in operations)
    {
        for(int i=0;i<30;i++)Check(operation(),"warm pixel operation");
        long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)operation();
        Check(GC.GetAllocatedBytesForCurrentThread()==start,"steady pixel operation must allocate no managed bytes");
    }
}
for(int sample=0;sample<360;sample++)
{
    int sw=sample%37+1,sh=sample%11+1,w=sample==359?2049:sample%67+1,h=sample%9+1,ss=sw*4+12,ds=w*4+20;
    byte[] pixels=new byte[(sh-1)*ss+sw*4];random.NextBytes(pixels);
    byte[] scalar=Enumerable.Repeat((byte)91,(h-1)*ds+w*4).ToArray(),vector=(byte[])scalar.Clone();
    SimdSkia.Cpu.PixelTransferKernel.ScaleLinear32(pixels,sw,sh,ss,scalar,w,h,ds,sample%2==0,sample%3==0,false);
    SimdSkia.Cpu.PixelTransferKernel.ScaleLinear32(pixels,sw,sh,ss,vector,w,h,ds,sample%2==0,sample%3==0,true);
    Check(scalar.AsSpan().SequenceEqual(vector),"linear scalar/SIMD sample "+sample);
    byte[] swapScalar=new byte[pixels.Length&~3],swapVector=new byte[swapScalar.Length];
    SimdSkia.Cpu.PixelTransferKernel.SwapRedBlue(pixels.AsSpan(0,swapScalar.Length),swapScalar,false);
    SimdSkia.Cpu.PixelTransferKernel.SwapRedBlue(pixels.AsSpan(0,swapScalar.Length),swapVector,true);
    Check(swapScalar.AsSpan().SequenceEqual(swapVector),"swap scalar/SIMD sample "+sample);
}
using(var source=new SKBitmap(6,4))
{
    source.Erase(SKColors.Red);using var map=source.PeekPixels();using var sub=map.ExtractSubset(new(2,1,5,4));source.Reset();
    Check(map.GetPixelColor(1,1)==SKColors.Red&&sub.GetPixelColor(0,0)==SKColors.Red,"retained reset lease");
    using var retag=sub.WithAlphaType(SKAlphaType.Unpremul);map.Dispose();sub.Dispose();Check(retag.GetPixelColor(0,0)==SKColors.Red,"nested lease");
}
using(var b=new SKBitmap(8,5))
{
    using var map=b.PeekPixels();Fill(map.GetPixelSpan(),b.Info,b.RowBytes);var snapshot=map.GetPixelSpan().ToArray();
    Check(map.ReadPixels(map,0,0)&&map.GetPixelSpan().SequenceEqual(snapshot),"overlap snapshot");
    Check(map.ReadPixels(map,-1,0),"overlap shift");
    for(int y=0;y<5;y++)for(int x=1;x<8;x++)Check(map.GetPixelSpan().Slice(y*b.RowBytes+x*4,4).SequenceEqual(snapshot.AsSpan(y*b.RowBytes+(x-1)*4,4)),"overlap value");
    snapshot=map.GetPixelSpan().ToArray();
    Check(!map.ReadPixels(new SKImageInfo(1,1),IntPtr.Zero,4),"null destination");
    bool thrown=false;try{map.ScalePixels(map,new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.Linear));}catch(NotSupportedException){thrown=true;}
    Check(thrown&&map.GetPixelSpan().SequenceEqual(snapshot),"mipmap fails before mutation");
    map.Dispose();thrown=false;try{_=map.GetPixelSpan();}catch(ObjectDisposedException){thrown=true;}Check(thrown,"disposed view");
}
#endif
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(output,"transfers.json"),JsonSerializer.Serialize(transfers,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks} local checks; {values.Count} metadata cases, {transfers.Count} pixel transfers");

static void Fill(Span<byte> data,SKImageInfo info,int stride)
{
    int ri=info.ColorType==SKColorType.Rgba8888?0:2;
    for(int y=0;y<info.Height;y++)for(int x=0;x<info.Width;x++)
    {
        byte a=info.AlphaType==SKAlphaType.Opaque?(byte)255:(byte)((x*37+y*19)%256),r=(byte)(x*17+y*3),g=(byte)(x*7+y*25),b=(byte)(x*11+y*21);
        if(info.AlphaType==SKAlphaType.Premul){r=(byte)((r*a+127)/255);g=(byte)((g*a+127)/255);b=(byte)((b*a+127)/255);}
        int at=y*stride+x*4;data[at+ri]=r;data[at+1]=g;data[at+2-ri]=b;data[at+3]=a;
    }
}
