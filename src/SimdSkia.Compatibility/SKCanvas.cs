using SimdSkia;
using SimdSkia.Cpu;

namespace SkiaSharp;

public class SKCanvas : SKObject
{
    private readonly ICanvasBackend backend;
    private readonly PixelBuffer target;
    private Matrix3 matrix=Matrix3.Identity;
    private RectF? clip;
    private readonly Dictionary<RectF,PathGeometry> rectangles=[];
    private readonly Dictionary<(PathGeometry Geometry,Matrix3 Matrix,float Width,float Miter),PathGeometry> strokes=[];
    private long strokeBytes;
    private readonly List<(Matrix3 Matrix,RectF? Clip)> states=[];
    public SKCanvas(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        target=bitmap.Buffer.Share();
        backend=new RasterCanvas(target);
    }
    internal SKCanvas(PixelBuffer target) { this.target=target.Share(); backend=new RasterCanvas(this.target); }
    internal SKCanvas(PixelBuffer target,ICanvasBackend backend){this.target=target.Share();this.backend=backend;}
    private void Validate() { ThrowIfDisposed(); target.ThrowIfDisposed(); }
    public void Clear() => Clear(SKColors.Empty);
    public void Clear(SKColor color) { Validate(); backend.Execute(DrawingCommand.Clear(color.ToCore())); }
    public void DrawRect(float x,float y,float w,float h,SKPaint paint) => DrawRect(SKRect.Create(x,y,w,h),paint);
    public void DrawRect(SKRect rect,SKPaint paint)
    {
        Validate(); ArgumentNullException.ThrowIfNull(paint);
        if(paint.Style==SKPaintStyle.Stroke)
        {
            var r=rect.ToCore();if(!rectangles.TryGetValue(r,out var geometry))
            {geometry=PathGeometry.Rectangle(r);if(rectangles.Count>=64)rectangles.Clear();rectangles.Add(r,geometry);}
            DrawGeometry(geometry,paint);return;
        }
        backend.Execute(DrawingCommand.FillRectangle(rect.ToCore(),matrix,paint.ToCore()) with {Clip=clip});
    }
    public void DrawPath(SKPath path,SKPaint paint){Validate();ArgumentNullException.ThrowIfNull(path);ArgumentNullException.ThrowIfNull(paint);DrawGeometry(path.ToCore(),paint);}
    private void DrawGeometry(PathGeometry geometry,SKPaint paint)
    {
        var core=paint.ToCore();var transform=matrix;
        if(paint.Style==SKPaintStyle.Stroke)
        {
            var key=(geometry,matrix,paint.StrokeWidth,paint.StrokeMiter);
            if(!strokes.TryGetValue(key,out var stroke))
            {
                if(paint.StrokeWidth==0)stroke=StrokeGeometry.Create(geometry.Flatten(matrix),1,paint.StrokeMiter);
                else{float scale=MathF.Max(MathF.Sqrt(matrix.M00*matrix.M00+matrix.M10*matrix.M10),MathF.Sqrt(matrix.M01*matrix.M01+matrix.M11*matrix.M11));stroke=StrokeGeometry.Create(geometry.Flatten(Matrix3.Identity,0.125f/MathF.Max(1,scale)),paint.StrokeWidth,paint.StrokeMiter);}
                long bytes=(long)(stroke.Segments.Length+geometry.Segments.Length)*32;
                if(bytes<=4*1024*1024)
                {if(strokes.Count>=64||strokeBytes+bytes>4*1024*1024){strokes.Clear();strokeBytes=0;}strokes.Add(key,stroke);strokeBytes+=bytes;}
            }
            geometry=stroke;if(paint.StrokeWidth==0)transform=Matrix3.Identity;
        }
        if(core.Shader is not null&&!matrix.TryInvert(out _))throw new NotSupportedException("A shader needs an invertible canvas transform.");
        backend.Execute(new DrawingCommand(CommandKind.FillPath,default,transform,core){Path=geometry,Clip=clip,ShaderTransform=matrix});
    }
    public void DrawCircle(float cx,float cy,float radius,SKPaint paint){using var path=new SKPath();path.AddCircle(cx,cy,radius);DrawPath(path,paint);}
    public void DrawCircle(SKPoint c,float radius,SKPaint paint)=>DrawCircle(c.X,c.Y,radius,paint);
    public void DrawText(string text,float x,float y,SKFont font,SKPaint paint)=>DrawText(text,x,y,SKTextAlign.Left,font,paint);
    public void DrawText(SKTextBlob text,float x,float y,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(text);ArgumentNullException.ThrowIfNull(paint);text.ThrowIfDisposed();
        if(!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentException("Text origin must be finite.");
        var p=paint.ToCore();if(paint.Style!=SKPaintStyle.Fill)throw new NotSupportedException("Stroked text blobs require separate outline processing.");
        var transform=matrix*new Matrix3(1,0,x,0,1,y,0,0,1);ValidateMatrix(transform);
        foreach(var part in text.Parts)
            backend.Execute(new DrawingCommand(CommandKind.FillPath,default,transform,p with {Antialias=part.Antialias}){Path=part.Geometry,Clip=clip,ShaderTransform=matrix,CacheTextCoverage=true});
    }
    public void DrawText(string text,float x,float y,SKTextAlign textAlign,SKFont font,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(paint);
        var geometry=font.Layout(text,x,y,textAlign);var p=paint.ToCore() with {Antialias=font.Edging==SKFontEdging.Antialias};
        if(paint.Style!=SKPaintStyle.Fill)throw new NotSupportedException("Stroked text is not implemented yet.");
        backend.Execute(new DrawingCommand(CommandKind.FillPath,default,matrix,p){Path=geometry,Clip=clip});
    }
    public void DrawLine(float x0,float y0,float x1,float y1,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(paint);var core=paint.ToCore();
        var path=new PathGeometry([new(PathVerb.Move,new(x0,y0)),new(PathVerb.Line,new(x1,y1))]);
        var transform=matrix;
        if(paint.StrokeWidth==0){path=StrokeGeometry.Create(path.Flatten(matrix),1,paint.StrokeMiter);transform=Matrix3.Identity;}
        else path=StrokeGeometry.Create(path.Flatten(Matrix3.Identity),paint.StrokeWidth,paint.StrokeMiter);
        if(paint.StrokeCap!=SKStrokeCap.Butt)throw new NotSupportedException("Only butt line caps are supported.");
        if(core.Shader is not null&&!matrix.TryInvert(out _))throw new NotSupportedException("A shader needs an invertible canvas transform.");
        backend.Execute(new DrawingCommand(CommandKind.FillPath,default,transform,core){Path=path,Clip=clip,ShaderTransform=matrix});
    }
    public void DrawLine(SKPoint p0,SKPoint p1,SKPaint paint)=>DrawLine(p0.X,p0.Y,p1.X,p1.Y,paint);
    public void ClipRect(SKRect rect,SKClipOperation operation=SKClipOperation.Intersect,bool antialias=false)
    {
        Validate();if(operation!=SKClipOperation.Intersect||antialias||matrix.M01!=0||matrix.M10!=0)throw new NotSupportedException("Clipping currently supports hard axis-aligned rectangle intersection.");
        if(!rect.ToCore().IsFinite)throw new ArgumentException("Clip coordinates must be finite.",nameof(rect));
        var a=matrix.Map(rect.Left,rect.Top);var b=matrix.Map(rect.Right,rect.Bottom);
        var next=new RectF(MathF.Min(a.X,b.X),MathF.Min(a.Y,b.Y),MathF.Max(a.X,b.X),MathF.Max(a.Y,b.Y));
        if(!next.IsFinite)throw new ArgumentException("Transformed clip exceeds finite coordinate range.",nameof(rect));
        next=new(MathF.Floor(next.Left+.5f),MathF.Floor(next.Top+.5f),MathF.Floor(next.Right+.5f),MathF.Floor(next.Bottom+.5f));
        clip=clip is {} prior?new(MathF.Max(prior.Left,next.Left),MathF.Max(prior.Top,next.Top),MathF.Min(prior.Right,next.Right),MathF.Min(prior.Bottom,next.Bottom)):next;
    }
    public void DrawBitmap(SKBitmap bitmap,float x,float y,SKPaint? paint=null){ArgumentNullException.ThrowIfNull(bitmap);DrawBitmap(bitmap,SKRect.Create(x,y,bitmap.Width,bitmap.Height),paint);}
    public void DrawBitmap(SKBitmap bitmap,SKRect dest,SKPaint? paint=null)
    {
        Validate();ArgumentNullException.ThrowIfNull(bitmap);
        if(paint is not null&&(paint.Style!=SKPaintStyle.Fill||paint.Shader is not null))throw new NotSupportedException("Bitmap drawing does not support stroke or shader overrides.");
        var p=paint?.ToCore()??new Paint(new(255,255,255,255),CompositeMode.SourceOver);
        backend.Execute(new DrawingCommand(CommandKind.DrawImage,dest.ToCore(),matrix,p){Image=bitmap.Buffer,Clip=clip});
    }
    public int SaveCount { get { Validate(); return states.Count+1; } }
    public int Save() { Validate(); int count=SaveCount; states.Add((matrix,clip)); return count; }
    public void Restore() { Validate(); if (states.Count==0) return; (matrix,clip)=states[^1]; states.RemoveAt(states.Count-1); }
    public void RestoreToCount(int count) { Validate(); while (SaveCount>Math.Max(1,count)) Restore(); }
    public SKMatrix TotalMatrix { get { Validate(); return SKMatrix.FromCore(matrix); } }
    public void SetMatrix(in SKMatrix matrix) { Validate(); ValidateMatrix(matrix.ToCore()); this.matrix=matrix.ToCore(); }
    public void ResetMatrix() { Validate(); matrix=Matrix3.Identity; }
    public void Concat(in SKMatrix m)
    {
        Validate(); var product=matrix*m.ToCore(); ValidateMatrix(product); matrix=product;
    }
    public void Translate(float dx,float dy) { var m=SKMatrix.CreateTranslation(dx,dy); Concat(in m); }
    public void Translate(SKPoint point) => Translate(point.X,point.Y);
    public void Scale(float s) => Scale(s,s);
    public void Scale(float sx,float sy) { var m=SKMatrix.CreateScale(sx,sy); Concat(in m); }
    public void Scale(SKPoint size) => Scale(size.X,size.Y);
    public void RotateDegrees(float degrees) { var m=SKMatrix.CreateRotationDegrees(degrees); Concat(in m); }
    public void RotateRadians(float radians) { var m=SKMatrix.CreateRotation(radians); Concat(in m); }
    private static void ValidateMatrix(Matrix3 m)
    {
        if (!m.IsFinite) throw new ArgumentException("Matrix components must be finite.");
        if (!m.IsAffine) throw new NotSupportedException("Perspective rasterization is not implemented.");
    }
    // CPU commands execute immediately, so Flush has no pending work to submit.
    public void Flush(){Validate();backend.Flush();}
    protected override void Dispose(bool disposing) { backend.Dispose(); target.Dispose();states.Clear();rectangles.Clear();strokes.Clear();strokeBytes=0; }
}
