#pragma warning disable CS0618
using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
using(var p=new SKPaint()){values["paint-default"]=p.IsEmbeddedBitmapText;p.IsEmbeddedBitmapText=false;using var clone=p.Clone();values["paint-clone"]=clone.IsEmbeddedBitmapText;p.Reset();values["paint-reset"]=p.IsEmbeddedBitmapText;}
using var face=SKTypeface.FromFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"simsun.ttc"));int scene=0;
foreach(int size in new[]{12,14,16,18,24})foreach(bool embedded in new[]{false,true})for(int blob=0;blob<2;blob++)
{
 using var font=new SKFont(face,size){Hinting=SKFontHinting.None,LinearMetrics=true,Subpixel=true,BaselineSnap=false};values[$"default-{scene}"]=font.EmbeddedBitmaps;font.EmbeddedBitmaps=embedded;using var b=new SKBitmap(180,56);using var c=new SKCanvas(b);using var p=new SKPaint{Color=SKColors.Black};c.Clear(SKColors.White);if(blob==0)c.DrawText("中文字体 ABC",4,36,font,p);else{using var text=SKTextBlob.Create("中文字体 ABC",font);font.Dispose();c.DrawText(text!,4,36,p);}var bytes=b.GetPixelSpan();cases.Add(new{kind="embedded-font",scene=scene++,transform=0,size,embedded,blob,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);checks++;
}
foreach(int size in new[]{12,14,16})foreach(char ch in new[]{'中','文','宋','A'})
{
 using var font=new SKFont(face,size){EmbeddedBitmaps=true,Edging=SKFontEdging.Alias,Hinting=SKFontHinting.None,Subpixel=true,LinearMetrics=true,BaselineSnap=false};using var b=new SKBitmap(new SKImageInfo(64,64,SKColorType.Rgba8888,SKAlphaType.Premul));using(var c=new SKCanvas(b))using(var p=new SKPaint{Color=SKColors.Black}){c.Clear(SKColors.White);c.DrawText(ch.ToString(),16,40,font,p);}File.WriteAllBytes(Path.Combine(output,$"glyph-{size}-{(int)ch:X4}.rgba"),b.GetPixelSpan().ToArray());checks++;
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"PASS {checks} embedded bitmap font captures");
