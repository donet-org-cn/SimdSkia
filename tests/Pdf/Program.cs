using SimdSkia.Pdf;
using SkiaSharp;
using System.Text;
string output=args.Length>0?args[0]:"artifacts/compat-expansion/pdf";Directory.CreateDirectory(output);
int checks=0;void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
using(var stream=new MemoryStream())
{
    using(var doc=SKDocument.CreatePdf(stream))
    {
        var canvas=doc.BeginPage(96,64);canvas.Clear(SKColors.White);using var p=new SKPaint{Color=SKColors.Red};canvas.DrawRect(10,10,20,20,p);doc.EndPage();doc.Close();
    }
    File.WriteAllBytes(Path.Combine(output,"roundtrip.pdf"),stream.ToArray());
    using var reader=PdfReader.Open(stream.ToArray());Check(reader.PageCount==1,"Page count");
    using var page=reader.RenderPage(1,72);Check(page.Bitmap.Width==96&&page.Bitmap.Height==64,"Page dimensions");
    Check(page.Bitmap.GetPixel(15,15)==SKColors.Red,"PDF image placement");Check(page.Bitmap.GetPixel(0,0)==SKColors.White,"PDF background");
    using var png=page.Bitmap.Encode(SKEncodedImageFormat.Png,100);using var pngFile=File.Create(Path.Combine(output,"roundtrip.png"));png.SaveTo(pngFile);
}
foreach(string file in new[]{Path.Combine(AppContext.BaseDirectory,"Fixtures/external.pdf"),Path.Combine(AppContext.BaseDirectory,"Fixtures/alpha.pdf")}.Concat(args.Skip(1)))
{
    using var reader=PdfReader.Open(file);Check(reader.PageCount>0,"External page count");
    for(int i=1;i<=reader.PageCount;i++)
    {
        using var page=reader.RenderPage(i,144);Check(page.Bitmap.Width>0&&page.Bitmap.Height>0,"External dimensions");
        if(Path.GetFileName(file)=="alpha.pdf")
        {var left=page.Bitmap.GetPixel(21,40);var right=page.Bitmap.GetPixel(178,40);Check(left.Blue>240&&left.Red<15&&right.Red>240&&right.Blue<15,"Independent image soft mask opacity");}
        using var encoded=page.Bitmap.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(Path.Combine(output,Path.GetFileNameWithoutExtension(file)+"-"+i+".png"));encoded.SaveTo(stream);
    }
}
// Small independent syntax fixtures: unsupported painting must not return a blank successful page.
byte[] Minimal(string operations,string pageExtra="")
{
    using var output=new MemoryStream();void Write(string s)=>output.Write(Encoding.ASCII.GetBytes(s));
    Write("%PDF-1.4\n");var offsets=new List<long>{0};
    void Obj(string value){offsets.Add(output.Position);Write($"{offsets.Count-1} 0 obj\n{value}\nendobj\n");}
    Obj("<< /Type /Catalog /Pages 2 0 R >>");Obj("<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
    Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 32 32] /Resources << >> /Contents 4 0 R {pageExtra} >>");
    Obj($"<< /Length {Encoding.ASCII.GetByteCount(operations)} >>\nstream\n{operations}\nendstream");
    long xref=output.Position;Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");foreach(long offset in offsets.Skip(1))Write(offset.ToString("D10")+" 00000 n \n");
    Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");return output.ToArray();
}
foreach(var fixture in new[]{("/Sh1 sh",""),("0 1 1 0 k 0 0 32 32 re f",""),("0 0 32 32 re f","/UserUnit 2"),("0 0 32 32 re f","/UserUnit 1.5"),("/OC BMC 0 0 32 32 re f EMC","")})
{using var reader=PdfReader.Open(Minimal(fixture.Item1,fixture.Item2));bool failed=false;try{using var page=reader.RenderPage(1);}catch(NotSupportedException){failed=true;}Check(failed,"Unsupported PDF content must fail explicitly");}
using(var reader=PdfReader.Open(Minimal("1 0 0 rg 0 0 32 32 re f")))
{
    using var first=reader.RenderPage(1,72);using var second=reader.RenderPage(1,144);Check(first.Bitmap.Width==32&&second.Bitmap.Width==64,"Repeated rendering uses requested DPI");
    reader.Dispose();Check(first.Bitmap.GetPixel(5,5)==SKColors.Red,"Rendered page outlives document");
    bool disposed=false;try{reader.RenderPage(1);}catch(ObjectDisposedException){disposed=true;}Check(disposed,"Disposed reader rejected");
}
#if NATIVE_AOT
Check(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported,"PDF Native AOT runtime");
#endif
File.WriteAllText(Path.Combine(output,"result.json"),"{\"checks\":"+checks+",\"pdf\":\"SimdSkia.Pdf\",\"nativeAot\":"+(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported).ToString().ToLowerInvariant()+"}");Console.WriteLine($"PASS {checks} PDF checks");
