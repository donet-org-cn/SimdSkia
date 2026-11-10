using System.Text;
using System.Xml;
using SimdSkia;
using SimdSkia.Imaging;
namespace SkiaSharp;

public class SKSvgCanvas
{
    private SKSvgCanvas() { }
    public static SKCanvas Create(SKRect bounds,Stream stream)
    {ArgumentNullException.ThrowIfNull(stream);if(!stream.CanWrite)throw new ArgumentException("SVG output must be writable.",nameof(stream));if(!bounds.ToCore().IsFinite)throw new ArgumentException("SVG bounds must be finite.",nameof(bounds));return new SvgCanvas(new(SKRect.Create(bounds.Width,bounds.Height)),stream,false);}
    public static SKCanvas Create(SKRect bounds,SKWStream stream)
    {ArgumentNullException.ThrowIfNull(stream);stream.ThrowIfDisposed();if(!bounds.ToCore().IsFinite)throw new ArgumentException("SVG bounds must be finite.",nameof(bounds));return new SvgCanvas(new(SKRect.Create(bounds.Width,bounds.Height)),new WStreamOutput(stream),true);}
    private sealed class SvgCanvas:SKCanvas
    {
        private readonly PictureCommands commands;
        private readonly Stream stream;
        private readonly bool owns;
        internal SvgCanvas(PictureCommands commands,Stream stream,bool owns):base(commands){this.commands=commands;this.stream=stream;this.owns=owns;}
        private bool finished;
        protected override void Dispose(bool disposing)
        {if(finished)return;finished=true;try{RestoreToCount(1);using var writer=new SvgDocument(stream,commands.Cull);writer.Replay(commands);}finally{try{base.Dispose(disposing);}finally{try{commands.Dispose();}finally{if(owns)stream.Dispose();}}}}
    }
}
internal sealed class SvgDocument : VectorDocument,IDisposable
{
    private const string Ns="http://www.w3.org/2000/svg",Xlink="http://www.w3.org/1999/xlink";
    private readonly XmlWriter writer;
    private readonly Stack<int> states=[];
    private int depth,id;
    internal SvgDocument(Stream stream,SKRect bounds):base(bounds)
    {
        writer=XmlWriter.Create(stream,new(){Encoding=new UTF8Encoding(false),CloseOutput=false,Indent=false});writer.WriteStartDocument();Start("svg");Attr("version","1.1");writer.WriteAttributeString("xmlns","xlink",null,Xlink);Attr("width",Number(MathF.Max(0,bounds.Width)));Attr("height",Number(MathF.Max(0,bounds.Height)));Attr("viewBox",$"{Number(bounds.Left)} {Number(bounds.Top)} {Number(bounds.Width)} {Number(bounds.Height)}");
    }
    private void Start(string name)=>writer.WriteStartElement(name,Ns);
    private void Attr(string name,string value)=>writer.WriteAttributeString(name,value);
    private void Group(){Start("g");depth++;}
    private void CloseGroups(int count){while(depth>count){writer.WriteEndElement();depth--;}}
    protected override void Save()=>states.Push(depth);
    protected override void Restore(){if(states.Count==0)throw new InvalidDataException("Unbalanced document restore.");CloseGroups(states.Pop());}
    protected override void Clip(PathGeometry path,Matrix3 matrix,SKClipOperation operation)
    {
        bool invert=(path.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd)^(operation==SKClipOperation.Difference);string name="c"+(++id);Start("defs");Start(invert?"mask":"clipPath");Attr("id",name);Attr(invert?"maskUnits":"clipPathUnits","userSpaceOnUse");
        if(invert){Attr("x",Number(Bounds.Left));Attr("y",Number(Bounds.Top));Attr("width",Number(Bounds.Width));Attr("height",Number(Bounds.Height));Start("rect");Rect(Bounds.ToCore());Attr("fill","white");writer.WriteEndElement();}
        Start("path");Attr("d",PathData(path,matrix));Attr("transform","matrix("+MatrixValues(matrix)+")");Attr(invert?"fill-rule":"clip-rule",(path.FillRule is FillRule.EvenOdd or FillRule.InverseEvenOdd)?"evenodd":"nonzero");if(invert)Attr("fill","black");writer.WriteEndElement();writer.WriteEndElement();writer.WriteEndElement();Group();Attr(invert?"mask":"clip-path","url(#"+name+")");
    }
    protected override void Layer(SKCanvasSaveLayerRec layer,Matrix3 matrix)
    {
        if(layer.Backdrop is not null||layer.Flags!=0)throw new NotSupportedException("SVG backdrop/initialized layers are not implemented.");Save();if(layer.Bounds is {} b)Clip(PathGeometry.Rectangle(b.ToCore()),matrix,SKClipOperation.Intersect);Group();
        if(layer.Paint is {} p){var material=p.ToCore(true);ValidatePaint(material);if(material.Shader is not null)throw new NotSupportedException("A layer shader is not supported.");Attr("opacity",Number(Opacity(material)));}
    }
    protected override void Path(PathGeometry path,Matrix3 matrix,Paint paint,Matrix3 shaderMatrix)
    {
        ValidatePaint(paint);bool inverse=path.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd;
        if(inverse){Save();Clip(path,matrix,SKClipOperation.Intersect);Path(PathGeometry.Rectangle(Bounds.ToCore()),Matrix3.Identity,paint,shaderMatrix);Restore();return;}
        string fill=Fill(paint,matrix,shaderMatrix);Start("path");Attr("d",PathData(path,matrix));Attr("transform","matrix("+MatrixValues(matrix)+")");Attr("fill-rule",path.FillRule==FillRule.EvenOdd?"evenodd":"nonzero");Attr("fill",fill);Attr("fill-opacity",Number(Opacity(paint)));writer.WriteEndElement();
    }
    private string Fill(Paint paint,Matrix3 matrix,Matrix3 shaderMatrix)
    {
        if(paint.Shader is null)return paint.FloatingColor is {} f?$"rgb({Number(f.X*100)}%,{Number(f.Y*100)}%,{Number(f.Z*100)}%)":$"#{paint.Color.R:X2}{paint.Color.G:X2}{paint.Color.B:X2}";
        if(paint.Shader is SolidColorShader solid)return $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}";
        var g=Gradient(paint.Shader)??throw new NotSupportedException("This SVG writer does not serialize this shader type.");if(!matrix.TryInvert(out var geometryInverse)||!g.SampleTransform.TryInvert(out var local))throw new NotSupportedException("SVG gradient transforms must be invertible.");
        string name="g"+(++id);Start("defs");Start("linearGradient");Attr("id",name);Attr("gradientUnits","userSpaceOnUse");Attr("x1",Number(g.Start.X));Attr("y1",Number(g.Start.Y));Attr("x2",Number(g.End.X));Attr("y2",Number(g.End.Y));Attr("spreadMethod",g.Extend switch{GradientExtendMode.Repeat=>"repeat",GradientExtendMode.Mirror=>"reflect",_=>"pad"});Attr("gradientTransform","matrix("+MatrixValues(geometryInverse*shaderMatrix*local)+")");
        foreach(var stop in g.Stops.Span){Start("stop");Attr("offset",Number(stop.Offset));Attr("stop-color",$"#{stop.Color.R:X2}{stop.Color.G:X2}{stop.Color.B:X2}");Attr("stop-opacity",Number(stop.Color.A/255f));writer.WriteEndElement();}writer.WriteEndElement();writer.WriteEndElement();return "url(#"+name+")";
    }
    private void Rect(RectF rect){Attr("x",Number(rect.Left));Attr("y",Number(rect.Top));Attr("width",Number(rect.Right-rect.Left));Attr("height",Number(rect.Bottom-rect.Top));}
    protected override void Image(DrawingCommand c)
    {
        ValidatePaint(c.Paint);if(c.Paint.Shader is not null||c.Image?.Format==PixelFormat.Alpha8)throw new NotSupportedException("SVG image material/Alpha8 mask export is not implemented.");var image=c.Image!;var source=c.ImageSource??new(0,0,image.Width,image.Height);var dest=c.Rectangle;if(source.IsEmpty||dest.IsEmpty)return;
        Save();Clip(PathGeometry.Rectangle(dest),c.Transform,SKClipOperation.Intersect);float sx=(dest.Right-dest.Left)/(source.Right-source.Left),sy=(dest.Bottom-dest.Top)/(source.Bottom-source.Top);Start("image");Attr("x",Number(dest.Left-source.Left*sx));Attr("y",Number(dest.Top-source.Top*sy));Attr("width",Number(image.Width*sx));Attr("height",Number(image.Height*sy));Attr("transform","matrix("+MatrixValues(c.Transform)+")");Attr("preserveAspectRatio","none");Attr("opacity",Number(Opacity(c.Paint)));if(c.Sampling==ImageSampling.Nearest)Attr("style","image-rendering:pixelated");writer.WriteAttributeString("xlink","href",Xlink,"data:image/png;base64,"+Convert.ToBase64String(PngCodec.Encode(image)));writer.WriteEndElement();Restore();
    }
    protected override void Annotation(CanvasAnnotation annotation,Matrix3 matrix)
    {Start("metadata");Attr("data-simdskia-key",annotation.Key);Attr("data-simdskia-transform",MatrixValues(matrix));writer.WriteBase64(annotation.Value,0,annotation.Value.Length);writer.WriteEndElement();}
    public void Dispose(){CloseGroups(0);writer.WriteEndElement();writer.WriteEndDocument();writer.Dispose();}
}
