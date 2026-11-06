namespace SimdSkia.Cpu;
public sealed partial class RasterCanvas
{
    private void DrawFiltered(in DrawingCommand command)
    {
        var filter=command.Paint.ImageFilter!;RectF bounds;
        if(command.Kind==CommandKind.FillPath&&command.Path?.FillRule is not (FillRule.InverseWinding or FillRule.InverseEvenOdd))bounds=command.Path?.GetTightBounds(command.Transform)??throw new ArgumentException("Path required.");
        else
        {
            var b=command.Kind switch{CommandKind.FillRectangle or CommandKind.DrawImage=>command.Rectangle,CommandKind.DrawGlyphRun=>command.GlyphRun?.Bounds??throw new ArgumentException("Glyph run required."),_=>new RectF(0,0,target.Width,target.Height)};
            var a=command.Transform.Map(b.Left,b.Top);var c=command.Transform.Map(b.Right,b.Top);var d=command.Transform.Map(b.Left,b.Bottom);var e=command.Transform.Map(b.Right,b.Bottom);
            bounds=new(MathF.Min(MathF.Min(a.X,c.X),MathF.Min(d.X,e.X)),MathF.Min(MathF.Min(a.Y,c.Y),MathF.Min(d.Y,e.Y)),MathF.Max(MathF.Max(a.X,c.X),MathF.Max(d.X,e.X)),MathF.Max(MathF.Max(a.Y,c.Y),MathF.Max(d.Y,e.Y)));
        }
        using var allocated=FilterFrame.Rent(bounds);
        using var source=new FilterFrame(allocated.Pixels?.Share(),allocated.Left,allocated.Top){TileBounds=new(0,0,target.Width,target.Height)};
        bool alphaAfter=command.ImageFilterAlphaAfter;
        if(source.Pixels is {} pixels)
        {
            var shift=new Matrix3(1,0,-source.Left,0,1,-source.Top,0,0,1);using var canvas=new RasterCanvas(pixels);
            canvas.Execute(command with{Transform=shift*command.Transform,ShaderTransform=shift*(command.ShaderTransform??command.Transform),Clip=null,CoverageClip=null,Paint=command.Paint with{ImageFilter=null,Blender=null,Composite=CompositeMode.SourceOver,Color=alphaAfter?command.Paint.Color with{A=255}:command.Paint.Color}});
        }
        using var result=filter.Apply(source,command.ImageFilterTransform??command.Transform,command.Clip??new(0,0,target.Width,target.Height));
        if(result.Pixels is not {} image)return;
        byte opacity=alphaAfter?command.Paint.Color.A:(byte)255;
        if(TryCompositeFiltered(result,command,opacity))return;
        Execute(new(CommandKind.DrawImage,result.Bounds,Matrix3.Identity,new(new(255,255,255,opacity),command.Paint.Composite){Blender=command.Paint.Blender}){Image=image,Clip=command.Clip,CoverageClip=command.CoverageClip});
    }
    private bool TryCompositeFiltered(FilterFrame frame,in DrawingCommand command,byte opacity)
    {
        var mode=command.Paint.Composite;
        if(command.Paint.Blender is not null||target.Alpha==AlphaFormat.Straight||command.CoverageClip is not null||mode is not (CompositeMode.SourceOver or CompositeMode.Source or CompositeMode.Clear)||mode==CompositeMode.Source&&opacity!=255)return false;
        var image=frame.Pixels!;int l=Math.Max(0,frame.Left),t=Math.Max(0,frame.Top),r=(int)Math.Min(target.Width,(long)frame.Left+image.Width),b=(int)Math.Min(target.Height,(long)frame.Top+image.Height);
        if(command.Clip is {} c){l=Math.Max(l,SampleEdge(c.Left,target.Width));t=Math.Max(t,SampleEdge(c.Top,target.Height));r=Math.Min(r,SampleEdge(c.Right,target.Width));b=Math.Min(b,SampleEdge(c.Bottom,target.Height));}
        if(l>=r||t>=b)return true;int length=(r-l)*4;byte[]? rented=null;Span<byte> converted=default;
        if(image.Format!=target.Format){rented=System.Buffers.ArrayPool<byte>.Shared.Rent(length);converted=rented.AsSpan(0,length);}
        try
        {
            for(int y=t;y<b;y++)
            {
                var src=image.GetRowSpan(y-frame.Top).Slice((l-frame.Left)*4,length);var dst=target.GetRowSpan(y).Slice(l*4,length);
                if(mode==CompositeMode.Clear){dst.Clear();continue;}
                ReadOnlySpan<byte> source=src;
                if(rented is not null){PixelTransferKernel.SwapRedBlue(src,converted,CpuOptions.EnableSimd);source=converted;}
                if(mode==CompositeMode.Source)source.CopyTo(dst);else ImageCompositor.SourceOver(source,dst,opacity,false,command.ImageFilterAlphaAfter);
            }
            return true;
        }
        finally{if(rented is not null)System.Buffers.ArrayPool<byte>.Shared.Return(rented);}
    }
}
