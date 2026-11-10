// SVG/Skia permutation and stitching algorithms, Google BSD-3. See licenses/Skia-BSD.txt.
using System.Numerics;
namespace SimdSkia;

public sealed class PerlinNoiseShader : IContextualColorShader,IPremultipliedColorShader
{
    private readonly Vector4[] gx=new Vector4[256],gy=new Vector4[256];
    private readonly int[] permutation=new int[256];
    private readonly float frequencyX,frequencyY;
    private readonly int octaves,tileWidth,tileHeight;
    private readonly bool fractal;
    private readonly Dictionary<(Matrix3,byte),IColorShader> contexts=[];
    private readonly Bound initial;
    public PerlinNoiseShader(float frequencyX,float frequencyY,int octaves,float seed,int tileWidth,int tileHeight,bool fractal)
    {
        this.frequencyX=frequencyX;this.frequencyY=frequencyY;this.octaves=octaves;this.tileWidth=tileWidth;this.tileHeight=tileHeight;this.fractal=fractal;
        int state=(int)Math.Clamp((double)seed,int.MinValue,int.MaxValue);if(state<=0)state=-(state%2147483646)+1;if(state>2147483646)state=2147483646;
        int Random(){int next=16807*(state%127773)-2836*(state/127773);if(next<=0)next+=2147483647;return state=next;}
        var raw=new int[4,256,2];for(int channel=0;channel<4;channel++)for(int i=0;i<256;i++){permutation[i]=i;raw[channel,i,0]=Random()%512;raw[channel,i,1]=Random()%512;}
        for(int i=255;i>0;i--){int j=Random()%256;(permutation[i],permutation[j])=(permutation[j],permutation[i]);}
        for(int i=0;i<256;i++)for(int channel=0;channel<4;channel++)
        {
            float x=(raw[channel,permutation[i],0]-256)*(1f/256),y=(raw[channel,permutation[i],1]-256)*(1f/256),length=MathF.Sqrt(x*x+y*y);
            if(length>0){x/=length;y/=length;}gx[i][channel]=x;gy[i][channel]=y;
        }
        initial=new(this,Matrix3.Identity,255);
    }
    public IColorShader Bind(Matrix3 drawingMatrix,byte opacity)
    {
        if(drawingMatrix==Matrix3.Identity&&opacity==255)return initial;
        lock(contexts){var key=(drawingMatrix,opacity);if(contexts.TryGetValue(key,out var found))return found;var result=new Bound(this,drawingMatrix,opacity);if(contexts.Count>=8)contexts.Clear();contexts.Add(key,result);return result;}
    }
    public Vector4 SamplePremultiplied(float x,float y)=>initial.SamplePremultiplied(x,y);
    public Rgba32 Sample(float x,float y)=>initial.Sample(x,y);
    private sealed class Bound : IPremultipliedColorShader
    {
        private readonly PerlinNoiseShader source;
        private readonly Matrix3 matrix;
        private readonly float fx,fy;
        private readonly int width,height;
        private readonly byte opacity;
        internal Bound(PerlinNoiseShader source,Matrix3 matrix,byte opacity)
        {
            this.source=source;this.matrix=matrix;this.opacity=opacity;
            float sx=MathF.Sqrt(matrix.M00*matrix.M00+matrix.M10*matrix.M10),sy=MathF.Sqrt(matrix.M01*matrix.M01+matrix.M11*matrix.M11);
            fx=source.frequencyX/(sx>0?sx:1f/4096);fy=source.frequencyY/(sy>0?sy:1f/4096);
            int tw=(int)Math.Clamp(MathF.Floor(matrix.M00*source.tileWidth+matrix.M01*source.tileHeight+.5f),int.MinValue,int.MaxValue),th=(int)Math.Clamp(MathF.Floor(matrix.M10*source.tileWidth+matrix.M11*source.tileHeight+.5f),int.MinValue,int.MaxValue);
            if(tw>0&&th>0)
            {fx=Stitch(fx,tw);fy=Stitch(fy,th);width=Extent(tw*fx);height=Extent(th*fy);}
        }
        private static float Stitch(float value,int size)
        {if(value==0)return 0;float low=MathF.Floor(size*value)/size,high=MathF.Ceiling(size*value)/size;return value/low<high/value?low:high;}
        private static int Extent(float value)=>(int)Math.Clamp(MathF.Floor(value+.5f),0,2147479551);
        private Vector4 Noise(float x,float y,int w,int h)
        {
            x+=4096;y+=4096;if(!float.IsFinite(x)||!float.IsFinite(y)||MathF.Abs(x)>int.MaxValue-8192f||MathF.Abs(y)>int.MaxValue-8192f)return Vector4.Zero;
            int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y),nx=ix+1,ny=iy+1;float rx=x-ix,ry=y-iy;
            if(source.tileWidth>0&&source.tileHeight>0){if(ix>=4096+w)ix-=w;if(nx>=4096+w)nx-=w;if(iy>=4096+h)iy-=h;if(ny>=4096+h)ny-=h;}
            ix&=255;iy&=255;nx&=255;ny&=255;int a=source.permutation[ix],b=source.permutation[nx],b00=(a+iy)&255,b10=(b+iy)&255,b01=(a+ny)&255,b11=(b+ny)&255;
            float u=rx*rx*(3-2*rx),v=ry*ry*(3-2*ry);
            var top=source.gx[b00]*rx+source.gy[b00]*ry;var topRight=source.gx[b10]*(rx-1)+source.gy[b10]*ry;
            var bottom=source.gx[b01]*rx+source.gy[b01]*(ry-1);var bottomRight=source.gx[b11]*(rx-1)+source.gy[b11]*(ry-1);
            // Keep the same arithmetic order as SkScalarInterp, not the BCL
            // weighted-endpoint Lerp variant used on some SIMD implementations.
            top+=(topRight-top)*u;bottom+=(bottomRight-bottom)*u;return top+(bottom-top)*v;
        }
        public Rgba32 Sample(float x,float y)
        {
            var point=matrix.Map(x,y);float px=MathF.Floor(point.X+1-matrix.M02)*fx,py=MathF.Floor(point.Y+1-matrix.M12)*fy,ratio=1;int w=width,h=height;Vector4 sum=Vector4.Zero;
            for(int i=0;i<source.octaves;i++){var noise=Noise(px,py,w,h);sum+=(source.fractal?noise:Vector4.Abs(noise))/ratio;px*=2;py*=2;ratio*=2;w=Extent(w*2f);h=Extent(h*2f);}
            if(source.fractal)sum=(sum+Vector4.One)*.5f;sum.W*=opacity/255f;sum=Vector4.Clamp(sum,Vector4.Zero,Vector4.One);
            return new((byte)(sum.X*255),(byte)(sum.Y*255),(byte)(sum.Z*255),(byte)(sum.W*255));
        }
        public Vector4 SamplePremultiplied(float x,float y)
        {var color=Sample(x,y);return new(PixelBuffer.Multiply(color.R,color.A)/255f,PixelBuffer.Multiply(color.G,color.A)/255f,PixelBuffer.Multiply(color.B,color.A)/255f,color.A/255f);}
    }
}
