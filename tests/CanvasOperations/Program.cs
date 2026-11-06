#pragma warning disable CS0618
using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);using var stream=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new SortedDictionary<string,object?>();int offset=0;
foreach(float width in new[]{0f,1f,5.5f})foreach(var cap in Enum.GetValues<SKStrokeCap>())foreach(bool aa in new[]{false,true})foreach(int transform in new[]{0,1})foreach(int scene in new[]{0,1,2,3,4})
{
    using var bitmap=new SKBitmap(new SKImageInfo(40,32,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);using var paint=new SKPaint{Color=new SKColor(32,95,181,173),StrokeWidth=width,StrokeCap=cap,IsAntialias=aa,Style=SKPaintStyle.Stroke};if(transform==1){canvas.Translate(2,3);canvas.Scale(1.25f,.8f);}
    SKPoint[] points=[new(4,5),new(14,7),new(22,16),new(11,24)];
    switch(scene){case 0:canvas.DrawPoint(new SKPoint(12.25f,15.5f),paint);break;case 1:canvas.DrawPoints(SKPointMode.Points,points,paint);break;case 2:canvas.DrawPoints(SKPointMode.Lines,points,paint);break;case 3:canvas.DrawPoints(SKPointMode.Polygon,points,paint);break;case 4:canvas.DrawPoint(new SKPoint(12.25f,15.5f),new SKColor(90,40,210,123));break;}
    var pixels=bitmap.GetPixelSpan();stream.Write(pixels);cases.Add(new{width,cap,aa,transform,scene,offset,length=pixels.Length});offset+=pixels.Length;
}
using(var face=SKTypeface.FromFile("baseline/fonts/ZCOOLXiaoWei-Regular.ttf"))using(var font=new SKFont(face,18)){using var paint=new SKPaint(font);values["paint-from-font-aa"]=paint.IsAntialias;values["font-edging"]=(int)font.Edging;paint.IsAntialias=false;values["paint-measure"]=paint.MeasureText("中文 Hello");using var surface=(SKSurface.Create(new SKImageInfo(120,40))??throw new Exception("surface creation failed"));surface.Canvas.Clear(SKColors.White);surface.Canvas.DrawText("中文 Hello",new SKPoint(3,27),paint);}
File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {cases.Count} canvas images");
