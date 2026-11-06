using SimdSkia;

namespace SkiaSharp;

public partial class SKCanvas
{
    public void DrawColor(SKColor color,SKBlendMode mode=SKBlendMode.Src)
    {
        Validate();if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        FillCanvas(new(color.ToCore(),SKPaint.GetComposite(mode)));
    }
    public void DrawPaint(SKPaint paint)
    {Validate();ArgumentNullException.ThrowIfNull(paint);FillCanvas(paint.ToCore(ignoreStyle:true),paint);}
    private void FillCanvas(Paint paint,SKPaint? owner=null)
    {
        if(noDraw){if(forwarding is not null)foreach(var child in forwarding)child.FillCanvas(overdraw?OverdrawPaint(paint):paint,overdraw?null:owner);return;}
        if(recording is not null){recording.Add(new(PictureCommands.Kind.Paint,matrix,new(CommandKind.FillRectangle,default,Matrix3.Identity,paint)),owner);return;}
        var bounds=new RectF(0,0,CanvasWidth,CanvasHeight);
        if(paint.Shader is null||matrix==Matrix3.Identity){Execute(DrawingCommand.FillRectangle(bounds,Matrix3.Identity,paint with {Antialias=false}));return;}
        if(!matrix.TryInvert(out _))throw new NotSupportedException("A shader needs an invertible canvas transform.");
        if(!rectangles.TryGetValue(bounds,out var geometry))
        {geometry=PathGeometry.Rectangle(bounds);if(rectangles.Count>=64)rectangles.Clear();rectangles.Add(bounds,geometry);}
        Execute(new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,paint with {Antialias=false}){Path=geometry,ShaderTransform=matrix});
    }
}
