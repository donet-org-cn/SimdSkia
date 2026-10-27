using SimdSkia;
using SimdSkia.Cpu;

namespace SkiaSharp;

public class SKCanvas : SKObject
{
    private ICanvasBackend backend;
    private PixelBuffer target;
    private Matrix3 matrix=Matrix3.Identity;
    private RectF? clip;
    private CoverageClip? coverageClip;
    private readonly Dictionary<RectF,PathGeometry> rectangles=[];
    private readonly Dictionary<(PathGeometry Geometry,Matrix3 Matrix,float Width,float Miter),PathGeometry> strokes=[];
    private long strokeBytes;
    private sealed record State(Matrix3 Matrix,RectF? Clip,CoverageClip? Coverage,ICanvasBackend? Parent=null,PixelBuffer? Pixels=null,Paint LayerPaint=default);
    private readonly List<State> states=[];
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
    public void Clear(SKColor color) { Validate(); if(clip is null&&coverageClip is null)backend.Execute(DrawingCommand.Clear(color.ToCore()));else Execute(DrawingCommand.FillRectangle(new(0,0,target.Width,target.Height),Matrix3.Identity,new(color.ToCore(),CompositeMode.Source))); }
    private void Execute(DrawingCommand command)=>backend.Execute(command with {Clip=clip,CoverageClip=coverageClip});
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
        Execute(DrawingCommand.FillRectangle(rect.ToCore(),matrix,paint.ToCore()));
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
        Execute(new DrawingCommand(CommandKind.FillPath,default,transform,core){Path=geometry,ShaderTransform=matrix});
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
            Execute(new DrawingCommand(CommandKind.FillPath,default,transform,p with {Antialias=part.Antialias}){Path=part.Geometry,ShaderTransform=matrix,CacheTextCoverage=true});
    }
    public void DrawText(string text,float x,float y,SKTextAlign textAlign,SKFont font,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(paint);
        var geometry=font.Layout(text,x,y,textAlign);var p=paint.ToCore() with {Antialias=font.Edging==SKFontEdging.Antialias};
        if(paint.Style!=SKPaintStyle.Fill)throw new NotSupportedException("Stroked text is not implemented yet.");
        Execute(new DrawingCommand(CommandKind.FillPath,default,matrix,p){Path=geometry});
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
        Execute(new DrawingCommand(CommandKind.FillPath,default,transform,core){Path=path,ShaderTransform=matrix});
    }
    public void DrawLine(SKPoint p0,SKPoint p1,SKPaint paint)=>DrawLine(p0.X,p0.Y,p1.X,p1.Y,paint);
    public void ClipRect(SKRect rect,SKClipOperation operation=SKClipOperation.Intersect,bool antialias=false)
    {
        Validate();if(!Enum.IsDefined(operation))throw new ArgumentOutOfRangeException(nameof(operation));
        if(operation!=SKClipOperation.Intersect||antialias||matrix.M01!=0||matrix.M10!=0){using var path=new SKPath();path.AddRect(rect);ClipPath(path,operation,antialias);return;}
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
        Execute(new DrawingCommand(CommandKind.DrawImage,dest.ToCore(),matrix,p){Image=bitmap.Buffer});
    }
    public void DrawBitmap(SKBitmap bitmap,SKRect source,SKRect dest,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(bitmap);DrawPixels(bitmap.Buffer,source,dest,SKSamplingOptions.Default,paint);}
    public void DrawImage(SKImage image,float x,float y,SKPaint? paint=null)=>DrawImage(image,x,y,SKSamplingOptions.Default,paint);
    public void DrawImage(SKImage image,SKRect dest,SKPaint? paint=null)=>DrawImage(image,dest,SKSamplingOptions.Default,paint);
    public void DrawImage(SKImage image,SKRect source,SKRect dest,SKPaint? paint=null)=>DrawImage(image,source,dest,SKSamplingOptions.Default,paint);
    public void DrawImage(SKImage image,float x,float y,SKSamplingOptions sampling,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(image);DrawImage(image,SKRect.Create(x,y,image.Width,image.Height),sampling,paint);}
    public void DrawImage(SKImage image,SKRect dest,SKSamplingOptions sampling,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(image);DrawImage(image,SKRect.Create(image.Width,image.Height),dest,sampling,paint);}
    public void DrawImage(SKImage image,SKRect source,SKRect dest,SKSamplingOptions sampling,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(image);DrawPixels(image.Buffer,source,dest,sampling,paint);}
    private void DrawPixels(PixelBuffer pixels,SKRect source,SKRect dest,SKSamplingOptions sampling,SKPaint? paint)
    {
        Validate();if(paint is not null&&(paint.Style!=SKPaintStyle.Fill||paint.Shader is not null))throw new NotSupportedException("Image drawing does not support stroke or shader overrides.");
        if(sampling.IsAniso||sampling.Mipmap!=SKMipmapMode.None)throw new NotSupportedException("Mipmaps and anisotropic sampling are not implemented.");
        ImageSampling mode;
        if(sampling.UseCubic){if(sampling.Cubic!=SKCubicResampler.Mitchell)throw new NotSupportedException("Only Mitchell cubic sampling is implemented.");mode=ImageSampling.Mitchell;}
        else mode=sampling.Filter switch{SKFilterMode.Nearest=>ImageSampling.Nearest,SKFilterMode.Linear=>ImageSampling.Bilinear,_=>throw new ArgumentOutOfRangeException(nameof(sampling))};
        Execute(new DrawingCommand(CommandKind.DrawImage,dest.ToCore(),matrix,paint?.ToCore()??new Paint(new(255,255,255,255),CompositeMode.SourceOver)){Image=pixels,ImageSource=source.ToCore(),Sampling=mode});
    }
    public int SaveCount { get { Validate(); return states.Count+1; } }
    public int Save() { Validate(); int count=SaveCount; states.Add(new(matrix,clip,coverageClip)); return count; }
    public int SaveLayer()=>SaveLayer((SKPaint?)null);
    public int SaveLayer(SKPaint? paint)
    {
        Validate();var p=paint?.ToCore()??new Paint(new(255,255,255,255),CompositeMode.SourceOver);
        if(p.Shader is not null||paint?.Style==SKPaintStyle.Stroke)throw new NotSupportedException("Layer restore supports alpha and blend mode, not a shader or stroke.");
        var pixels=PixelBuffer.Rent(target.Width,target.Height);var next=new RasterCanvas(pixels);int count=SaveCount;
        states.Add(new(matrix,clip,coverageClip,backend,target,p));backend=next;target=pixels;
        // Apply the inherited AA clip once, on restore; nested clips belong to the layer.
        coverageClip=null;return count;
    }
    public int SaveLayer(SKRect limit,SKPaint? paint)
    {
        if(!limit.ToCore().IsFinite)throw new ArgumentException("Layer bounds must be finite.",nameof(limit));
        int count=SaveLayer(paint);
        // Src/Clear affect transparent pixels and must cover the entire parent clip.
        // The bounds argument is an allocation hint, not an unconditional ClipRect.
        if(paint is null||paint.BlendMode==SKBlendMode.SrcOver)ClipRect(limit);
        return count;
    }
    public void Restore()
    {
        Validate();if(states.Count==0)return;var state=states[^1];states.RemoveAt(states.Count-1);
        matrix=state.Matrix;clip=state.Clip;coverageClip=state.Coverage;
        if(state.Parent is null)return;
        var layer=target;var layerBackend=backend;backend=state.Parent;target=state.Pixels!;
        try{layerBackend.Flush();Execute(new DrawingCommand(CommandKind.DrawImage,new(0,0,layer.Width,layer.Height),Matrix3.Identity,state.LayerPaint){Image=layer});backend.Flush();}
        finally{layerBackend.Dispose();layer.Dispose();}
    }
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
    protected override void Dispose(bool disposing) { try{RestoreToCount(1);}finally{backend.Dispose();target.Dispose();states.Clear();rectangles.Clear();strokes.Clear();strokeBytes=0;} }

    public void DrawRoundRect(SKRect rect,float rx,float ry,SKPaint paint){using var rounded=new SKRoundRect(rect,rx,ry);DrawRoundRect(rounded,paint);}
    public void DrawRoundRect(SKRect rect,SKSize r,SKPaint paint)=>DrawRoundRect(rect,r.Width,r.Height,paint);
    public void DrawRoundRect(float x,float y,float w,float h,float rx,float ry,SKPaint paint)=>DrawRoundRect(SKRect.Create(x,y,w,h),rx,ry,paint);
    public void DrawRoundRect(SKRoundRect rect,SKPaint paint){ArgumentNullException.ThrowIfNull(rect);using var path=rect.ToPath();DrawPath(path,paint);}
    public void ClipRoundRect(SKRoundRect rect,SKClipOperation operation=SKClipOperation.Intersect,bool antialias=false){ArgumentNullException.ThrowIfNull(rect);using var path=rect.ToPath();ClipPath(path,operation,antialias);}
    public void ClipPath(SKPath path,SKClipOperation operation=SKClipOperation.Intersect,bool antialias=false)
    {
        Validate();ArgumentNullException.ThrowIfNull(path);if(!Enum.IsDefined(operation))throw new ArgumentOutOfRangeException(nameof(operation));
        using var clipper=new RasterCanvas(target);var next=clipper.CreateClip(path.ToCore(),matrix,antialias);
        if(operation==SKClipOperation.Intersect){coverageClip=coverageClip is null?next:CoverageClip.Intersect(coverageClip,next);return;}
        var extent=clip??new RectF(0,0,target.Width,target.Height);
        int l=(int)Math.Clamp(extent.Left,0,target.Width),t=(int)Math.Clamp(extent.Top,0,target.Height);
        int r=Math.Clamp((int)MathF.Min(target.Width,extent.Right),l,target.Width),b=Math.Clamp((int)MathF.Min(target.Height,extent.Bottom),t,target.Height);
        var bytes=new byte[checked((r-l)*(b-t))];
        for(int y=t;y<b;y++)for(int x=l;x<r;x++)bytes[(y-t)*(r-l)+x-l]=PixelBuffer.Multiply(coverageClip?.At(x,y)??255,(byte)(255-next.At(x,y)));
        coverageClip=new(l,t,r-l,b-t,bytes);
    }
}
