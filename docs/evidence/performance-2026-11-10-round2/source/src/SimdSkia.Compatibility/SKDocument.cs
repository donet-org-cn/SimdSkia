using SimdSkia.Imaging;

namespace SkiaSharp;

/// <summary>Multi-page raster PDF export. Text is drawn into page pixels, not embedded as searchable PDF text.</summary>
public class SKDocument : SKObject
{
    public const float DefaultRasterDpi=72;
    private readonly Stream stream;
    private readonly bool ownsStream;
    private readonly SKDocumentPdfMetadata metadata;
    private readonly PdfWriter? writer;
    private readonly XpsDocumentWriter? xps;
    private PictureCommands? vectorPage;
    private SKBitmap? page;
    private SKCanvas? canvas;
    private float width,height;
    private bool closed;
    private readonly List<PdfAnnotation> annotations=[];
    private SKDocument(Stream stream,SKDocumentPdfMetadata metadata,bool ownsStream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ValidateMetadata(metadata);
        this.stream=stream;this.metadata=metadata;this.ownsStream=ownsStream;writer=new(stream);
    }
    private SKDocument(Stream stream,float dpi,bool ownsStream)
    {ArgumentNullException.ThrowIfNull(stream);if(!float.IsFinite(dpi)||dpi<=0)throw new ArgumentOutOfRangeException(nameof(dpi));if(!stream.CanWrite)throw new ArgumentException("XPS output must be writable.",nameof(stream));this.stream=stream;this.ownsStream=ownsStream;metadata=new(dpi);xps=new(stream);}
    public static SKDocument CreateXps(Stream stream)=>CreateXps(stream,DefaultRasterDpi);
    public static SKDocument CreateXps(Stream stream,float dpi)=>new(stream,dpi,false);
    public static SKDocument CreateXps(SKWStream stream)=>CreateXps(stream,DefaultRasterDpi);
    public static SKDocument CreateXps(SKWStream stream,float dpi)
    {ArgumentNullException.ThrowIfNull(stream);stream.ThrowIfDisposed();return new(new WStreamOutput(stream),dpi,true);}
    public static SKDocument CreateXps(string path)=>CreateXps(path,DefaultRasterDpi);
    public static SKDocument CreateXps(string path,float dpi)
    {ArgumentNullException.ThrowIfNull(path);if(!float.IsFinite(dpi)||dpi<=0)throw new ArgumentOutOfRangeException(nameof(dpi));var output=File.Create(path);try{return new(output,dpi,true);}catch{output.Dispose();throw;}}
    private static void ValidateMetadata(SKDocumentPdfMetadata metadata)
    {
        if(metadata.PdfA)throw new NotSupportedException("PDF/A export requires validated profiles, metadata and font rules.");
        if(!float.IsFinite(metadata.RasterDpi)||metadata.RasterDpi<=0)throw new ArgumentOutOfRangeException(nameof(metadata));
        if(metadata.EncodingQuality is <0 or >101)throw new ArgumentOutOfRangeException(nameof(metadata));
    }
    public static SKDocument CreatePdf(Stream stream)=>CreatePdf(stream,SKDocumentPdfMetadata.Default);
    public static SKDocument CreatePdf(SKWStream stream)=>CreatePdf(stream,SKDocumentPdfMetadata.Default);
    public static SKDocument CreatePdf(SKWStream stream,float dpi)=>CreatePdf(stream,new SKDocumentPdfMetadata(dpi));
    public static SKDocument CreatePdf(SKWStream stream,SKDocumentPdfMetadata metadata)
    {ArgumentNullException.ThrowIfNull(stream);stream.ThrowIfDisposed();return new(new WStreamOutput(stream),metadata,true);}
    public static SKDocument CreatePdf(Stream stream,float dpi)=>CreatePdf(stream,new SKDocumentPdfMetadata(dpi));
    public static SKDocument CreatePdf(Stream stream,SKDocumentPdfMetadata metadata)=>new(stream,metadata,false);
    public static SKDocument CreatePdf(string path)=>CreatePdf(path,SKDocumentPdfMetadata.Default);
    public static SKDocument CreatePdf(string path,float dpi)=>CreatePdf(path,new SKDocumentPdfMetadata(dpi));
    public static SKDocument CreatePdf(string path,SKDocumentPdfMetadata metadata)
    {ArgumentNullException.ThrowIfNull(path);ValidateMetadata(metadata);var output=File.Create(path);try{return new(output,metadata,true);}catch{output.Dispose();throw;}}
    public SKCanvas BeginPage(float width,float height)
    {
        ThrowIfDisposed();if(closed)throw new InvalidOperationException("The document is closed.");
        if(canvas is not null)throw new InvalidOperationException("End the current page first.");
        if(!float.IsFinite(width)||!float.IsFinite(height)||width<=0||height<=0)throw new ArgumentOutOfRangeException(nameof(width));
        if(xps is not null){this.width=width;this.height=height;vectorPage=new(new(0,0,width,height));return canvas=new(vectorPage);}
        float scale=metadata.RasterDpi/72;double pw=Math.Ceiling((double)width*scale),ph=Math.Ceiling((double)height*scale);
        if(pw<1||ph<1||pw>int.MaxValue||ph>int.MaxValue||pw*ph>16*1024*1024)throw new NotSupportedException("PDF raster page exceeds the 16 megapixel budget; select a lower RasterDpi.");
        this.width=width;this.height=height;page=new(new SKImageInfo((int)pw,(int)ph,SKColorType.Bgra8888,SKAlphaType.Premul));annotations.Clear();canvas=new(page){AnnotationSink=annotation=>annotations.Add(new(new(annotation.Bounds.Left/scale,annotation.Bounds.Top/scale,annotation.Bounds.Right/scale,annotation.Bounds.Bottom/scale),annotation.Key,annotation.Value))};canvas.Scale(scale);return canvas;
    }
    public SKCanvas BeginPage(float width,float height,SKRect content)
    {if(!content.ToCore().IsFinite)throw new ArgumentException("Content must be finite.",nameof(content));var result=BeginPage(width,height);result.ClipRect(content);result.Translate(content.Left,content.Top);return result;}
    public void EndPage()
    {
        ThrowIfDisposed();if(canvas is null)return;
        if(xps is not null){try{canvas.RestoreToCount(1);xps.AddPage(vectorPage!,width,height);}finally{canvas.Dispose();canvas=null;vectorPage!.Dispose();vectorPage=null;}return;}
        try{canvas.RestoreToCount(1);canvas.Flush();writer!.AddPage(page!.Buffer,width,height,metadata.EncodingQuality,annotations);}
        finally{canvas!.Dispose();page!.Dispose();canvas=null;page=null;annotations.Clear();}
    }
    public void Close()
    {
        ThrowIfDisposed();if(closed)return;EndPage();var values=new Dictionary<string,string>();
        void Add(string key,string? value){if(value is not null)values[key]=value;}
        Add("Title",metadata.Title);Add("Author",metadata.Author);Add("Subject",metadata.Subject);Add("Keywords",metadata.Keywords);Add("Creator",metadata.Creator);Add("Producer",metadata.Producer??"SimdSkia");
        Add("CreationDate",metadata.Creation?.ToUniversalTime().ToString("'D:'yyyyMMddHHmmss'Z'"));Add("ModDate",metadata.Modified?.ToUniversalTime().ToString("'D:'yyyyMMddHHmmss'Z'"));
        if(xps is not null)xps.Close();else writer!.Close(values);closed=true;if(ownsStream)stream.Dispose();
    }
    public void Abort(){ThrowIfDisposed();if(closed)return;closed=true;canvas?.Dispose();page?.Dispose();vectorPage?.Dispose();canvas=null;page=null;vectorPage=null;xps?.Abort();if(ownsStream)stream.Dispose();}
    protected override void Dispose(bool disposing){try{Close();}finally{canvas?.Dispose();page?.Dispose();vectorPage?.Dispose();xps?.Dispose();if(ownsStream)stream.Dispose();}}
}
