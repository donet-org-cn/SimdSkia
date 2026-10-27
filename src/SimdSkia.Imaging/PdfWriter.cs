using System.Globalization;
using System.Text;

namespace SimdSkia.Imaging;

/// <summary>Streaming PDF 1.4 writer for lossless or JPEG raster pages. No native PDF/codec dependency.</summary>
public sealed class PdfWriter
{
    private readonly Stream output;
    private readonly List<long> offsets=[0,0,0];
    private readonly List<int> pages=[];
    private long position;
    private bool closed;
    public PdfWriter(Stream output)
    {ArgumentNullException.ThrowIfNull(output);if(!output.CanWrite)throw new ArgumentException("PDF output must be writable.",nameof(output));this.output=output;Write("%PDF-1.4\n%SimdSkia\n");}
    private void Write(string value)=>Write(Encoding.ASCII.GetBytes(value));
    private void Write(ReadOnlySpan<byte> value){output.Write(value);position+=value.Length;}
    private int Reserve(){offsets.Add(0);return offsets.Count-1;}
    private void Object(int id,string value){offsets[id]=position;Write($"{id} 0 obj\n{value}\nendobj\n");}
    private void ObjectStream(int id,string dictionary,byte[] data){offsets[id]=position;Write($"{id} 0 obj\n<< {dictionary} /Length {data.Length} >>\nstream\n");Write(data);Write("\nendstream\nendobj\n");}
    public void AddPage(PixelBuffer pixels,float width,float height,int quality=101)
    {
        if(closed)throw new InvalidOperationException("The PDF is closed.");pixels.ThrowIfDisposed();
        int page=Reserve(),image=Reserve(),content=Reserve();byte[] data;string filter;
        // PDF pages have a white backdrop. Flatten alpha explicitly in sRGB.
        using var flattened=new PixelBuffer(pixels.Width,pixels.Height,PixelFormat.Rgba8888,AlphaFormat.Straight);
        for(int y=0;y<pixels.Height;y++)for(int x=0;x<pixels.Width;x++)
        {var c=pixels.ReadPixel(x,y);flattened.WritePixel(x,y,new((byte)(PixelBuffer.Multiply(c.R,c.A)+255-c.A),(byte)(PixelBuffer.Multiply(c.G,c.A)+255-c.A),(byte)(PixelBuffer.Multiply(c.B,c.A)+255-c.A),255));}
        if(quality<=100){data=JpegCodec.Encode(flattened,quality);filter="DCTDecode";}
        else
        {
            var rgb=new byte[checked(pixels.Width*pixels.Height*3)];var bytes=flattened.GetPixelSpan();
            for(int i=0,j=0;i<bytes.Length;i+=4,j+=3)bytes.Slice(i,3).CopyTo(rgb.AsSpan(j,3));data=ManagedZlib.Encode(rgb);filter="FlateDecode";
        }
        string w=width.ToString("R",CultureInfo.InvariantCulture),h=height.ToString("R",CultureInfo.InvariantCulture);
        ObjectStream(image,$"/Type /XObject /Subtype /Image /Width {pixels.Width} /Height {pixels.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /{filter}",data);
        ObjectStream(content,"",Encoding.ASCII.GetBytes($"q {w} 0 0 {h} 0 0 cm /Im0 Do Q\n"));
        Object(page,$"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Resources << /XObject << /Im0 {image} 0 R >> >> /Contents {content} 0 R >>");pages.Add(page);
    }
    public void Close(IReadOnlyDictionary<string,string> metadata)
    {
        if(closed)return;closed=true;
        Object(1,"<< /Type /Catalog /Pages 2 0 R >>");Object(2,$"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ",pages.Select(id=>$"{id} 0 R"))}] >>");
        int info=Reserve();string values=string.Join(" ",metadata.Select(p=>$"/{p.Key} <FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(p.Value))}>"));Object(info,$"<< {values} >>");
        long xref=position;Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach(long offset in offsets.Skip(1)){if(offset>9999999999)throw new NotSupportedException("Classic PDF xref exceeds ten digits.");Write(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");}
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");output.Flush();
    }
}
