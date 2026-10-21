using SimdSkia;
using SimdSkia.Cpu;

namespace SimdSkia.OptimizationReference;

/// <summary>Scalar reference rasterizer. Samples pixel centers; no antialiasing yet.</summary>
public sealed class BaselineRasterCanvas : ICanvasBackend
{
    private readonly PixelBuffer target;
    private bool disposed;
    public BaselineRasterCanvas(PixelBuffer target) { ArgumentNullException.ThrowIfNull(target); target.ThrowIfDisposed(); this.target=target; }

    public void Execute(in DrawingCommand command)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        target.ThrowIfDisposed();
        switch (command.Kind)
        {
            case CommandKind.Clear: Clear(command.Paint.Color); break;
            case CommandKind.FillRectangle: FillRectangle(command); break;
            case CommandKind.FillPath: FillPath(command);break;
            case CommandKind.DrawImage: DrawImage(command);break;
            default: throw new NotSupportedException($"Drawing command {command.Kind} is unsupported.");
        }
    }

    private void Clear(Rgba32 color)
    {
        ClearKernel.Fill(target,color,CpuOptions.EnableSimd);
    }

    private void FillRectangle(in DrawingCommand command)
    {
        if(command.Paint.Antialias)
        {FillPath(command with {Path=PathGeometry.Rectangle(command.Rectangle)});return;}
        var rect = command.Rectangle;
        var matrix = command.Transform;
        if (!rect.IsFinite) throw new ArgumentException("Rectangle coordinates must be finite.");
        rect=new(MathF.Min(rect.Left,rect.Right),MathF.Min(rect.Top,rect.Bottom),MathF.Max(rect.Left,rect.Right),MathF.Max(rect.Top,rect.Bottom));
        if (!matrix.IsFinite) throw new ArgumentException("Matrix components must be finite.");
        if (!matrix.IsAffine) throw new NotSupportedException("Perspective rasterization is not implemented.");
        if (!Enum.IsDefined(command.Paint.Composite)) throw new NotSupportedException("Unknown compositing mode.");
        if (rect.IsEmpty || !matrix.TryInvert(out var inverse)) return; // Degenerate geometry covers no pixels.

        var p0=matrix.Map(rect.Left,rect.Top); var p1=matrix.Map(rect.Right,rect.Top);
        var p2=matrix.Map(rect.Right,rect.Bottom); var p3=matrix.Map(rect.Left,rect.Bottom);
        float left=MathF.Min(MathF.Min(p0.X,p1.X),MathF.Min(p2.X,p3.X));
        float top=MathF.Min(MathF.Min(p0.Y,p1.Y),MathF.Min(p2.Y,p3.Y));
        float right=MathF.Max(MathF.Max(p0.X,p1.X),MathF.Max(p2.X,p3.X));
        float bottom=MathF.Max(MathF.Max(p0.Y,p1.Y),MathF.Max(p2.Y,p3.Y));
        if (!float.IsFinite(left) || !float.IsFinite(top) || !float.IsFinite(right) || !float.IsFinite(bottom))
            throw new ArgumentException("Transformed rectangle exceeds finite coordinate range.");
        if (matrix.M01==0 && matrix.M10==0)
        {
            // Skia's non-AA axis-aligned fill rounds edges with floor(edge + 0.5).
            // A right edge at n + 0.5 includes pixel n; a left edge excludes it.
            int startX=(int)Math.Clamp(MathF.Floor(left+0.5f),0,target.Width);
            int startY=(int)Math.Clamp(MathF.Floor(top+0.5f),0,target.Height);
            int endX=(int)Math.Clamp(MathF.Floor(right+0.5f),0,target.Width);
            int endY=(int)Math.Clamp(MathF.Floor(bottom+0.5f),0,target.Height);
            for (int y=startY; y<endY; y++)
            {
                var row=target.GetRowSpan(y);
                for (int x=startX; x<endX; x++)
                {
                    if(command.Clip is {} clipped&&!clipped.Contains(x+.5f,y+.5f))continue;
                    var paint=command.Paint;
                    if(paint.Shader is {} shader){var p=inverse.Map(x+.5f,y+.5f);var color=shader.Sample(p.X,p.Y);paint=paint with {Color=color with {A=PixelBuffer.Multiply(color.A,paint.Color.A)}};}
                    Composite(row.Slice(x*4,4),paint);
                }
            }
            return;
        }
        int x0=(int)Math.Clamp(MathF.Floor(left),0,target.Width), y0=(int)Math.Clamp(MathF.Floor(top),0,target.Height);
        int x1=(int)Math.Clamp(MathF.Ceiling(right),0,target.Width), y1=(int)Math.Clamp(MathF.Ceiling(bottom),0,target.Height);
        for (int y=y0; y<y1; y++)
        {
            var row=target.GetRowSpan(y);
            for (int x=x0; x<x1; x++)
            {
                var local=inverse.Map(x+0.5f,y+0.5f);
                if (rect.Contains(local.X,local.Y)&&(command.Clip is not {} clipped||clipped.Contains(x+.5f,y+.5f)))
                {var paint=command.Paint;if(paint.Shader is {} shader){var color=shader.Sample(local.X,local.Y);paint=paint with {Color=color with {A=PixelBuffer.Multiply(color.A,paint.Color.A)}};}Composite(row.Slice(x*4,4),paint);}
            }
        }
    }

    private void FillPath(in DrawingCommand command)
    {
        var geometry=command.Path??throw new ArgumentException("Path command requires geometry.");
        var mask=BaselinePathRasterizer.Coverage(geometry.Flatten(command.Transform),geometry.FillRule,target.Width,target.Height,command.Paint.Antialias,command.Clip);
        (command.ShaderTransform??command.Transform).TryInvert(out var inverse);
        for(int y=0;y<target.Height;y++)
        {
            var row=target.GetRowSpan(y);
            for(int x=0;x<target.Width;x++)
            {
                byte coverage=mask[y*target.Width+x];if(coverage==0)continue;
                var paint=command.Paint;
                if(paint.Shader is {} shader){var p=inverse.Map(x+0.5f,y+0.5f);var color=shader.Sample(p.X,p.Y);paint=paint with {Color=color with {A=PixelBuffer.Multiply(color.A,paint.Color.A)}};}
                Composite(row.Slice(x*4,4),paint,coverage);
            }
        }
    }
    private void DrawImage(in DrawingCommand command)
    {
        var image=command.Image??throw new ArgumentException("Image command requires pixels.");image.ThrowIfDisposed();
        if(!command.Transform.TryInvert(out var inverse))return;
        var rect=command.Rectangle;
        if(rect.IsEmpty)return;
        var mask=BaselinePathRasterizer.Coverage(PathGeometry.Rectangle(rect).Flatten(command.Transform),FillRule.Winding,target.Width,target.Height,command.Paint.Antialias,command.Clip);
        for(int y=0;y<target.Height;y++)for(int x=0;x<target.Width;x++)
        {
            byte c=mask[y*target.Width+x];if(c==0)continue;
            var p=inverse.Map(x+0.5f,y+0.5f);
            int ix=(int)Math.Clamp((p.X-rect.Left)/(rect.Right-rect.Left)*image.Width,0,image.Width-1);
            int iy=(int)Math.Clamp((p.Y-rect.Top)/(rect.Bottom-rect.Top)*image.Height,0,image.Height-1);
            var color=image.ReadPixel(ix,iy);color=color with {A=PixelBuffer.Multiply(image.Alpha==AlphaFormat.Opaque?(byte)255:color.A,command.Paint.Color.A)};
            Composite(target.GetRowSpan(y).Slice(x*4,4),command.Paint with {Color=color},c);
        }
    }
    private void Composite(Span<byte> pixel, Paint paint,byte coverage=255)
    {
        if(coverage!=255)
        {
            int pri=target.Format==PixelFormat.Rgba8888?0:2,pbi=2-pri;byte pda=pixel[3];
            byte pr=pixel[pri],pg=pixel[1],pb=pixel[pbi];
            if(target.Alpha==AlphaFormat.Straight){pr=PixelBuffer.Multiply(pr,pda);pg=PixelBuffer.Multiply(pg,pda);pb=PixelBuffer.Multiply(pb,pda);}
            byte sa=PixelBuffer.Multiply(paint.Color.A,coverage);
            byte remainder=paint.Composite==CompositeMode.SourceOver?(byte)(255-sa):(byte)(255-coverage);
            byte poa=(byte)(PixelBuffer.Multiply(pda,remainder)+(paint.Composite==CompositeMode.Clear?0:sa));
            byte Channel(byte d,byte s)=>(byte)Math.Min(255,PixelBuffer.Multiply(d,remainder)+(paint.Composite==CompositeMode.Clear?0:PixelBuffer.Multiply(s,sa)));
            pr=Channel(pr,paint.Color.R);pg=Channel(pg,paint.Color.G);pb=Channel(pb,paint.Color.B);
            if(target.Alpha==AlphaFormat.Straight){pr=PixelBuffer.Unmultiply(pr,poa);pg=PixelBuffer.Unmultiply(pg,poa);pb=PixelBuffer.Unmultiply(pb,poa);}
            pixel[pri]=pr;pixel[1]=pg;pixel[pbi]=pb;pixel[3]=poa;return;
        }
        if (paint.Composite == CompositeMode.Clear) { pixel.Clear(); return; }
        int ri=target.Format == PixelFormat.Rgba8888 ? 0 : 2, bi=2-ri;
        var src=paint.Color;
        if (paint.Composite == CompositeMode.Source)
        {
            byte a=src.A;
            pixel[ri]=target.Alpha != AlphaFormat.Straight ? PixelBuffer.Multiply(src.R,a) : src.R;
            pixel[1]=target.Alpha != AlphaFormat.Straight ? PixelBuffer.Multiply(src.G,a) : src.G;
            pixel[bi]=target.Alpha != AlphaFormat.Straight ? PixelBuffer.Multiply(src.B,a) : src.B;
            pixel[3]=a;
            return;
        }

        if (src.A == 0) return;
        byte da=pixel[3];
        byte dr=pixel[ri], dg=pixel[1], db=pixel[bi];
        if (target.Alpha == AlphaFormat.Straight) { dr=PixelBuffer.Multiply(dr,da); dg=PixelBuffer.Multiply(dg,da); db=PixelBuffer.Multiply(db,da); }
        byte inverse=(byte)(255-src.A);
        byte oa=(byte)Math.Min(255,src.A+PixelBuffer.Multiply(da,inverse));
        byte r=(byte)Math.Min(255,PixelBuffer.Multiply(src.R,src.A)+PixelBuffer.Multiply(dr,inverse));
        byte g=(byte)Math.Min(255,PixelBuffer.Multiply(src.G,src.A)+PixelBuffer.Multiply(dg,inverse));
        byte b=(byte)Math.Min(255,PixelBuffer.Multiply(src.B,src.A)+PixelBuffer.Multiply(db,inverse));
        if (target.Alpha == AlphaFormat.Straight) { r=PixelBuffer.Unmultiply(r,oa); g=PixelBuffer.Unmultiply(g,oa); b=PixelBuffer.Unmultiply(b,oa); }
        pixel[ri]=r; pixel[1]=g; pixel[bi]=b; pixel[3]=oa;
    }

    public void Dispose() => disposed=true; // The bitmap owns pixels; disposing a canvas never frees them.
    public void Flush(){ObjectDisposedException.ThrowIf(disposed,this);target.ThrowIfDisposed();}
}

