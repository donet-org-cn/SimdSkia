using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;
internal static class Program
{
 [STAThread] private static void Main(string[] args)
 {
  using var document=new XpsDocument(Path.GetFullPath(args[0]),FileAccess.Read);var sequence=document.GetFixedDocumentSequence()??throw new Exception("Missing FixedDocumentSequence");var paginator=sequence.DocumentPaginator;paginator.ComputePageCount();if(paginator.PageCount!=5)throw new Exception("Expected five independently readable pages");Directory.CreateDirectory(args[1]);var samples=new List<object>();var failures=new List<string>();int checks=0;
  for(int i=0;i<5;i++)
  {
   using var page=paginator.GetPage(i);if(Math.Abs(page.Size.Width-80)>.001||Math.Abs(page.Size.Height-64)>.001)throw new Exception("XPS point/DIP size mismatch");var bitmap=new RenderTargetBitmap(80,64,96,96,PixelFormats.Pbgra32);bitmap.Render(page.Visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(args[1],$"page-{i}.png")))encoder.Save(output);byte[] pixels=new byte[80*64*4];bitmap.CopyPixels(pixels,320,0);
   byte[] Pixel(int x,int y)=>pixels.AsSpan(y*320+x*4,4).ToArray();void Expected(int x,int y,byte r,byte g,byte b){checks++;var p=Pixel(x,y);if(p[0]!=b||p[1]!=g||p[2]!=r||p[3]!=255)failures.Add($"XPS rendered page {i} ({x},{y})={string.Join(',',p)}, expected BGRA {b},{g},{r},255");}
   Expected(1,1,255,255,255);if(i==0){Expected(10,10,255,0,0);Expected(50,42,0,0,255);var f=Pixel(60,12);checks++;if(Math.Abs(f[0]-247)>1||Math.Abs(f[1]-218)>1||Math.Abs(f[2]-180)>1||f[3]!=255)failures.Add("XPS float/scRGB paint did not retain the encoded color and fractional alpha.");}if(i==1){var p=Pixel(40,20);checks++;if(p[0]<90||p[2]<90||p[1]>5)throw new Exception("XPS gradient did not interpolate");}if(i==2){Expected(10,10,255,255,255);Expected(20,20,255,0,0);}if(i==3)Expected(20,20,0,128,0);if(i==4){Expected(10,10,255,127,127);Expected(40,30,127,127,255);}samples.Add(new{page=i,size=new[]{page.Size.Width,page.Size.Height},center=Pixel(40,30)});
  }
  File.WriteAllText(Path.Combine(args[1],"reader.json"),JsonSerializer.Serialize(new{renderer="System.Windows.Xps.Packaging + WPF RenderTargetBitmap (test-only Windows system renderer)",checks,pages=paginator.PageCount,samples,failures}));Console.WriteLine($"Independent WPF XPS reader: {checks} exact/gradient checks, {failures.Count} failures");if(failures.Count!=0)Environment.ExitCode=2;
 }
}
