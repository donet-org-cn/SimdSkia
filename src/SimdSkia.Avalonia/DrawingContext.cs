using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Vector4=System.Numerics.Vector4;
using SimdSkia.Cpu;

namespace SimdSkia.Avalonia;

internal sealed partial class DrawingContext:IDrawingContextImplWithEffects
{
    private PixelBuffer target;
    private ICanvasBackend canvas;
    private readonly ICanvasBackend? rootBackend;
    private readonly bool flushBackend;
    private readonly PixelBuffer root;
    private readonly bool ownsRoot;
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
    private readonly FrameScratch scratch=FrameScratch.Rent();
    private Stack<(RectF? Rect,CoverageClip? Coverage)> clips=>scratch.Clips;
    private Stack<RenderOptions> renderStack=>scratch.RenderOptions;
    private Stack<TextOptions> textStack=>scratch.TextOptions;
    private Stack<Group> groups=>scratch.Groups;
    private Stack<GroupScope> groupScopes=>scratch.GroupScopes;
    private RectF? dirty;
    private int activeLayers;
    private int originX,originY;
    private enum GroupKind {Opacity,Layer,Mask,Effect}
    private readonly record struct GroupScope(GroupKind Kind,bool HasLayer);
    private readonly record struct Group(PixelBuffer Parent,ICanvasBackend Canvas,RectF? Clip,CoverageClip? Coverage,byte Opacity,IBrush? Mask,Rect Bounds,RectF? ParentDirty,Matrix3 Matrix,FilterSpec? Filter=null,int OffsetX=0,int OffsetY=0);
    // Reuse only empty internal storage. Public context wrappers are never
    // recycled, so a disposed context cannot become valid in a later frame.
    private sealed class FrameScratch
    {
        internal readonly Stack<(RectF? Rect,CoverageClip? Coverage)> Clips=[];
        internal readonly Stack<RenderOptions> RenderOptions=[];
        internal readonly Stack<TextOptions> TextOptions=[];
        internal readonly Stack<Group> Groups=[];
        internal readonly Stack<GroupScope> GroupScopes=[];
        private static readonly object gate=new();
        private static readonly Stack<FrameScratch> pool=new(8);
        internal static FrameScratch Rent(){lock(gate)return pool.TryPop(out var value)?value:new();}
        internal static void Return(FrameScratch value)
        {
            value.Clips.Clear();value.RenderOptions.Clear();value.TextOptions.Clear();value.Groups.Clear();value.GroupScopes.Clear();
            if(value.Clips.EnsureCapacity(0)>64||value.RenderOptions.EnsureCapacity(0)>64||value.TextOptions.EnsureCapacity(0)>64||value.Groups.EnsureCapacity(0)>16||value.GroupScopes.EnsureCapacity(0)>64)return;
            lock(gate)if(pool.Count<8)pool.Push(value);
        }
    }
    private enum ShapeKind { Rounded, Ellipse, Rectangle }
    private readonly record struct ShapeKey(Rect Bounds, Vector TopLeft, Vector TopRight,
        Vector BottomRight, Vector BottomLeft, ShapeKind Kind);
    private static readonly object shapeGate=new();
    private static readonly Dictionary<ShapeKey,Geometry> shapes=[];
    private static readonly Dictionary<(Point Start,Point End),Geometry> lines=[];
    internal DrawingContext(PixelBuffer pixels,Vector dpi,bool scaleToDpi=true,Action<bool>? completed=null,ICanvasBackend? backend=null,bool flushBackend=true)
        : this(pixels,dpi,scaleToDpi,completed,backend,flushBackend,borrowPixels:false) { }

