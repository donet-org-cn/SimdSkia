using SkiaSharp;
using System.Xml.Linq;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);int checks=0;var values=new Dictionary<string,object?>();void Check(bool b){checks++;if(!b)throw new Exception("Vector document assertion "+checks);}
for(int scene=0;scene<7;scene++)
{
 var bounds=scene==6?new SKRect(10,20,90,84):new SKRect(0,0,80,64);using var stream=new MemoryStream();using(var canvas=SKSvgCanvas.Create(bounds,stream))
 {
  values[$"matrix-{scene}"]=canvas.TotalMatrix.Values;values[$"clip-{scene}"]=canvas.DeviceClipBounds.ToString();canvas.Clear(SKColors.White);using var paint=new SKPaint{Color=new(220,40,70,180),IsAntialias=true};
  if(scene==0||scene==6){canvas.DrawRect(5,7,35,24,paint);paint.Color=SKColors.Blue;canvas.DrawRect(30,20,38,28,paint);}
  if(scene==1){using var path=new SKPath();path.MoveTo(4,9);path.CubicTo(30,-5,35,70,74,20);path.LineTo(65,56);path.QuadTo(4,50,4,9);path.Close();canvas.DrawPath(path,paint);}
  if(scene==2){canvas.Translate(6,5);canvas.RotateDegrees(8);using var gradient=SKShader.CreateLinearGradient(new(0,0),new(60,40),[SKColors.Red,SKColors.Green,SKColors.Blue],new[]{0f,.3f,1f},SKShaderTileMode.Mirror,SKMatrix.CreateScale(.9f,1.1f));paint.Shader=gradient;canvas.DrawRoundRect(new(3,4,63,49),8,7,paint);}
  if(scene==3){canvas.Save();canvas.ClipRect(new(7,6,65,53));canvas.DrawCircle(35,30,28,paint);canvas.Save();using var hole=new SKPath();hole.AddRect(new(20,17,50,35));canvas.ClipPath(hole,SKClipOperation.Difference);paint.Color=SKColors.Blue;canvas.DrawRect(0,0,80,64,paint);canvas.Restore();canvas.Restore();}
  if(scene==4){using var opacity=new SKPaint{Color=SKColors.White.WithAlpha(130)};canvas.SaveLayer(opacity);canvas.DrawRect(6,8,42,32,paint);paint.Color=SKColors.Blue;canvas.DrawRect(26,20,38,28,paint);canvas.Restore();}
  if(scene==5){using var bitmap=new SKBitmap(8,6);for(int y=0;y<6;y++)for(int x=0;x<8;x++)bitmap.SetPixel(x,y,new((byte)(x*30),(byte)(y*40),70,(byte)(80+x*20)));using var image=SKImage.FromBitmap(bitmap);canvas.DrawImage(image,new SKRect(2,1,7,5),new SKRect(5,7,70,52),new SKSamplingOptions(SKFilterMode.Nearest),paint);}
 }
 Check(stream.CanWrite);var bytes=stream.ToArray();Check(bytes.Length>100);var xml=XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));Check(xml.Root?.Name.LocalName=="svg");Check(xml.Descendants().Any(e=>e.Name.LocalName is "path" or "image" or "rect" or "circle" or "ellipse"));File.WriteAllBytes(Path.Combine(output,$"scene-{scene}.svg"),bytes);
}
using(var stream=new SKDynamicMemoryWStream()){using(var c=SKSvgCanvas.Create(new(0,0,80,64),stream)){using var p=new SKPaint{Color=SKColors.Red};c.DrawRect(3,4,40,30,p);}using var data=stream.CopyToData();Check(data.Size>100);Check(stream.Write8(10));File.WriteAllBytes(Path.Combine(output,"wstream.svg"),data.ToArray());}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"PASS {checks} SVG structure/ownership checks");
