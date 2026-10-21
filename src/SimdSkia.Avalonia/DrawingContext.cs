using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SimdSkia.Cpu;

namespace SimdSkia.Avalonia;

internal sealed class DrawingContext:IDrawingContextImplWithEffects
{
    private PixelBuffer target;
    private RasterCanvas canvas;
    private readonly PixelBuffer root;
    private readonly Matrix dpiTransform;
    private readonly Vector dpi;
    private readonly Action? completed;
    private bool disposed;
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
    private readonly record struct Group(string Kind,PixelBuffer? Parent,RasterCanvas? Canvas,RectF? Clip,CoverageClip? Coverage,byte Opacity,IBrush? Mask,Rect Bounds,RectF? ParentDirty,Matrix3 Matrix);
    private static readonly object shapeGate=new();
    private static readonly Dictionary<(RoundedRect Rect,bool Ellipse),Geometry> shapes=[];
    internal DrawingContext(PixelBuffer pixels,Vector dpi,bool scaleToDpi=true,Action? completed=null)
    {root=target=pixels.Share();canvas=new(target);this.dpi=dpi;dpiTransform=scaleToDpi?Matrix.CreateScale(dpi.X/96,dpi.Y/96):Matrix.Identity;this.completed=completed;}
    public Matrix Transform {get=>transform;set{Check();if(!value.IsIdentity&&!ConvertTo.Matrix(value).IsFinite)throw new ArgumentException("Transform must be finite.");transform=value;}}
    private Matrix3 DeviceMatrix=>ConvertTo.Matrix(transform*dpiTransform);
    private bool Antialias=>renderOptions.EdgeMode!=EdgeMode.Aliased;
    private void Check(){ObjectDisposedException.ThrowIf(disposed,this);target.ThrowIfDisposed();}
    private void Execute(DrawingCommand command)
    {
        using var measurement=SimdSkiaDiagnostics.Measure(command.Kind==CommandKind.FillPath?command.Paint.Shader is null?"path-solid":"path-gradient":command.Kind==CommandKind.DrawImage?"image":"rectangle");
        Check();canvas.Execute(command with {Clip=clip,CoverageClip=coverageClip,CurveTolerance=0.015625f});
        if(groups.Count==0)return;
        var bounds=command.Path?.ControlBounds??command.Rectangle;var m=command.Transform;
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
        geometry.Validate();var paint=BrushFactory.Create(brush,geometry.Bounds,text?textOptions.TextRenderingMode!=TextRenderingMode.Alias:Antialias);
        Execute(new(CommandKind.FillPath,default,matrix??DeviceMatrix,paint){Path=geometry.Path,CacheTextCoverage=true});
    }
    public void DrawGeometry(IBrush? brush,IPen? pen,IGeometryImpl geometry)
    {
        Check();var g=geometry as Geometry??throw new ArgumentException("Foreign geometry backend.");g.Validate();
        // Validate both brushes before touching destination pixels.
        if(brush is not null)BrushFactory.Create(brush,g.Bounds,Antialias);
        if(pen?.Brush is not null)BrushFactory.Create(pen.Brush,g.Bounds,Antialias);
        var stroke=pen?.Brush is not null&&pen.Thickness>0?(Geometry)g.GetWidenedGeometry(pen):null;
        if(brush is not null)DrawPath(brush,g);if(stroke is not null)DrawPath(pen!.Brush!,stroke);
    }
    public void DrawLine(IPen? pen,Point a,Point b)=>DrawGeometry(null,pen,new Geometry(new SimdSkia.PathGeometry([new(PathVerb.Move,ConvertTo.Point(a)),new(PathVerb.Line,ConvertTo.Point(b))])));
    public void DrawRectangle(IBrush? brush,IPen? pen,RoundedRect rect,BoxShadows shadows=default)
    {
        Check();if(shadows.Count>0)throw new NotSupportedException("Box shadows are not implemented by this backend.");
        if(rect.Rect.Width<=0||rect.Rect.Height<=0)return;
        if(!rect.IsRounded)
        {
            var stroke=pen?.Brush is not null&&pen.Thickness>0?(Geometry)Geometry.Rectangle(rect.Rect).GetWidenedGeometry(pen):null;
            if(stroke is not null)BrushFactory.Create(pen!.Brush!,stroke.Bounds,Antialias);
            if(brush is not null)Execute(DrawingCommand.FillRectangle(ConvertTo.Rect(rect.Rect),DeviceMatrix,BrushFactory.Create(brush,rect.Rect,Antialias)));
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
        var matrix=ConvertTo.Matrix(Matrix.CreateTranslation(origin.X,origin.Y)*transform*dpiTransform);
        if(textOptions.BaselinePixelAlignment==BaselinePixelAlignment.Aligned)matrix=matrix with {M12=MathF.Round(matrix.M12)};
        DrawPath(brush,new Geometry(glyph.Path),matrix,true);
    }
    private CompositeMode BitmapComposite=>renderOptions.BitmapBlendingMode switch {BitmapBlendingMode.Unspecified or BitmapBlendingMode.SourceOver=>CompositeMode.SourceOver,BitmapBlendingMode.Source=>CompositeMode.Source,_=>throw new NotSupportedException($"Bitmap blend {renderOptions.BitmapBlendingMode} is not implemented.")};
    internal static ImageSampling Sampling(BitmapInterpolationMode mode)=>mode switch {BitmapInterpolationMode.None=>ImageSampling.Nearest,BitmapInterpolationMode.Unspecified or BitmapInterpolationMode.LowQuality or BitmapInterpolationMode.MediumQuality=>ImageSampling.Bilinear,BitmapInterpolationMode.HighQuality=>ImageSampling.Mitchell,_=>throw new NotSupportedException("Unknown interpolation mode.")};
    public void DrawBitmap(IBitmapImpl source,double opacity,Rect sourceRect,Rect destRect)
    {
        Check();var image=((Bitmap)source).Pixels;
        Execute(new(CommandKind.DrawImage,ConvertTo.Rect(destRect),DeviceMatrix,new(new(255,255,255,BrushFactory.Opacity(opacity)),BitmapComposite,Antialias))
        {Image=image,ImageSource=ConvertTo.Rect(sourceRect),Sampling=Sampling(renderOptions.BitmapInterpolationMode)});
    }
    public void DrawBitmap(IBitmapImpl source,IBrush mask,Rect maskRect,Rect dest)
    {PushOpacityMask(mask,maskRect);try{DrawBitmap(source,1,new Rect(source.PixelSize.ToSize(1)),dest);}finally{PopOpacityMask();}}
    internal void BlitDevice(Bitmap source)=>Execute(new(CommandKind.DrawImage,new(0,0,source.PixelSize.Width,source.PixelSize.Height),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver))
        {Image=source.Pixels,ImageSource=new(0,0,source.PixelSize.Width,source.PixelSize.Height),Sampling=ImageSampling.Nearest});
    public void PushEffect(Rect? bounds,IEffect effect)=>throw new NotSupportedException($"Effect {effect.GetType().Name} is not implemented.");
    public void PopEffect()=>throw new InvalidOperationException("No supported effect was pushed.");
    public IDrawingContextLayerImpl CreateLayer(PixelSize size)=>new Bitmap(PixelBuffer.Rent(size.Width,size.Height),dpi){ScaleToDpi=false};
    public void PushClip(Rect rect)
    {
        Check();var m=DeviceMatrix;
        if(m.M01!=0||m.M10!=0){PushGeometryClip(Geometry.Rectangle(rect));return;}
        clips.Push((clip,coverageClip));var a=m.Map((float)rect.Left,(float)rect.Top);var b=m.Map((float)rect.Right,(float)rect.Bottom);
        var next=new RectF(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y));clip=Intersect(clip,next);
    }
    private static RectF Intersect(RectF? a,RectF b)=>a is {} r?new(Math.Max(r.Left,b.Left),Math.Max(r.Top,b.Top),Math.Min(r.Right,b.Right),Math.Min(r.Bottom,b.Bottom)):b;
    public void PushClip(RoundedRect rect){if(!rect.IsRounded)PushClip(rect.Rect);else PushGeometryClip(Shape(rect));}
    public void PushClip(IPlatformRenderInterfaceRegion region)=>PushGeometryClip(((Region)region).Geometry);
    public void PushGeometryClip(IGeometryImpl geometry)
    {
        using var measurement=SimdSkiaDiagnostics.Measure("clip");
        Check();var g=(Geometry)geometry;g.Validate();var next=canvas.CreateClip(g.Path,DeviceMatrix,Antialias);var combined=coverageClip is null?next:CoverageClip.Intersect(coverageClip,next);
        clips.Push((clip,coverageClip));coverageClip=combined;clip=Intersect(clip,next.Bounds);
    }
    public void PopClip(){Check();if(!clips.TryPop(out var state))throw new InvalidOperationException("Unbalanced clip stack.");(clip,coverageClip)=state;}
    public void PopGeometryClip()=>PopClip();
    private void BeginGroup(string kind,byte opacity,IBrush? mask,Rect bounds)
    {
        using var measurement=SimdSkiaDiagnostics.Measure("push-group");
        Check();if(mask is not null)BrushFactory.Create(mask,bounds,Antialias);
        if(opacity==255&&mask is null&&kind=="opacity"){groups.Push(new(kind,null,null,clip,coverageClip,opacity,null,bounds,dirty,DeviceMatrix));return;}
        var buffer=PixelBuffer.Rent(target.Width,target.Height,target.Format,AlphaFormat.Premultiplied);var next=new RasterCanvas(buffer);
        groups.Push(new(kind,target,canvas,clip,coverageClip,opacity,mask,bounds,dirty,DeviceMatrix));target=buffer;canvas=next;dirty=null;
        // Apply the existing clip exactly once when restoring this group.
        coverageClip=null;clip=null;
    }
    private void EndGroup(string kind)
    {
        using var measurement=SimdSkiaDiagnostics.Measure("pop-group");
        Check();if(!groups.TryPeek(out var state)||state.Kind!=kind)throw new InvalidOperationException($"Unbalanced {kind} stack.");groups.Pop();if(state.Parent is null)return;
        var layer=target;var region=dirty;canvas.Dispose();target=state.Parent;canvas=state.Canvas!;clip=state.Clip;coverageClip=state.Coverage;dirty=state.ParentDirty;
        try
        {
            if(region is null)return;
            var area=new RectF(Math.Max(0,MathF.Floor(region.Value.Left)),Math.Max(0,MathF.Floor(region.Value.Top)),Math.Min(layer.Width,MathF.Ceiling(region.Value.Right)),Math.Min(layer.Height,MathF.Ceiling(region.Value.Bottom)));
            if(area.IsEmpty)return;
            if(state.Mask is not null)
            {
                using var mask=PixelBuffer.Rent(layer.Width,layer.Height,layer.Format);using var renderer=new RasterCanvas(mask);
                renderer.Execute(DrawingCommand.FillRectangle(ConvertTo.Rect(state.Bounds),state.Matrix,BrushFactory.Create(state.Mask,state.Bounds,Antialias)) with {CurveTolerance=0.015625f,Clip=area});
                for(int y=(int)area.Top;y<(int)area.Bottom;y++){var row=layer.GetRowSpan(y);var alpha=mask.GetRowSpan(y);for(int x=(int)area.Left;x<(int)area.Right;x++)for(int c=0;c<4;c++)row[x*4+c]=PixelBuffer.Multiply(row[x*4+c],alpha[x*4+3]);}
            }
            Execute(new(CommandKind.DrawImage,area,Matrix3.Identity,new(new(255,255,255,state.Opacity),CompositeMode.SourceOver))
            {Image=layer,ImageSource=area,Sampling=ImageSampling.Nearest});
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
        try
        {
            canvas.Dispose();if(!ReferenceEquals(target,root))target.Dispose();
            foreach(var group in groups)if(group.Parent is not null){group.Canvas?.Dispose();if(!ReferenceEquals(group.Parent,root))group.Parent.Dispose();}
            groups.Clear();root.Dispose();
        }
        finally{completed?.Invoke();}
        if(unbalanced)throw new InvalidOperationException("Drawing context disposed with unbalanced layers, clips or options.");
    }
}
