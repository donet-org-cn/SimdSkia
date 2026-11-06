using System.Numerics;
namespace SimdSkia.Cpu;
public sealed partial class RasterImageFilter
{
    public static RasterImageFilter Morphology(float radiusX,float radiusY,bool dilate,RasterImageFilter? input=null,RectF? crop=null)
    {
        ValidateSigma(radiusX);ValidateSigma(radiusY);
        return new((source,m,clip)=>
        {
            using var a=Input(input,source,m,clip);int rx=(int)MathF.Floor(MathF.Abs(m.M00)*radiusX+MathF.Abs(m.M01)*radiusY+.5f),ry=(int)MathF.Floor(MathF.Abs(m.M10)*radiusX+MathF.Abs(m.M11)*radiusY+.5f);
            if(rx>1024||ry>1024)throw new NotSupportedException("Morphology radius exceeds 1024 device pixels.");
            var bounds=Crop(dilate?Inflate(a.Bounds,rx,ry):a.Bounds,crop,m);using var temp=FilterFrame.Rent(Inflate(bounds,0,ry));var result=FilterFrame.Rent(bounds);
            try
            {
                if(temp.Pixels is {} t)for(int y=0;y<t.Height;y++){var row=t.GetRowSpan(y);for(int x=0;x<t.Width;x++){var value=dilate?Vector4.Zero:Vector4.One;for(int k=-rx;k<=rx;k++){var v=Read(a,x+temp.Left+k,y+temp.Top);value=dilate?Vector4.Max(value,v):Vector4.Min(value,v);}Write(row,x,value);}}
                if(result.Pixels is {} p)for(int y=0;y<p.Height;y++){var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++){var value=dilate?Vector4.Zero:Vector4.One;for(int k=-ry;k<=ry;k++){var v=Read(temp,x+result.Left,y+result.Top+k);value=dilate?Vector4.Max(value,v):Vector4.Min(value,v);}Write(row,x,value);}}
                return result;
            }
            catch{result.Dispose();throw;}
        },input);
    }
    private sealed class Tint(Rgba32 color):IColorFilter
    {
        public Vector4 Filter(Vector4 p){float a=p.W*color.A/255f;return new(color.R/255f*a,color.G/255f*a,color.B/255f*a,a);}
    }
    public static RasterImageFilter Shadow(float dx,float dy,float sigmaX,float sigmaY,Rgba32 color,bool only,RasterImageFilter? input=null,RectF? crop=null)
    {
        var blurred=Blur(sigmaX,sigmaY,3,input);var tinted=Color(new Tint(color),blurred);var moved=Matrix(new(1,0,dx,0,1,dy,0,0,1),ImageSampling.Bilinear,tinted);
        if(crop is {} c)moved=Color(new Identity(),moved,c);
        return only?moved:Merge([moved,input],crop);
    }
    private sealed class Identity:IColorFilter {public Vector4 Filter(Vector4 value)=>value;}
    public static RasterImageFilter Convolution(int width,int height,ReadOnlySpan<float> kernel,float gain,float bias,int offsetX,int offsetY,int tile,bool convolveAlpha,RasterImageFilter? input=null,RectF? crop=null)
    {
        if(width<=0||height<=0||checked(width*height)!=kernel.Length)throw new ArgumentException("Kernel dimensions must match its length.");
        if(width>256||height>256||kernel.Length>4096)throw new NotSupportedException("Convolution exceeds 4096 coefficients / 256 pixels per dimension.");
        if(!float.IsFinite(gain)||!float.IsFinite(bias)||(uint)offsetX>=(uint)width||(uint)offsetY>=(uint)height||(uint)tile>3)throw new ArgumentOutOfRangeException(nameof(gain));
        foreach(float k in kernel)if(!float.IsFinite(k))throw new ArgumentException("Kernel coefficients must be finite.");var coefficients=kernel.ToArray();
        return new((source,m,clip)=>
        {
            using var a=Input(input,source,m,clip);var result=FilterFrame.Rent(convolveAlpha&&bias>0?(crop is {} c?Map(c,m):clip):Crop(a.Bounds,crop,m));
            try
            {
                if(result.Pixels is {} p)for(int y=0;y<p.Height;y++)
                {
                    var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++)
                    {
                        var sum=Vector4.Zero;int px=x+result.Left,py=y+result.Top;
                        for(int ky=0;ky<height;ky++)for(int kx=0;kx<width;kx++)
                        {
                            // The integer kernel is defined in filter/layer pixels, as in Skia.
                            var v=Read(a,px+kx-offsetX,py+ky-offsetY,tile);
                            if(!convolveAlpha&&v.W>0){v.X/=v.W;v.Y/=v.W;v.Z/=v.W;}sum+=v*coefficients[ky*width+kx];
                        }
                        sum=Vector4.Clamp(sum*gain+new Vector4(bias/255f),Vector4.Zero,Vector4.One);
                        if(!convolveAlpha){sum.W=Read(a,px,py).W;sum.X*=sum.W;sum.Y*=sum.W;sum.Z*=sum.W;}
                        else{sum.X=MathF.Min(sum.X,sum.W);sum.Y=MathF.Min(sum.Y,sum.W);sum.Z=MathF.Min(sum.Z,sum.W);}Write(row,x,sum);
                    }
                }
                return result;
            }
            catch{result.Dispose();throw;}
        },input);
    }
    public static RasterImageFilter Displacement(int xChannel,int yChannel,float scale,RasterImageFilter displacement,RasterImageFilter? input=null,RectF? crop=null)
    {
        ArgumentNullException.ThrowIfNull(displacement);if((uint)xChannel>3||(uint)yChannel>3||!float.IsFinite(scale))throw new ArgumentOutOfRangeException(nameof(scale));
        return new((source,m,clip)=>
        {
            using var image=Input(input,source,m,clip);using var map=displacement.Apply(source,m,clip);
            float sx=(MathF.Abs(m.M00)+MathF.Abs(m.M01))*MathF.Abs(scale)*.5f,sy=(MathF.Abs(m.M10)+MathF.Abs(m.M11))*MathF.Abs(scale)*.5f;
            var result=FilterFrame.Rent(Crop(Inflate(image.Bounds,sx,sy),crop,m));
            try
            {
                if(result.Pixels is {} p)for(int y=0;y<p.Height;y++){var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++)
                {
                    int px=x+result.Left,py=y+result.Top;var v=Read(map,px,py);if(v.W>0){v.X/=v.W;v.Y/=v.W;v.Z/=v.W;}
                    float dx=(v[xChannel]-.5f)*scale,dy=(v[yChannel]-.5f)*scale;Write(row,x,Read(image,(int)MathF.Floor(px+.5f+m.M00*dx+m.M01*dy),(int)MathF.Floor(py+.5f+m.M10*dx+m.M11*dy)));
                }}return result;
            }
            catch{result.Dispose();throw;}
        },displacement,input);
    }
    public static RasterImageFilter Image(PixelBuffer image,RectF src,RectF dst,ImageSampling sampling)
    {
        ArgumentNullException.ThrowIfNull(image);image.ThrowIfDisposed();if(!src.IsFinite||!dst.IsFinite||src.Left<0||src.Top<0||src.Right>image.Width||src.Bottom>image.Height)throw new ArgumentOutOfRangeException(nameof(src));
        return new((source,m,clip)=>
        {
            var result=FilterFrame.Rent(Map(dst,m));try{if(result.Pixels is {} p){using var canvas=new RasterCanvas(p);var shift=new Matrix3(1,0,-result.Left,0,1,-result.Top,0,0,1);canvas.Execute(new(CommandKind.DrawImage,dst,shift*m,new(new(255,255,255,255),CompositeMode.SourceOver,true)){Image=image,ImageSource=src,Sampling=sampling});}return result;}catch{result.Dispose();throw;}
        });
    }
    public static RasterImageFilter Shader(IColorShader shader,bool dither,RectF? crop=null)
    {
        ArgumentNullException.ThrowIfNull(shader);return new((source,m,clip)=>
        {
            if(!m.TryInvert(out var inverse))return new(null);var result=FilterFrame.Rent(crop is {} b?Map(b,m):clip);
            try{if(result.Pixels is {} p)for(int y=0;y<p.Height;y++){var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++)
            {
                var q=inverse.Map(x+result.Left+.5f,y+result.Top+.5f);Vector4 value;
                if(shader is IPremultipliedColorShader f)value=f.SamplePremultiplied(q.X,q.Y);
                else{var c=shader.Sample(q.X,q.Y);float a=c.A/255f;value=new(c.R/255f*a,c.G/255f*a,c.B/255f*a,a);}
                if(dither){ReadOnlySpan<byte> bayer=[0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5];float n=(bayer[((y+result.Top)&3)*4+((x+result.Left)&3)]/16f-.46875f)/255f;value.X=Math.Clamp(value.X+n,0,value.W);value.Y=Math.Clamp(value.Y+n,0,value.W);value.Z=Math.Clamp(value.Z+n,0,value.W);}Write(row,x,value);
            }}return result;}catch{result.Dispose();throw;}
        });
    }
    public static RasterImageFilter TileImage(RectF src,RectF dst,RasterImageFilter input)
    {
        ArgumentNullException.ThrowIfNull(input);if(!src.IsFinite||!dst.IsFinite||src.IsEmpty)throw new ArgumentException("Tile rectangles must be finite, with a nonempty source.");
        return new((source,m,clip)=>
        {
            using var a=input.Apply(source,m,clip);if(!m.TryInvert(out var inverse))return new(null);var result=FilterFrame.Rent(Map(dst,m));
            try{if(result.Pixels is {} p)for(int y=0;y<p.Height;y++){var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++){var q=inverse.Map(x+result.Left+.5f,y+result.Top+.5f);float tx=(q.X-src.Left)%(src.Right-src.Left),ty=(q.Y-src.Top)%(src.Bottom-src.Top);if(tx<0)tx+=src.Right-src.Left;if(ty<0)ty+=src.Bottom-src.Top;var pos=m.Map(src.Left+tx,src.Top+ty);Write(row,x,Read(a,(int)MathF.Floor(pos.X),(int)MathF.Floor(pos.Y)));}}return result;}catch{result.Dispose();throw;}
        },input);
    }
}
