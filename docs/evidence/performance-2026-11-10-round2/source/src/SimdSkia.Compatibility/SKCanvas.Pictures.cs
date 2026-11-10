using SimdSkia;
namespace SkiaSharp;

public partial class SKCanvas
{
    private int RecordLayer(SKCanvasSaveLayerRec rec)
    {
        Validate();if((rec.Flags&~SKCanvasSaveLayerRecFlags.InitializeWithPrevious)!=0)throw new NotSupportedException("F16/LCD layers are not supported.");
        if(rec.Bounds is {} bounds&&!bounds.ToCore().IsFinite)throw new ArgumentException("Layer bounds must be finite.",nameof(rec));
        if(rec.Paint?.Shader is not null||rec.Paint?.Style==SKPaintStyle.Stroke)throw new NotSupportedException("Layer paints support alpha, filters and blend modes.");
        int count=SaveCount;recording!.Layer(matrix,rec);states.Add(new(matrix,clip,coverageClip));return count;
    }
    internal void Playback(PictureCommands picture)
    {
        if(noDraw){Validate();if(overdraw)throw new NotSupportedException("Picture playback through an overdraw view is not yet implemented.");if(forwarding is not null)foreach(var child in forwarding)child.Playback(picture);return;}
        Validate();var outer=matrix;int count=Save();recording?.Own(picture.Retain());
        try
        {
            foreach(var op in picture.Items)
            {
                matrix=outer*op.Matrix;
                switch(op.Kind)
                {
                    case PictureCommands.Kind.Save:Save();break;
                    case PictureCommands.Kind.Restore:Restore();break;
                    case PictureCommands.Kind.Layer:SaveLayer(op.Layer);break;
                    case PictureCommands.Kind.Clear:Clear(new SKColor(op.Command.Paint.Color.R,op.Command.Paint.Color.G,op.Command.Paint.Color.B,op.Command.Paint.Color.A));break;
                    case PictureCommands.Kind.Paint:FillCanvas(op.Command.Paint);break;
                    case PictureCommands.Kind.Annotation:EmitAnnotation(op.Annotation!);break;
                    case PictureCommands.Kind.Clip:using(var path=new SKPath(op.Path!))ClipPath(path,op.ClipOperation,op.Antialias);break;
                    case PictureCommands.Kind.Draw:
                        var command=op.Command;Execute(command with {Transform=outer*command.Transform,ShaderTransform=outer*(command.ShaderTransform??command.Transform)});break;
                    default:throw new InvalidOperationException("Unknown recorded picture operation.");
                }
            }
        }
        finally{RestoreToCount(count);}
    }
    public void DrawPicture(SKPicture picture,SKPaint? paint=null)=>DrawPicture(picture,in SKMatrix.Identity,paint);
    public void DrawPicture(SKPicture picture,float x,float y,SKPaint? paint=null){var m=SKMatrix.CreateTranslation(x,y);DrawPicture(picture,in m,paint);}
    public void DrawPicture(SKPicture picture,SKPoint p,SKPaint? paint=null)=>DrawPicture(picture,p.X,p.Y,paint);
    public void DrawPicture(SKPicture picture,in SKMatrix matrix,SKPaint? paint=null)
    {
        Validate();ArgumentNullException.ThrowIfNull(picture);int count=Save();
        try{Concat(in matrix);if(paint is not null)SaveLayer(picture.CullRect,paint);picture.Playback(this);}finally{RestoreToCount(count);}
    }
    public void DrawDrawable(SKDrawable drawable,in SKMatrix matrix){ArgumentNullException.ThrowIfNull(drawable);drawable.Draw(this,in matrix);}
    public void DrawDrawable(SKDrawable drawable,float x,float y){ArgumentNullException.ThrowIfNull(drawable);drawable.Draw(this,x,y);}
    public void DrawDrawable(SKDrawable drawable,SKPoint p)=>DrawDrawable(drawable,p.X,p.Y);
}

public partial class SKShader
{
    public static SKShader CreatePicture(SKPicture src){ArgumentNullException.ThrowIfNull(src);return src.ToShader();}
    public static SKShader CreatePicture(SKPicture src,SKShaderTileMode tmx,SKShaderTileMode tmy){ArgumentNullException.ThrowIfNull(src);return src.ToShader(tmx,tmy);}
    public static SKShader CreatePicture(SKPicture src,SKShaderTileMode tmx,SKShaderTileMode tmy,SKFilterMode filterMode){ArgumentNullException.ThrowIfNull(src);return src.ToShader(tmx,tmy,filterMode);}
    public static SKShader CreatePicture(SKPicture src,SKShaderTileMode tmx,SKShaderTileMode tmy,SKRect tile){ArgumentNullException.ThrowIfNull(src);return src.ToShader(tmx,tmy,tile);}
    public static SKShader CreatePicture(SKPicture src,SKShaderTileMode tmx,SKShaderTileMode tmy,SKFilterMode filterMode,SKRect tile){ArgumentNullException.ThrowIfNull(src);return src.ToShader(tmx,tmy,filterMode,tile);}
    public static SKShader CreatePicture(SKPicture src,SKShaderTileMode tmx,SKShaderTileMode tmy,SKMatrix localMatrix,SKRect tile){ArgumentNullException.ThrowIfNull(src);return src.ToShader(tmx,tmy,localMatrix,tile);}
    public static SKShader CreatePicture(SKPicture src,SKShaderTileMode tmx,SKShaderTileMode tmy,SKFilterMode filterMode,SKMatrix localMatrix,SKRect tile){ArgumentNullException.ThrowIfNull(src);return src.ToShader(tmx,tmy,filterMode,localMatrix,tile);}
}
