using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Geometry;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Graphics.Core;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Outline.Destinations;
using UglyToad.PdfPig.Parser;
using UglyToad.PdfPig.PdfFonts;
using UglyToad.PdfPig.Tokenization.Scanner;
using UglyToad.PdfPig.Tokens;
using UglyToad.PdfPig.XObjects;

namespace SimdSkia.Pdf;

/// <summary>Optional managed PDF reader. Page indices are one based. Rendering is serialized per document.</summary>
public sealed class PdfReader : IDisposable
{
    private readonly PdfDocument document;
    private readonly ReaderOptions options;
    private readonly object gate=new();
    private bool disposed;
    private PdfReader(PdfDocument document,ReaderOptions options){this.document=document;this.options=options;document.AddPageFactory<RenderedPdfPage,RenderFactory>();}
    public static PdfReader Open(string path,string? password=null)
    {ArgumentNullException.ThrowIfNull(path);ValidateInputLength(new FileInfo(path).Length);var options=new ReaderOptions{UseLenientParsing=false,SkipMissingFonts=false,FilterProvider=new ManagedFilters(),Password=password??""};return new(PdfDocument.Open(path,options),options);}
    public static PdfReader Open(byte[] bytes,string? password=null)
    {ArgumentNullException.ThrowIfNull(bytes);ValidateInputLength(bytes.Length);var options=new ReaderOptions{UseLenientParsing=false,SkipMissingFonts=false,FilterProvider=new ManagedFilters(),Password=password??""};return new(PdfDocument.Open(bytes,options),options);}
    private static void ValidateInputLength(long length)
    {if(length>256L*1024*1024)throw new NotSupportedException("PDF input exceeds the 256 MiB input budget.");}
    public int PageCount {get{lock(gate){ObjectDisposedException.ThrowIf(disposed,this);return document.NumberOfPages;}}}
    public RenderedPdfPage RenderPage(int pageNumber,float dpi=96)
    {
        if(!float.IsFinite(dpi)||dpi<=0)throw new ArgumentOutOfRangeException(nameof(dpi));
        lock(gate){ObjectDisposedException.ThrowIf(disposed,this);options.Dpi=dpi;return document.GetPage<RenderedPdfPage>(pageNumber);}
    }
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;document.Dispose();}}
    internal sealed class ReaderOptions : ParsingOptions {public float Dpi=96;}
}

public sealed class RenderedPdfPage(SKBitmap bitmap,double width,double height) : IDisposable
{
    public SKBitmap Bitmap {get;}=bitmap;
    public double WidthPoints {get;}=width;
    public double HeightPoints {get;}=height;
    public void Dispose()=>Bitmap.Dispose();
}

internal sealed class RenderFactory : BasePageFactory<RenderedPdfPage>
{
    public RenderFactory(IPdfTokenScanner pdfScanner,IResourceStore resourceStore,ILookupFilterProvider filterProvider,IPageContentParser pageContentParser,ParsingOptions parsingOptions):base(pdfScanner,resourceStore,filterProvider,pageContentParser,parsingOptions) { }
    protected override RenderedPdfPage ProcessPage(int pageNumber,DictionaryToken dictionary,NamedDestinations namedDestinations,MediaBox mediaBox,CropBox cropBox,UserSpaceUnit userSpaceUnit,PageRotationDegrees rotation,TransformationMatrix initialMatrix,IReadOnlyList<IGraphicsStateOperation> operations)
    {
        if(dictionary.ContainsKey(NameToken.Annots))throw new NotSupportedException("PDF annotations/forms require appearance rendering; this reader does not silently omit them.");
        if(dictionary.ContainsKey(NameToken.Group))throw new NotSupportedException("PDF page transparency groups are not supported.");
        if(userSpaceUnit.PointMultiples!=1||(dictionary.TryGet(NameToken.UserUnit,out var unitToken)&&(unitToken is not NumericToken number||number.Double!=1)))throw new NotSupportedException("Non-default PDF UserUnit requires a separately validated page transform.");
        var crop=new CropBox(mediaBox.Bounds.Intersect(cropBox.Bounds)??cropBox.Bounds);
        return new RenderProcessor(pageNumber,ResourceStore,PdfScanner,PageContentParser,FilterProvider,crop,userSpaceUnit,rotation,initialMatrix,(PdfReader.ReaderOptions)ParsingOptions).Process(pageNumber,operations);
    }
}

