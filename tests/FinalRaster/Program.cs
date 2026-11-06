using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);int checks=0;var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));
void Check(bool b){checks++;if(!b)throw new Exception("Final raster assertion "+checks);}
byte[] Read(SKImage image){using var bitmap=new SKBitmap(new SKImageInfo(image.Width,image.Height,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(bitmap.Info,bitmap.GetPixels(),bitmap.RowBytes));return bitmap.GetPixelSpan().ToArray();}
void Capture(byte[] data,string kind,int scene,int transform=0){cases.Add(new{kind,scene,transform,offset=pixels.Position,length=data.Length});pixels.Write(data);}
for(int method=0;method<4;method++)
{
 using var recorder=new SKPictureRecorder();var rc=recorder.BeginRecording(new(0,0,40,32));using(var p=new SKPaint{Color=new(240,40,70,190)})rc.DrawRect(3,4,20,15,p);using(var p=new SKPaint{Color=new(30,90,220,150)})rc.DrawRect(12,10,23,18,p);using var picture=recorder.EndRecording();using var paint=new SKPaint{Color=SKColors.White.WithAlpha(170)};
 var matrix=SKMatrix.CreateTranslation(6,5);using var image=method switch{0=>SKImage.FromPicture(picture,new(64,48)),1=>SKImage.FromPicture(picture,new(64,48),matrix),2=>SKImage.FromPicture(picture,new(64,48),paint),_=>SKImage.FromPicture(picture,new(64,48),matrix,paint)};
 picture.Dispose();paint.Color=SKColors.Red;paint.Dispose();values[$"picture-{method}-info"]=new{image.Width,image.Height,color=image.ColorType.ToString(),alpha=image.AlphaType.ToString(),srgb=image.ColorSpace?.IsSrgb,lazy=image.IsLazyGenerated};using var peek=image.PeekPixels();values[$"picture-{method}-peek"]=peek is not null;using var shared=image.ToRasterImage(false);values[$"picture-{method}-shared"]=new{lazy=shared.IsLazyGenerated,sameId=shared.UniqueId==image.UniqueId,sameObject=ReferenceEquals(shared,image)};var first=Read(image);Check(first.AsSpan().SequenceEqual(Read(image)));Capture(first,"picture-image",method);using var raster=image.ToRasterImage(true);Check(!raster.IsLazyGenerated);Check(first.AsSpan().SequenceEqual(Read(raster)));Check(first.AsSpan().SequenceEqual(Read(shared)));
}
foreach(int size in new[]{0,-1}){using var rec=new SKPictureRecorder();rec.BeginRecording(new(0,0,10,10));using var pic=rec.EndRecording();using var image=SKImage.FromPicture(pic,new(size,10));values[$"picture-size-{size}"]=image is null;}
int scene=0;
foreach(var format in new[]{SKColorType.Rgba8888,SKColorType.Bgra8888})foreach(var alpha in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul})foreach(var compression in new[]{SKWebpEncoderCompression.Lossless,SKWebpEncoderCompression.Lossy})foreach(float quality in new[]{25f,75.5f,100f})
{
 using var bitmap=new SKBitmap(new SKImageInfo(32,24,format,alpha),136);for(int y=0;y<24;y++)for(int x=0;x<32;x++)bitmap.SetPixel(x,y,new((byte)(x*7),(byte)(y*9),(byte)(x*3+y*5),(byte)((x+y)*13%256)));
 using var map=bitmap.PeekPixels();var options=new SKWebpEncoderOptions(compression,quality);using var data=map.Encode(options);Check(data is not null&&data.Size>16);using var decoded=SKImage.FromEncodedData(data!);Check(decoded.Width==32&&decoded.Height==24);Capture(Read(decoded),compression==SKWebpEncoderCompression.Lossless?"webp-lossless":"webp-lossy",scene++);
 using var stream=new MemoryStream();Check(map.Encode(stream,options));using var ws=new SKDynamicMemoryWStream();Check(map.Encode(ws,options));using var copy=ws.CopyToData();Check(copy.AsSpan().SequenceEqual(stream.ToArray()));File.WriteAllBytes(Path.Combine(output,$"webp-{scene}.webp"),data!.ToArray());
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"PASS {checks} checks, {cases.Count} images and {values.Count} metadata records");