    internal DrawingContext(PixelBuffer pixels,Vector dpi,bool scaleToDpi,Action<bool>? completed,ICanvasBackend? backend,bool flushBackend,bool borrowPixels)
    {
        ownsRoot=!borrowPixels;
        root=target=borrowPixels?pixels:pixels.Share();
        rootBackend=backend;
        this.flushBackend=flushBackend;
        canvas=backend??new RasterCanvas(target);
        this.dpi=dpi;
        dpiTransform=scaleToDpi?Matrix.CreateScale(dpi.X/96,dpi.Y/96):Matrix.Identity;
        this.completed=completed;
    }
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
        Check();if(rootBackend is not null and not RasterCanvas&&canvas is RasterCanvas)SimdSkiaDiagnostics.SoftwareLayerCommand();
        canvas.Execute(command with {Clip=clip,CoverageClip=coverageClip,CurveTolerance=0.015625f});
        if(activeLayers==0)return;
        var bounds=command.GlyphRun?.Bounds??command.Path?.ControlBounds??command.Rectangle;var m=command.Transform;
        var a=m.Map(bounds.Left,bounds.Top);var b=m.Map(bounds.Right,bounds.Top);var c=m.Map(bounds.Left,bounds.Bottom);var d=m.Map(bounds.Right,bounds.Bottom);
        var area=Intersect(clip,new RectF(Math.Min(Math.Min(a.X,b.X),Math.Min(c.X,d.X))-1,Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y))-1,Math.Max(Math.Max(a.X,b.X),Math.Max(c.X,d.X))+1,Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))+1));
        area=Intersect(area,new(0,0,target.Width,target.Height));if(area.IsEmpty)return;
        dirty=dirty is {} old?new(Math.Min(area.Left,old.Left),Math.Min(area.Top,old.Top),Math.Max(area.Right,old.Right),Math.Max(area.Bottom,old.Bottom)):area;
    }
    private static Geometry Shape(RoundedRect rect,ShapeKind kind=ShapeKind.Rounded)
    {
        // RoundedRect uses object equality, which boxes on shape cache hits.
        var key = new ShapeKey(rect.Rect, rect.RadiiTopLeft, rect.RadiiTopRight,
            rect.RadiiBottomRight, rect.RadiiBottomLeft, kind);
        lock (shapeGate)
        {
            if (shapes.TryGetValue(key, out var shape))
                return shape;

            shape = kind switch
            {
                ShapeKind.Rectangle => Geometry.Rectangle(rect.Rect),
                ShapeKind.Ellipse => Geometry.Ellipse(rect.Rect),
                _ => Geometry.Rounded(rect)
            };
            if (shapes.Count >= 256)
                shapes.Clear();
            shapes.Add(key, shape);
            return shape;
        }
    }
    public void Clear(Color color)
    {Check();if(clip is null&&coverageClip is null&&activeLayers==0)canvas.Execute(DrawingCommand.Clear(ConvertTo.Color(color)));else Execute(DrawingCommand.FillRectangle(new(0,0,target.Width,target.Height),Matrix3.Identity,new(ConvertTo.Color(color),CompositeMode.Source)));}
    public void DrawGeometry(IBrush? brush,IPen? pen,IGeometryImpl geometry)=>DrawGeometry(brush,pen,geometry,null,null);
    private void DrawGeometry(IBrush? brush,IPen? pen,IGeometryImpl geometry,AnalyticShape? fillShape,AnalyticShape? strokeShape)
    {
        Check();var g=geometry as Geometry??throw new ArgumentException("Foreign geometry backend.");g.Validate();
        var stroke=pen?.Brush is not null&&pen.Thickness>0?(Geometry)g.GetWidenedGeometry(pen):null;
        // Validate once, retaining any tile pixels until the actual draw. Visual
        // content must not be rendered repeatedly merely to validate a paint.
        using var fill=brush is not null?CreatePaint(brush,g.Bounds,Antialias):default;
        using var outline=stroke is not null?CreatePaint(pen!.Brush!,stroke.Bounds,Antialias):default;
        if(brush is not null)Execute(new(CommandKind.FillPath,default,DeviceMatrix,fill.Paint){Path=g.Path,CacheTextCoverage=true,Shape=fillShape});
        if(stroke is not null)Execute(new(CommandKind.FillPath,default,DeviceMatrix,outline.Paint){Path=stroke.Path,CacheTextCoverage=true,Shape=strokeShape});
    }
    // Analytic descriptions of rectangle fills and solid strokes. They describe
    // exactly the geometry Geometry.Rounded and the stroker produce, so a device
    // backend can evaluate coverage without rasterizing the path.
    private static (RectF Rect,Vector4 Radii)? Circular(RoundedRect r)
    {
        var rect=r.Rect;var tl=r.RadiiTopLeft;var tr=r.RadiiTopRight;var br=r.RadiiBottomRight;var bl=r.RadiiBottomLeft;
        double scale=Math.Min(1,Math.Min(Math.Min(rect.Width/Math.Max(tl.X+tr.X,bl.X+br.X),rect.Height/Math.Max(tl.Y+bl.Y,tr.Y+br.Y)),1));
        if(!double.IsFinite(scale))scale=1;tl*=scale;tr*=scale;br*=scale;bl*=scale;
        if(tl.X!=tl.Y||tr.X!=tr.Y||br.X!=br.Y||bl.X!=bl.Y)return null;
        var radii=new Vector4((float)tl.X,(float)tr.X,(float)br.X,(float)bl.X);
        return float.IsFinite(radii.X+radii.Y+radii.Z+radii.W)?(ConvertTo.Rect(rect),radii):null;
    }
    private static AnalyticShape? FillShape(RoundedRect r)=>Circular(r) is {} c?new(c.Rect,c.Radii,default,default,false):null;
    private static AnalyticShape? StrokeShape(RectF rect,Vector4 radii,IPen? pen)
    {
        if(pen?.Brush is null||!(pen.Thickness>0)||pen.DashStyle is not null)return null;
        float half=(float)pen.Thickness/2;
        // Rounded corners offset into arcs; a sharp corner keeps a miter (90° needs
        // a limit of √2) or becomes a round join. Bevels need the path.
        bool miter=pen.LineJoin==PenLineJoin.Miter&&pen.MiterLimit>=1.4143,round=pen.LineJoin==PenLineJoin.Round;
        float Outer(float r)=>r>0?r+half:round?half:miter?0:float.NaN;
        var outer=new Vector4(Outer(radii.X),Outer(radii.Y),Outer(radii.Z),Outer(radii.W));
        if(!float.IsFinite(outer.X+outer.Y+outer.Z+outer.W))return null;
        var inner=new RectF(rect.Left+half,rect.Top+half,rect.Right-half,rect.Bottom-half);
        bool ring=inner.Right>inner.Left&&inner.Bottom>inner.Top;
        return new(new(rect.Left-half,rect.Top-half,rect.Right+half,rect.Bottom+half),outer,ring?inner:default,
            Vector4.Max(radii-new Vector4(half),Vector4.Zero),ring);
    }
    public void DrawLine(IPen? pen,Point a,Point b)
    {
        Check();
        static Geometry Line(Point start,Point end)=>new(new SimdSkia.PathGeometry(
            [new(PathVerb.Move,ConvertTo.Point(start)),new(PathVerb.Line,ConvertTo.Point(end))]));
        Geometry geometry;
        if(pen?.Brush is null||!(pen.Thickness>0)||pen.DashStyle is not null)geometry=Line(a,b);
        else lock(shapeGate)
        {
            if(!lines.TryGetValue((a,b),out var existing))
            {
                existing=Line(a,b);
                if(lines.Count>=64)lines.Clear();
                lines.Add((a,b),existing);
            }
            geometry=existing;
        }
        DrawGeometry(null,pen,geometry);
    }
    public void DrawRectangle(IBrush? brush,IPen? pen,RoundedRect rect,BoxShadows shadows=default)
    {
        Check();ValidateShadows(shadows);
        if(rect.Rect.Width<=0||rect.Rect.Height<=0)return;
        if(shadows.Count>0){DrawShadowedRectangle(brush,pen,rect,shadows);return;}
        if(!rect.IsRounded)
        {
            var stroke=pen?.Brush is not null&&pen.Thickness>0
                ?(Geometry)(pen.DashStyle is null?Shape(rect,ShapeKind.Rectangle):Geometry.Rectangle(rect.Rect)).GetWidenedGeometry(pen):null;
            using var outline=stroke is not null?CreatePaint(pen!.Brush!,stroke.Bounds,Antialias):default;
            if(brush is not null){using var fill=CreatePaint(brush,rect.Rect,Antialias);Execute(DrawingCommand.FillRectangle(ConvertTo.Rect(rect.Rect),DeviceMatrix,fill.Paint));}
            if(stroke is not null)Execute(new(CommandKind.FillPath,default,DeviceMatrix,outline.Paint){Path=stroke.Path,CacheTextCoverage=true,Shape=StrokeShape(ConvertTo.Rect(rect.Rect),Vector4.Zero,pen)});
            return;
        }
        var shape=FillShape(rect);
        DrawGeometry(brush,pen,Shape(rect),shape,shape is {} f?StrokeShape(f.Outer,f.OuterRadii,pen):null);
    }
    public void DrawEllipse(IBrush? brush,IPen? pen,Rect rect)=>DrawGeometry(brush,pen,Shape(new(rect),ShapeKind.Ellipse));
    public void DrawRegion(IBrush? brush,IPen? pen,IPlatformRenderInterfaceRegion region)=>DrawGeometry(brush,pen,((Region)region).Geometry);
    public void DrawGlyphRun(IBrush? brush,IGlyphRunImpl run)
    {
        Check();if(brush is null)return;
        if(textOptions.TextHintingMode is not (TextHintingMode.Unspecified or TextHintingMode.None))throw new NotSupportedException("TrueType hinting is not implemented; select TextHintingMode.None.");
        if(textOptions.TextRenderingMode==TextRenderingMode.SubpixelAntialias)throw new NotSupportedException("LCD subpixel rendering is not implemented; select Antialias.");
        var glyph=(GlyphRun)run;var origin=run.BaselineOrigin;
        var matrix=ApplyOrigin(Matrix.CreateTranslation(origin.X,origin.Y)*transform*dpiTransform);
        bool textAa=textOptions.TextRenderingMode==TextRenderingMode.Unspecified?Antialias:textOptions.TextRenderingMode!=TextRenderingMode.Alias;
        using var paint=CreatePaint(brush,ConvertTo.Rect(glyph.LocalInkBounds),textAa);
        var command=new DrawingCommand(CommandKind.DrawGlyphRun,default,matrix,paint.Paint)
            {GlyphRun=glyph.Glyphs,TextMaskPreblend=true,SnapGlyphBaseline=textOptions.BaselinePixelAlignment!=BaselinePixelAlignment.Unaligned};
        if(glyph.LayerDraws is not {} layers){Execute(command);return;}
        foreach(var layer in layers)
        {
            var layerPaint=paint.Paint;
            if(layer.Bitmap is {} bitmap)
            {
                DrawGlyphBitmap(bitmap,(float)glyph.FontRenderingEmSize,command,layerPaint.Color.A);
                continue;
            }
            if(layer.Color is {} color)layerPaint=new(color with {A=PixelBuffer.Multiply(color.A,layerPaint.Color.A)},layerPaint.Composite,textAa);
            Execute(command with {GlyphRun=layer.Geometry,Paint=layerPaint});
        }
    }
    private void DrawGlyphBitmap(GlyphRun.BitmapDraw draw,float size,in DrawingCommand glyph,byte opacity)
    {
        if(opacity==0)return;
        var m=glyph.Transform;
        if(!m.IsAffine)throw new NotSupportedException("Glyph transforms must be affine.");
        float ppem=size*MathF.Sqrt(m.M01*m.M01+m.M11*m.M11);
        var bitmap=ppem==size?draw.Bitmap:draw.Font.GetBitmapGlyph(draw.Glyph,ppem)??draw.Bitmap;
        using var lease=BitmapGlyphCache.Acquire(draw.Font,bitmap);
        var origin=m.Map(draw.Position.X,draw.Position.Y);
        float snapX=glyph.Paint.Antialias?4:1,snapY=snapX;
        if(glyph.SnapGlyphBaseline){if(m.M10==0)snapY=1;else if(m.M00==0)snapX=1;}
        m=m with {M02=MathF.Floor(origin.X*snapX+.5f)/snapX,M12=MathF.Floor(origin.Y*snapY+.5f)/snapY};
        var destination=bitmap.Bounds(size,draw.OutlineOrigin);
        Execute(new(CommandKind.DrawImage,destination,m,new(new(255,255,255,opacity),glyph.Paint.Composite,glyph.Paint.Antialias))
        {Image=lease.Image,ImageIsImmutable=true,ImageContentVersion=1,Sampling=glyph.Paint.Antialias?ImageSampling.Bilinear:ImageSampling.Nearest});
    }
    private CompositeMode BitmapComposite=>renderOptions.BitmapBlendingMode switch {BitmapBlendingMode.Unspecified or BitmapBlendingMode.SourceOver=>CompositeMode.SourceOver,BitmapBlendingMode.Source=>CompositeMode.Source,_=>throw new NotSupportedException($"Bitmap blend {renderOptions.BitmapBlendingMode} is not implemented.")};
    internal static ImageSampling Sampling(BitmapInterpolationMode mode)=>mode switch {BitmapInterpolationMode.None=>ImageSampling.Nearest,BitmapInterpolationMode.Unspecified or BitmapInterpolationMode.LowQuality or BitmapInterpolationMode.MediumQuality=>ImageSampling.Bilinear,BitmapInterpolationMode.HighQuality=>ImageSampling.Mitchell,_=>throw new NotSupportedException("Unknown interpolation mode.")};
    public void DrawBitmap(IBitmapImpl source,double opacity,Rect sourceRect,Rect destRect)
    {
        Check();var bitmap=(Bitmap)source;var image=bitmap.Pixels;
        // Uniform image layout can subtract almost equal doubles and produce a
        // tiny negative origin. Normalize arithmetic roundoff before float conversion.
        // Genuine out-of-bounds rectangles still reach the backend's strict validation.
        double extent=Math.Max(image.Width,image.Height),epsilon=8*(Math.BitIncrement(extent)-extent);
        double left=sourceRect.Left,top=sourceRect.Top,right=sourceRect.Right,bottom=sourceRect.Bottom;
        if(left<0&&left>=-epsilon)left=0;if(top<0&&top>=-epsilon)top=0;
        if(right>image.Width&&right<=image.Width+epsilon)right=image.Width;
        if(bottom>image.Height&&bottom<=image.Height+epsilon)bottom=image.Height;
        var crop=new RectF((float)left,(float)top,(float)right,(float)bottom);
        Execute(new(CommandKind.DrawImage,ConvertTo.Rect(destRect),DeviceMatrix,new(new(255,255,255,BrushFactory.Opacity(opacity)),BitmapComposite,Antialias))
        {Image=image,ImageContentVersion=bitmap.ImageContentVersion,ImageSource=crop,Sampling=Sampling(renderOptions.BitmapInterpolationMode),NearestLowerBoundary=nearestLowerBoundary&&Sampling(renderOptions.BitmapInterpolationMode)==ImageSampling.Nearest});
    }
    public void DrawBitmap(IBitmapImpl source,IBrush mask,Rect maskRect,Rect dest)
    {PushOpacityMask(mask,maskRect);try{DrawBitmap(source,1,new Rect(source.PixelSize.ToSize(1)),dest);}finally{PopOpacityMask();}}
    internal void BlitDevice(Bitmap source)=>Execute(new(CommandKind.DrawImage,new(0,0,source.PixelSize.Width,source.PixelSize.Height),new(1,0,-originX,0,1,-originY,0,0,1),new(new(255,255,255,255),CompositeMode.SourceOver))
        {Image=source.Pixels,ImageContentVersion=source.ImageContentVersion,ImageSource=new(0,0,source.PixelSize.Width,source.PixelSize.Height),Sampling=ImageSampling.Nearest});
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
        Check();var g=(Geometry)geometry;g.Validate();
        var matrix = DeviceMatrix;
        if (TryGetHardClip(g.Path, matrix, out var rectangle))
        {
            clips.Push((clip, coverageClip));
            clip = Intersect(clip, rectangle);
            return;
        }
        var next=CreateCoverageClip(g.Path,matrix,Antialias);
        var combined=coverageClip is null?next:CoverageClip.Intersect(coverageClip,next);
        clips.Push((clip,coverageClip));coverageClip=combined;clip=Intersect(clip,next.Bounds);
    }
    private bool TryGetHardClip(PathGeometry path, Matrix3 matrix, out RectF rectangle)
    {
        rectangle = default;
        var segments = path.Segments;
        if (segments.Length != 5 || segments[0].Verb != PathVerb.Move ||
            segments[1].Verb != PathVerb.Line || segments[2].Verb != PathVerb.Line ||
            segments[3].Verb != PathVerb.Line || segments[4].Verb != PathVerb.Close ||
            path.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd ||
            matrix.M01 != 0 || matrix.M10 != 0 || !matrix.IsFinite)
            return false;

        var a = segments[0].A;
        var b = segments[1].A;
        var c = segments[2].A;
        var d = segments[3].A;
        if (!(a.Y == b.Y && b.X == c.X && c.Y == d.Y && d.X == a.X) &&
            !(a.X == b.X && b.Y == c.Y && c.X == d.X && d.Y == a.Y))
            return false;

        var start = matrix.Map(a.X, a.Y);
        var end = matrix.Map(c.X, c.Y);
        // Integral edges have identical hard-clip and path coverage on every pixel.
        static bool Integral(float v) => float.IsFinite(v) && v == MathF.Truncate(v);
        if (!Integral(start.X) || !Integral(start.Y) || !Integral(end.X) || !Integral(end.Y))
            return false;

        rectangle = new(
            Math.Clamp(MathF.Min(start.X, end.X), 0, target.Width),
            Math.Clamp(MathF.Min(start.Y, end.Y), 0, target.Height),
            Math.Clamp(MathF.Max(start.X, end.X), 0, target.Width),
            Math.Clamp(MathF.Max(start.Y, end.Y), 0, target.Height));
        return true;
    }
    public void PopClip(){Check();if(!clips.TryPop(out var state))throw new InvalidOperationException("Unbalanced clip stack.");(clip,coverageClip)=state;}
    public void PopGeometryClip()=>PopClip();
    private void BeginGroup(GroupKind kind,byte opacity,IBrush? mask,Rect bounds,FilterSpec? filter=null)
    {
        using var measurement=SimdSkiaDiagnostics.Measure("push-group");
        Check();using var validation=mask is not null?CreatePaint(mask,bounds,Antialias):default;
        // Avalonia pushes opacity 1 for ordinary visuals. Retain the balanced
        // scope, but no saved state or dirty bounds are needed without a layer.
        if(opacity==255&&mask is null&&kind==GroupKind.Opacity){groupScopes.Push(new(kind,false));return;}
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
        // A device backend keeps the layer on its device; otherwise a CPU layer.
        PixelBuffer buffer;ICanvasBackend next;
        if(canvas is ILayerBackendProvider provider&&provider.TryCreateLayer(width,height,target.Format,out var devicePixels,out var deviceCanvas)){buffer=devicePixels;next=deviceCanvas;}
        else{buffer=PixelBuffer.Rent(width,height,target.Format,AlphaFormat.Premultiplied);next=new RasterCanvas(buffer);}
        groups.Push(new(target,canvas,clip,coverageClip,opacity,mask,bounds,dirty,DeviceMatrix,filter,x,y));groupScopes.Push(new(kind,true));activeLayers++;target=buffer;canvas=next;dirty=null;originX+=x;originY+=y;
        // Apply the existing clip exactly once when restoring this group.
        coverageClip=null;clip=null;
    }
    private void EndGroup(GroupKind kind)
    {
        using var measurement=SimdSkiaDiagnostics.Measure("pop-group");
        Check();if(!groupScopes.TryPeek(out var scope)||scope.Kind!=kind)throw new InvalidOperationException($"Unbalanced {kind} stack.");groupScopes.Pop();if(!scope.HasLayer)return;
        var state=groups.Pop();
        activeLayers--;
        var layer=target;var region=dirty;var layerCanvas=canvas;
        // A CPU layer is complete once disposed. A device layer stays alive until
        // its effect was encoded; its pixels are then sampled in place.
        var device=layerCanvas as IFilterBackend;
        if(device is null)layerCanvas.Dispose();
        target=state.Parent;canvas=state.Canvas!;clip=state.Clip;coverageClip=state.Coverage;dirty=state.ParentDirty;originX-=state.OffsetX;originY-=state.OffsetY;
        PixelBuffer? filtered=null;
        try
        {
            if(region is null)return;
            if(state.Filter is {} spec&&device is not null)
                filtered=device.TryFilter(layer,region.Value,spec.SigmaX,spec.SigmaY,spec.OffsetX,spec.OffsetY,spec.Color,spec.Shadow);
            // CPU filters and masks read or modify layer bytes.
            if(device is not null&&(filtered is null&&state.Filter is not null||state.Mask is not null))device.SynchronizeForCpu();
            filtered??=state.Filter?.Apply(layer,region.Value);
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
        finally
        {
            try{if(device is not null)layerCanvas.Dispose();}
            finally{filtered?.Dispose();layer.Dispose();}
        }
    }
    public void PushLayer(Rect bounds)=>BeginGroup(GroupKind.Layer,255,null,bounds);
    public void PopLayer()=>EndGroup(GroupKind.Layer);
    public void PushOpacity(double opacity,Rect? bounds)=>BeginGroup(GroupKind.Opacity,BrushFactory.Opacity(opacity),null,bounds??default);
    public void PopOpacity()=>EndGroup(GroupKind.Opacity);
    public void PushOpacityMask(IBrush mask,Rect bounds)=>BeginGroup(GroupKind.Mask,255,mask,bounds);
    public void PopOpacityMask()=>EndGroup(GroupKind.Mask);
    public void PushRenderOptions(RenderOptions options){Check();renderStack.Push(renderOptions);renderOptions=options.MergeWith(renderOptions);}
    public void PopRenderOptions(){Check();if(!renderStack.TryPop(out renderOptions))throw new InvalidOperationException("Unbalanced render options.");}
    public void PushTextOptions(TextOptions options){Check();textStack.Push(textOptions);textOptions=options.MergeWith(textOptions);}
    public void PopTextOptions(){Check();if(!textStack.TryPop(out textOptions))throw new InvalidOperationException("Unbalanced text options.");}
    public object? GetFeature(Type type)=>null; // Optional extension query; no unsupported drawing is accepted here.
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        bool unbalanced=groupScopes.Count>0||clips.Count>0||renderStack.Count>0||textStack.Count>0;
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
                groups.Clear();if(ownsRoot)root.Dispose();
            }
            finally{try{completed?.Invoke(success);}finally{FrameScratch.Return(scratch);}}
        }
        if(unbalanced)throw new InvalidOperationException("Drawing context disposed with unbalanced layers, clips or options.");
    }
}
