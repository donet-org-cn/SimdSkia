using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text.Json;

string output=args.Length>0?args[0]:"artifacts/compat-expansion/managed";Directory.CreateDirectory(output);
int checks=0;void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
void Capture(SKBitmap bitmap,string name)=>File.WriteAllBytes(Path.Combine(output,name+".bgra"),bitmap.GetPixelSpan().ToArray());
var results=new Dictionary<string,object>();
using(var bitmap=new SKBitmap(new SKImageInfo(7,3,SKColorType.Bgra8888,SKAlphaType.Premul),40))
{
    var p=bitmap.GetPixels(out var length);Check(p!=IntPtr.Zero&&length.ToInt64()==108,"Pointer length/stride");
    Marshal.WriteInt32(p,40+4,unchecked((int)0xff0b0703));GC.Collect(2,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();
    Check(p==bitmap.GetPixels(),"Pointer moved after compacting GC");Check(bitmap.GetPixel(1,1)==new SKColor(11,7,3,255),"Native write was not visible");
    bitmap.TryAllocPixels(new SKImageInfo(4,4));Check(bitmap.GetPixels()!=IntPtr.Zero,"Reallocated pointer");
}
using(var empty=new SKBitmap())Check(empty.GetPixels()==IntPtr.Zero,"Empty bitmap pointer");
var rect=new SKRect(2,3,10,13);rect.Inflate(2,3);Check(rect==new SKRect(0,0,12,16),"Inflate");
using(var rounded=new SKRoundRect(new SKRect(0,0,100,40),40,30))
{
    results["roundRect"]=new{type=rounded.Type.ToString(),radii=rounded.Radii.Select(r=>new[]{r.X,r.Y}).ToArray(),contains=rounded.Contains(new SKRect(35,10,65,30))};
    Check(rounded.Radii.All(r=>r.X<=50&&r.Y<=20),"Radius clamping");
}
using(var bitmap=new SKBitmap(64,48))using(var canvas=new SKCanvas(bitmap))using(var paint=new SKPaint{Color=SKColors.Red})
{
    canvas.Clear(SKColors.White);int saved=canvas.Save();
    using var r=new SKRoundRect(new SKRect(4,4,60,44),8);canvas.ClipRoundRect(r,antialias:false);canvas.Clear(SKColors.Blue);
    Check(bitmap.GetPixel(4,4)==SKColors.White&&bitmap.GetPixel(32,24)==SKColors.Blue,"Rounded clip/Clear");canvas.RestoreToCount(saved);
    Capture(bitmap,"round-clip");canvas.Clear(SKColors.White);
    using(var opacity=new SKPaint{Color=new SKColor(0,0,0,128)}){int count=canvas.SaveLayer(opacity);canvas.DrawRect(5,5,30,30,paint);canvas.DrawRect(15,5,30,30,paint);canvas.RestoreToCount(count);}
    Check(bitmap.GetPixel(10,10)==bitmap.GetPixel(20,10),"Group alpha applied twice in overlap");results["layerPixel"]=bitmap.GetPixel(20,10).ToString();Capture(bitmap,"group-alpha");
    canvas.Clear(SKColors.White);canvas.Save();canvas.Translate(4,3);canvas.ClipRect(new SKRect(0,0,24,20));canvas.SaveLayer(new SKRect(4,4,6,6),null);canvas.Clear(SKColors.Red);canvas.Restore();canvas.Restore();
    Check(bitmap.GetPixel(9,8)==SKColors.Red&&bitmap.GetPixel(30,30)==SKColors.White,"Layer bounds/transform/restore clip");Capture(bitmap,"layer-clip");
    canvas.Clear(SKColors.White);canvas.Save();canvas.ClipRect(new SKRect(10,10,20,20),SKClipOperation.Difference);canvas.Clear(SKColors.Blue);canvas.Restore();
    Check(bitmap.GetPixel(15,15)==SKColors.White&&bitmap.GetPixel(0,0)==SKColors.Blue,"Difference clip");
    canvas.Clear(SKColors.White);using(var sourcePaint=new SKPaint{BlendMode=SKBlendMode.Src}){canvas.SaveLayer(new SKRect(8,8,20,20),sourcePaint);canvas.Clear(SKColors.Red);canvas.Restore();}
    results["layerSourceOutside"]=bitmap.GetPixel(0,0).ToString();
    Check(bitmap.GetPixel(10,10)==SKColors.Red&&bitmap.GetPixel(0,0)==SKColors.Red,"Src layer bounds hint expands to parent clip");Capture(bitmap,"layer-source");
    using(var clearPaint=new SKPaint{BlendMode=SKBlendMode.Clear}){canvas.SaveLayer(new SKRect(8,8,20,20),clearPaint);canvas.Restore();}
    Check(bitmap.GetPixel(0,0).Alpha==0,"Clear layer bounds hint expands to parent clip");
    canvas.Clear(SKColors.White);paint.IsAntialias=true;canvas.DrawRoundRect(new SKRect(5.25f,5.5f,55.75f,40.25f),8,6,paint);Capture(bitmap,"round-aa");
    canvas.Clear(SKColors.White);canvas.Save();canvas.ClipRoundRect(r,antialias:true);using var layerPaint=new SKPaint{Color=new SKColor(0,0,0,128)};canvas.SaveLayer(layerPaint);canvas.Clear(SKColors.Red);canvas.Restore();canvas.Restore();Capture(bitmap,"layer-aa-clip");
}
foreach(var file in new[]{"disposal.gif","interlaced.gif"})
{
    using var codec=SKCodec.Create(Path.Combine(AppContext.BaseDirectory,"Fixtures",file))??throw new Exception("GIF create "+file);
    results[file]=new{codec.FrameCount,codec.RepetitionCount,Info=new{codec.Info.Width,codec.Info.Height,Alpha=codec.Info.AlphaType.ToString()},frames=codec.FrameInfo.Select(f=>new{f.RequiredFrame,f.Duration,f.FullyRecieved,Alpha=f.AlphaType.ToString(),f.HasAlphaWithinBounds,Disposal=f.DisposalMethod.ToString(),Blend=f.Blend.ToString(),Rect=f.FrameRect.ToString()}).ToArray()};
    using var bitmap=new SKBitmap(codec.Info.WithAlphaType(SKAlphaType.Premul));
    for(int i=0;i<codec.FrameCount;i++){Check(codec.GetPixels(bitmap.Info,bitmap.GetPixels(),new SKCodecOptions(i))==SKCodecResult.Success,"GIF decode frame "+i);Capture(bitmap,file+"-"+i);}
    Check(codec.GetPixels(bitmap.Info,bitmap.GetPixels(),new SKCodecOptions(0))==SKCodecResult.Success,"Random frame rewind");Capture(bitmap,file+"-rewind");
    var invalid=codec.GetPixels(bitmap.Info,bitmap.GetPixels(),new SKCodecOptions(codec.FrameCount));results[file+"-invalidFrame"]=invalid.ToString();Check(invalid!=SKCodecResult.Success,"Invalid GIF frame index");
}
if(OperatingSystem.IsWindows()&&File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"msyh.ttc")))
{
    using var face=SKTypeface.FromFamilyName("Microsoft YaHei");Check(face is not null&&face.GetGlyph(0x4e2d)!=0,"CJK family lookup");
    using var bold=SKTypeface.FromFamilyName("Microsoft YaHei",SKFontStyle.Bold);Check(bold.IsBold,"Bold family lookup");results["family"]=face!.FamilyName;
    using var font=new SKFont(face,24){Hinting=SKFontHinting.None,Subpixel=true,BaselineSnap=false,LinearMetrics=true,Edging=SKFontEdging.Antialias};using var bitmap=new SKBitmap(180,50);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);using var p=new SKPaint();canvas.DrawText("Hello \u4e2d\u6587",5,32,font,p);Capture(bitmap,"text");
}
using(var stream=new MemoryStream())
{
    using(var document=SKDocument.CreatePdf(stream,new SKDocumentPdfMetadata(144f){Title="SimdSkia \u4e2d\u6587"}))
    {
        for(int page=0;page<2;page++){var canvas=document.BeginPage(96+page*8,64);canvas.Clear(SKColors.White);using var paint=new SKPaint{Color=SKColors.Red};canvas.DrawRect(10,10,20,20,paint);document.EndPage();}
        document.Close();
    }
    Check(stream.CanWrite,"PDF must not close caller-owned stream");File.WriteAllBytes(Path.Combine(output,"pages.pdf"),stream.ToArray());Check(stream.Length>100,"PDF output");
}
#if MANAGED
{
    using var bitmap=new SKBitmap(3,3);var pointer=bitmap.GetPixels();using var canvas=new SKCanvas(bitmap);
    bitmap.Dispose();canvas.Clear(SKColors.Green);Check(pointer!=IntPtr.Zero,"Retained canvas storage");
    bool rejected=false;try{bitmap.GetPixels();}catch(ObjectDisposedException){rejected=true;}Check(rejected,"Disposed bitmap pointer rejected");
    using var bytes=SKData.CreateCopy(new byte[]{0,1,2,3});Check(SKCodec.Create(bytes) is null,"Invalid encoded input must not succeed");
    using var stream=new MemoryStream();bool pdfA=false;try{using var doc=SKDocument.CreatePdf(stream,new SKDocumentPdfMetadata(72f){PdfA=true});}catch(NotSupportedException){pdfA=true;}
    Check(pdfA&&stream.Length==0,"Unsupported PDF/A must fail before writing");
    using var document=SKDocument.CreatePdf(stream);var pageCanvas=document.BeginPage(12,12);document.EndPage();
    bool ended=false;try{pageCanvas.Clear();}catch(ObjectDisposedException){ended=true;}Check(ended,"Ended PDF page canvas is disposed");
    document.Abort();bool aborted=false;try{document.BeginPage(12,12);}catch(InvalidOperationException){aborted=true;}Check(aborted&&stream.CanWrite,"Aborted document rejects new pages but preserves caller stream");
    using var badGif=SKData.CreateCopy("GIF89a"u8);Check(SKCodec.Create(badGif) is null,"Truncated GIF header cannot succeed");
    using var imageSource=new SKBitmap(2,2);using var image=SKImage.FromBitmap(imageSource);using var imageTarget=new SKBitmap(4,4);using var imageCanvas=new SKCanvas(imageTarget);
    bool mipmap=false;try{imageCanvas.DrawImage(image,new SKRect(0,0,4,4),new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.Linear));}catch(NotSupportedException){mipmap=true;}Check(mipmap,"Unsupported mipmaps must not be silently ignored");
    byte[] payload=Enumerable.Range(0,150000).Select(i=>(byte)(i*17)).ToArray();var encoded=SimdSkia.Imaging.ManagedZlib.Encode(payload);
    Check(SimdSkia.Imaging.ManagedZlib.DecodeBounded(encoded).AsSpan().SequenceEqual(payload),"Unknown-length managed zlib growth");
    bool budget=false;try{SimdSkia.Imaging.ManagedZlib.DecodeBounded(encoded,1000);}catch(NotSupportedException){budget=true;}Check(budget,"PDF inflation budget");
    encoded[^1]^=1;bool checksum=false;try{SimdSkia.Imaging.ManagedZlib.DecodeBounded(encoded);}catch(InvalidDataException){checksum=true;}Check(checksum,"PDF zlib checksum");
}
#endif
#if NATIVE_AOT
Check(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported,"Native AOT runtime");
File.WriteAllText(Path.Combine(output,"result.json"),"{\"checks\":"+checks+",\"nativeAot\":true}");
#else
results["checks"]=checks;File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
#endif
Console.WriteLine($"PASS {checks} compatibility expansion checks; {output}");
