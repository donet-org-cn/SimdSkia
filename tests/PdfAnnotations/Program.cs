using SkiaSharp;
using UglyToad.PdfPig;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);int checks=0;var values=new Dictionary<string,object?>();void Check(bool b){checks++;if(!b)throw new Exception("PDF annotation assertion "+checks);}
foreach(float dpi in new[]{72f,144f})
{
 using var memory=new MemoryStream();using var stream=new SKManagedWStream(memory);using(var doc=SKDocument.CreatePdf(stream,dpi))
 {
  var c=doc.BeginPage(120,100);c.Clear(SKColors.White);c.Translate(5,7);using var url=c.DrawUrlAnnotation(new(10,12,40,32),"https://example.com/path?a=1&b=2");using var link=c.DrawLinkDestinationAnnotation(new(50,10,90,30),"chapter2");
  using(var rec=new SKPictureRecorder()){var rc=rec.BeginRecording(new(0,0,30,30));using var annotation=rc.DrawUrlAnnotation(new(1,2,11,12),"https://example.com/picture");using var picture=rec.EndRecording();c.DrawPicture(picture,SKMatrix.CreateTranslation(4,6));}
  doc.EndPage();c=doc.BeginPage(120,100);using var dest=c.DrawNamedDestinationAnnotation(new(8,9),"chapter2");doc.EndPage();doc.Close();
 }
 Check(memory.CanWrite);Check(stream.Write8(10));var bytes=memory.ToArray();File.WriteAllBytes(Path.Combine(output,$"annotations-{dpi}.pdf"),bytes);using var read=PdfDocument.Open(bytes);Check(read.NumberOfPages==2);
 var page=read.GetPage(1);var annotations=page.GetAnnotations().ToArray();Check(annotations.Length==3);var links=page.GetHyperlinks().ToArray();values[$"{dpi}-links"]=links.Select(x=>new {x.Uri,rect=x.Bounds.ToString()}).ToArray();Check(links.Any(x=>x.Uri=="https://example.com/path?a=1&b=2"));Check(links.Any(x=>x.Uri=="https://example.com/picture"));
 values[$"{dpi}-annots"]=annotations.Select(x=>new{type=x.Type.ToString(),rect=x.Rectangle.ToString(),action=x.Action?.ToString()}).ToArray();
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"PASS {checks} PDF annotation/stream checks");
