using System.IO.Compression;
using System.Text;
using System.Xml;
using SimdSkia;
using SimdSkia.Imaging;
namespace SkiaSharp;

/// <summary>Managed XPS/OPC package with retained vector paths and PNG assets.</summary>
internal sealed class XpsDocumentWriter(Stream output):IDisposable
{
    internal const string Ns="http://schemas.microsoft.com/xps/2005/06";
    private readonly ZipArchive zip=new(output,ZipArchiveMode.Create,true);
    private readonly List<(float Width,float Height)> pages=[];
    private readonly Dictionary<int,List<string>> resources=[];
    private bool closed;
    internal void AddPage(PictureCommands commands,float width,float height)
    {
        if(closed)throw new InvalidOperationException("The XPS document is closed.");int number=pages.Count+1;
        using var page=new MemoryStream();
        using(var writer=new XpsPage(page,this,new(0,0,width,height),number))writer.Replay(commands);
        using(var file=zip.CreateEntry($"Documents/1/Pages/{number}.fpage",CompressionLevel.Optimal).Open()){page.Position=0;page.CopyTo(file);}
        if(resources.TryGetValue(number,out var assets))Xml($"Documents/1/Pages/_rels/{number}.fpage.rels",x=>{x.WriteStartElement("Relationships",Relationships);for(int i=0;i<assets.Count;i++){x.WriteStartElement("Relationship",Relationships);x.WriteAttributeString("Id","R"+i);x.WriteAttributeString("Type",Ns+"/required-resource");x.WriteAttributeString("Target",assets[i]);x.WriteEndElement();}x.WriteEndElement();});
        pages.Add((width,height));
    }
    private const string Relationships="http://schemas.openxmlformats.org/package/2006/relationships",ContentTypes="http://schemas.openxmlformats.org/package/2006/content-types";
    internal string AddImage(PixelBuffer pixels,int page,int index)
    {string path=$"Resources/Images/p{page}-{index}.png";using(var image=zip.CreateEntry(path,CompressionLevel.Optimal).Open())image.Write(PngCodec.Encode(pixels));if(!resources.TryGetValue(page,out var list))resources[page]=list=[];list.Add("/"+path);return "/"+path;}
    private void Xml(string path,Action<XmlWriter> write)
    {using var stream=zip.CreateEntry(path,CompressionLevel.Optimal).Open();using var xml=XmlWriter.Create(stream,new(){Encoding=new UTF8Encoding(false),CloseOutput=false});write(xml);}
    internal void Close()
    {
        if(closed)return;closed=true;
        Xml("_rels/.rels",x=>{x.WriteStartElement("Relationships",Relationships);x.WriteStartElement("Relationship",Relationships);x.WriteAttributeString("Id","R1");x.WriteAttributeString("Type",Ns+"/fixedrepresentation");x.WriteAttributeString("Target","/FixedDocumentSequence.fdseq");x.WriteEndElement();x.WriteEndElement();});
        Xml("FixedDocumentSequence.fdseq",x=>{x.WriteStartElement("FixedDocumentSequence",Ns);x.WriteStartElement("DocumentReference",Ns);x.WriteAttributeString("Source","/Documents/1/FixedDocument.fdoc");x.WriteEndElement();x.WriteEndElement();});
        Xml("Documents/1/FixedDocument.fdoc",x=>{x.WriteStartElement("FixedDocument",Ns);for(int i=0;i<pages.Count;i++){x.WriteStartElement("PageContent",Ns);x.WriteAttributeString("Source",$"Pages/{i+1}.fpage");x.WriteEndElement();}x.WriteEndElement();});
        Xml("[Content_Types].xml",x=>{x.WriteStartElement("Types",ContentTypes);void Default(string ext,string type){x.WriteStartElement("Default",ContentTypes);x.WriteAttributeString("Extension",ext);x.WriteAttributeString("ContentType",type);x.WriteEndElement();}void Part(string name,string type){x.WriteStartElement("Override",ContentTypes);x.WriteAttributeString("PartName",name);x.WriteAttributeString("ContentType","application/vnd.ms-package.xps-"+type+"+xml");x.WriteEndElement();}Default("rels","application/vnd.openxmlformats-package.relationships+xml");Default("png","image/png");Default("bin","application/octet-stream");Part("/FixedDocumentSequence.fdseq","fixeddocumentsequence");Part("/Documents/1/FixedDocument.fdoc","fixeddocument");for(int i=0;i<pages.Count;i++)Part($"/Documents/1/Pages/{i+1}.fpage","fixedpage");x.WriteEndElement();});
        zip.Dispose();output.Flush();
    }
    public void Dispose()=>Close();
    internal void Abort(){if(closed)return;closed=true;zip.Dispose();}

