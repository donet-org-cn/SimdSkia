using SimdSkia;
using SimdSkia.Cpu;
namespace SkiaSharp;

public partial class SKCanvas
{
    private int? logicalWidth,logicalHeight;
    private bool noDraw,overdraw;
    private List<SKCanvas>? forwarding;
    private int CanvasWidth=>logicalWidth??target.Width;
    private int CanvasHeight=>logicalHeight??target.Height;
    internal SKCanvas(int width,int height)
    {
        if(width<0||height<0)throw new ArgumentOutOfRangeException(nameof(width));
        target=new(1,1,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);backend=new RasterCanvas(target);logicalWidth=width;logicalHeight=height;noDraw=true;
    }
    internal void EnableForwarding(bool overdraw=false){forwarding=[];this.overdraw=overdraw;}
    internal void AddForward(SKCanvas canvas)
    {
        Validate();ArgumentNullException.ThrowIfNull(canvas);canvas.Validate();
        if(ReferenceEquals(canvas,this)||canvas.Reaches(this))throw new ArgumentException("Canvas forwarding cannot contain a cycle.",nameof(canvas));
        forwarding!.Add(canvas);
    }
    private bool Reaches(SKCanvas other)=>forwarding?.Any(c=>ReferenceEquals(c,other)||c.Reaches(other))==true;
    internal void RemoveForward(SKCanvas canvas){Validate();forwarding!.Remove(canvas);}
    internal void ClearForwards(){Validate();forwarding!.Clear();}
    private static Paint OverdrawPaint(Paint original)=>new(new(0,0,0,1),CompositeMode.Plus,false);
    private void ForwardDrawing(DrawingCommand command,SKPaint? owner)
    {
        if(forwarding is null)return;if(!matrix.TryInvert(out var inverse))return;
        foreach(var canvas in forwarding)
        {
            canvas.Validate();var relative=canvas.matrix*inverse;
            var paint=overdraw?OverdrawPaint(command.Paint):command.Paint;
            var forwarded=command with {Transform=relative*command.Transform,ShaderTransform=relative*(command.ShaderTransform??command.Transform),Paint=paint};
            if(overdraw&&forwarded.Kind==CommandKind.DrawImage)forwarded=forwarded with {Kind=CommandKind.FillRectangle,Image=null};
            canvas.Execute(forwarded,overdraw?null:owner);
        }
    }
    private int SaveNoDraw(SKCanvasSaveLayerRec? layer=null)
    {
        Validate();int count=SaveCount;
        if(forwarding is not null)foreach(var child in forwarding){if(layer is {} rec)child.SaveLayer(rec);else child.Save();}
        states.Add(new(matrix,clip,coverageClip));
        if(layer?.Bounds is {} bounds){var m=matrix;var a=m.Map(bounds.Left,bounds.Top);var b=m.Map(bounds.Right,bounds.Bottom);matrix=Matrix3.Identity;try{ClipRect(new(MathF.Floor(MathF.Min(a.X,b.X)),MathF.Floor(MathF.Min(a.Y,b.Y)),MathF.Ceiling(MathF.Max(a.X,b.X)),MathF.Ceiling(MathF.Max(a.Y,b.Y))));}finally{matrix=m;}}
        return count;
    }
    public void Discard(){Validate();if(forwarding is not null)foreach(var child in forwarding)child.Discard();backend.Flush();if(backend is RasterCanvas raster)raster.DropCaches();ClearAtlasCache();ClearPatchCache();strokes.Clear();strokeBytes=0;rectangles.Clear();}
    public void Clear(SKColorF color)=>DrawColor(color,SKBlendMode.Src);
    public void DrawColor(SKColorF color,SKBlendMode mode=SKBlendMode.Src)
    {Validate();using var shader=SKShader.CreateColor(color,null);using var paint=new SKPaint{Shader=shader,BlendMode=mode};DrawPaint(paint);}
    public void DrawRoundRectDifference(SKRoundRect outer,SKRoundRect inner,SKPaint paint)
    {Validate();ArgumentNullException.ThrowIfNull(outer);ArgumentNullException.ThrowIfNull(inner);ArgumentNullException.ThrowIfNull(paint);using var a=outer.ToPath();using var b=inner.ToPath();using var difference=a.Op(b,SKPathOp.Difference);DrawPath(difference,paint);}
    private ClipDescription RecordingClipDescription()
    {
        using var probe=new SKNoDrawCanvas(Math.Max(0,(int)MathF.Ceiling(recording!.Cull.Right)),Math.Max(0,(int)MathF.Ceiling(recording.Cull.Bottom)));probe.ClipRect(recording.Cull);
        foreach(var op in recording.Items)
        {
            probe.matrix=op.Matrix;
            switch(op.Kind)
            {case PictureCommands.Kind.Save:probe.Save();break;case PictureCommands.Kind.Restore:probe.Restore();break;case PictureCommands.Kind.Layer:probe.SaveLayer(op.Layer);break;case PictureCommands.Kind.Clip:using(var path=new SKPath(op.Path!))probe.ClipPath(path,op.ClipOperation,op.Antialias);break;}
        }
        return new(null,null,probe.DeviceClipBounds,probe.IsClipRect);
    }
}
public class SKNoDrawCanvas : SKCanvas
{public SKNoDrawCanvas(int width,int height):base(width,height) { }}
public class SKNWayCanvas : SKNoDrawCanvas
{
    public SKNWayCanvas(int width,int height):base(width,height)=>EnableForwarding();
    public void AddCanvas(SKCanvas canvas)=>AddForward(canvas);
    public void RemoveCanvas(SKCanvas canvas)=>RemoveForward(canvas);
    public void RemoveAll()=>ClearForwards();
}
public class SKOverdrawCanvas : SKNWayCanvas
{public SKOverdrawCanvas(SKCanvas canvas):base((canvas??throw new ArgumentNullException(nameof(canvas))).DeviceClipBounds.Right,canvas.DeviceClipBounds.Bottom){EnableForwarding(true);AddCanvas(canvas);}}
