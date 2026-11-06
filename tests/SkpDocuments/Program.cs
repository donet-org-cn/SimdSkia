using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);int checks=0;var cases=new List<object>();var values=new Dictionary<string,object?>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));
void Check(bool b){checks++;if(!b)throw new Exception("SKP check "+checks);}
void Capture(SKPicture pic,int scene,string kind){using var b=new SKBitmap(new SKImageInfo(80,64,SKColorType.Rgba8888,SKAlphaType.Premul));using(var c=new SKCanvas(b)){c.Clear(SKColors.White);c.DrawPicture(pic);}var bytes=b.GetPixelSpan();cases.Add(new{scene,kind,transform=0,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);}
for(int scene=0;scene<5;scene++)
{
 using var rec=new SKPictureRecorder();var c=rec.BeginRecording(new(0,0,80,64));c.Clear(SKColors.White);using var paint=new SKPaint{Color=new(220,40,70,180),IsAntialias=true};
 if(scene==0){c.DrawRect(5,7,35,24,paint);paint.Color=SKColors.Blue;c.DrawRect(30,20,38,28,paint);}
 if(scene==1){c.Save();c.Translate(6,5);c.RotateDegrees(8);using var path=new SKPath();path.MoveTo(4,9);path.CubicTo(30,-5,35,70,65,20);path.LineTo(58,50);path.QuadTo(4,50,4,9);path.Close();c.ClipRect(new(0,0,58,48));c.DrawPath(path,paint);c.Restore();}
 if(scene==2){using var opacity=new SKPaint{Color=SKColors.White.WithAlpha(130)};c.SaveLayer(opacity);c.DrawRoundRect(new(4,5,70,53),8,6,paint);paint.Color=SKColors.Blue;c.DrawRect(30,20,38,28,paint);c.Restore();}
 if(scene==3){using var b=new SKBitmap(8,6);for(int y=0;y<6;y++)for(int x=0;x<8;x++)b.SetPixel(x,y,new((byte)(x*30),(byte)(y*40),70,(byte)(80+x*20)));using var image=SKImage.FromBitmap(b);c.DrawImage(image,new SKRect(2,1,7,5),new SKRect(5,7,70,52),new SKSamplingOptions(SKFilterMode.Nearest),paint);}
 if(scene==4){using var path=new SKPath{FillType=SKPathFillType.EvenOdd};path.AddCircle(36,28,23);path.AddCircle(36,28,11);c.ClipPath(path);c.DrawRect(0,0,80,64,paint);}
 using var picture=rec.EndRecording();Capture(picture,scene,"direct");using var data=picture.Serialize();Check(data.Size>50);Check(data.AsSpan()[..8].SequenceEqual("skiapict"u8));File.WriteAllBytes(Path.Combine(output,$"scene-{scene}.skp"),data.ToArray());
 using var stream=new MemoryStream();picture.Serialize(stream);Check(stream.CanWrite);using var ws=new SKDynamicMemoryWStream();picture.Serialize(ws);using var wdata=ws.CopyToData();Check(data.AsSpan().SequenceEqual(wdata.AsSpan()));Check(data.AsSpan().SequenceEqual(stream.ToArray()));
 for(int method=0;method<5;method++){using var ms=new MemoryStream(data.ToArray());using var ss=new SKMemoryStream(data);using var read=method switch{0=>SKPicture.Deserialize(data),1=>SKPicture.Deserialize(data.AsSpan()),2=>SKPicture.Deserialize(ms),3=>SKPicture.Deserialize(ss),_=>SKPicture.Deserialize(data.Data,(int)data.Size)};Check(read is not null);values[$"cull-{scene}-{method}"]=read!.CullRect.ToString();Capture(read,scene,"roundtrip-"+method);}
}
if(args.Length>1)foreach(var file in Directory.GetFiles(args[1],"*.skp").Order())
{using var stream=File.OpenRead(file);using var picture=SKPicture.Deserialize(stream);Check(picture is not null);int scene=int.Parse(Path.GetFileNameWithoutExtension(file).Split('-')[1]);Capture(picture!,scene,"cross");}
Check(SKPicture.Deserialize(ReadOnlySpan<byte>.Empty) is null);File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"PASS {checks} SKP checks, {cases.Count} draw/readback cases");
