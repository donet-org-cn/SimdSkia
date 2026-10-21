using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SimdSkia.Cpu;

namespace SimdSkia.Avalonia;

internal sealed partial class DrawingContext:IDrawingContextImplWithEffects
{
    private PixelBuffer target;
    private ICanvasBackend canvas;
    private readonly ICanvasBackend? rootBackend;
    private readonly bool flushBackend;
    private readonly PixelBuffer root;
    private readonly Matrix dpiTransform;
    private readonly Vector dpi;
    private readonly Action<bool>? completed;
    private bool disposed;
    private bool nearestLowerBoundary;
    private Matrix transform=Matrix.Identity;
    private RectF? clip;
    private CoverageClip? coverageClip;
    private RenderOptions renderOptions;
    private TextOptions textOptions;
    private readonly Stack<(RectF? Rect,CoverageClip? Coverage)> clips=[];
    private readonly Stack<RenderOptions> renderStack=[];
    private readonly Stack<TextOptions> textStack=[];
    private readonly Stack<Group> groups=[];
    private RectF? dirty;
    private int originX,originY;
    private readonly record struct Group(string Kind,PixelBuffer? Parent,ICanvasBackend? Canvas,RectF? Clip,CoverageClip? Coverage,byte Opacity,IBrush? Mask,Rect Bounds,RectF? ParentDirty,Matrix3 Matrix,FilterSpec? Filter=null,int OffsetX=0,int OffsetY=0);
    private static readonly object shapeGate=new();
    private static readonly Dictionary<(RoundedRect Rect,bool Ellipse),Geometry> shapes=[];
    internal DrawingContext(PixelBuffer pixels,Vector dpi,bool scaleToDpi=true,Action<bool>? completed=null,ICanvasBackend? backend=null,bool flushBackend=true)
    {root=target=pixels.Share();rootBackend=backend;this.flushBackend=flushBackend;canvas=backend??new RasterCanvas(target);this.dpi=dpi;dpiTransform=scaleToDpi?Matrix.CreateScale(dpi.X/96,dpi.Y/96):Matrix.Identity;this.completed=completed;}
    public Matrix Transform {get=>transform;set{Check();if(!value.IsIdentity&&!ConvertTo.Matrix(value).IsFinite)throw new ArgumentException("Transform must be finite.");transform=value;}}
    private Matrix3 ApplyOrigin(Matrix matrix)
    {var value=ConvertTo.Matrix(matrix);return (originX|originY)==0?value:value with{M02=value.M02-originX,M12=value.M12-originY};}
    private Matrix3 DeviceMatrix=>ApplyOrigin(transform*dpiTransform);
    private CoverageClip CreateCoverageClip(PathGeometry path,Matrix3 matrix,bool antialias)
    {if(canvas is RasterCanvas raster)return raster.CreateClip(path,matrix,antialias);using var clipper=new RasterCanvas(target);return clipper.CreateClip(path,matrix,antialias);}
    private bool Antialias=>renderOptions.EdgeMode!=EdgeMode.Aliased;
    private void Check(){ObjectDisposedException.ThrowIf(disposed,this);target.ThrowIfDisposed();}
    private void Execute(DrawingCommand command)
    {
        using var measurement=SimdSkiaDiagnostics.Measure(command.Kind==CommandKind.DrawGlyphRun?"glyph-run":command.Kind==CommandKind.FillPath?command.Paint.Shader is null?"path-solid":"path-gradient":command.Kind==CommandKind.DrawImage?"image":"rectangle");
        Check();if(rootBackend is not null&&!ReferenceEquals(canvas,rootBackend))SimdSkiaDiagnostics.SoftwareLayerCommand();
        canvas.Execute(command with {Clip=clip,CoverageClip=coverageClip,CurveTolerance=0.015625f});
        if(groups.Count==0)return;
        var bounds=command.GlyphRun?.Bounds??command.Path?.ControlBounds??command.Rectangle;var m=command.Transform;
        var a=m.Map(bounds.Left,bounds.Top);var b=m.Map(bounds.Right,bounds.Top);var c=m.Map(bounds.Left,bounds.Bottom);var d=m.Map(bounds.Right,bounds.Bottom);
        var area=Intersect(clip,new RectF(Math.Min(Math.Min(a.X,b.X),Math.Min(c.X,d.X))-1,Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y))-1,Math.Max(Math.Max(a.X,b.X),Math.Max(c.X,d.X))+1,Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))+1));
        area=Intersect(area,new(0,0,target.Width,target.Height));if(area.IsEmpty)return;
        dirty=dirty is {} old?new(Math.Min(area.Left,old.Left),Math.Min(area.Top,old.Top),Math.Max(area.Right,old.Right),Math.Max(area.Bottom,old.Bottom)):area;
    }
    private static Geometry Shape(RoundedRect rect,bool ellipse=false)
    {
        lock(shapeGate){if(shapes.TryGetValue((rect,ellipse),out var result))return result;result=ellipse?Geometry.Ellipse(rect.Rect):Geometry.Rounded(rect);if(shapes.Count>=256)shapes.Clear();shapes.Add((rect,ellipse),result);return result;}
    }
    public void Clear(Color color)
    {Check();if(clip is null&&coverageClip is null&&groups.Count==0)canvas.Execute(DrawingCommand.Clear(ConvertTo.Color(color)));else Execute(DrawingCommand.FillRectangle(new(0,0,target.Width,target.Height),Matrix3.Identity,new(ConvertTo.Color(color),CompositeMode.Source)));}
    private void DrawPath(IBrush brush,Geometry geometry,Matrix3? matrix=null,bool text=false)
    {
        geometry.Validate();using var paint=CreatePaint(brush,geometry.Bounds,text?textOptions.TextRenderingMode!=TextRenderingMode.Alias:Antialias);
        Execute(new(CommandKind.FillPath,default,matrix??DeviceMatrix,paint.Paint){Path=geometry.Path,CacheTextCoverage=true});
    }
    public void DrawGeometry(IBrush? brush,IPen? pen,IGeometryImpl geometry)
    {
        Check();var g=geometry as Geometry??throw new ArgumentException("Foreign geometry backend.");g.Validate();
        var stroke=pen?.Brush is not null&&pen.Thickness>0?(Geometry)g.GetWidenedGeometry(pen):null;
        // Validate once, retaining any tile pixels until the actual draw. Visual
        // content must not be rendered repeatedly merely to validate a paint.
        using var fill=brush is not null?CreatePaint(brush,g.Bounds,Antialias):default;
        using var outline=stroke is not null?CreatePaint(pen!.Brush!,stroke.Bounds,Antialias):default;
        if(brush is not null)Execute(new(CommandKind.FillPath,default,DeviceMatrix,fill.Paint){Path=g.Path,CacheTextCoverage=true});
        if(stroke is not null)Execute(new(CommandKind.FillPath,default,DeviceMatrix,outline.Paint){Path=stroke.Path,CacheTextCoverage=true});
    }
    public void DrawLine(IPen? pen,Point a,Point b)=>DrawGeometry(null,pen,new Geometry(new SimdSkia.PathGeometry([new(PathVerb.Move,ConvertTo.Point(a)),new(PathVerb.Line,ConvertTo.Point(b))])));
    public void DrawRectangle(IBrush? brush,IPen? pen,RoundedRect rect,BoxShadows shadows=default)
    {
        Check();ValidateShadows(shadows);
        if(rect.Rect.Width<=0||rect.Rect.Height<=0)return;
        if(shadows.Count>0){DrawShadowedRectangle(brush,pen,rect,shadows);return;}
        if(!rect.IsRounded)
        {
            var stroke=pen?.Brush is not null&&pen.Thickness>0?(Geometry)Geometry.Rectangle(rect.Rect).GetWidenedGeometry(pen):null;
            using var strokeValidation=stroke is not null?CreatePaint(pen!.Brush!,stroke.Bounds,Antialias):default;
            if(brush is not null){using var fill=CreatePaint(brush,rect.Rect,Antialias);Execute(DrawingCommand.FillRectangle(ConvertTo.Rect(rect.Rect),DeviceMatrix,fill.Paint));}
            if(stroke is not null)DrawPath(pen!.Brush!,stroke);return;
        }
        DrawGeometry(brush,pen,Shape(rect));
    }
    public void DrawEllipse(IBrush? brush,IPen? pen,Rect rect)=>DrawGeometry(brush,pen,Shape(new(rect),true));
    public void DrawRegion(IBrush? brush,IPen? pen,IPlatformRenderInterfaceRegion region)=>DrawGeometry(brush,pen,((Region)region).Geometry);
    public void DrawGlyphRun(IBrush? brush,IGlyphRunImpl run)
    {
        Check();if(brush is null)return;
        if(textOptions.TextHintingMode is not (TextHintingMode.Unspecified or TextHintingMode.None))throw new NotSupportedException("TrueType hinting is not implemented; select TextHintingMode.None.");
        if(textOptions.TextRenderingMode==TextRenderingMode.SubpixelAntialias)throw new NotSupportedException("LCD subpixel rendering is not implemented; select Antialias.");
        var glyph=(GlyphRun)run;var origin=run.BaselineOrigin;
        var matrix=ApplyOrigin(Matrix.CreateTranslation(origin.X,origin.Y)*transform*dpiTransform);
        bool textAa=textOptions.TextRenderingMode==TextRenderingMode.Unspecified?Antialias:textOptions.TextRenderingMode!=TextRenderingMode.Alias;
        using var paint=CreatePaint(brush,ConvertTo.Rect(glyph.Glyphs.Bounds),textAa);
        Execute(new(CommandKind.DrawGlyphRun,default,matrix,paint.Paint){GlyphRun=glyph.Glyphs,TextMaskPreblend=true,SnapGlyphBaseline=textOptions.BaselinePixelAlignment!=BaselinePixelAlignment.Unaligned});
    }
    private CompositeMode BitmapComposite=>renderOptions.BitmapBlendingMode switch {BitmapBlendingMode.Unspecified or BitmapBlendingMode.SourceOver=>CompositeMode.SourceOver,BitmapBlendingMode.Source=>CompositeMode.Source,_=>throw new NotSupportedException($"Bitmap blend {renderOptions.BitmapBlendingMode} is not implemented.")};
    internal static ImageSampling Sampling(BitmapInterpolationMode mode)=>mode switch {BitmapInterpolationMode.None=>ImageSampling.Nearest,BitmapInterpolationMode.Unspecified or BitmapInterpolationMode.LowQuality or BitmapInterpolationMode.MediumQuality=>ImageSampling.Bilinear,BitmapInterpolationMode.HighQuality=>ImageSampling.Mitchell,_=>throw new NotSupportedException("Unknown interpolation mode.")};
    public void DrawBitmap(IBitmapImpl source,double opacity,Rect sourceRect,Rect destRect)
    {
        Check();var image=((Bitmap)source).Pixels;
        Execute(new(CommandKind.DrawImage,ConvertTo.Rect(destRect),DeviceMatrix,new(new(255,255,255,BrushFactory.Opacity(opacity)),BitmapComposite,Antialias))
        {Image=image,ImageSource=ConvertTo.Rect(sourceRect),Sampling=Sampling(renderOptions.BitmapInterpolationMode),NearestLowerBoundary=nearestLowerBoundary&&Sampling(renderOptions.BitmapInterpolationMode)==ImageSampling.Nearest});
    }
    public void DrawBitmap(IBitmapImpl source,IBrush mask,Rect maskRect,Rect dest)
    {PushOpacityMask(mask,maskRect);try{DrawBitmap(source,1,new Rect(source.PixelSize.ToSize(1)),dest);}finally{PopOpacityMask();}}
    internal void BlitDevice(Bitmap source)=>Execute(new(CommandKind.DrawImage,new(0,0,source.PixelSize.Width,source.PixelSize.Height),new(1,0,-originX,0,1,-originY,0,0,1),new(new(255,255,255,255),CompositeMode.SourceOver))
        {Image=source.Pixels,ImageSource=new(0,0,source.PixelSize.Width,source.PixelSize.Height),Sampling=ImageSampling.Nearest});
    public IDrawingContextLayerImpl CreateLayer(PixelSize size)=>new Bitmap(PixelBuffer.Rent(size.Width,size.Height),dpi){ScaleToDpi=false};
    public void PushClip(Rect rect)
    {
        Check();var m=DeviceMatrix;
        if(m.M01!=0||m.M10!=0){PushGeometryClip(Geometry.Rectangle(rect));return;}
        clips.Push((clip,coverageClip));var a=m.Map((float)rect.Left,(float)rect.Top);var b=m.Map((float)rect.Right,(float)rect.Bottom);
        // Rect clips use the native backend's non-AA device-rectangle rule.
        // In particular, +0.5 rounds up on both edges; integrating a fractional
        // clip as shape coverage or testing pixel centers produces different ink.
        static float Edge(float value)=>MathF.Floor(value+.5f);
        var next=new RectF(Edge(Math.Min(a.X,b.X)),Edge(Math.Min(a.Y,b.Y)),Edge(Math.Max(a.X,b.X)),Edge(Math.Max(a.Y,b.Y)));clip=Intersect(clip,next);
    }
    private static RectF Intersect(RectF? a,RectF b)=>a is {} r?new(Math.Max(r.Left,b.Left),Math.Max(r.Top,b.Top),Math.Min(r.Right,b.Right),Math.Min(r.Bottom,b.Bottom)):b;
    public void PushClip(RoundedRect rect){if(!rect.IsRounded)PushClip(rect.Rect);else PushGeometryClip(Shape(rect));}
    public void PushClip(IPlatformRenderInterfaceRegion region)=>PushGeometryClip(((Region)region).Geometry);
    public void PushGeometryClip(IGeometryImpl geometry)
    {
        using var measurement=SimdSkiaDiagnostics.Measure("clip");
        Check();var g=(Geometry)geometry;g.Validate();var next=CreateCoverageClip(g.Path,DeviceMatrix,Antialias);
        var combined=coverageClip is null?next:CoverageClip.Intersect(coverageClip,next);
        clips.Push((clip,coverageClip));coverageClip=combined;clip=Intersect(clip,next.Bounds);
    }
    public void PopClip(){Check();if(!clips.TryPop(out var state))throw new InvalidOperationException("Unbalanced clip stack.");(clip,coverageClip)=state;}
    public void PopGeometryClip()=>PopClip();
    private void BeginGroup(string kind,byte opacity,IBrush? mask,Rect bounds,FilterSpec? filter=null)
    {
        using var measurement=SimdSkiaDiagnostics.Measure("push-group");
        Check();using var validation=mask is not null?CreatePaint(mask,bounds,Antialias):default;
        if(opacity==255&&mask is null&&kind=="opacity"){groups.Push(new(kind,null,null,clip,coverageClip,opacity,null,bounds,dirty,DeviceMatrix));return;}
        // Only pixels that can reach the restore clip are needed. Layer bounds
        // are hints and do NOT crop arbitrary opacity/layer content. A mask is
        // zero outside its actual bounds; filters additionally need their halo.
        var extent=Intersect(clip,new(0,0,target.Width,target.Height));
        if(coverageClip is not null)extent=Intersect(extent,coverageClip.Bounds);
        if(mask is not null)
        {
            var m=DeviceMatrix;var a=m.Map((float)bounds.Left,(float)bounds.Top);var b=m.Map((float)bounds.Right,(float)bounds.Top);var c=m.Map((float)bounds.Left,(float)bounds.Bottom);var d=m.Map((float)bounds.Right,(float)bounds.Bottom);
            extent=Intersect(extent,new(Math.Min(Math.Min(a.X,b.X),Math.Min(c.X,d.X))-1,Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y))-1,Math.Max(Math.Max(a.X,b.X),Math.Max(c.X,d.X))+1,Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))+1));
        }
        if(filter is {} f&&!extent.IsEmpty)
        {float px=MathF.Ceiling(f.SigmaX*3)+1,py=MathF.Ceiling(f.SigmaY*3)+1;extent=new(Math.Min(extent.Left,extent.Left-f.OffsetX-px),Math.Min(extent.Top,extent.Top-f.OffsetY-py),Math.Max(extent.Right,extent.Right-f.OffsetX+px),Math.Max(extent.Bottom,extent.Bottom-f.OffsetY+py));}
        int x=(int)MathF.Floor(extent.Left),y=(int)MathF.Floor(extent.Top),width=Math.Max(1,(int)MathF.Ceiling(extent.Right)-x),height=Math.Max(1,(int)MathF.Ceiling(extent.Bottom)-y);
        if(width>32768||height>32768||(long)width*height*4>256*1024*1024)throw new NotSupportedException("Layer exceeds the configured 256 MiB limit.");
        var buffer=PixelBuffer.Rent(width,height,target.Format,AlphaFormat.Premultiplied);var next=new RasterCanvas(buffer);
        groups.Push(new(kind,target,canvas,clip,coverageClip,opacity,mask,bounds,dirty,DeviceMatrix,filter,x,y));target=buffer;canvas=next;dirty=null;originX+=x;originY+=y;
        // Apply the existing clip exactly once when restoring this group.
        coverageClip=null;clip=null;
    }
    private void EndGroup(string kind)
    {
        using var measurement=SimdSkiaDiagnostics.Measure("pop-group");
        Check();if(!groups.TryPeek(out var state)||state.Kind!=kind)throw new InvalidOperationException($"Unbalanced {kind} stack.");groups.Pop();if(state.Parent is null)return;
        var layer=target;var region=dirty;canvas.Dispose();target=state.Parent;canvas=state.Canvas!;clip=state.Clip;coverageClip=state.Coverage;dirty=state.ParentDirty;originX-=state.OffsetX;originY-=state.OffsetY;
        try
        {
            if(region is null)return;
            using var filtered=state.Filter?.Apply(layer,region.Value);
            var image=filtered??layer;
            if(state.Filter is {} filter)region=filter.Expand(region.Value);
            var area=new RectF(Math.Max(0,MathF.Floor(region.Value.Left)),Math.Max(0,MathF.Floor(region.Value.Top)),Math.Min(layer.Width,MathF.Ceiling(region.Value.Right)),Math.Min(layer.Height,MathF.Ceiling(region.Value.Bottom)));
            if(area.IsEmpty)return;
            if(state.Mask is not null)
            {
                using var mask=PixelBuffer.Rent(layer.Width,layer.Height,layer.Format);using var renderer=new RasterCanvas(mask);
                using var maskPaint=CreatePaint(state.Mask,state.Bounds,Antialias);
                var maskMatrix=state.Matrix with{M02=state.Matrix.M02-state.OffsetX,M12=state.Matrix.M12-state.OffsetY};
                renderer.Execute(DrawingCommand.FillRectangle(ConvertTo.Rect(state.Bounds),maskMatrix,maskPaint.Paint) with {CurveTolerance=0.015625f,Clip=area});
                for(int y=(int)area.Top;y<(int)area.Bottom;y++){var row=image.GetRowSpan(y);var alpha=mask.GetRowSpan(y);for(int x=(int)area.Left;x<(int)area.Right;x++)for(int c=0;c<4;c++)row[x*4+c]=PixelBuffer.Multiply(row[x*4+c],alpha[x*4+3]);}
            }
            var destination=new RectF(area.Left+state.OffsetX,area.Top+state.OffsetY,area.Right+state.OffsetX,area.Bottom+state.OffsetY);
            Execute(new(CommandKind.DrawImage,destination,Matrix3.Identity,new(new(255,255,255,state.Opacity),CompositeMode.SourceOver))
            {Image=image,ImageSource=area,Sampling=ImageSampling.Nearest});
        }
        finally{layer.Dispose();}
    }
    public void PushLayer(Rect bounds)=>BeginGroup("layer",255,null,bounds);
    public void PopLayer()=>EndGroup("layer");
    public void PushOpacity(double opacity,Rect? bounds)=>BeginGroup("opacity",BrushFactory.Opacity(opacity),null,bounds??default);
    public void PopOpacity()=>EndGroup("opacity");
    public void PushOpacityMask(IBrush mask,Rect bounds)=>BeginGroup("mask",255,mask,bounds);
    public void PopOpacityMask()=>EndGroup("mask");
    public void PushRenderOptions(RenderOptions options){Check();renderStack.Push(renderOptions);renderOptions=options.MergeWith(renderOptions);}
    public void PopRenderOptions(){Check();if(!renderStack.TryPop(out renderOptions))throw new InvalidOperationException("Unbalanced render options.");}
    public void PushTextOptions(TextOptions options){Check();textStack.Push(textOptions);textOptions=options.MergeWith(textOptions);}
    public void PopTextOptions(){Check();if(!textStack.TryPop(out textOptions))throw new InvalidOperationException("Unbalanced text options.");}
    public object? GetFeature(Type type)=>null; // Optional extension query; no unsupported drawing is accepted here.
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        bool unbalanced=groups.Count>0||clips.Count>0||renderStack.Count>0||textStack.Count>0;
        bool success=false;
        try
        {
            if(ReferenceEquals(canvas,rootBackend)){if(flushBackend)canvas.Flush();}else canvas.Dispose();
            success=!unbalanced;
        }
        finally
        {
            try
            {
                if(!ReferenceEquals(target,root))target.Dispose();
                foreach(var group in groups)if(group.Parent is not null){if(!ReferenceEquals(group.Canvas,rootBackend))group.Canvas?.Dispose();if(!ReferenceEquals(group.Parent,root))group.Parent.Dispose();}
                groups.Clear();root.Dispose();
            }
            finally{completed?.Invoke(success);}
        }
        if(unbalanced)throw new InvalidOperationException("Drawing context disposed with unbalanced layers, clips or options.");
    }
}
