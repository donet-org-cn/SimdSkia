using SimdSkia;
namespace SkiaSharp;
public struct SKCanvasSaveLayerRec
{
    public SKRect? Bounds {readonly get;set;}
    public SKPaint? Paint {readonly get;set;}
    public SKImageFilter? Backdrop {readonly get;set;}
    public SKCanvasSaveLayerRecFlags Flags {readonly get;set;}
}
public partial class SKCanvas
{
    public int SaveLayer(in SKCanvasSaveLayerRec rec)
    {
        if(noDraw)return SaveNoDraw(rec);
        if(recording is not null)return RecordLayer(rec);
        if(backend is SimdSkia.Gpu.IGpuCanvas){Validate();throw new NotSupportedException("GPU save layers require GPU offscreen attachments.");}
        Validate();if((rec.Flags&~SKCanvasSaveLayerRecFlags.InitializeWithPrevious)!=0)throw new NotSupportedException("F16 and LCD-preserving layers require pixel/text formats not implemented by this raster backend.");
        if(rec.Bounds is {} bounds&&!bounds.ToCore().IsFinite)throw new ArgumentException("Layer bounds must be finite.",nameof(rec));
        var backdrop=rec.Backdrop?.ToCore();rec.Paint?.ThrowIfDisposed();
        if(backdrop is null&&(rec.Flags&SKCanvasSaveLayerRecFlags.InitializeWithPrevious)==0)return rec.Bounds is {} b?SaveLayer(b,rec.Paint):SaveLayer(rec.Paint);
        // Evaluate before changing the state stack. An invalid filter cannot
        // composite a partially initialized layer into the parent on rollback.
        backend.Flush();using var previous=target.Share();using var source=new FilterFrame(previous.Share());
        using var filtered=backdrop?.Apply(source,matrix,clip??new(0,0,CanvasWidth,CanvasHeight));var initial=filtered??source;
        int count=backdrop is null&&rec.Bounds is {} limit?SaveLayer(limit,rec.Paint):SaveLayer(rec.Paint);
        if(initial.Pixels is {} image)
            backend.Execute(new(CommandKind.DrawImage,initial.Bounds,Matrix3.Identity,new(new(255,255,255,255),CompositeMode.Source)){Image=image,Clip=clip});
        return count;
    }
}
