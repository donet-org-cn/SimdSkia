using SkiaSharp;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
var output=args[0];Directory.CreateDirectory(output);int checks=0;void Check(bool b){checks++;if(!b)throw new Exception("XPS check "+checks);}
void Draw(SKCanvas c,int scene)
{
 c.Clear(SKColors.White);using var p=new SKPaint{Color=SKColors.Red,IsAntialias=true};
 if(scene==0){c.DrawRect(6,6,24,18,p);p.Color=SKColors.Blue;c.DrawRect(36,30,18,12,p);p.ColorF=new(.12345f,.56789f,.90123f,.33333f);c.DrawRect(40,6,12,12,p);}
 if(scene==1){using var g=SKShader.CreateLinearGradient(new(6,0),new(54,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);p.Shader=g;c.DrawRect(6,6,48,36,p);}
 if(scene==2){c.Save();c.ClipRect(new(12,12,48,36));c.DrawRect(0,0,60,48,p);c.Restore();}
 if(scene==3){using var b=new SKBitmap(4,4);b.Erase(SKColors.Green);using var im=SKImage.FromBitmap(b);c.DrawImage(im,new SKRect(6,6,54,42),new SKSamplingOptions(SKFilterMode.Nearest));}
 if(scene==4){using var alpha=new SKPaint{Color=SKColors.White.WithAlpha(128)};c.SaveLayer(alpha);c.DrawRect(6,6,30,24,p);p.Color=SKColors.Blue;c.DrawRect(24,18,30,24,p);c.Restore();}
}
using var bytes=new MemoryStream();using(var doc=SKDocument.CreateXps(bytes))
{
 if(doc is null){File.WriteAllText(Path.Combine(output,"status.json"),JsonSerializer.Serialize(new{available=false,checks,reason="Native Skia XPS factory returned null in this build."}));Console.WriteLine("UNAVAILABLE native XPS factory");return;}
 for(int i=0;i<5;i++){Draw(doc.BeginPage(60,48),i);doc.EndPage();}doc.Close();doc.Close();
}
Check(bytes.CanWrite);File.WriteAllBytes(Path.Combine(output,"pages.xps"),bytes.ToArray());
using(var z=new ZipArchive(new MemoryStream(bytes.ToArray()),ZipArchiveMode.Read)){Check(z.Entries.Count>7);var pages=z.Entries.Where(e=>e.FullName.EndsWith(".fpage")).ToArray();Check(pages.Length==5);foreach(var e in pages){using var input=e.Open();var xml=XDocument.Load(input);Check(xml.Root?.Name.NamespaceName=="http://schemas.microsoft.com/xps/2005/06");Check(xml.Descendants().Any(e=>e.Name.LocalName=="Path"));}}
using(var stream=new SKDynamicMemoryWStream()){using(var doc=SKDocument.CreateXps(stream,144)){Draw(doc.BeginPage(60,48),0);doc.Close();}Check(stream.Write8(42));using var data=stream.CopyToData();Check(data.Size>100);}
string path=Path.Combine(output,"path.xps");using(var doc=SKDocument.CreateXps(path)){Draw(doc.BeginPage(60,48),0);doc.Close();}Check(File.Exists(path));
using(var s=new MemoryStream()){using(var d=SKDocument.CreateXps(s,96)){Draw(d.BeginPage(60,48),0);d.Abort();}Check(s.CanWrite);File.WriteAllBytes(Path.Combine(output,"aborted.xps"),s.ToArray());}
File.WriteAllText(Path.Combine(output,"status.json"),JsonSerializer.Serialize(new{available=true,checks,bytes=bytes.Length,pages=5}));Console.WriteLine("PASS "+checks+" XPS package and stream checks");
