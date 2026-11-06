using System.Numerics;
using System.Runtime.InteropServices;

namespace SimdSkia.Cpu;

/// <summary>Opaque two-stop rows: no destination reads, temporary images, or per-pixel compositor.</summary>
internal static class LinearGradientKernel
{
    internal static void Fill(PixelBuffer target,LinearGradient gradient,Matrix3 inverse,int left,int top,int right,int bottom)
    {
        if(!float.IsFinite(gradient.Start.X)||!float.IsFinite(gradient.Start.Y)||!float.IsFinite(gradient.End.X)||!float.IsFinite(gradient.End.Y))throw new ArgumentException("Gradient coordinates must be finite.");
        double dx=(double)gradient.End.X-gradient.Start.X,dy=(double)gradient.End.Y-gradient.Start.Y,den=dx*dx+dy*dy;
        var a=gradient.StartColor;var b=gradient.EndColor;bool rgba=target.Format==PixelFormat.Rgba8888;
        Span<int> indices=stackalloc int[Vector<float>.Count];for(int i=0;i<indices.Length;i++)indices[i]=i;
        var lanes=new Vector<int>(indices);var step=new Vector<int>(Vector<float>.Count);
        var half=new Vector<float>(.5f);var scale=new Vector<float>(inverse.M00);var translation=new Vector<float>(inverse.M02);
        var start=new Vector<double>(gradient.Start.X);var delta=new Vector<double>(dx);var denominator=new Vector<double>(den);
        var c0=new Vector<float>(rgba?a.R:a.B);var c1=new Vector<float>(a.G);var c2=new Vector<float>(rgba?a.B:a.R);
        var d0=new Vector<float>((rgba?b.R:b.B)-(rgba?a.R:a.B));var d1=new Vector<float>(b.G-a.G);var d2=new Vector<float>((rgba?b.B:b.R)-(rgba?a.B:a.R));
        for(int y=top;y<bottom;y++)
        {
            var bytes=target.GetRowSpan(y).Slice(left*4,(right-left)*4);int x=left;
            if(CpuOptions.EnableSimd&&Vector.IsHardwareAccelerated&&BitConverter.IsLittleEndian)
            {
                var row=MemoryMarshal.Cast<byte,uint>(bytes);var positions=new Vector<int>(left)+lanes;
                float ly=inverse.M11*(y+.5f)+inverse.M12;
                var vertical=new Vector<double>(((double)ly-gradient.Start.Y)*dy);
                for(;x<=right-Vector<float>.Count;x+=Vector<float>.Count,positions+=step)
                {
                    var local=(Vector.ConvertToSingle(positions)+half)*scale+translation;
                    Vector.Widen(local,out Vector<double> low,out Vector<double> high);
                    var t=den==0?Vector<float>.One:Vector.Narrow(
                        Vector.Clamp(((low-start)*delta+vertical)/denominator,Vector<double>.Zero,Vector<double>.One),
                        Vector.Clamp(((high-start)*delta+vertical)/denominator,Vector<double>.Zero,Vector<double>.One));
                    var r=Vector.AsVectorUInt32(Vector.ConvertToInt32(Vector.Round(c0+d0*t)));
                    var g=Vector.AsVectorUInt32(Vector.ConvertToInt32(Vector.Round(c1+d1*t)));
                    var bvec=Vector.AsVectorUInt32(Vector.ConvertToInt32(Vector.Round(c2+d2*t)));
                    (r|(g<<8)|(bvec<<16)|new Vector<uint>(0xff000000)).CopyTo(row[(x-left)..]);
                }
            }
            for(;x<right;x++)
            {
                var p=inverse.Map(x+.5f,y+.5f);var color=gradient.Sample(p.X,p.Y);int i=(x-left)*4;
                bytes[i]=rgba?color.R:color.B;bytes[i+1]=color.G;bytes[i+2]=rgba?color.B:color.R;bytes[i+3]=255;
            }
        }
    }
}
