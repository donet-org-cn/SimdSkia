using SimdSkia;

namespace SimdSkia.Cpu;

/// <summary>Managed rasterizer with bounded immutable-geometry coverage caches.</summary>
public sealed partial class RasterCanvas : ICanvasBackend
{
    private readonly PixelBuffer target;
    private bool disposed;
    private readonly record struct MaskKey(PathGeometry Geometry,Matrix3 Transform,bool Antialias,RectF? Clip,bool CompactRuns,float Tolerance);
    private readonly Dictionary<MaskKey,LinkedListNode<(MaskKey Key,CoverageMask Mask)>> masks=[];
    private readonly LinkedList<(MaskKey Key,CoverageMask Mask)> maskOrder=[];
    private long maskBytes;
    private readonly Dictionary<RectF,PathGeometry> imageRectangles=[];
    private int[] imageX=[];
    private long gradientBytes;
    private readonly Dictionary<(IColorShader Gradient,Matrix3 Inverse,int Left,int Right),byte[]> gradientRows=[];
    public RasterCanvas(PixelBuffer target) { ArgumentNullException.ThrowIfNull(target);target.ThrowIfDisposed();this.target=target; }
    public void Execute(in DrawingCommand command)
    {
        ObjectDisposedException.ThrowIf(disposed,this);target.ThrowIfDisposed();
        if(!Enum.IsDefined(command.Paint.Composite))throw new NotSupportedException("Unknown compositing mode.");
        if(command.Kind!=CommandKind.Clear&&(!command.Transform.IsFinite||!command.Transform.IsAffine))throw new NotSupportedException("Drawing requires a finite affine transform.");
        if(command.Clip is {} clip&&!clip.IsFinite)throw new ArgumentException("Clip coordinates must be finite.");
        if(!float.IsFinite(command.CurveTolerance)||command.CurveTolerance<=0)throw new ArgumentOutOfRangeException(nameof(command.CurveTolerance));
        if(!Enum.IsDefined(command.Sampling))throw new ArgumentOutOfRangeException(nameof(command.Sampling));
        if(command.CoverageClip is not null&&command.Kind==CommandKind.Clear)throw new NotSupportedException("Clear with a coverage clip is undefined; use a Source rectangle.");
        switch(command.Kind)
        {
            case CommandKind.Clear:ClearKernel.Fill(target,command.Paint.Color,CpuOptions.EnableSimd);break;
            case CommandKind.FillRectangle:FillRectangle(command);break;
            case CommandKind.FillPath:FillPath(command);break;
            case CommandKind.DrawImage:DrawImage(command);break;
            default:throw new NotSupportedException($"Drawing command {command.Kind} is unsupported.");
        }
    }
    private void FillRectangle(in DrawingCommand command)
    {
        if(command.CoverageClip is not null){FillPath(command with {Path=PathGeometry.Rectangle(command.Rectangle)});return;}
        var rect=command.Rectangle;var matrix=command.Transform;
        if(!rect.IsFinite)throw new ArgumentException("Rectangle coordinates must be finite.");
        rect=new(MathF.Min(rect.Left,rect.Right),MathF.Min(rect.Top,rect.Bottom),MathF.Max(rect.Left,rect.Right),MathF.Max(rect.Top,rect.Bottom));
        if(!matrix.IsFinite)throw new ArgumentException("Matrix components must be finite.");
        if(!matrix.IsAffine)throw new NotSupportedException("Perspective rasterization is not implemented.");
        if(!Enum.IsDefined(command.Paint.Composite))throw new NotSupportedException("Unknown compositing mode.");
        if(rect.IsEmpty||!matrix.TryInvert(out var inverse))return;
        var p0=matrix.Map(rect.Left,rect.Top);var p1=matrix.Map(rect.Right,rect.Top);
        var p2=matrix.Map(rect.Right,rect.Bottom);var p3=matrix.Map(rect.Left,rect.Bottom);
        float left=MathF.Min(MathF.Min(p0.X,p1.X),MathF.Min(p2.X,p3.X)),top=MathF.Min(MathF.Min(p0.Y,p1.Y),MathF.Min(p2.Y,p3.Y));
        float right=MathF.Max(MathF.Max(p0.X,p1.X),MathF.Max(p2.X,p3.X)),bottom=MathF.Max(MathF.Max(p0.Y,p1.Y),MathF.Max(p2.Y,p3.Y));
        if(!float.IsFinite(left)||!float.IsFinite(top)||!float.IsFinite(right)||!float.IsFinite(bottom))throw new ArgumentException("Transformed rectangle exceeds finite coordinate range.");
        if(matrix.M01==0&&matrix.M10==0)
        {
            if(command.Paint.Antialias&&command.Paint.Shader is null&&target.Height<=262144)
            {FillAntialiasedRectangle(left,top,right,bottom,command);return;}
            if(!command.Paint.Antialias)
            {
                int x0=RoundEdge(left,target.Width),y0=RoundEdge(top,target.Height),x1=RoundEdge(right,target.Width),y1=RoundEdge(bottom,target.Height);
                if(command.Clip is {} clip)
                {x0=Math.Max(x0,SampleEdge(clip.Left,target.Width));y0=Math.Max(y0,SampleEdge(clip.Top,target.Height));x1=Math.Min(x1,SampleEdge(clip.Right,target.Width));y1=Math.Min(y1,SampleEdge(clip.Bottom,target.Height));}
                if(x0>=x1||y0>=y1)return;
                var paint=command.Paint;
                if(paint.Shader is null)
                {for(int y=y0;y<y1;y++)SpanCompositor.Apply(target.GetRowSpan(y).Slice(x0*4,(x1-x0)*4),target.Format,target.Alpha,paint);return;}
                if(paint.Shader is {} gradient&&gradient.IsHorizontal&&inverse.M01==0&&paint.Color.A==255&&gradient.IsOpaque&&(paint.Composite is CompositeMode.Source or CompositeMode.SourceOver)&&Finite(inverse.Map(x0+.5f,y0+.5f))&&Finite(inverse.Map(x1-.5f,y1-.5f)))
                {
                    var key=(gradient,inverse,x0,x1);
                    if(!gradientRows.TryGetValue(key,out var colors))
                    {
                        colors=new byte[checked((x1-x0)*4)];
                        for(int x=x0;x<x1;x++)
                        {var local=inverse.Map(x+.5f,y0+.5f);SpanCompositor.Apply(colors.AsSpan((x-x0)*4,4),target.Format,target.Alpha,new(paint.Shader.Sample(local.X,local.Y),CompositeMode.Source));}
                        if(colors.Length<=4*1024*1024){if(gradientRows.Count>=16||gradientBytes+colors.Length>4*1024*1024){gradientRows.Clear();gradientBytes=0;}gradientRows.Add(key,colors);gradientBytes+=colors.Length;}
                    }
                    for(int y=y0;y<y1;y++)colors.AsSpan().CopyTo(target.GetRowSpan(y).Slice(x0*4));
                    return;
                }
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
                {var local=inverse.Map(x+.5f,y+.5f);var color=paint.Shader.Sample(local.X,local.Y);Composite(target.GetRowSpan(y).Slice(x*4,4),paint with {Color=color with {A=PixelBuffer.Multiply(color.A,paint.Color.A)}});}
                return;
            }
        }
        if(command.Paint.Antialias){FillPath(command with {Path=PathGeometry.Rectangle(command.Rectangle)});return;}
        int startX=(int)Math.Clamp(MathF.Floor(left),0,target.Width),startY=(int)Math.Clamp(MathF.Floor(top),0,target.Height);
        int endX=(int)Math.Clamp(MathF.Ceiling(right),0,target.Width),endY=(int)Math.Clamp(MathF.Ceiling(bottom),0,target.Height);
        for(int y=startY;y<endY;y++)
        {
            var row=target.GetRowSpan(y);
            for(int x=startX;x<endX;x++)
            {
                var local=inverse.Map(x+.5f,y+.5f);
                if(!rect.Contains(local.X,local.Y)||(command.Clip is {} clip&&!clip.Contains(x+.5f,y+.5f)))continue;
                var paint=command.Paint;
                if(paint.Shader is {} shader){var color=shader.Sample(local.X,local.Y);paint=paint with {Color=color with {A=PixelBuffer.Multiply(color.A,paint.Color.A)}};}
                Composite(row.Slice(x*4,4),paint);
            }
        }
    }
    private static bool Finite((float X,float Y) point)=>float.IsFinite(point.X)&&float.IsFinite(point.Y);
    private static int RoundEdge(float value,int limit)=>(int)Math.Clamp(MathF.Floor(value+.5f),0,limit);
    private static int SampleEdge(float value,int limit)=>(int)Math.Clamp(MathF.Ceiling(value-.5f),0,limit);
    private static int SampleGrid(float value,int limit)=>(int)Math.Clamp(MathF.Ceiling(value*16-.5f),0,checked(limit*16));
    private void FillAntialiasedRectangle(float left,float top,float right,float bottom,in DrawingCommand command)
    {
        if(command.Clip is {} clip){left=MathF.Max(left,clip.Left);right=MathF.Min(right,clip.Right);top=MathF.Max(top,clip.Top);bottom=MathF.Min(bottom,clip.Bottom);}
        if(left>=right||top>=bottom)return;
        int first=SampleGrid(left,target.Width),end=SampleGrid(right,target.Width),firstY=SampleGrid(top,target.Height),endY=SampleGrid(bottom,target.Height);
        int fullStart=(first+15)/16,fullEnd=end/16;
        for(int y=firstY/16;y<(endY+15)/16;y++)
        {
            int cy=Math.Max(0,Math.Min(endY,(y+1)*16)-Math.Max(firstY,y*16));var row=target.GetRowSpan(y);
            for(int x=first/16;x<(end+15)/16;)
            {
                int next=x<fullStart?Math.Min(fullStart,(end+15)/16):x<fullEnd?fullEnd:x+1;
                int cx=Math.Max(0,Math.Min(end,(x+1)*16)-Math.Max(first,x*16));
                byte coverage=(byte)((cx*cy*255+128)/256);
                SpanCompositor.Apply(row.Slice(x*4,(next-x)*4),target.Format,target.Alpha,command.Paint,coverage);x=next;
            }
        }
    }
    private CoverageMask GetMask(PathGeometry geometry,in DrawingCommand command)
    {
        bool compact=command.Kind==CommandKind.FillPath;
        var key=new MaskKey(geometry,command.Transform,command.Paint.Antialias,command.Clip,compact,command.CurveTolerance);
        if(masks.TryGetValue(key,out var node)){maskOrder.Remove(node);maskOrder.AddLast(node);return node.Value.Mask;}
        var mask=PathRasterizer.Rasterize(geometry.Flatten(command.Transform,command.CurveTolerance),geometry.FillRule,target.Width,target.Height,command.Paint.Antialias,command.Clip,compact);
        long bytes=MaskBytes(mask,geometry);
        if(bytes<=16*1024*1024)
        {
            while(masks.Count>=64||maskBytes+bytes>16*1024*1024)
            {var old=maskOrder.First!;maskBytes-=MaskBytes(old.Value.Mask,old.Value.Key.Geometry);masks.Remove(old.Value.Key);maskOrder.RemoveFirst();}
            masks.Add(key,maskOrder.AddLast((key,mask)));maskBytes+=bytes;
        }
        return mask;
    }
    private static long MaskBytes(CoverageMask mask,PathGeometry geometry)=>mask.Data.LongLength+(long)mask.Runs.Length*16+(long)geometry.Segments.Length*32;
    private void FillPath(in DrawingCommand command)
    {
        var geometry=command.Path??throw new ArgumentException("Path command requires geometry.");
        int dx=0,dy=0;CoverageMask mask=null!;
        bool text=command.CacheTextCoverage&&TextCoverageCache.TryGet(geometry,command,target.Width,target.Height,out mask!,out dx,out dy);
        if(!text)mask=GetMask(geometry,command);
        int left=0,top=0,right=target.Width,bottom=target.Height;
        if(text&&command.Clip is {} clip){left=SampleEdge(clip.Left,target.Width);top=SampleEdge(clip.Top,target.Height);right=SampleEdge(clip.Right,target.Width);bottom=SampleEdge(clip.Bottom,target.Height);}
        (command.ShaderTransform??command.Transform).TryInvert(out var inverse);
        if(!mask.Dense&&TryFillHorizontalGradient(mask,command,inverse,dx,dy,left,top,right,bottom))return;
        if(mask.Dense)
        {
            for(int y=0;y<mask.Height;y++)
            {
                int deviceY=y+mask.Top+dy;if(deviceY<top||deviceY>=bottom)continue;
                int start=Math.Max(left,mask.Left+dx),end=Math.Min(right,mask.Left+dx+mask.Width);if(start>=end)continue;
                var row=target.GetRowSpan(deviceY).Slice(start*4,(end-start)*4);
                var coverage=mask.Data.AsSpan(y*mask.Width+start-mask.Left-dx,end-start);
                if(command.Paint.Shader is null)ApplyMasked(row,coverage,start,deviceY,command.Paint,command.CoverageClip);
                else for(int x=0;x<coverage.Length;x++)
                {
                    if(coverage[x]==0)continue;
                    var p=inverse.Map(start+x+.5f,deviceY+.5f);var color=command.Paint.Shader.Sample(p.X,p.Y);
                    Composite(row.Slice(x*4,4),command.Paint with {Color=color with {A=PixelBuffer.Multiply(color.A,command.Paint.Color.A)}},ClipCoverage(coverage[x],start+x,deviceY,command.CoverageClip));
                }
            }
            return;
        }
        foreach(var run in mask.Runs)
        {
            int y=run.Y+dy;if(y<top||y>=bottom)continue;
            int start=Math.Max(left,run.X+dx),end=Math.Min(right,run.X+dx+run.Length);if(start>=end)continue;
            var row=target.GetRowSpan(y);
            if(command.Paint.Shader is null)
                ApplyMasked(row.Slice(start*4,(end-start)*4),start,y,command.Paint,run.Coverage,command.CoverageClip);
            else for(int x=start;x<end;x++)
            {
                var p=inverse.Map(x+.5f,y+.5f);var color=command.Paint.Shader.Sample(p.X,p.Y);
                Composite(row.Slice(x*4,4),command.Paint with {Color=color with {A=PixelBuffer.Multiply(color.A,command.Paint.Color.A)}},ClipCoverage(run.Coverage,x,y,command.CoverageClip));
            }
        }
    }
    private void DrawImage(in DrawingCommand command)
    {
        if(command.ImageSource is not null||command.Sampling!=ImageSampling.Nearest||command.CoverageClip is not null){DrawSampledImage(command);return;}
        var image=command.Image??throw new ArgumentException("Image command requires pixels.");image.ThrowIfDisposed();
        if(!command.Transform.TryInvert(out var inverse))return;var rect=command.Rectangle;if(rect.IsEmpty)return;
        if(command.Transform.IsAffine&&command.Transform.M01==0&&command.Transform.M10==0&&!command.Paint.Antialias&&!image.GetPixelSpan().Overlaps(target.GetPixelSpan()))
        {DrawAxisImage(command,inverse,image);return;}
        if(!imageRectangles.TryGetValue(rect,out var geometry))
        {geometry=PathGeometry.Rectangle(rect);if(imageRectangles.Count>=16)imageRectangles.Clear();imageRectangles.Add(rect,geometry);}
        var mask=GetMask(geometry,command);
        // Source buffers are mutable. Cache geometry only; always read current pixels.
        // Aliased source/destination retain the original immediate pixel traversal.
        if(command.Transform.M01==0&&command.Transform.M10==0&&!image.GetPixelSpan().Overlaps(target.GetPixelSpan()))
        {
            if(imageX.Length<target.Width)imageX=new int[target.Width];
            for(int x=mask.Left;x<mask.Left+mask.Width;x++)
            {var p=inverse.Map(x+.5f,.5f);imageX[x]=(int)Math.Clamp((p.X-rect.Left)/(rect.Right-rect.Left)*image.Width,0,image.Width-1);}
            bool consecutive=true;for(int x=mask.Left+1;x<mask.Left+mask.Width;x++)if(imageX[x]!=imageX[x-1]+1){consecutive=false;break;}
            bool opaqueCopy=consecutive&&image.Alpha==AlphaFormat.Opaque&&command.Paint.Color.A==255&&image.Format==target.Format&&command.Paint.Composite is CompositeMode.Source or CompositeMode.SourceOver;
            int sourceRi=image.Format==PixelFormat.Rgba8888?0:2;
            foreach(var run in mask.Runs)
            {
                var p=inverse.Map(.5f,run.Y+.5f);
                int iy=(int)Math.Clamp((p.Y-rect.Top)/(rect.Bottom-rect.Top)*image.Height,0,image.Height-1);
                var source=image.GetRowSpan(iy);var row=target.GetRowSpan(run.Y);
                if(opaqueCopy&&run.Coverage==255)
                {SpanCompositor.CopyOpaque(source.Slice(imageX[run.X]*4,run.Length*4),row.Slice(run.X*4,run.Length*4));continue;}
                for(int x=run.X;x<run.X+run.Length;)
                {
                    int ix=imageX[x],end=x+1;while(end<run.X+run.Length&&imageX[end]==ix)end++;
                    var pixel=source.Slice(ix*4,4);byte r=pixel[sourceRi],g=pixel[1],b=pixel[2-sourceRi],a=pixel[3];
                    if(image.Alpha==AlphaFormat.Premultiplied){r=PixelBuffer.Unmultiply(r,a);g=PixelBuffer.Unmultiply(g,a);b=PixelBuffer.Unmultiply(b,a);}
                    a=PixelBuffer.Multiply(image.Alpha==AlphaFormat.Opaque?(byte)255:a,command.Paint.Color.A);
                    SpanCompositor.Apply(row.Slice(x*4,(end-x)*4),target.Format,target.Alpha,command.Paint with {Color=new(r,g,b,a)},run.Coverage);x=end;
                }
            }
            return;
        }
        foreach(var run in mask.Runs)
        {
            var row=target.GetRowSpan(run.Y);
            for(int x=run.X;x<run.X+run.Length;x++)
            {
                var p=inverse.Map(x+.5f,run.Y+.5f);
                int ix=(int)Math.Clamp((p.X-rect.Left)/(rect.Right-rect.Left)*image.Width,0,image.Width-1),iy=(int)Math.Clamp((p.Y-rect.Top)/(rect.Bottom-rect.Top)*image.Height,0,image.Height-1);
                var color=image.ReadPixel(ix,iy);color=color with {A=PixelBuffer.Multiply(image.Alpha==AlphaFormat.Opaque?(byte)255:color.A,command.Paint.Color.A)};
                Composite(row.Slice(x*4,4),command.Paint with {Color=color},run.Coverage);
            }
        }
    }
    private void DrawAxisImage(in DrawingCommand command,Matrix3 inverse,PixelBuffer image)
    {
        var rect=command.Rectangle;var a=command.Transform.Map(rect.Left,rect.Top);var b=command.Transform.Map(rect.Right,rect.Bottom);
        if(!float.IsFinite(a.X)||!float.IsFinite(a.Y)||!float.IsFinite(b.X)||!float.IsFinite(b.Y))throw new ArgumentException("Transformed image exceeds finite coordinate range.");
        int x0=SampleEdge(MathF.Min(a.X,b.X),target.Width),x1=SampleEdge(MathF.Max(a.X,b.X),target.Width);
        int y0=SampleEdge(MathF.Min(a.Y,b.Y),target.Height),y1=SampleEdge(MathF.Max(a.Y,b.Y),target.Height);
        if(command.Clip is {} clip){x0=Math.Max(x0,SampleEdge(clip.Left,target.Width));x1=Math.Min(x1,SampleEdge(clip.Right,target.Width));y0=Math.Max(y0,SampleEdge(clip.Top,target.Height));y1=Math.Min(y1,SampleEdge(clip.Bottom,target.Height));}
        if(x0>=x1||y0>=y1)return;
        if(imageX.Length<target.Width)imageX=new int[target.Width];
        bool consecutive=true;
        for(int x=x0;x<x1;x++)
        {var local=inverse.Map(x+.5f,.5f);imageX[x]=(int)Math.Clamp((local.X-rect.Left)/(rect.Right-rect.Left)*image.Width,0,image.Width-1);if(x>x0&&imageX[x]!=imageX[x-1]+1)consecutive=false;}
        bool copy=consecutive&&image.Alpha==AlphaFormat.Opaque&&command.Paint.Color.A==255&&image.Format==target.Format&&command.Paint.Composite is CompositeMode.Source or CompositeMode.SourceOver;
        int ri=image.Format==PixelFormat.Rgba8888?0:2;
        for(int y=y0;y<y1;y++)
        {
            var local=inverse.Map(.5f,y+.5f);int iy=(int)Math.Clamp((local.Y-rect.Top)/(rect.Bottom-rect.Top)*image.Height,0,image.Height-1);
            var source=image.GetRowSpan(iy);var destination=target.GetRowSpan(y);
            if(copy){SpanCompositor.CopyOpaque(source.Slice(imageX[x0]*4,(x1-x0)*4),destination.Slice(x0*4,(x1-x0)*4));continue;}
            for(int x=x0;x<x1;)
            {
                int ix=imageX[x],end=x+1;while(end<x1&&imageX[end]==ix)end++;
                var p=source.Slice(ix*4,4);byte r=p[ri],g=p[1],blue=p[2-ri],alpha=p[3];
                if(image.Alpha==AlphaFormat.Premultiplied){r=PixelBuffer.Unmultiply(r,alpha);g=PixelBuffer.Unmultiply(g,alpha);blue=PixelBuffer.Unmultiply(blue,alpha);}
                alpha=PixelBuffer.Multiply(image.Alpha==AlphaFormat.Opaque?(byte)255:alpha,command.Paint.Color.A);
                SpanCompositor.Apply(destination.Slice(x*4,(end-x)*4),target.Format,target.Alpha,command.Paint with {Color=new(r,g,blue,alpha)});x=end;
            }
        }
    }
    private void Composite(Span<byte> pixel,Paint paint,byte coverage=255)=>SpanCompositor.Apply(pixel,target.Format,target.Alpha,paint,coverage);
    public void Dispose(){disposed=true;masks.Clear();maskOrder.Clear();maskBytes=0;gradientRows.Clear();gradientBytes=0;imageRectangles.Clear();imageX=[];}
    public void Flush(){ObjectDisposedException.ThrowIf(disposed,this);target.ThrowIfDisposed();}
}
