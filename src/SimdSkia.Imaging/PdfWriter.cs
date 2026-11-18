using System.Globalization;
using System.Text;
using Sdcb.SimdCodecs.Abstractions;
using CodecPixelFormat = Sdcb.SimdCodecs.Abstractions.PixelFormat;

namespace SimdSkia.Imaging;
public sealed record PdfAnnotation(RectF Bounds,string Key,byte[] Value);

/// <summary>Streaming PDF 1.4 writer for lossless or JPEG raster pages. No native PDF/codec dependency.</summary>
public sealed class PdfWriter
{
    private readonly Stream output;
    private readonly List<long> offsets=[0,0,0];
    private readonly List<int> pages=[];
    private long position;
    private bool closed;
    private readonly Dictionary<string,(int Page,float X,float Y)> destinations=new(StringComparer.Ordinal);
    public PdfWriter(Stream output)
    {ArgumentNullException.ThrowIfNull(output);if(!output.CanWrite)throw new ArgumentException("PDF output must be writable.",nameof(output));this.output=output;Write("%PDF-1.4\n%SimdSkia\n");}
    private void Write(string value)=>Write(Encoding.ASCII.GetBytes(value));
    private void Write(ReadOnlySpan<byte> value){output.Write(value);position+=value.Length;}
    private int Reserve(){offsets.Add(0);return offsets.Count-1;}
    private void Object(int id,string value){offsets[id]=position;Write($"{id} 0 obj\n{value}\nendobj\n");}
    private void ObjectStream(int id,string dictionary,byte[] data){offsets[id]=position;Write($"{id} 0 obj\n<< {dictionary} /Length {data.Length} >>\nstream\n");Write(data);Write("\nendstream\nendobj\n");}
    public void AddPage(PixelBuffer pixels,float width,float height,int quality=101,IReadOnlyList<PdfAnnotation>? annotations=null)
    {
        if(closed)throw new InvalidOperationException("The PDF is closed.");pixels.ThrowIfDisposed();
        int page=Reserve(),image=Reserve(),content=Reserve();
        var data = EncodePagePixels(pixels, quality);
        string filter = quality <= 100 ? "DCTDecode" : "FlateDecode";
        string w=width.ToString("R",CultureInfo.InvariantCulture),h=height.ToString("R",CultureInfo.InvariantCulture);
        ObjectStream(image,$"/Type /XObject /Subtype /Image /Width {pixels.Width} /Height {pixels.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /{filter}",data);
        ObjectStream(content,"",Encoding.ASCII.GetBytes($"q {w} 0 0 {h} 0 0 cm /Im0 Do Q\n"));
        var links=new List<int>();var custom=new List<string>();
        if(annotations is not null)foreach(var annotation in annotations)
        {
            var value=annotation.Value.AsSpan();int zero=value.IndexOf((byte)0);if(zero>=0)value=value[..zero];string text=Encoding.UTF8.GetString(value);var r=annotation.Bounds;
            if(annotation.Key=="SkAnnotationKey_Define_Named_Dest"){destinations[text]=(page,r.Left,height-r.Top);continue;}
            string rect=$"[{Number(r.Left)} {Number(height-r.Bottom)} {Number(r.Right)} {Number(height-r.Top)}]";
            if(annotation.Key is "SkAnnotationKey_URL" or "SkAnnotationKey_Link_Named_Dest")
            {int id=Reserve();string action=annotation.Key=="SkAnnotationKey_URL"?$"/A << /S /URI /URI <{Convert.ToHexString(Encoding.UTF8.GetBytes(text))}> >>":$"/Dest {PdfString(text)}";Object(id,$"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] {action} >>");links.Add(id);}
            else custom.Add($"<< /Key {PdfString(annotation.Key)} /Data <{Convert.ToHexString(annotation.Value)}> /Rect {rect} >>");
        }
        string references=links.Count>0?$" /Annots [{string.Join(" ",links.Select(id=>$"{id} 0 R"))}]":"";string metadata=custom.Count>0?$" /SimdSkiaAnnotations [{string.Join(" ",custom)}]":"";
        Object(page,$"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Resources << /XObject << /Im0 {image} 0 R >> >> /Contents {content} 0 R{references}{metadata} >>");pages.Add(page);
    }

    private static byte[] EncodePagePixels(PixelBuffer pixels, int quality)
    {
        int rowBytes = checked(pixels.Width * 3);
        var rgb = GC.AllocateUninitializedArray<byte>(checked(rowBytes * pixels.Height));
        bool premultiplied = pixels.Alpha == AlphaFormat.Premultiplied;
        int redIndex = pixels.Format == PixelFormat.Rgba8888 ? 0 : 2;
        for (int y = 0; y < pixels.Height; y++)
        {
            ReadOnlySpan<byte> row = pixels.GetRowSpan(y);
            var destination = rgb.AsSpan(y * rowBytes, rowBytes);
            if (pixels.Format == PixelFormat.Alpha8)
            {
                for (int x = 0, d = 0; x < pixels.Width; x++, d += 3)
                    destination[d] = destination[d + 1] = destination[d + 2] = (byte)(255 - row[x]);
                continue;
            }
            row = row[..(pixels.Width * 4)];
            if (PixelBuffer.HasOnlyOpaqueAlpha(row))
            {
                PixelConverter.ConvertRow(row, redIndex == 0 ? CodecPixelFormat.Rgba32 : CodecPixelFormat.Bgra32,
                    destination, CodecPixelFormat.Rgb24, pixels.Width);
                continue;
            }
            for (int s = 0, d = 0; s < row.Length; s += 4, d += 3)
            {
                byte a = row[s + 3];
                destination[d] = OverWhite(row[s + redIndex], a, premultiplied);
                destination[d + 1] = OverWhite(row[s + 1], a, premultiplied);
                destination[d + 2] = OverWhite(row[s + 2 - redIndex], a, premultiplied);
            }
        }
        return quality <= 100
            ? JpegCodec.EncodeRgb24(rgb, pixels.Width, pixels.Height, quality)
            : ManagedZlib.Encode(rgb);

        // Unpremultiplying and multiplying again recovers a valid stored
        // premultiplied sample. The white blend saturates samples above alpha.
        static byte OverWhite(byte color, byte alpha, bool premultiplied) =>
            (byte)((premultiplied ? Math.Min(color, alpha) : PixelBuffer.Multiply(color, alpha)) + 255 - alpha);
    }

    public void Close(IReadOnlyDictionary<string,string> metadata)
    {
        if(closed)return;closed=true;
        string names=destinations.Count==0?"":$" /Names << /Dests << /Names [{string.Join(" ",destinations.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>$"{PdfString(p.Key)} [{p.Value.Page} 0 R /XYZ {Number(p.Value.X)} {Number(p.Value.Y)} null]"))}] >> >>";
        Object(1,$"<< /Type /Catalog /Pages 2 0 R{names} >>");Object(2,$"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ",pages.Select(id=>$"{id} 0 R"))}] >>");
        int info=Reserve();string values=string.Join(" ",metadata.Select(p=>$"/{p.Key} <FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(p.Value))}>"));Object(info,$"<< {values} >>");
        long xref=position;Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach(long offset in offsets.Skip(1)){if(offset>9999999999)throw new NotSupportedException("Classic PDF xref exceeds ten digits.");Write(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");}
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");output.Flush();
    }
    private static string Number(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
    private static string PdfString(string value)=>$"<FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value))}>";
}