internal sealed class RenderProcessor : BaseStreamProcessor<RenderedPdfPage>
{
    private readonly double width,height;
    private readonly float scale;
    private SKCanvas canvas=null!;
    private SKPath path=new();
    private PdfPoint? last,start;
    private FillingRule? pendingClip;
    private int markedDepth;
    public RenderProcessor(int page,IResourceStore resources,IPdfTokenScanner scanner,IPageContentParser parser,ILookupFilterProvider filters,CropBox crop,UserSpaceUnit unit,PageRotationDegrees rotation,TransformationMatrix initial,PdfReader.ReaderOptions options):base(page,resources,scanner,parser,filters,crop,unit,rotation,initial,options)
    {var visible=crop.GetVisibleBounds(rotation);width=visible.Width;height=visible.Height;scale=options.Dpi/72;}
    public override RenderedPdfPage Process(int pageNumberCurrent,IReadOnlyList<IGraphicsStateOperation> operations)
    {
        double w=Math.Ceiling(width*scale),h=Math.Ceiling(height*scale);if(w<=0||h<=0||w*h>16*1024*1024)throw new NotSupportedException("PDF page exceeds the 16 megapixel render budget.");
        var bitmap=new SKBitmap((int)w,(int)h);
        try
        {
            using(canvas=new SKCanvas(bitmap)){canvas.Clear(SKColors.White);canvas.Scale(scale,-scale);canvas.Translate(0,(float)-height);CloneAllStates();ProcessOperations(operations);if(markedDepth!=0)throw new InvalidDataException("Unbalanced PDF marked content.");canvas.Flush();}
            return new(bitmap,width,height);
        }
        catch{bitmap.Dispose();throw;}
        finally{path.Dispose();}
    }
    private static byte Byte(double value)=>(byte)Math.Clamp((int)Math.Round(value*255),0,255);
    private static SKColor Color(IColor color,double alpha)
    {
        if(color.ColorSpace is not (ColorSpace.DeviceRGB or ColorSpace.DeviceGray))throw new NotSupportedException($"PDF paint color space {color.ColorSpace} requires validated color management.");
        var (r,g,b)=color.ToRGBValues();return new(Byte(r),Byte(g),Byte(b),Byte(alpha));
    }
    private void CheckState()
    {var s=GetCurrentState();if(s.SoftMask is not null||s.BlendMode!=BlendMode.Normal||s.Overprint||s.NonStrokingOverprint)throw new NotSupportedException("PDF soft masks, non-normal blend modes and overprint are not supported.");}
    public override void PushState(){base.PushState();canvas.Save();}
    public override void PopState(){base.PopState();canvas.Restore();}
    private PdfPoint Map(double x,double y)=>GetCurrentState().CurrentTransformationMatrix.Transform(new PdfPoint(x,y));
    private static SKPoint Point(PdfPoint p)=>new((float)p.X,(float)p.Y);
    public override void BeginSubpath(){last=null;start=null;}
    public override void MoveTo(double x,double y){var p=Map(x,y);path.MoveTo(Point(p));last=start=p;}
    public override void LineTo(double x,double y){var p=Map(x,y);path.LineTo(Point(p));last=p;}
    public override void BezierCurveTo(double x1,double y1,double x2,double y2,double x3,double y3){var p=Map(x3,y3);path.CubicTo(Point(Map(x1,y1)),Point(Map(x2,y2)),Point(p));last=p;}
    public override void BezierCurveTo(double x2,double y2,double x3,double y3){var p=Map(x3,y3);path.CubicTo(Point(last??throw new InvalidDataException("Curve without current point.")),Point(Map(x2,y2)),Point(p));last=p;}
    public override void Rectangle(double x,double y,double width,double height){MoveTo(x,y);LineTo(x+width,y);LineTo(x+width,y+height);LineTo(x,y+height);CloseSubpath();}
    public override PdfPoint? CloseSubpath(){if(start is null)return null;path.Close();last=start;return start;}
    public override void ClosePath()=>CloseSubpath();
    private void Paint(SKPath geometry,bool stroke,FillingRule rule)
    {
        CheckState();var s=GetCurrentState();geometry.FillType=(int)rule==1?SKPathFillType.EvenOdd:SKPathFillType.Winding;
        using var paint=new SKPaint{IsAntialias=true,Color=Color(stroke?s.CurrentStrokingColor:s.CurrentNonStrokingColor,stroke?s.AlphaConstantStroking:s.AlphaConstantNonStroking)};
        if(stroke)
        {
            if(s.LineDashPattern.Array.Count!=0)throw new NotSupportedException("PDF dashed strokes are not supported.");
            var m=s.CurrentTransformationMatrix;double sx=Math.Sqrt(m.A*m.A+m.B*m.B),sy=Math.Sqrt(m.C*m.C+m.D*m.D);
            if(Math.Abs(sx-sy)>1e-6||Math.Abs(m.A*m.C+m.B*m.D)>1e-6)throw new NotSupportedException("PDF anisotropic strokes require stroking before transformation.");
            paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=(float)(s.LineWidth*sx);paint.StrokeMiter=(float)s.MiterLimit;paint.StrokeCap=(SKStrokeCap)s.CapStyle;paint.StrokeJoin=(SKStrokeJoin)s.JoinStyle;
        }
        canvas.DrawPath(geometry,paint);
    }
    public override void StrokePath(bool close){if(close)CloseSubpath();Paint(path,true,FillingRule.NonZeroWinding);EndPath();}
    public override void FillPath(FillingRule fillingRule,bool close){if(close)CloseSubpath();Paint(path,false,fillingRule);EndPath();}
    public override void FillStrokePath(FillingRule fillingRule,bool close){if(close)CloseSubpath();Paint(path,false,fillingRule);Paint(path,true,fillingRule);EndPath();}
    public override void ModifyClippingIntersect(FillingRule clippingRule)=>pendingClip=clippingRule;
    public override void EndPath(){if(pendingClip is {} rule){path.FillType=(int)rule==1?SKPathFillType.EvenOdd:SKPathFillType.Winding;canvas.ClipPath(path,antialias:true);}path.Dispose();path=new();pendingClip=null;last=start=null;}
    protected override void ClipToRectangle(PdfRectangle rectangle,FillingRule clippingRule)
    {using var clip=new SKPath();clip.MoveTo(Point(rectangle.BottomLeft));clip.LineTo(Point(rectangle.BottomRight));clip.LineTo(Point(rectangle.TopRight));clip.LineTo(Point(rectangle.TopLeft));clip.Close();canvas.ClipPath(clip,antialias:true);}
    public override void RenderGlyph(IFont font,CurrentGraphicsState currentState,double fontSize,double pointSize,int code,string unicode,long currentOffset,in TransformationMatrix renderingMatrix,in TransformationMatrix textMatrix,in TransformationMatrix transformationMatrix,CharacterBoundingBox characterBoundingBox)
    {
        int mode=(int)currentState.FontState.TextRenderingMode;if(mode==3)return;CheckState();
        if(font.IsVertical)throw new NotSupportedException("Vertical PDF font metrics and glyph placement have not been validated.");
        if(mode>=4)throw new NotSupportedException("PDF text clipping modes are not supported.");
        if(!font.TryGetNormalisedPath(code,out var outlines))
        {if(!string.IsNullOrEmpty(unicode)&&string.IsNullOrWhiteSpace(unicode))return;throw new NotSupportedException($"PDF glyph {code} has no supported outline; system font substitution is not automatic.");}
        using var glyph=new SKPath();var transform=renderingMatrix.Multiply(textMatrix).Multiply(transformationMatrix);
        SKPoint P(PdfPoint p)=>Point(transform.Transform(p));
        foreach(var subpath in outlines)foreach(var command in subpath.Commands)switch(command)
        {
            case PdfSubpath.Move m:glyph.MoveTo(P(m.Location));break;
            case PdfSubpath.Line l:glyph.LineTo(P(l.To));break;
            case PdfSubpath.CubicBezierCurve c:glyph.CubicTo(P(c.FirstControlPoint),P(c.SecondControlPoint),P(c.EndPoint));break;
            case PdfSubpath.QuadraticBezierCurve q:glyph.QuadTo(P(q.ControlPoint),P(q.EndPoint));break;
            case PdfSubpath.Close:glyph.Close();break;
            default:throw new NotSupportedException("Unknown PDF glyph path command.");
        }
        if(mode is 0 or 2)Paint(glyph,false,FillingRule.NonZeroWinding);if(mode is 1 or 2)Paint(glyph,true,FillingRule.NonZeroWinding);
    }
    protected override void ProcessFormXObject(StreamToken formStream,NameToken xObjectName)
    {if(formStream.StreamDictionary.ContainsKey(NameToken.Group))throw new NotSupportedException("PDF transparency form groups are not supported.");base.ProcessFormXObject(formStream,xObjectName);}
    protected override void RenderXObjectImage(XObjectContentRecord image)=>DrawImage(XObjectFactory.ReadImage(image,PdfScanner,FilterProvider,ResourceStore),image.AppliedTransformation);
    protected override void RenderInlineImage(InlineImage inlineImage)=>DrawImage(inlineImage,GetCurrentState().CurrentTransformationMatrix);
    private void DrawImage(IPdfImage image,TransformationMatrix matrix)
    {
        CheckState();using var bitmap=DecodeImage(image);int save=canvas.Save();
        try
        {
            var m=new SKMatrix{ScaleX=(float)matrix.A,SkewX=(float)matrix.C,TransX=(float)matrix.E,SkewY=(float)matrix.B,ScaleY=(float)matrix.D,TransY=(float)matrix.F,Persp2=1};
            canvas.Concat(m);canvas.Translate(0,1);canvas.Scale(1f/bitmap.Width,-1f/bitmap.Height);
            using var paint=new SKPaint{Color=new SKColor(255,255,255,Byte(GetCurrentState().AlphaConstantNonStroking))};
            using var snapshot=SKImage.FromBitmap(bitmap);canvas.DrawImage(snapshot,0,0,new SKSamplingOptions(image.Interpolate?SKFilterMode.Linear:SKFilterMode.Nearest),paint);
        }
        finally{canvas.RestoreToCount(save);}
    }
    private SKBitmap DecodeImage(IPdfImage image,int depth=0)
    {
        if(depth>16)throw new NotSupportedException("PDF image mask nesting exceeds 16.");
        if(image.WidthInSamples<=0||image.HeightInSamples<=0||(long)image.WidthInSamples*image.HeightInSamples>16*1024*1024)throw new NotSupportedException("PDF image exceeds the pixel budget.");
        if(image.ImageDictionary.TryGet(NameToken.Mask,out ArrayToken _))throw new NotSupportedException("PDF color-key image masks are not supported.");
        SKBitmap bitmap;
        if(image.RawBytes.StartsWith(new byte[]{255,216}))
        {
            if(image.Decode.Count!=0)throw new NotSupportedException("JPEG PDF Decode remapping requires conversion before JPEG color management.");
            bitmap=SKBitmap.Decode(image.RawBytes);
        }
        else
        {
            var space=image.ColorSpaceDetails;
            if(!image.IsImageMask&&(space is null||space.Type is not (ColorSpace.DeviceRGB or ColorSpace.DeviceGray)))throw new NotSupportedException("PDF images support RGB/Gray; other profiles require validated color management.");
            int bits=image.BitsPerComponent;
            if(bits is not (1 or 2 or 4 or 8 or 16))throw new NotSupportedException("Unsupported PDF image sample size.");
            if(!image.TryGetBytesAsMemory(out var data))throw new NotSupportedException("PDF image filter is unsupported.");
            int components=image.IsImageMask?1:space!.NumberOfColorComponents,row=checked((image.WidthInSamples*components*bits+7)/8),expected=checked(row*image.HeightInSamples);
            if(data.Length!=expected)throw new InvalidDataException("PDF image byte count mismatch.");
            if(image.Decode.Count!=0&&image.Decode.Count!=components*2)throw new InvalidDataException("Invalid PDF image Decode array.");
            bitmap=new(new SKImageInfo(image.WidthInSamples,image.HeightInSamples,SKColorType.Rgba8888,SKAlphaType.Unpremul));var target=bitmap.GetPixelSpan();var source=data.Span;
            SKColor stencil=image.IsImageMask?Color(GetCurrentState().CurrentNonStrokingColor,1):SKColors.Black;
            for(int y=0;y<image.HeightInSamples;y++)for(int x=0;x<image.WidthInSamples;x++)
            {
                int n=(y*image.WidthInSamples+x)*4;
                for(int c=0;c<(image.IsImageMask?1:3);c++)
                {
                    int component=components==1?0:c,bit=x*components*bits+component*bits,value=0;
                    for(int b=0;b<bits;b++)value=(value<<1)|((source[y*row+((bit+b)>>3)]>>(7-((bit+b)&7)))&1);
                    double decoded=(double)value/((1<<bits)-1);
                    if(image.Decode.Count!=0)decoded=image.Decode[component*2]+decoded*(image.Decode[component*2+1]-image.Decode[component*2]);
                    if(image.IsImageMask){target[n]=stencil.Red;target[n+1]=stencil.Green;target[n+2]=stencil.Blue;target[n+3]=Byte(1-decoded);}
                    else target[n+c]=Byte(decoded);
                }
                if(!image.IsImageMask)target[n+3]=255;
            }
        }
        try
        {
            if(image.MaskImage is {} mask)
            {
                if(mask.ImageDictionary.ContainsKey(NameToken.Create("Matte")))throw new NotSupportedException("PDF image soft masks with preblended Matte colors require unmatting.");
                using var alpha=DecodeImage(mask,depth+1);var bytes=bitmap.GetPixelSpan();
                for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)
                {var m=alpha.GetPixel(x*alpha.Width/bitmap.Width,y*alpha.Height/bitmap.Height);bytes[y*bitmap.RowBytes+x*4+3]=PixelBuffer.Multiply(bytes[y*bitmap.RowBytes+x*4+3],mask.IsImageMask?m.Alpha:m.Red);}
            }
            return bitmap;
        }
        catch{bitmap.Dispose();throw;}
    }
    public override void BeginMarkedContent(NameToken name,NameToken? propertyDictionaryName,DictionaryToken? properties)
    {if(name.Data=="OC")throw new NotSupportedException("PDF optional-content visibility requires configuration.");markedDepth++;}
    public override void EndMarkedContent(){if(markedDepth==0)throw new InvalidDataException("Unbalanced PDF marked content.");markedDepth--;}
    public override void PaintShading(NameToken shadingName)=>throw new NotSupportedException("PDF shading patterns are not supported.");
}
