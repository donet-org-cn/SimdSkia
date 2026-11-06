using SkiaSharp;
using System.Text.Json;
AppContext.SetSwitch("SimdSkia.Cpu.Alpha8Reference",args.Contains("--alpha-reference"));
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;void Check(bool b){checks++;if(!b)throw new Exception("Alpha check "+checks);}
void Capture(SKBitmap bitmap,string kind,int scene,int transform=0)
{using var copy=new SKBitmap(new SKImageInfo(bitmap.Width,bitmap.Height,SKColorType.Rgba8888,SKAlphaType.Premul));using var map=bitmap.PeekPixels();Check(map.ReadPixels(copy.Info,copy.GetPixels(),copy.RowBytes,0,0));var data=copy.GetPixelSpan();cases.Add(new{kind,scene,transform,offset=pixels.Position,length=data.Length});pixels.Write(data);}
int scene=0;
foreach(var format in new[]{SKColorType.Rgba8888,SKColorType.Bgra8888,SKColorType.Alpha8})foreach(var alpha in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul,SKAlphaType.Opaque})for(int filter=0;filter<7;filter++)
{
 using var bitmap=new SKBitmap(new SKImageInfo(13,9,format,alpha));for(int y=0;y<9;y++)for(int x=0;x<13;x++)bitmap.SetPixel(x,y,new((byte)(x*18),(byte)(y*25),(byte)(x*7+y*11),(byte)((x+y*13)*29%256)));
 if(filter==0){using var copied=bitmap.Copy(SKColorType.Alpha8);values[$"copy-a8-{scene}"]=new{color=copied.ColorType.ToString(),alpha=copied.AlphaType.ToString()};using var view=bitmap.PeekPixels();values[$"source-{scene}"]=new{color=bitmap.ColorType.ToString(),alpha=bitmap.AlphaType.ToString(),bitmap.RowBytes,bitmap.ByteCount,opaque=view.ComputeIsOpaque(),first=bitmap.GetPixel(1,0).ToString()};}
 using var mf=filter switch{1=>SKMaskFilter.CreateGamma(.7f),2=>SKMaskFilter.CreateClip(30,180),>=3=>SKMaskFilter.CreateBlur((SKBlurStyle)(filter-3),1.5f),_=>null};using var paint=new SKPaint{Color=new(255,0,0,0),MaskFilter=mf};using var dst=new SKBitmap(2,2);dst.Erase(SKColors.Blue);
 Check(bitmap.ExtractAlpha(dst,paint,out var offset));values[$"extract-{scene}"]=new{dst.Width,dst.Height,dst.RowBytes,dst.ByteCount,color=dst.ColorType.ToString(),alpha=dst.AlphaType.ToString(),offset.X,offset.Y};Capture(dst,"extract",scene++);
 using var copy=dst.Copy();Check(copy.GetPixel(3,3)==dst.GetPixel(3,3));if(filter==0){Check(offset==SKPointI.Empty);Check(bitmap.ExtractAlpha(bitmap));Capture(bitmap,"self-extract",scene-1);}
}
using(var empty=new SKBitmap())using(var dst=new SKBitmap(2,2)){dst.Erase(SKColors.Red);values["empty"]=empty.ExtractAlpha(dst,out var offset);Check(dst.GetPixel(0,0)==SKColors.Red);}
for(int blend=0;blend<4;blend++)for(int shader=0;shader<2;shader++)
{
 using var bitmap=new SKBitmap(new SKImageInfo(32,24,SKColorType.Alpha8,SKAlphaType.Premul),37);using(var canvas=new SKCanvas(bitmap))
 {canvas.Clear(new SKColor(80,90,100,60));using var gradient=shader==0?null:SKShader.CreateLinearGradient(new(0,0),new(32,24),[new SKColor(230,40,70,50),new SKColor(40,80,220,220)],SKShaderTileMode.Clamp);using var paint=new SKPaint{Color=new(40,80,220,170),Shader=gradient,BlendMode=new[]{SKBlendMode.SrcOver,SKBlendMode.Src,SKBlendMode.DstIn,SKBlendMode.Multiply}[blend]};canvas.DrawRect(3,4,20,15,paint);bitmap.GetPixelSpan()[0]=130;canvas.DrawRect(0,0,2,2,paint);}
 Capture(bitmap,"a8-target",blend,shader);using var image=SKImage.FromBitmap(bitmap);values[$"image-alpha-{blend}-{shader}"]=image.IsAlphaOnly;
 for(int sampling=0;sampling<3;sampling++)for(int material=0;material<3;material++)
 {using var target=new SKBitmap(64,48);using var c=new SKCanvas(target);c.Clear(new SKColor(30,40,50));c.Translate(2,3);using var gradient=material==2?SKShader.CreateLinearGradient(new(0,0),new(64,48),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp):null;using var paint=material==0?null:new SKPaint{Color=new(220,60,100,190),Shader=gradient};var sample=sampling==2?new SKSamplingOptions(SKCubicResampler.Mitchell):new SKSamplingOptions((SKFilterMode)sampling);c.DrawImage(image,new SKRect(3,4,53,39),sample,paint);Capture(target,"a8-image",(blend*2+shader)*9+sampling*3+material);}
 using(var rgba=new SKBitmap(new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul))){using var map=bitmap.PeekPixels();Check(map.ReadPixels(rgba.Info,rgba.GetPixels(),rgba.RowBytes,0,0));using var back=rgba.Copy(SKColorType.Alpha8);Check(back.GetPixel(10,10)==bitmap.GetPixel(10,10));}
 using(var resized=bitmap.Resize(new SKImageInfo(47,31,SKColorType.Alpha8,SKAlphaType.Premul),new SKSamplingOptions(SKFilterMode.Linear)))Capture(resized,"a8-scale",blend,shader);
}
for(int factory=0;factory<3;factory++)foreach(var tile in new[]{SKShaderTileMode.Clamp,SKShaderTileMode.Repeat,SKShaderTileMode.Mirror,SKShaderTileMode.Decal})for(int sampling=0;sampling<2;sampling++)for(int transform=0;transform<2;transform++)
{
 using var mask=new SKBitmap(new SKImageInfo(9,7,SKColorType.Alpha8,SKAlphaType.Premul));for(int y=0;y<7;y++)for(int x=0;x<9;x++)mask.GetPixelSpan()[y*mask.RowBytes+x]=(byte)((x*29+y*37)%256);using var image=SKImage.FromBitmap(mask);var local=transform==0?SKMatrix.Identity:SKMatrix.CreateScale(1.7f,1.3f);var sample=new SKSamplingOptions((SKFilterMode)sampling);using var shader=factory switch{0=>mask.ToShader(tile,tile,sample,local),1=>image.ToShader(tile,tile,sample,local),_=>image.ToRawShader(tile,tile,sample,local)};mask.Dispose();image.Dispose();using var b=new SKBitmap(36,28);using var c=new SKCanvas(b);c.Clear(new(30,50,80,180));c.Translate(2,3);using var paint=new SKPaint{Shader=shader,Color=new(230,50,90,170)};c.DrawRect(-2,-3,19,28,paint);paint.Color=new(40,180,220,170);c.DrawRect(17,-3,19,28,paint);Capture(b,"a8-shader",(factory*4+(int)tile)*2+sampling,transform);
}
foreach(var blend in Enum.GetValues<SKBlendMode>())foreach(bool aa in new[]{false,true})for(int shape=0;shape<2;shape++)
{
 using var bitmap=new SKBitmap(new SKImageInfo(33,25,SKColorType.Alpha8,SKAlphaType.Premul),39);using var c=new SKCanvas(bitmap);c.Clear(new(0,0,0,83));using var p=new SKPaint{Color=new(120,30,220,173),BlendMode=blend,IsAntialias=aa};using var path=new SKPath();if(shape==0){for(int y=0;y<24;y+=2)for(int x=0;x<32;x+=2)path.AddRect(new(x,y,x+1,y+1));}else{path.MoveTo(2.2f,2.8f);path.CubicTo(20,-7,18,30,30.1f,19);path.LineTo(5.1f,20);path.Close();}c.ClipRect(new(1,1,32,24));c.DrawPath(path,p);Capture(bitmap,"a8-solid-path",(int)blend*2+(aa?1:0),shape);
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"PASS {checks} checks; {cases.Count} A8/extraction images; {values.Count} metadata records");
