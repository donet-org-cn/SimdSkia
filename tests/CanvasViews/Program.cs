using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("Canvas view assertion "+checks);}
void Capture(SKBitmap bitmap,string kind,int scene,int transform)
{using var image=SKImage.FromBitmap(bitmap);using var read=new SKBitmap(new SKImageInfo(96,72,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind,scene,transform,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);}
using(var canvas=new SKNoDrawCanvas(960000,720000)){var b=canvas.DeviceClipBounds;values["large-no-draw"]=new[]{b.Left,b.Top,b.Right,b.Bottom};using var paint=new SKPaint();for(int i=0;i<100;i++)canvas.DrawRect(new SKRect(i,i,i+10,i+10),paint);Check(canvas.Save()==1);canvas.Translate(4,7);canvas.ClipRect(new(10,10,90,80));values["no-draw-clip"]=new[]{canvas.DeviceClipBounds.Left,canvas.DeviceClipBounds.Top,canvas.DeviceClipBounds.Right,canvas.DeviceClipBounds.Bottom};canvas.Restore();Check(canvas.SaveCount==1);}
for(int scene=0;scene<6;scene++)
{
 using var first=new SKBitmap(96,72);using var second=new SKBitmap(96,72);using var a=new SKCanvas(first);using var b=new SKCanvas(second);a.Clear(SKColors.Transparent);b.Clear(SKColors.Transparent);b.Translate(5,4);
 using(var view=new SKNWayCanvas(96,72))
 {
  view.AddCanvas(a);view.AddCanvas(b);using var paint=new SKPaint{Color=new SKColor(210,70,130,170)};
  if(scene==0)view.DrawRect(new SKRect(5,6,35,31),paint);
  if(scene==1){view.Save();view.Translate(7,9);view.ClipRect(new(2,3,34,27));view.DrawRect(new SKRect(0,0,60,50),paint);view.Restore();}
  if(scene==2){view.Clear(new SKColorF(.12f,.38f,.57f,.7f));view.DrawColor(new SKColorF(.7f,.2f,.4f,.6f),SKBlendMode.SrcOver);}
  if(scene==3){view.SaveLayer(new SKPaint{Color=new SKColor(255,255,255,170)});view.DrawRect(new SKRect(5,6,35,31),paint);view.Restore();}
  if(scene==4){view.RemoveCanvas(a);view.DrawRect(new SKRect(5,6,35,31),paint);view.RemoveAll();view.DrawRect(new SKRect(35,31,70,65),paint);}
  if(scene==5){using var r=new SKPictureRecorder();var c=r.BeginRecording(new(0,0,70,60));c.Translate(5,8);c.ClipRect(new(0,0,35,31));c.DrawRect(new SKRect(0,0,55,40),paint);using var picture=r.EndRecording();view.DrawPicture(picture,7,3);}
 }
 using(var p=new SKPaint{Color=SKColors.Gold}){a.DrawRect(new SKRect(75,50,80,55),p);b.DrawRect(new SKRect(75,50,80,55),p);}Capture(first,"n-way",scene,0);Capture(second,"n-way",scene,1);
}
for(int scene=0;scene<5;scene++)
{
 using var bitmap=new SKBitmap(96,72);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.Transparent);using(var overdraw=new SKOverdrawCanvas(canvas))
 {
  using var paint=new SKPaint{Color=new SKColor(230,20,70,5),IsAntialias=scene==4};overdraw.DrawRect(new SKRect(6,5,62,48),paint);overdraw.DrawRect(new SKRect(24,20,75,59),paint);
  if(scene==1)overdraw.Clear(SKColors.Blue);
  if(scene==2){using var image=new SKBitmap(10,8);image.Erase(SKColors.Transparent);overdraw.DrawBitmap(image,new SKRect(1,2,21,18));}
  if(scene==3){overdraw.SaveLayer(new SKPaint{Color=new SKColor(255,255,255,50)});overdraw.DrawRect(new SKRect(12,11,45,40),paint);overdraw.Restore();}
  if(scene==4)overdraw.DrawCircle(37,28,20,paint);
 }Capture(bitmap,"overdraw",scene,0);
}
using(var recorder=new SKPictureRecorder()){var c=recorder.BeginRecording(new(0,0,96,72));values["recording-initial-bounds"]=new[]{c.DeviceClipBounds.Left,c.DeviceClipBounds.Top,c.DeviceClipBounds.Right,c.DeviceClipBounds.Bottom};c.Save();c.Translate(4,5);c.ClipRect(new(10,8,80,60));values["recording-clip-bounds"]=new[]{c.DeviceClipBounds.Left,c.DeviceClipBounds.Top,c.DeviceClipBounds.Right,c.DeviceClipBounds.Bottom};using var picture=recorder.EndRecording();}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} canvas view images, {values.Count} state entries and {checks} ownership checks");
