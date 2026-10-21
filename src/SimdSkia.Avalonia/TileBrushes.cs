using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using SimdSkia.Cpu;

namespace SimdSkia.Avalonia;

internal readonly struct PaintLease(Paint paint,IDisposable? resource=null):IDisposable
{
    internal Paint Paint {get;}=paint;
    public void Dispose()=>resource?.Dispose();
}

internal sealed partial class DrawingContext
{
    [ThreadStatic] private static int sceneDepth;
    // The runtime contract is deliberately internal in the pinned Avalonia
    // package. Cache the interface getter; never expose or fabricate a handle.
    private static readonly PropertyInfo imageSourceProperty=typeof(IImageBrushSource).GetProperty("Bitmap",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
        ??throw new MissingMemberException("Avalonia 12.1.4 IImageBrushSource.Bitmap contract is missing.");
    private static readonly PropertyInfo imageReferenceItem=GetImageReferenceItem();
    // The pinned contract is IRef<IBitmapImpl>, instantiated by Avalonia's Bitmap.
    // Root its getter metadata explicitly; the trimmer cannot infer PropertyType's value.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties,"Avalonia.Utilities.IRef`1","Avalonia.Base")]
    [UnconditionalSuppressMessage("Trimming","IL2075",Justification="The exact IRef<IBitmapImpl>.Item getter is retained by DynamicDependency on Avalonia 12.1.4's IRef<T> public properties.")]
    private static PropertyInfo GetImageReferenceItem()=>imageSourceProperty.PropertyType.GetProperty("Item")
        ??throw new MissingMemberException("Avalonia 12.1.4 image reference contract is missing.");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PaintLease CreatePaint(IBrush brush,Rect bounds,bool aa,CompositeMode composite=CompositeMode.SourceOver)
    {
        if(brush is ISolidColorBrush solid)
        {var color=ConvertTo.Color(solid.Color);return new(new(color with{A=PixelBuffer.Multiply(color.A,BrushFactory.Opacity(brush.Opacity))},composite,aa));}
        return CreateComplexPaint(brush,bounds,aa,composite);
    }
    private PaintLease CreateComplexPaint(IBrush brush,Rect bounds,bool aa,CompositeMode composite)
    {
        if(brush is not (IImageBrush or ISceneBrush or ISceneBrushContent))return new(BrushFactory.Create(brush,bounds,aa,composite));
        byte opacity=BrushFactory.Opacity(brush.Opacity);
        ISceneBrushContent? owned=null;ISceneBrushContent? content=brush as ISceneBrushContent;
        try
        {
            if(brush is ISceneBrush scene)content=owned=scene.CreateContent();
            var tile=content?.Brush??(ITileBrush)brush;
            if(!Enum.IsDefined(tile.TileMode)||!Enum.IsDefined(tile.Stretch)||!Enum.IsDefined(tile.AlignmentX)||!Enum.IsDefined(tile.AlignmentY))throw new NotSupportedException("Unknown tile brush layout mode.");
            Bitmap? bitmap=null;
            if(brush is IImageBrush image&&image.Source is not null)
            {
                var reference=imageSourceProperty.GetValue(image.Source);
                bitmap=(reference is null?null:imageReferenceItem.GetValue(reference)) as Bitmap??throw new ArgumentException("Image brush source belongs to a foreign rendering backend or is disposed.");
                bitmap.Pixels.ThrowIfDisposed();
            }
            if(content is null&&bitmap is null)return new(new(new(0,0,0,0),composite,aa));
            if(sceneDepth>=32)throw new InvalidOperationException("Visual brush recursion exceeded 32 nested scenes (possible cycle).");
            var destination=tile.DestinationRect.ToPixels(bounds.Size);
            if(!Finite(destination)||!Finite(bounds))throw new ArgumentException("Tile brush bounds must be finite.");
            if(destination.Width<=0||destination.Height<=0||bounds.Width<=0||bounds.Height<=0)return new(new(default,composite,aa));
            bool visual=content is not null;
            var sourceBounds=visual?content!.Rect:new Rect(0,0,bitmap!.PixelSize.Width*96/dpi.X,bitmap.PixelSize.Height*96/dpi.Y);
            var source=visual?tile.SourceRect.ToPixels(sourceBounds):tile.SourceRect.ToPixels(sourceBounds.Size);
            if(!Finite(source))throw new ArgumentException("Tile source rectangle must be finite.");
            if(source.Width<=0||source.Height<=0)return new(new(default,composite,aa));
            var logicalSize=visual||tile.TileMode!=TileMode.None?destination.Size:bounds.Size;
            int width=(int)Math.Ceiling(logicalSize.Width*dpi.X/96),height=(int)Math.Ceiling(logicalSize.Height*dpi.Y/96);
            if(width<=0||height<=0)return new(new(default,composite,aa));
            if(width>16384||height>16384||(long)width*height>16*1024*1024)throw new NotSupportedException("Tile brush rasterization exceeds 16384 pixels/64 MiB.");
            var scale=tile.Stretch.CalculateScaling(destination.Size,source.Size);
            double ax=tile.AlignmentX switch{AlignmentX.Left=>0,AlignmentX.Center=>.5,AlignmentX.Right=>1,_=>throw new NotSupportedException()},ay=tile.AlignmentY switch{AlignmentY.Top=>0,AlignmentY.Center=>.5,AlignmentY.Bottom=>1,_=>throw new NotSupportedException()};
            var drawing=Matrix.CreateTranslation(-source.X,-source.Y)*Matrix.CreateScale(scale)*Matrix.CreateTranslation((destination.Width-source.Width*scale.X)*ax,(destination.Height-source.Height*scale.Y)*ay);
            var brushOrigin=brush.TransformOrigin.ToPixels(bounds.Size)+new Vector(bounds.X,bounds.Y);
            var brushTransform=Matrix.CreateTranslation(-brushOrigin.X,-brushOrigin.Y)*(brush.Transform?.Value??Matrix.Identity)*Matrix.CreateTranslation(brushOrigin.X,brushOrigin.Y);
            if(!visual&&tile.TileMode==TileMode.None)drawing*=Matrix.CreateTranslation(destination.X,destination.Y);
            var cacheKey=new TileRasterCache.Key((object?)content??bitmap!,bitmap?.Version??0,tile.SourceRect,tile.DestinationRect,tile.Stretch,tile.AlignmentX,tile.AlignmentY,tile.TileMode,bounds.Size,dpi,renderOptions,textOptions);
            var pixels=TileRasterCache.Get(cacheKey);
            if(pixels is null)
            {
                using var raster=PixelBuffer.Rent(width,height,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
                using(var renderer=new DrawingContext(raster,dpi,false){nearestLowerBoundary=true})
                {
                    renderer.PushRenderOptions(renderOptions);renderer.PushTextOptions(textOptions);
                    var deviceDrawing=drawing*Matrix.CreateScale(dpi.X/96,dpi.Y/96);
                    if(content is not null)
                    {sceneDepth++;try{content.Render(renderer,deviceDrawing);}finally{sceneDepth--;}}
                    else
                    {
                        var clipBounds=tile.TileMode==TileMode.None?destination:new Rect(destination.Size);
                        renderer.Transform=Matrix.CreateScale(dpi.X/96,dpi.Y/96);renderer.PushClip(clipBounds);
                        renderer.Transform=deviceDrawing;
                        renderer.DrawBitmap(bitmap!,1,new Rect(bitmap!.PixelSize.ToSize(1)),sourceBounds);
                        renderer.PopClip();
                    }
                    renderer.PopTextOptions();renderer.PopRenderOptions();
                }
                pixels=TileRasterCache.Add(cacheKey,raster);
            }
            Matrix imageToLocal;
            if(visual)imageToLocal=Matrix.CreateScale(96/dpi.X,96/dpi.Y)*brushTransform*Matrix.CreateTranslation(tile.DestinationRect.ToPixels(bounds).Position);
            else
            {
                imageToLocal=(tile.DestinationRect.Unit==RelativeUnit.Relative?Matrix.CreateTranslation(bounds.Position):Matrix.Identity)*brushTransform*Matrix.CreateScale(96/dpi.X,96/dpi.Y);
                if(tile.TileMode!=TileMode.None)imageToLocal*=Matrix.CreateTranslation(-destination.X,-destination.Y);
            }
            if(!imageToLocal.TryInvert(out var inverse)){pixels.Dispose();throw new NotSupportedException("Singular tile brush transforms are not supported.");}
            var shader=new TileShader(pixels,inverse,tile.TileMode,visual?ImageSampling.Nearest:Sampling(renderOptions.BitmapInterpolationMode));
            return new(new(new(255,255,255,opacity),composite,aa,shader),shader);
        }
        finally{owned?.Dispose();}
    }
    private static bool Finite(Rect rect)=>double.IsFinite(rect.X)&&double.IsFinite(rect.Y)&&double.IsFinite(rect.Width)&&double.IsFinite(rect.Height);
}

internal static class TileRasterCache
{
    internal readonly record struct Key(object Source,int Version,RelativeRect SourceRect,RelativeRect DestinationRect,Stretch Stretch,AlignmentX AlignmentX,AlignmentY AlignmentY,TileMode Mode,Size Bounds,Vector Dpi,RenderOptions Render,TextOptions Text);
    private sealed record Entry(Key Key,PixelBuffer Pixels,long Bytes);
    private static readonly object gate=new();
    private static readonly Dictionary<Key,LinkedListNode<Entry>> entries=[];
    private static readonly LinkedList<Entry> order=[];
    private static long bytes;
    internal static PixelBuffer? Get(Key key)
    {lock(gate){if(!entries.TryGetValue(key,out var entry))return null;order.Remove(entry);order.AddLast(entry);return entry.Value.Pixels.Share();}}
    internal static PixelBuffer Add(Key key,PixelBuffer pixels)
    {
        long size=(long)pixels.Width*pixels.Height*4;
        lock(gate)
        {
            if(entries.TryGetValue(key,out var existing))return existing.Value.Pixels.Share();
            if(size<=16*1024*1024)
            {
                while(entries.Count>=64||bytes+size>16*1024*1024)
                {var first=order.First!;bytes-=first.Value.Bytes;entries.Remove(first.Value.Key);order.RemoveFirst();first.Value.Pixels.Dispose();}
                entries.Add(key,order.AddLast(new Entry(key,pixels.Share(),size)));bytes+=size;
            }
            return pixels.Share();
        }
    }
}

internal sealed class TileShader(PixelBuffer pixels,Matrix inverse,TileMode mode,ImageSampling sampling):IColorShader,IDisposable
{
    private static int Mod(int value,int length){int v=value%length;return v<0?v+length:v;}
    private Rgba32 Read(int x,int y)
    {
        if(mode==TileMode.None&&(x<0||y<0||x>=pixels.Width||y>=pixels.Height))return default;
        bool mirrorX=mode is TileMode.FlipX or TileMode.FlipXY,mirrorY=mode is TileMode.FlipY or TileMode.FlipXY;
        x=Mod(x,pixels.Width*(mirrorX?2:1));y=Mod(y,pixels.Height*(mirrorY?2:1));
        if(x>=pixels.Width)x=pixels.Width*2-1-x;if(y>=pixels.Height)y=pixels.Height*2-1-y;
        var p=pixels.GetRowSpan(y).Slice(x*4,4);return new(p[2],p[1],p[0],p[3]);
    }
    public Rgba32 Sample(float x,float y)
    {
        var local=inverse.Transform(new Point(x,y));double px=local.X-.5,py=local.Y-.5;
        Rgba32 premultiplied;
        // The pinned nearest sampler uses fixed-point coordinates and chooses
        // the lower texel on a boundary. Remove float-transform overshoot at
        // tile seams before applying that rule (also for negative coordinates).
        if(sampling==ImageSampling.Nearest)
        {
            double tx=Math.Floor(local.X*65536)/65536,ty=Math.Floor(local.Y*65536)/65536;
            int ix=(int)Math.Ceiling(tx)-1,iy=(int)Math.Ceiling(ty)-1;
            if(mode==TileMode.FlipX&&ty%pixels.Height==0)iy=(int)ty;
            if(mode==TileMode.FlipY&&tx%pixels.Width==0)ix=(int)tx;
            premultiplied=Read(ix,iy);
        }
        else
        {
            int ix=(int)Math.Floor(px),iy=(int)Math.Floor(py);double fx=px-ix,fy=py-iy,r=0,g=0,b=0,a=0;
            static double Mitchell(double v){v=Math.Abs(v);return v<1?(7*v*v*v-12*v*v+16d/3)/6:v<2?(-7d/3*v*v*v+12*v*v-20*v+32d/3)/6:0;}
            int start=sampling==ImageSampling.Mitchell?-1:0,end=sampling==ImageSampling.Mitchell?2:1;
            for(int j=start;j<=end;j++)for(int i=start;i<=end;i++)
            {double weight=sampling==ImageSampling.Mitchell?Mitchell(i-fx)*Mitchell(j-fy):(i==0?1-fx:fx)*(j==0?1-fy:fy);var p=Read(ix+i,iy+j);r+=p.R*weight;g+=p.G*weight;b+=p.B*weight;a+=p.A*weight;}
            byte alpha=(byte)Math.Clamp(Math.Round(a),0,255);byte Channel(double c)=>(byte)Math.Clamp(Math.Round(c),0,alpha);premultiplied=new(Channel(r),Channel(g),Channel(b),alpha);
        }
        return new(PixelBuffer.Unmultiply(premultiplied.R,premultiplied.A),PixelBuffer.Unmultiply(premultiplied.G,premultiplied.A),PixelBuffer.Unmultiply(premultiplied.B,premultiplied.A),premultiplied.A);
    }
    public void Dispose()=>pixels.Dispose();
}
