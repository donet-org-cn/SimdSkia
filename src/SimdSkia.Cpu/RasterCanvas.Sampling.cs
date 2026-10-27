namespace SimdSkia.Cpu;

public sealed partial class RasterCanvas
{
    private bool TryFillHorizontalGradient(CoverageMask mask,in DrawingCommand command,Matrix3 inverse,int dx,int dy,int left,int top,int right,int bottom)
    {
        var shader=command.Paint.Shader;
        if(shader is null||!shader.IsHorizontal||!shader.IsOpaque||inverse.M01!=0||command.Paint.Color.A!=255||target.Alpha==AlphaFormat.Straight||command.Paint.Composite is not (CompositeMode.Source or CompositeMode.SourceOver))return false;
        int start=Math.Max(left,mask.Left+dx),end=Math.Min(right,mask.Left+dx+mask.Width);if(start>=end)return true;
        var key=(shader,inverse,start,end);
        gradientRows??=[];
        if(!gradientRows.TryGetValue(key,out var colors))
        {
            colors=new byte[checked((end-start)*4)];
            int ri=target.Format==PixelFormat.Rgba8888?0:2;
            for(int x=start;x<end;x++){var p=inverse.Map(x+.5f,.5f);var color=shader.Sample(p.X,p.Y);int at=(x-start)*4;colors[at+ri]=color.R;colors[at+1]=color.G;colors[at+2-ri]=color.B;colors[at+3]=255;}
            if(colors.Length<=4*1024*1024){if(gradientRows.Count>=16||gradientBytes+colors.Length>4*1024*1024){gradientRows.Clear();gradientBytes=0;}gradientRows.Add(key,colors);gradientBytes+=colors.Length;}
        }
        foreach(var run in mask.Runs)
        {
            int y=run.Y+dy;if(y<top||y>=bottom)continue;int a=Math.Max(start,run.X+dx),b=Math.Min(end,run.X+dx+run.Length);if(a>=b)continue;
            var source=colors.AsSpan((a-start)*4,(b-a)*4);var row=target.GetRowSpan(y).Slice(a*4,(b-a)*4);
            if(command.CoverageClip is null){if(run.Coverage==255)source.CopyTo(row);else ImageCompositor.SourceOver(source,row,run.Coverage,true);continue;}
            for(int i=0;i<b-a;)
            {
                int length=ClipRun(command.CoverageClip,a+i,y,b-a-i,out byte clipAlpha);
                byte coverage=PixelBuffer.Multiply(run.Coverage,clipAlpha);int next=i+length;
                if(coverage==255)source.Slice(i*4,(next-i)*4).CopyTo(row.Slice(i*4,(next-i)*4));
                else if(coverage>0)ImageCompositor.SourceOver(source.Slice(i*4,(next-i)*4),row.Slice(i*4,(next-i)*4),coverage,true);i=next;
            }
        }
        return true;
    }
    public CoverageClip CreateClip(PathGeometry geometry,Matrix3 transform,bool antialias=true,float curveTolerance=0.015625f)
    {
        ObjectDisposedException.ThrowIf(disposed,this);target.ThrowIfDisposed();
        if(!float.IsFinite(curveTolerance)||curveTolerance<=0)throw new ArgumentOutOfRangeException(nameof(curveTolerance));
        var key=new ClipCoverageCache.Key(geometry,transform,target.Width,target.Height,antialias,curveTolerance);
        if(ClipCoverageCache.TryGet(key,out var cached))return cached;
        var command=new DrawingCommand(CommandKind.FillPath,default,transform,new(default,CompositeMode.Source,antialias)){CurveTolerance=curveTolerance};
        var mask=GetMask(geometry,command);var data=new byte[checked(mask.Width*mask.Height)];
        if(mask.Dense)mask.Data.CopyTo(data,0);
        else foreach(var run in mask.Runs)data.AsSpan((run.Y-mask.Top)*mask.Width+run.X-mask.Left,run.Length).Fill(run.Coverage);
        return ClipCoverageCache.Add(key,new(mask.Left,mask.Top,mask.Width,mask.Height,data));
    }
    private static byte ClipCoverage(byte coverage,int x,int y,CoverageClip? clip)=>clip is null?coverage:PixelBuffer.Multiply(coverage,clip.At(x,y));
    private static int ClipRun(CoverageClip clip,int x,int y,int maximum,out byte coverage)
    {
        var row=clip.GetRow(y);coverage=0;
        if(row.IsEmpty||x>=clip.Left+clip.Width)return maximum;
        if(x<clip.Left)return (int)Math.Min(maximum,(long)clip.Left-x);
        int offset=x-clip.Left,count=Math.Min(maximum,row.Length-offset);
        var values=row.Slice(offset,count);coverage=values[0];int different;
        // Large opaque interiors dominate rounded UI clips. Let the BCL scan
        // entire vectors instead of repeating bounds tests and alpha math for
        // each pixel; edges still use their exact original coverage bytes.
        if(CpuOptions.EnableSimd)different=values.IndexOfAnyExcept(coverage);
        else{different=1;while(different<values.Length&&values[different]==coverage)different++;}
        return different<0?count:different;
    }
    private void ApplyMasked(Span<byte> row,int x,int y,Paint paint,byte coverage,CoverageClip? clip)
    {
        if(clip is null){SpanCompositor.Apply(row,target.Format,target.Alpha,paint,coverage);return;}
        for(int i=0;i<row.Length/4;)
        {
            int count=ClipRun(clip,x+i,y,row.Length/4-i,out byte clipAlpha);
            byte c=PixelBuffer.Multiply(coverage,clipAlpha);int end=i+count;
            SpanCompositor.Apply(row.Slice(i*4,(end-i)*4),target.Format,target.Alpha,paint,c);i=end;
        }
    }
    private void ApplyMasked(Span<byte> row,ReadOnlySpan<byte> coverage,int x,int y,Paint paint,CoverageClip? clip)
    {
        if(clip is null){SpanCompositor.ApplyCoverage(row,coverage,target.Format,target.Alpha,paint);return;}
        for(int i=0;i<coverage.Length;)
        {
            byte c=ClipCoverage(coverage[i],x+i,y,clip);int end=i+1;
            while(end<coverage.Length&&ClipCoverage(coverage[end],x+end,y,clip)==c)end++;
            SpanCompositor.Apply(row.Slice(i*4,(end-i)*4),target.Format,target.Alpha,paint,c);i=end;
        }
    }
    private void DrawSampledImage(in DrawingCommand command)
    {
        var image=command.Image??throw new ArgumentException("Image pixels missing.");image.ThrowIfDisposed();
        if(!command.Transform.IsAffine||!command.Transform.IsFinite)throw new NotSupportedException("Image drawing requires a finite affine transform.");
        var source=command.ImageSource??new RectF(0,0,image.Width,image.Height);
        if(!source.IsFinite||source.IsEmpty||source.Left<0||source.Top<0||source.Right>image.Width||source.Bottom>image.Height)
            throw new ArgumentOutOfRangeException(nameof(command.ImageSource),$"Source [{source.Left:R}, {source.Top:R}, {source.Right:R}, {source.Bottom:R}] must be a nonempty rectangle inside the {image.Width} x {image.Height} bitmap.");
        if(command.Rectangle.IsEmpty)return;
        if(!command.Rectangle.IsFinite)throw new ArgumentException("Destination must be finite.");
        if(!command.Transform.TryInvert(out var inverse))return;
        using var snapshot=image.GetPixelSpan().Overlaps(target.GetPixelSpan())?image.Clone():null;
        image=snapshot??image;
        if(TryBlit(command,source,image))return;
        if(TryDrawAxisSampled(command,source,image,inverse))return;
        var geometry=RectangleGeometry(command.Rectangle);
        var mask=GetMask(geometry,command);var rect=command.Rectangle;
        float sx=(source.Right-source.Left)/(rect.Right-rect.Left),sy=(source.Bottom-source.Top)/(rect.Bottom-rect.Top);
        foreach(var run in mask.Runs)
        {
            var row=target.GetRowSpan(run.Y);
            for(int x=run.X;x<run.X+run.Length;x++)
            {
                byte coverage=ClipCoverage(run.Coverage,x,run.Y,command.CoverageClip);if(coverage==0)continue;
                var p=inverse.Map(x+.5f,run.Y+.5f);
                var sample=Sample(image,source.Left+(p.X-rect.Left)*sx-.5f,source.Top+(p.Y-rect.Top)*sy-.5f,command.Sampling,source,command.NearestLowerBoundary);
                CompositePremultiplied(row.Slice(x*4,4),sample,command.Paint.Color.A,coverage,command.Paint.Composite,command.Sampling!=ImageSampling.Mitchell);
            }
        }
    }
    internal readonly record struct ImageTap(int First,int Second,float Fraction);
    private bool TryDrawAxisSampled(in DrawingCommand command,RectF source,PixelBuffer image,Matrix3 inverse)
    {
        var m=command.Transform;if(m.M01!=0||m.M10!=0||command.Sampling==ImageSampling.Mitchell)return false;
        var rect=command.Rectangle;var a=m.Map(rect.Left,rect.Top);var b=m.Map(rect.Right,rect.Bottom);
        float left=Math.Min(a.X,b.X),top=Math.Min(a.Y,b.Y),right=Math.Max(a.X,b.X),bottom=Math.Max(a.Y,b.Y);
        if(!float.IsFinite(left)||!float.IsFinite(top)||!float.IsFinite(right)||!float.IsFinite(bottom))throw new ArgumentException("Transformed image must be finite.");
        if(command.Clip is {} clip){left=Math.Max(left,clip.Left);right=Math.Min(right,clip.Right);top=Math.Max(top,clip.Top);bottom=Math.Min(bottom,clip.Bottom);}
        if(left>=right||top>=bottom)return true;
        int n=command.Paint.Antialias?16:1;
        bool lowerBoundary=command.NearestLowerBoundary&&command.Sampling==ImageSampling.Nearest,analyticCoverage=lowerBoundary&&command.Paint.Antialias;
        int x0=n==16?SampleGrid(left,target.Width):SampleEdge(left,target.Width),x1=n==16?SampleGrid(right,target.Width):SampleEdge(right,target.Width);
        int y0=n==16?SampleGrid(top,target.Height):SampleEdge(top,target.Height),y1=n==16?SampleGrid(bottom,target.Height):SampleEdge(bottom,target.Height);
        if(analyticCoverage){x0=(int)Math.Clamp(MathF.Floor(left),0,target.Width)*n;x1=(int)Math.Clamp(MathF.Ceiling(right),0,target.Width)*n;y0=(int)Math.Clamp(MathF.Floor(top),0,target.Height)*n;y1=(int)Math.Clamp(MathF.Ceiling(bottom),0,target.Height)*n;}
        int start=x0/n,end=(x1+n-1)/n;if(start>=end||y0>=y1)return true;
        int minX=(int)MathF.Floor(source.Left),maxX=(int)MathF.Ceiling(source.Right)-1,minY=(int)MathF.Floor(source.Top),maxY=(int)MathF.Ceiling(source.Bottom)-1;
        var taps=System.Buffers.ArrayPool<ImageTap>.Shared.Rent(end-start);
        // Sample once into a reusable PMA scanline, then composite constant
        // coverage spans with the same integer kernel as unscaled images.
        byte[]? sampled=command.Paint.Composite==CompositeMode.SourceOver&&target.Alpha!=AlphaFormat.Straight
            ?System.Buffers.ArrayPool<byte>.Shared.Rent(checked((end-start)*4)):null;
        float sx=(source.Right-source.Left)/(rect.Right-rect.Left),sy=(source.Bottom-source.Top)/(rect.Bottom-rect.Top);
        bool nearest=command.Sampling==ImageSampling.Nearest;
        bool vectorBilinear=!nearest&&sampled is not null&&CpuOptions.EnableSimd&&System.Runtime.Intrinsics.X86.Sse2.IsSupported;
        try
        {
            for(int x=start;x<end;x++)
            {
                var p=inverse.Map(x+.5f,.5f);float u=source.Left+(p.X-rect.Left)*sx-.5f;int ix=(int)MathF.Floor(u);
                taps[x-start]=nearest?new(Math.Clamp(lowerBoundary?LowerTexel(u):(int)MathF.Floor(u+.5f),minX,maxX),0,0):new(Math.Clamp(ix,minX,maxX),Math.Clamp(ix+1,minX,maxX),u-ix);
            }
            int ri=image.Format==PixelFormat.Rgba8888?0:2;bool straight=image.Alpha==AlphaFormat.Straight,opaque=image.Alpha==AlphaFormat.Opaque;
            Rgba32 Read(ReadOnlySpan<byte> row,int x)
            {
                var p=row.Slice(x*4,4);byte alpha=opaque?(byte)255:p[3],r=p[ri],g=p[1],blue=p[2-ri];
                if(straight){r=PixelBuffer.Multiply(r,alpha);g=PixelBuffer.Multiply(g,alpha);blue=PixelBuffer.Multiply(blue,alpha);}return new(r,g,blue,alpha);
            }
            int lastRow0=-1,lastRow1=-1;float lastFraction=float.NaN;var coverageClip=command.CoverageClip;
            for(int y=y0/n;y<(y1+n-1)/n;y++)
            {
                var p=inverse.Map(.5f,y+.5f);float v=source.Top+(p.Y-rect.Top)*sy-.5f;int iy=(int)MathF.Floor(v);float fy=v-iy;
                int rowIndex0=nearest?Math.Clamp(lowerBoundary?LowerTexel(v):(int)MathF.Floor(v+.5f),minY,maxY):Math.Clamp(iy,minY,maxY),rowIndex1=Math.Clamp(iy+1,minY,maxY);
                var row0=image.GetRowSpan(rowIndex0);
                var row1=image.GetRowSpan(rowIndex1);var destination=target.GetRowSpan(y);
                int cy=Math.Max(0,Math.Min(y1,(y+1)*n)-Math.Max(y0,y*n));
                byte Coverage(int x)
                {
                    byte c;
                    if(analyticCoverage)c=(byte)Math.Clamp((int)MathF.Round(Math.Max(0,Math.Min(right,x+1)-Math.Max(left,x))*Math.Max(0,Math.Min(bottom,y+1)-Math.Max(top,y))*255),0,255);
                    else{int cx=Math.Max(0,Math.Min(x1,(x+1)*n)-Math.Max(x0,x*n));c=(byte)((cx*cy*255+n*n/2)/(n*n));}
                    return ClipCoverage(c,x,y,coverageClip);
                }
                bool reuse=sampled is not null&&rowIndex0==lastRow0&&(nearest||(rowIndex1==lastRow1&&fy==lastFraction));
                for(int x=start;!reuse&&x<end;x++)
                {
                    if(vectorBilinear&&System.Runtime.Intrinsics.X86.Avx2.IsSupported&&x<=end-8)
                    {
                        BilinearSampler.Sample8(row0,row1,taps.AsSpan(x-start,8),fy,opaque,straight,image.Format!=target.Format,sampled!.AsSpan((x-start)*4,32));
                        x+=7;continue;
                    }
                    byte coverage=255;
                    if(sampled is null){coverage=Coverage(x);if(coverage==0)continue;}
                    var tap=taps[x-start];Rgba32 color;
                    if(vectorBilinear&&!straight)
                    {
                        uint pixel=BilinearSampler.Sample(row0,row1,tap.First,tap.Second,tap.Fraction,fy,opaque);
                        if(image.Format!=target.Format)pixel=(pixel&0xff00ff00)|((pixel&0xff)<<16)|((pixel>>16)&0xff);
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(sampled!.AsSpan((x-start)*4,4),pixel);
                        continue;
                    }
                    if(nearest)color=Read(row0,tap.First);
                    else
                    {
                        var c00=Read(row0,tap.First);var c10=Read(row0,tap.Second);var c01=Read(row1,tap.First);var c11=Read(row1,tap.Second);float fx=tap.Fraction;
                        float w00=(1-fx)*(1-fy),w10=fx*(1-fy),w01=(1-fx)*fy,w11=fx*fy;
                        static float Mix(byte a,byte b,byte c,byte d,float w0,float w1,float w2,float w3)=>a*w0+b*w1+c*w2+d*w3;
                        byte alpha=(byte)Math.Clamp((int)MathF.Round(Mix(c00.A,c10.A,c01.A,c11.A,w00,w10,w01,w11)),0,255);
                        byte Channel(byte a,byte b,byte c,byte d)=>(byte)Math.Clamp((int)MathF.Round(Mix(a,b,c,d,w00,w10,w01,w11)),0,alpha);
                        color=new(Channel(c00.R,c10.R,c01.R,c11.R),Channel(c00.G,c10.G,c01.G,c11.G),Channel(c00.B,c10.B,c01.B,c11.B),alpha);
                    }
                    if(sampled is null)CompositePremultiplied(destination.Slice(x*4,4),color,command.Paint.Color.A,coverage,command.Paint.Composite);
                    else
                    {
                        int at=(x-start)*4,di=target.Format==PixelFormat.Rgba8888?0:2;
                        sampled[at+di]=color.R;sampled[at+1]=color.G;sampled[at+2-di]=color.B;sampled[at+3]=color.A;
                    }
                }
                if(sampled is not null)
                {
                    for(int x=start;x<end;)
                    {
                        byte coverage=Coverage(x);int next=x+1;
                        if(coverageClip is null){if(x>start&&x<end-1)next=end-1;}
                        else if(x>start&&x<end-1)next=x+ClipRun(coverageClip,x,y,end-1-x,out _);
                        if(coverage!=0)ImageCompositor.SourceOver(sampled.AsSpan((x-start)*4,(next-x)*4),destination.Slice(x*4,(next-x)*4),PixelBuffer.Multiply(command.Paint.Color.A,coverage),opaque,true);
                        x=next;
                    }
                }
                lastRow0=rowIndex0;lastRow1=rowIndex1;lastFraction=fy;
            }
        }
        finally{System.Buffers.ArrayPool<ImageTap>.Shared.Return(taps);if(sampled is not null)System.Buffers.ArrayPool<byte>.Shared.Return(sampled);}return true;
    }
    private bool TryBlit(in DrawingCommand command,RectF source,PixelBuffer image)
    {
        var m=command.Transform;var r=command.Rectangle;
        if(command.CoverageClip is not null||command.Sampling==ImageSampling.Mitchell||image.Format!=target.Format||image.Alpha==AlphaFormat.Straight||target.Alpha==AlphaFormat.Straight||
            command.Paint.Composite!=CompositeMode.SourceOver||m.M00!=1||m.M11!=1||m.M01!=0||m.M10!=0||source.Right-source.Left!=r.Right-r.Left||source.Bottom-source.Top!=r.Bottom-r.Top)return false;
        float left=r.Left+m.M02,top=r.Top+m.M12,right=r.Right+m.M02,bottom=r.Bottom+m.M12;
        if(left!=MathF.Floor(left)||top!=MathF.Floor(top)||right!=MathF.Floor(right)||bottom!=MathF.Floor(bottom)||source.Left!=MathF.Floor(source.Left)||source.Top!=MathF.Floor(source.Top))return false;
        int x0=(int)Math.Clamp(left,0,target.Width),y0=(int)Math.Clamp(top,0,target.Height),x1=(int)Math.Clamp(right,0,target.Width),y1=(int)Math.Clamp(bottom,0,target.Height);
        if(command.Clip is {} clip){x0=Math.Max(x0,SampleEdge(clip.Left,target.Width));y0=Math.Max(y0,SampleEdge(clip.Top,target.Height));x1=Math.Min(x1,SampleEdge(clip.Right,target.Width));y1=Math.Min(y1,SampleEdge(clip.Bottom,target.Height));}
        if(x0>=x1||y0>=y1)return true;
        int sx=checked((int)(source.Left+x0-left)),sy=checked((int)(source.Top+y0-top));
        for(int y=y0;y<y1;y++)ImageCompositor.SourceOver(image.GetRowSpan(sy+y-y0).Slice(sx*4,(x1-x0)*4),target.GetRowSpan(y).Slice(x0*4,(x1-x0)*4),command.Paint.Color.A,image.Alpha==AlphaFormat.Opaque,true);
        return true;
    }
    // Filtering takes place in premultiplied space so transparent colored texels cannot bleed.
    private static int LowerTexel(float coordinate)=>(int)Math.Ceiling(Math.Floor((double)(coordinate+.5f)*65536)/65536)-1;
    private static Rgba32 Sample(PixelBuffer image,float x,float y,ImageSampling sampling,RectF source,bool nearestLowerBoundary=false)
    {
        int left=(int)MathF.Floor(source.Left),top=(int)MathF.Floor(source.Top),right=(int)MathF.Ceiling(source.Right)-1,bottom=(int)MathF.Ceiling(source.Bottom)-1;
        Rgba32 Read(int ix,int iy)
        {
            var p=image.GetRowSpan(Math.Clamp(iy,top,bottom)).Slice(Math.Clamp(ix,left,right)*4,4);
            int ri=image.Format==PixelFormat.Rgba8888?0:2;byte a=image.Alpha==AlphaFormat.Opaque?(byte)255:p[3],r=p[ri],g=p[1],b=p[2-ri];
            if(image.Alpha==AlphaFormat.Straight){r=PixelBuffer.Multiply(r,a);g=PixelBuffer.Multiply(g,a);b=PixelBuffer.Multiply(b,a);}
            return new(r,g,b,a);
        }
        if(sampling==ImageSampling.Nearest)return nearestLowerBoundary?Read(LowerTexel(x),LowerTexel(y)):Read((int)MathF.Floor(x+.5f),(int)MathF.Floor(y+.5f));
        int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y);float fx=x-ix,fy=y-iy;
        float r=0,g=0,b=0,a=0;
        if(sampling==ImageSampling.Bilinear)
        {
            for(int j=0;j<2;j++)for(int i=0;i<2;i++){float w=(i==0?1-fx:fx)*(j==0?1-fy:fy);var p=Read(ix+i,iy+j);r+=p.R*w;g+=p.G*w;b+=p.B*w;a+=p.A*w;}
        }
        else
        {
            static float Mitchell(float v){v=MathF.Abs(v);return v<1?(7*v*v*v-12*v*v+16f/3)/6:v<2?(-7f/3*v*v*v+12*v*v-20*v+32f/3)/6:0;}
            for(int j=-1;j<=2;j++)for(int i=-1;i<=2;i++){float w=Mitchell(i-fx)*Mitchell(j-fy);var p=Read(ix+i,iy+j);r+=p.R*w;g+=p.G*w;b+=p.B*w;a+=p.A*w;}
        }
        byte alpha=(byte)Math.Clamp((int)MathF.Round(a),0,255);
        byte Clamp(float value)=>(byte)Math.Clamp((int)MathF.Round(value),0,alpha);
        return new(Clamp(r),Clamp(g),Clamp(b),alpha);
    }
    private void CompositePremultiplied(Span<byte> pixel,Rgba32 source,byte opacity,byte coverage,CompositeMode mode,bool filtered=false)
    {
        if(filtered&&mode==CompositeMode.SourceOver&&target.Alpha!=AlphaFormat.Straight)
        {
            Span<byte> color=stackalloc byte[4];int channel=target.Format==PixelFormat.Rgba8888?0:2;
            color[channel]=source.R;color[1]=source.G;color[2-channel]=source.B;color[3]=source.A;
            ImageCompositor.SourceOver(color,pixel,PixelBuffer.Multiply(opacity,coverage),false,true);return;
        }
        byte scale=PixelBuffer.Multiply(opacity,coverage),a=PixelBuffer.Multiply(source.A,scale);
        byte remainder=mode==CompositeMode.SourceOver?(byte)(255-a):(byte)(255-coverage);
        int ri=target.Format==PixelFormat.Rgba8888?0:2;
        byte da=pixel[3],dr=pixel[ri],dg=pixel[1],db=pixel[2-ri];
        if(target.Alpha==AlphaFormat.Straight){dr=PixelBuffer.Multiply(dr,da);dg=PixelBuffer.Multiply(dg,da);db=PixelBuffer.Multiply(db,da);}
        byte Add(byte s,byte d)=>(byte)Math.Min(255,(mode==CompositeMode.Clear?0:PixelBuffer.Multiply(s,scale))+PixelBuffer.Multiply(d,remainder));
        byte oa=(byte)Math.Min(255,(mode==CompositeMode.Clear?0:a)+PixelBuffer.Multiply(da,remainder));
        byte r=Add(source.R,dr),g=Add(source.G,dg),b=Add(source.B,db);
        if(target.Alpha==AlphaFormat.Straight){r=PixelBuffer.Unmultiply(r,oa);g=PixelBuffer.Unmultiply(g,oa);b=PixelBuffer.Unmultiply(b,oa);}
        pixel[ri]=r;pixel[1]=g;pixel[2-ri]=b;pixel[3]=oa;
    }
}
