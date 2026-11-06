namespace SimdSkia.Cpu;
public sealed partial class RasterCanvas
{
    private static readonly bool alphaReference=AppContext.TryGetSwitch("SimdSkia.Cpu.Alpha8Reference",out bool reference)&&reference;
    private PixelBuffer? alphaWork;
    private RasterCanvas? alphaRaster;
    private void ExecuteAlphaTarget(in DrawingCommand command)
    {
        if(command.Kind==CommandKind.Clear&&command.Paint.ImageFilter is null&&command.Paint.MaskFilter is null&&command.Paint.ColorFilter is null&&command.CoverageClip is null)
        {ClearKernel.Fill(target,command.Paint.Color,CpuOptions.EnableSimd);return;}
        if(!alphaReference&&command.Paint.Shader is null&&command.Paint.ImageFilter is null&&command.Paint.MaskFilter is null&&command.Kind is CommandKind.FillRectangle or CommandKind.FillPath)
        {FillAlphaMask(command);return;}
        if((long)target.Width*target.Height>16_777_216)throw new NotSupportedException("Alpha8 raster effects exceed the 16 megapixel working-surface budget.");
        alphaWork??=PixelBuffer.Rent(target.Width,target.Height);alphaRaster??=new(alphaWork);
        // Acquired spans can change between commands. Expand the live A8 plane;
        // the color channels of an alpha-only destination are defined as zero.
        for(int y=0;y<target.Height;y++){var from=target.GetRowSpan(y);var to=alphaWork.GetRowSpan(y);for(int x=0;x<target.Width;x++){int at=x*4;to[at]=to[at+1]=to[at+2]=0;to[at+3]=from[x];}}
        alphaRaster.Execute(command with{TextPixelGeometry=0});
        for(int y=0;y<target.Height;y++){var from=alphaWork.GetRowSpan(y);var to=target.GetRowSpan(y);for(int x=0;x<target.Width;x++)to[x]=from[x*4+3];}
    }
    private void FillAlphaMask(in DrawingCommand command)
    {
        var path=command.Kind==CommandKind.FillRectangle?RectangleGeometry(command.Rectangle):command.Path??throw new ArgumentException("A path is required.");var mask=GetMask(path,command);
        // Reuse the already validated scalar/SIMD RGBA compositor in short
        // stack spans. No destination-sized RGBA shadow plane is allocated for
        // solid A8 shapes, and untouched rows are never copied twice per draw.
        Span<byte> scratch=stackalloc byte[256];
        if(mask.Dense)
        {for(int y=0;y<mask.Height;y++){var data=mask.Data.AsSpan(y*mask.Width,mask.Width);for(int x=0;x<data.Length;){byte coverage=data[x];int end=x+1;while(end<data.Length&&data[end]==coverage)end++;if(coverage!=0)FillAlphaSpan(mask.Top+y,mask.Left+x,end-x,coverage,command.Paint,command.CoverageClip,scratch);x=end;}}return;}
        foreach(var run in mask.Runs)
            FillAlphaSpan(run.Y,run.X,run.Length,run.Coverage,command.Paint,command.CoverageClip,scratch);
    }
    private void FillAlphaSpan(int y,int start,int count,byte coverage,Paint paint,CoverageClip? clip,Span<byte> scratch)
    {
            var row=target.GetRowSpan(y);
            if(clip is null&&AlphaCompositor.TryApply(row.Slice(start,count),paint,coverage))return;
            for(int x=start;x<start+count;)
            {
                int length=Math.Min(64,start+count-x);byte current=coverage;
                if(clip is not null){length=ClipRun(clip,x,y,length,out byte alpha);current=PixelBuffer.Multiply(coverage,alpha);}
                if(current!=0&&!AlphaCompositor.TryApply(row.Slice(x,length),paint,current)){var bytes=scratch[..(length*4)];bytes.Clear();for(int i=0;i<length;i++)bytes[i*4+3]=row[x+i];SpanCompositor.Apply(bytes,PixelFormat.Rgba8888,AlphaFormat.Premultiplied,paint,current);for(int i=0;i<length;i++)row[x+i]=bytes[i*4+3];}x+=length;
            }
    }
    private void DrawAlphaImage(in DrawingCommand command)
    {
        var image=command.Image!;image.ThrowIfDisposed();var rect=command.Rectangle;var source=command.ImageSource??new(0,0,image.Width,image.Height);
        if(!source.IsFinite||source.IsEmpty||source.Left<0||source.Top<0||source.Right>image.Width||source.Bottom>image.Height)throw new ArgumentOutOfRangeException(nameof(command.ImageSource));
        if(!rect.IsFinite)throw new ArgumentException("Image destination must be finite.");if(rect.IsEmpty||!command.Transform.TryInvert(out var inverse))return;
        var mask=GetMask(RectangleGeometry(rect),command);float sx=(source.Right-source.Left)/(rect.Right-rect.Left),sy=(source.Bottom-source.Top)/(rect.Bottom-rect.Top);
        foreach(var run in mask.Runs)
        {
            var row=target.GetRowSpan(run.Y);
            for(int x=run.X;x<run.X+run.Length;x++)
            {
                byte coverage=ClipCoverage(run.Coverage,x,run.Y,command.CoverageClip);if(coverage==0)continue;var p=inverse.Map(x+.5f,run.Y+.5f);
                byte a=Sample(image,source.Left+(p.X-rect.Left)*sx-.5f,source.Top+(p.Y-rect.Top)*sy-.5f,command.Sampling,source,command.NearestLowerBoundary).A;
                var paint=command.Paint with {Color=command.Paint.Color with {A=PixelBuffer.Multiply(a,command.Paint.Color.A)},FilteredConstant=null,FloatingColor=command.Paint.FloatingColor is {} f?f with {W=f.W*a/255f}:null};
                // Skia's A8 bitmap paint shader uses the bitmap's source space.
                // Applying it in destination space stretches the mask but not
                // its material, changing gradients under scaled image draws.
                if(paint.Shader is not null)CompositeShader(row.Slice(x*4,4),paint,source.Left+(p.X-rect.Left)*sx,source.Top+(p.Y-rect.Top)*sy,coverage,x,run.Y);else Composite(row.Slice(x*4,4),paint,coverage);
            }
        }
    }
    private void DisposeAlphaWork(){alphaRaster?.Dispose();alphaRaster=null;alphaWork?.Dispose();alphaWork=null;}
}