    private sealed class XpsPage:VectorDocument,IDisposable
    {
        private readonly XmlWriter writer;
        private readonly XpsDocumentWriter package;
        private readonly int page;
        private int depth,imageIndex;
        private readonly Stack<int> states=[];
        internal XpsPage(Stream output,XpsDocumentWriter package,SKRect bounds,int page):base(bounds)
        {
            this.package=package;this.page=page;writer=XmlWriter.Create(output,new(){Encoding=new UTF8Encoding(false),CloseOutput=false});Start("FixedPage");Attr("Width",Number(bounds.Width*(96f/72)));Attr("Height",Number(bounds.Height*(96f/72)));writer.WriteAttributeString("xml","lang",null,"en-US");Group();Attr("RenderTransform","1.3333333333333333,0,0,1.3333333333333333,0,0");
        }
        private void Start(string name)=>writer.WriteStartElement(name,Ns);
        private void Attr(string name,string value)=>writer.WriteAttributeString(name,value);
        private static string Matrix(Matrix3 m)=>MatrixValues(m).Replace(' ',',');
        private void Group(){Start("Canvas");depth++;}
        private void CloseGroups(int count){while(depth>count){writer.WriteEndElement();depth--;}}
        protected override void Save()=>states.Push(depth);
        protected override void Restore(){if(states.Count==0)throw new InvalidDataException("Unbalanced XPS restore.");CloseGroups(states.Pop());}
        protected override void Clip(PathGeometry path,Matrix3 matrix,SKClipOperation operation)
        {
            if(operation!=SKClipOperation.Intersect||path.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd)throw new NotSupportedException("XPS inverse/difference clipping requires a geometry combination serializer not implemented yet.");Group();Attr("Clip",(path.FillRule==FillRule.EvenOdd?"F0 ":"F1 ")+PathData(path.Transform(matrix),Matrix3.Identity));
        }
        protected override void Layer(SKCanvasSaveLayerRec layer,Matrix3 matrix)
        {if(layer.Backdrop is not null||layer.Flags!=0)throw new NotSupportedException("XPS backdrop/initialized layers are not implemented.");Save();if(layer.Bounds is {} b)Clip(PathGeometry.Rectangle(b.ToCore()),matrix,SKClipOperation.Intersect);Group();if(layer.Paint is {} p){var material=p.ToCore(true);ValidatePaint(material);if(material.Shader is not null)throw new NotSupportedException("XPS layer shaders are not implemented.");Attr("Opacity",Number(Opacity(material)));}}
        private static string Color(Rgba32 c)=>$"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        private static string PaintColor(Paint p)
        {if(p.FloatingColor is not {} f)return Color(p.Color);var transfer=SKColorSpaceTransferFn.Srgb;return $"sc#{Number(f.W)},{Number(transfer.Transform(f.X))},{Number(transfer.Transform(f.Y))},{Number(transfer.Transform(f.Z))}";}
        protected override void Path(PathGeometry path,Matrix3 matrix,Paint paint,Matrix3 shaderMatrix)
        {
            ValidatePaint(paint);if(path.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd)throw new NotSupportedException("XPS inverse path serialization is not implemented.");
            Start("Path");Attr("Data",(path.FillRule==FillRule.EvenOdd?"F0 ":"F1 ")+PathData(path,matrix));Attr("RenderTransform",Matrix(matrix));
            if(paint.Shader is null)Attr("Fill",PaintColor(paint));
            else
            {
                var g=Gradient(paint.Shader)??throw new NotSupportedException("This XPS writer does not serialize this shader type.");if(!matrix.TryInvert(out var inverse)||!g.SampleTransform.TryInvert(out var local))throw new NotSupportedException("XPS gradient transforms must be invertible.");Attr("Opacity",Number(Opacity(paint)));Start("Path.Fill");Start("LinearGradientBrush");Attr("MappingMode","Absolute");Attr("StartPoint",Number(g.Start.X)+","+Number(g.Start.Y));Attr("EndPoint",Number(g.End.X)+","+Number(g.End.Y));Attr("ColorInterpolationMode","SRgbLinearInterpolation");Attr("SpreadMethod",g.Extend switch{GradientExtendMode.Repeat=>"Repeat",GradientExtendMode.Mirror=>"Reflect",_=>"Pad"});Attr("Transform",Matrix(inverse*shaderMatrix*local));
                foreach(var stop in g.Stops.Span){Start("GradientStop");Attr("Color",Color(stop.Color));Attr("Offset",Number(stop.Offset));writer.WriteEndElement();}writer.WriteEndElement();writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        protected override void Image(DrawingCommand c)
        {
            ValidatePaint(c.Paint);if(c.Paint.Shader is not null||c.Image?.Format==PixelFormat.Alpha8)throw new NotSupportedException("XPS image shaders/Alpha8 mask serialization is not implemented.");var image=c.Image!;var source=c.ImageSource??new(0,0,image.Width,image.Height);var dest=c.Rectangle;if(source.IsEmpty||dest.IsEmpty)return;
            string asset=package.AddImage(image,page,++imageIndex);Start("Path");Attr("Data",PathData(PathGeometry.Rectangle(dest),c.Transform));Attr("RenderTransform",Matrix(c.Transform));Attr("Opacity",Number(Opacity(c.Paint)));Start("Path.Fill");Start("ImageBrush");Attr("ImageSource",asset);Attr("Viewbox",$"{Number(source.Left)},{Number(source.Top)},{Number(source.Right-source.Left)},{Number(source.Bottom-source.Top)}");Attr("Viewport",$"{Number(dest.Left)},{Number(dest.Top)},{Number(dest.Right-dest.Left)},{Number(dest.Bottom-dest.Top)}");Attr("ViewboxUnits","Absolute");Attr("ViewportUnits","Absolute");writer.WriteEndElement();writer.WriteEndElement();writer.WriteEndElement();
        }
        protected override void Annotation(CanvasAnnotation annotation,Matrix3 matrix)
        {
            // Unknown nonvisual annotation payload is retained as OPC metadata.
            // It is not treated as a visible annotation appearance.
            string name=$"Metadata/p{page}-{++imageIndex}.bin";using var file=package.zip.CreateEntry(name).Open();file.Write(annotation.Value);
        }
        public void Dispose(){CloseGroups(0);writer.WriteEndElement();writer.Dispose();}
    }
}
