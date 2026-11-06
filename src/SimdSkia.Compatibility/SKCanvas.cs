using SimdSkia;
using SimdSkia.Cpu;

namespace SkiaSharp;

public partial class SKCanvas : SKObject
{
    private ICanvasBackend backend;
    private PixelBuffer target;
    private Matrix3 matrix=Matrix3.Identity;
    private RectF? clip;
    private CoverageClip? coverageClip;
    private readonly PictureCommands? recording;
    internal bool ColorManagementEnabled {get;set;}
    internal SKPixelGeometry TextPixelGeometry {get;set;}
    internal SKCanvas(PictureCommands recording):this(new PixelBuffer(1,1,PixelFormat.Bgra8888,AlphaFormat.Premultiplied))
    {this.recording=recording;}
    private readonly Dictionary<RectF,PathGeometry> rectangles=[];
    private readonly Dictionary<(PathGeometry Geometry,Matrix3 Matrix,float Width,float Miter,SKStrokeCap Cap,SKStrokeJoin Join),PathGeometry> strokes=[];
    private long strokeBytes;
    private sealed record State(Matrix3 Matrix,RectF? Clip,CoverageClip? Coverage,ICanvasBackend? Parent=null,PixelBuffer? Pixels=null,Paint LayerPaint=default,IDisposable? LayerResources=null);
    private readonly List<State> states=[];
    public SKCanvas(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        RequireSrgb(bitmap.ColorSpace);
        ColorManagementEnabled=bitmap.ColorSpace is not null;
        target=bitmap.Buffer.Share();
        backend=new RasterCanvas(target);
    }
    internal SKCanvas(PixelBuffer target) { this.target=target.Share(); backend=new RasterCanvas(this.target); }
    internal SKCanvas(PixelBuffer target,ICanvasBackend backend){this.target=target.Share();this.backend=backend;}
    private void Validate() { ThrowIfDisposed(); target.ThrowIfDisposed(); }
    private static void RequireSrgb(SKColorSpace? space)
    {if(space is not null&&!space.IsSrgb)throw new NotSupportedException("Non-sRGB drawing/sampling requires the destination color pipeline. Matrix/TRC pixel transfers are supported; convert to sRGB before drawing.");}
    public void Clear() => Clear(SKColors.Empty);
    public void Clear(SKColor color) { Validate();if(noDraw){if(forwarding is not null)foreach(var child in forwarding){if(overdraw)child.FillCanvas(OverdrawPaint(default));else child.Clear(color);}return;}if(color.Alpha==0)color=SKColor.Empty;if(recording is not null){recording.Add(new(PictureCommands.Kind.Clear,matrix,DrawingCommand.Clear(color.ToCore())));return;} if(clip is null&&coverageClip is null)backend.Execute(DrawingCommand.Clear(color.ToCore()));else Execute(DrawingCommand.FillRectangle(new(0,0,CanvasWidth,CanvasHeight),Matrix3.Identity,new(color.ToCore(),CompositeMode.Source))); }
    private void Execute(DrawingCommand command,SKPaint? owner=null)
    {if(noDraw){ForwardDrawing(command,owner);return;}if(recording is not null){recording.Add(new(PictureCommands.Kind.Draw,matrix,command),owner);return;}if(command.Paint.Shader is {} shader){shader=shader.ForDestination(ColorManagementEnabled);if(shader is IContextualColorShader contextual)command=command with {Paint=command.Paint with {Shader=contextual.Bind(command.ShaderTransform??command.Transform,command.Paint.FloatingColor??new System.Numerics.Vector4(command.Paint.Color.R/255f,command.Paint.Color.G/255f,command.Paint.Color.B/255f,command.Paint.Color.A/255f)),Color=command.Paint.Color with {A=255},FloatingColor=command.Paint.FloatingColor is {} f?f with {W=1}:null}};else command=command with {Paint=command.Paint with {Shader=shader}};}backend.Execute(command with {Clip=clip,CoverageClip=coverageClip,ImageFilterTransform=matrix});}
    public void DrawRect(float x,float y,float w,float h,SKPaint paint) => DrawRect(SKRect.Create(x,y,w,h),paint);
    public void DrawRect(SKRect rect,SKPaint paint)
    {
        Validate(); ArgumentNullException.ThrowIfNull(paint);
        if(paint.Style==SKPaintStyle.Stroke||paint.HasPathEffect)
        {
            var r=rect.ToCore();if(!rectangles.TryGetValue(r,out var geometry))
            {geometry=PathGeometry.Rectangle(r);if(rectangles.Count>=64)rectangles.Clear();rectangles.Add(r,geometry);}
            DrawGeometry(geometry,paint);return;
        }
        Execute(DrawingCommand.FillRectangle(rect.ToCore(),matrix,paint.ToCore()),paint);
    }
    public void DrawPath(SKPath path,SKPaint paint){Validate();ArgumentNullException.ThrowIfNull(path);ArgumentNullException.ThrowIfNull(paint);DrawGeometry(path.ToCore(),paint);}
    private void DrawGeometry(PathGeometry geometry,SKPaint paint,bool forceStroke=false)
    {
        var effected=paint.ApplyPathEffect(geometry,forceStroke);geometry=effected.Geometry;
        var core=paint.ToCore(ignoreStyle:forceStroke||effected.Style!=paint.Style);var transform=matrix;float width=effected.Width;
        if(effected.Style==SKPaintStyle.Stroke)
        {
            var key=(geometry,matrix,width,paint.StrokeMiter,paint.StrokeCap,paint.StrokeJoin);
            if(!strokes.TryGetValue(key,out var stroke))
            {
                if(width==0)stroke=StrokeGeometry.Create(geometry.Flatten(matrix),1,paint.StrokeMiter,(StrokeCap)paint.StrokeCap,(StrokeJoin)paint.StrokeJoin);
                else{float scale=MathF.Max(MathF.Sqrt(matrix.M00*matrix.M00+matrix.M10*matrix.M10),MathF.Sqrt(matrix.M01*matrix.M01+matrix.M11*matrix.M11));stroke=StrokeGeometry.Create(geometry.Flatten(Matrix3.Identity,0.125f/MathF.Max(1,scale)),width,paint.StrokeMiter,(StrokeCap)paint.StrokeCap,(StrokeJoin)paint.StrokeJoin);}
                long bytes=(long)(stroke.Segments.Length+geometry.Segments.Length)*32;
                if(bytes<=4*1024*1024)
                {if(strokes.Count>=64||strokeBytes+bytes>4*1024*1024){strokes.Clear();strokeBytes=0;}strokes.Add(key,stroke);strokeBytes+=bytes;}
            }
            geometry=stroke;if(width==0)transform=Matrix3.Identity;
        }
        if(core.Shader is not null&&!matrix.TryInvert(out _))throw new NotSupportedException("A shader needs an invertible canvas transform.");
        Execute(new DrawingCommand(CommandKind.FillPath,default,transform,core){Path=geometry,ShaderTransform=matrix},paint);
    }
    public void DrawCircle(float cx,float cy,float radius,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(paint);if(!float.IsFinite(radius))throw new ArgumentOutOfRangeException(nameof(radius));if(radius<=0)return;
        DrawCachedCircle(cx,cy,radius,paint);
    }
    public void DrawCircle(SKPoint c,float radius,SKPaint paint)=>DrawCircle(c.X,c.Y,radius,paint);
    public void DrawText(string text,float x,float y,SKFont font,SKPaint paint)=>DrawText(text,x,y,SKTextAlign.Left,font,paint);
    public void DrawText(SKTextBlob text,float x,float y,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(text);ArgumentNullException.ThrowIfNull(paint);text.ThrowIfDisposed();
        if(!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentException("Text origin must be finite.");
        if(paint.HasPathEffect||paint.Style!=SKPaintStyle.Fill)
        {
            var previous=matrix;matrix*=SKMatrix.CreateTranslation(x,y).ToCore();
            try{foreach(var part in text.Parts)DrawGeometry(part.GlyphRun is {} run?GlyphOutline(run):part.Geometry,paint);}
            finally{matrix=previous;}
            return;
        }
        var p=paint.ToCore();if(paint.Style!=SKPaintStyle.Fill)throw new NotSupportedException("Stroked text blobs require separate outline processing.");
        var transform=matrix*new Matrix3(1,0,x,0,1,y,0,0,1);ValidateMatrix(transform);
        foreach(var part in text.Parts)
        {
            if(part.GlyphRun is {} glyphs)Execute(new DrawingCommand(CommandKind.DrawGlyphRun,default,transform,p with {Antialias=part.Antialias}){GlyphRun=glyphs,ShaderTransform=matrix,CurveTolerance=.015625f,TextMaskPreblend=true,TextPixelGeometry=part.LcdText?(byte)TextPixelGeometry:(byte)0},paint);
            else Execute(new DrawingCommand(CommandKind.FillPath,default,transform,p with {Antialias=part.Antialias}){Path=part.Geometry,ShaderTransform=matrix,CacheTextCoverage=true},paint);
        }
    }
    public void DrawText(string text,float x,float y,SKTextAlign textAlign,SKFont font,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(paint);
        var geometry=font.Layout(text,x,y,textAlign);
        if(paint.HasPathEffect||paint.Style!=SKPaintStyle.Fill){using var outlinePaint=paint.Clone();outlinePaint.IsAntialias=font.Edging!=SKFontEdging.Alias;DrawGeometry(GlyphOutline(geometry),outlinePaint);return;}
        var p=paint.ToCore() with {Antialias=font.Edging!=SKFontEdging.Alias};
        if(paint.Style!=SKPaintStyle.Fill)throw new NotSupportedException("Stroked text is not implemented yet.");
        Execute(new DrawingCommand(CommandKind.DrawGlyphRun,default,matrix,p){GlyphRun=geometry,ShaderTransform=matrix,CurveTolerance=.015625f,TextMaskPreblend=true,TextPixelGeometry=font.Edging==SKFontEdging.SubpixelAntialias?(byte)TextPixelGeometry:(byte)0},paint);
    }
    public void DrawLine(float x0,float y0,float x1,float y1,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(paint);
        var key=(new PointF(x0,y0),new PointF(x1,y1));
        lines??=[];
        if(!lines.TryGetValue(key,out var path))
        {
            path=new PathGeometry([new(PathVerb.Move,key.Item1),new(PathVerb.Line,key.Item2)]);
            if(lines.Count>=64)lines.Clear();lines.Add(key,path);
        }
        DrawGeometry(path,paint,true);
    }
    public void DrawLine(SKPoint p0,SKPoint p1,SKPaint paint)=>DrawLine(p0.X,p0.Y,p1.X,p1.Y,paint);
    public void ClipRect(SKRect rect,SKClipOperation operation=SKClipOperation.Intersect,bool antialias=false)
    {
        Validate();if(!Enum.IsDefined(operation))throw new ArgumentOutOfRangeException(nameof(operation));
        if(recording is not null){if(!rect.ToCore().IsFinite)throw new ArgumentException("Clip coordinates must be finite.",nameof(rect));recording.Add(new(PictureCommands.Kind.Clip,matrix,Path:PathGeometry.Rectangle(rect.ToCore()),ClipOperation:operation,Antialias:antialias));return;}
        if(operation!=SKClipOperation.Intersect||antialias||matrix.M01!=0||matrix.M10!=0){using var path=new SKPath();path.AddRect(rect);ClipPath(path,operation,antialias);return;}
        if(forwarding is not null)foreach(var child in forwarding)child.ClipRect(rect,operation,antialias);
        if(!rect.ToCore().IsFinite)throw new ArgumentException("Clip coordinates must be finite.",nameof(rect));
        var a=matrix.Map(rect.Left,rect.Top);var b=matrix.Map(rect.Right,rect.Bottom);
        var next=new RectF(MathF.Min(a.X,b.X),MathF.Min(a.Y,b.Y),MathF.Max(a.X,b.X),MathF.Max(a.Y,b.Y));
        if(!next.IsFinite)throw new ArgumentException("Transformed clip exceeds finite coordinate range.",nameof(rect));
        next=new(MathF.Floor(next.Left+.5f),MathF.Floor(next.Top+.5f),MathF.Floor(next.Right+.5f),MathF.Floor(next.Bottom+.5f));
        clip=clip is {} prior?new(MathF.Max(prior.Left,next.Left),MathF.Max(prior.Top,next.Top),MathF.Min(prior.Right,next.Right),MathF.Min(prior.Bottom,next.Bottom)):next;
    }
    public void DrawBitmap(SKBitmap bitmap,float x,float y,SKPaint? paint=null){ArgumentNullException.ThrowIfNull(bitmap);DrawBitmap(bitmap,SKRect.Create(x,y,bitmap.Width,bitmap.Height),paint);}
    public void DrawBitmap(SKBitmap bitmap,SKRect dest,SKPaint? paint=null)
    {Validate();ArgumentNullException.ThrowIfNull(bitmap);RequireSrgb(bitmap.ColorSpace);DrawPixels(bitmap.Buffer,SKRect.Create(bitmap.Width,bitmap.Height),dest,paint?.LegacySampling??SKSamplingOptions.Default,paint);}
    public void DrawBitmap(SKBitmap bitmap,SKRect source,SKRect dest,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(bitmap);RequireSrgb(bitmap.ColorSpace);DrawPixels(bitmap.Buffer,source,dest,paint?.LegacySampling??SKSamplingOptions.Default,paint);}
    public void DrawImage(SKImage image,float x,float y,SKPaint? paint=null)=>DrawImage(image,x,y,paint?.LegacySampling??SKSamplingOptions.Default,paint);
    public void DrawImage(SKImage image,SKRect dest,SKPaint? paint=null)=>DrawImage(image,dest,paint?.LegacySampling??SKSamplingOptions.Default,paint);
    public void DrawImage(SKImage image,SKRect source,SKRect dest,SKPaint? paint=null)=>DrawImage(image,source,dest,paint?.LegacySampling??SKSamplingOptions.Default,paint);
    public void DrawImage(SKImage image,float x,float y,SKSamplingOptions sampling,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(image);DrawImage(image,SKRect.Create(x,y,image.Width,image.Height),sampling,paint);}
    public void DrawImage(SKImage image,SKRect dest,SKSamplingOptions sampling,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(image);DrawImage(image,SKRect.Create(image.Width,image.Height),dest,sampling,paint);}
    public void DrawImage(SKImage image,SKRect source,SKRect dest,SKSamplingOptions sampling,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(image);RequireSrgb(image.ColorSpace);if(backend is SimdSkia.Gpu.GlCanvas gl&&image.TextureSource is {} texture&&gl.Accepts(texture))DrawPixels(null,source,dest,sampling,paint,true,texture);else DrawPixels(image.Buffer,source,dest,sampling,paint,true);}
    private void DrawPixels(PixelBuffer? pixels,SKRect source,SKRect dest,SKSamplingOptions sampling,SKPaint? paint,bool immutable=false,object? imageResource=null)
    {
        Validate();if(paint is not null&&(paint.Style!=SKPaintStyle.Fill||paint.Shader is not null&&pixels?.Format!=PixelFormat.Alpha8))throw new NotSupportedException("Image drawing does not support stroke or shader overrides.");
        if(sampling.IsAniso||sampling.Mipmap!=SKMipmapMode.None)throw new NotSupportedException("Mipmaps and anisotropic sampling are not implemented.");
        ImageSampling mode;
        if(sampling.UseCubic){if(sampling.Cubic!=SKCubicResampler.Mitchell)throw new NotSupportedException("Only Mitchell cubic sampling is implemented.");mode=ImageSampling.Mitchell;}
        else mode=sampling.Filter switch{SKFilterMode.Nearest=>ImageSampling.Nearest,SKFilterMode.Linear=>ImageSampling.Bilinear,_=>throw new ArgumentOutOfRangeException(nameof(sampling))};
        Execute(new DrawingCommand(CommandKind.DrawImage,dest.ToCore(),matrix,paint?.ToCore()??new Paint(pixels?.Format==PixelFormat.Alpha8?new(0,0,0,255):new(255,255,255,255),CompositeMode.SourceOver)){Image=pixels,ImageResource=imageResource,ImageSource=source.ToCore(),Sampling=mode,ImageIsImmutable=immutable,AliasedRectangleEdges=true,NearestLowerBoundary=true},paint);
    }
    public int SaveCount { get { Validate(); return states.Count+1; } }
    public int Save() { Validate();if(noDraw)return SaveNoDraw();int count=SaveCount;recording?.Add(new(PictureCommands.Kind.Save,matrix));states.Add(new(matrix,clip,coverageClip)); return count; }
    public int SaveLayer()=>SaveLayer((SKPaint?)null);
    public int SaveLayer(SKPaint? paint)
    {
        if(backend is SimdSkia.Gpu.IGpuCanvas){Validate();throw new NotSupportedException("SKCanvas GPU save layers require GPU offscreen attachments; use the explicit raster surface or the Avalonia adapter's configured fallback.");}
        if(noDraw)return SaveNoDraw(new(){Paint=paint});
        if(recording is not null)return RecordLayer(new(){Paint=paint});
        Validate();var p=paint?.ToCore()??new Paint(new(255,255,255,255),CompositeMode.SourceOver);
        if(p.Shader is not null||paint?.Style==SKPaintStyle.Stroke)throw new NotSupportedException("Layer restore supports alpha and blend mode, not a shader or stroke.");
        var pixels=PixelBuffer.Rent(CanvasWidth,CanvasHeight);var next=new RasterCanvas(pixels);int count=SaveCount;
        ShaderResources? resources;
        try{resources=paint?.RetainImageFilterResources();}catch{next.Dispose();pixels.Dispose();throw;}
        states.Add(new(matrix,clip,coverageClip,backend,target,p,resources));backend=next;target=pixels;
        // Apply the inherited AA clip once, on restore; nested clips belong to the layer.
        coverageClip=null;return count;
    }
    public int SaveLayer(SKRect limit,SKPaint? paint)
    {
        if(noDraw)return SaveNoDraw(new(){Bounds=limit,Paint=paint});
        if(recording is not null)return RecordLayer(new(){Bounds=limit,Paint=paint});
        if(!limit.ToCore().IsFinite)throw new ArgumentException("Layer bounds must be finite.",nameof(limit));
        int count=SaveLayer(paint);
        // The raster baseline limits the layer's allocation for every blend
        // mode, including transparent Src/Clear pixels inside that allocation.
        var transform=matrix;
        var a=matrix.Map(limit.Left,limit.Top);var b=matrix.Map(limit.Right,limit.Top);var c=matrix.Map(limit.Left,limit.Bottom);var d=matrix.Map(limit.Right,limit.Bottom);
        var device=new SKRect(MathF.Floor(MathF.Min(MathF.Min(a.X,b.X),MathF.Min(c.X,d.X))),MathF.Floor(MathF.Min(MathF.Min(a.Y,b.Y),MathF.Min(c.Y,d.Y))),MathF.Ceiling(MathF.Max(MathF.Max(a.X,b.X),MathF.Max(c.X,d.X))),MathF.Ceiling(MathF.Max(MathF.Max(a.Y,b.Y),MathF.Max(c.Y,d.Y))));
        matrix=Matrix3.Identity;try{ClipRect(device);}finally{matrix=transform;}
        return count;
    }
    public void Restore()
    {
        Validate();if(states.Count==0)return;var state=states[^1];states.RemoveAt(states.Count-1);
        if(forwarding is not null)foreach(var child in forwarding)child.Restore();
        recording?.Add(new(PictureCommands.Kind.Restore,matrix));
        matrix=state.Matrix;clip=state.Clip;coverageClip=state.Coverage;
        if(state.Parent is null)return;
        var layer=target;var layerBackend=backend;backend=state.Parent;target=state.Pixels!;
        try{layerBackend.Flush();Execute(new DrawingCommand(CommandKind.DrawImage,new(0,0,layer.Width,layer.Height),Matrix3.Identity,state.LayerPaint){Image=layer,ImageSource=new(0,0,layer.Width,layer.Height),ImageFilterAlphaAfter=true});backend.Flush();}
        finally{try{layerBackend.Dispose();layer.Dispose();}finally{state.LayerResources?.Dispose();}}
    }
    public void RestoreToCount(int count) { Validate(); while (SaveCount>Math.Max(1,count)) Restore(); }
    public SKMatrix TotalMatrix { get { Validate(); return SKMatrix.FromCore(matrix); } }
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public void SetMatrix(in SKMatrix matrix) { Validate(); ValidateMatrix(matrix.ToCore()); this.matrix=matrix.ToCore();if(forwarding is not null)foreach(var child in forwarding)child.SetMatrix(in matrix); }
    public void ResetMatrix() { Validate(); matrix=Matrix3.Identity;if(forwarding is not null)foreach(var child in forwarding)child.ResetMatrix(); }
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public void Concat(in SKMatrix m)
    {
        Validate(); var product=matrix*m.ToCore(); ValidateMatrix(product); matrix=product;
        if(forwarding is not null)foreach(var child in forwarding)child.Concat(in m);
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
    protected override void Dispose(bool disposing) { try{RestoreToCount(1);}finally{backend.Dispose();target.Dispose();ClearAtlasCache();ClearPatchCache();AnnotationMetadata?.Clear();AnnotationSink=null;states.Clear();rectangles.Clear();strokes.Clear();forwarding?.Clear();lines?.Clear();circles?.Clear();ovals?.Clear();arcs?.Clear();strokeBytes=0;} }

    public void DrawRoundRect(SKRect rect,float rx,float ry,SKPaint paint){using var rounded=new SKRoundRect(rect,rx,ry);DrawRoundRect(rounded,paint);}
    public void DrawRoundRect(SKRect rect,SKSize r,SKPaint paint)=>DrawRoundRect(rect,r.Width,r.Height,paint);
    public void DrawRoundRect(float x,float y,float w,float h,float rx,float ry,SKPaint paint)=>DrawRoundRect(SKRect.Create(x,y,w,h),rx,ry,paint);
    public void DrawRoundRect(SKRoundRect rect,SKPaint paint){ArgumentNullException.ThrowIfNull(rect);using var path=rect.ToPath();DrawPath(path,paint);}
    public void ClipRoundRect(SKRoundRect rect,SKClipOperation operation=SKClipOperation.Intersect,bool antialias=false){ArgumentNullException.ThrowIfNull(rect);using var path=rect.ToPath();ClipPath(path,operation,antialias);}
    public void ClipPath(SKPath path,SKClipOperation operation=SKClipOperation.Intersect,bool antialias=false)
    {
        Validate();ArgumentNullException.ThrowIfNull(path);if(!Enum.IsDefined(operation))throw new ArgumentOutOfRangeException(nameof(operation));
        if(forwarding is not null)foreach(var child in forwarding)child.ClipPath(path,operation,antialias);
        if(recording is not null){recording.Add(new(PictureCommands.Kind.Clip,matrix,Path:path.ToCore(),ClipOperation:operation,Antialias:antialias));return;}
        using var clipper=new RasterCanvas(target);var next=noDraw?RasterCanvas.CreateClipForBounds(path.ToCore(),matrix,CanvasWidth,CanvasHeight,antialias):clipper.CreateClip(path.ToCore(),matrix,antialias);
        if(operation==SKClipOperation.Intersect){coverageClip=coverageClip is null?next:CoverageClip.Intersect(coverageClip,next);return;}
        var extent=clip??new RectF(0,0,CanvasWidth,CanvasHeight);
        int l=(int)Math.Clamp(extent.Left,0,CanvasWidth),t=(int)Math.Clamp(extent.Top,0,CanvasHeight);
        int r=Math.Clamp((int)MathF.Min(CanvasWidth,extent.Right),l,CanvasWidth),b=Math.Clamp((int)MathF.Min(CanvasHeight,extent.Bottom),t,CanvasHeight);
        var bytes=new byte[checked((r-l)*(b-t))];
        for(int y=t;y<b;y++)for(int x=l;x<r;x++)bytes[(y-t)*(r-l)+x-l]=PixelBuffer.Multiply(coverageClip?.At(x,y)??255,(byte)(255-next.At(x,y)));
        coverageClip=new(l,t,r-l,b-t,bytes);
    }
}
