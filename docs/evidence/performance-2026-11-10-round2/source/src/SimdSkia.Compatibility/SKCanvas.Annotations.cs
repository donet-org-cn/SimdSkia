using SimdSkia;
namespace SkiaSharp;

internal sealed record CanvasAnnotation(SKRect Bounds,string Key,byte[] Value);
public partial class SKCanvas
{
    internal Action<CanvasAnnotation>? AnnotationSink {get;set;}
    internal List<CanvasAnnotation>? AnnotationMetadata {get;private set;}
    private long annotationBytes;
    private int annotationCount;
    public void DrawAnnotation(SKRect rect,string key,SKData? value)
    {
        Validate();ArgumentNullException.ThrowIfNull(key);if(!rect.ToCore().IsFinite)throw new ArgumentException("Annotation coordinates must be finite.",nameof(rect));
        if(value is null)return;var data=value.ToArray();EmitAnnotation(new(rect,key,data));
    }
    private void EmitAnnotation(CanvasAnnotation annotation)
    {
        if(annotationCount>=65536||annotation.Value.Length>16*1024*1024||annotationBytes+annotation.Value.Length>16*1024*1024)throw new NotSupportedException("Canvas annotations exceed the 65536 item or 16 MiB payload budget.");
        annotationCount++;
        annotationBytes+=annotation.Value.Length;
        if(recording is not null){recording.Add(new(PictureCommands.Kind.Annotation,matrix,Annotation:annotation));return;}
        if(forwarding is not null)foreach(var child in forwarding)child.EmitAnnotation(annotation);
        var rect=annotation.Bounds;var a=matrix.Map(rect.Left,rect.Top);var b=matrix.Map(rect.Right,rect.Top);var c=matrix.Map(rect.Left,rect.Bottom);var d=matrix.Map(rect.Right,rect.Bottom);
        var transformed=new SKRect(MathF.Min(MathF.Min(a.X,b.X),MathF.Min(c.X,d.X)),MathF.Min(MathF.Min(a.Y,b.Y),MathF.Min(c.Y,d.Y)),MathF.Max(MathF.Max(a.X,b.X),MathF.Max(c.X,d.X)),MathF.Max(MathF.Max(a.Y,b.Y),MathF.Max(c.Y,d.Y)));
        if(annotation.Key!="SkAnnotationKey_Define_Named_Dest")
        {var clip=DeviceClipBounds;transformed=new(MathF.Max(clip.Left,transformed.Left),MathF.Max(clip.Top,transformed.Top),MathF.Min(clip.Right,transformed.Right),MathF.Min(clip.Bottom,transformed.Bottom));if(transformed.IsEmpty)return;}
        var mapped=annotation with {Bounds=transformed};
        if(AnnotationSink is {} sink)sink(mapped);else (AnnotationMetadata??=[]).Add(mapped);
    }
    private static SKData AnnotationString(string value){ArgumentNullException.ThrowIfNull(value);return SKData.CreateCopy(System.Text.Encoding.UTF8.GetBytes(value+"\0"));}
    public void DrawUrlAnnotation(SKRect rect,SKData value)=>DrawAnnotation(rect,"SkAnnotationKey_URL",value);
    public SKData DrawUrlAnnotation(SKRect rect,string value){var data=AnnotationString(value);try{DrawUrlAnnotation(rect,data);return data;}catch{data.Dispose();throw;}}
    public void DrawNamedDestinationAnnotation(SKPoint point,SKData value)=>DrawAnnotation(new(point.X,point.Y,point.X,point.Y),"SkAnnotationKey_Define_Named_Dest",value);
    public SKData DrawNamedDestinationAnnotation(SKPoint point,string value){var data=AnnotationString(value);try{DrawNamedDestinationAnnotation(point,data);return data;}catch{data.Dispose();throw;}}
    public void DrawLinkDestinationAnnotation(SKRect rect,SKData value)=>DrawAnnotation(rect,"SkAnnotationKey_Link_Named_Dest",value);
    public SKData DrawLinkDestinationAnnotation(SKRect rect,string value){var data=AnnotationString(value);try{DrawLinkDestinationAnnotation(rect,data);return data;}catch{data.Dispose();throw;}}
}
