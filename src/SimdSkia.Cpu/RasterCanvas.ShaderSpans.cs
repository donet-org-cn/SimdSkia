using System.Numerics;

namespace SimdSkia.Cpu;

public sealed partial class RasterCanvas
{
    private bool CanBatchShader(Paint paint)=>target.Alpha!=AlphaFormat.Straight&&
        paint.Shader is IPremultipliedColorShader&&paint.Composite==CompositeMode.SourceOver&&
        paint.ColorFilter is null&&paint.FloatingColor is null&&paint.Blender is null&&!paint.Dither;

    private void FillShaderSpan(Span<byte> destination,IPremultipliedColorShader shader,Matrix3 inverse,int x,int y,byte opacity)
    {
        // A fixed-size stack block avoids retaining image-sized shader caches.
        // Sampling keeps its original coordinates and full float precision.
        Span<Vector4> colors=stackalloc Vector4[128];
        float scale=opacity/255f;
        for(int start=0;start<destination.Length/4;start+=colors.Length)
        {
            int count=Math.Min(colors.Length,destination.Length/4-start);
            for(int i=0;i<count;i++)
            {
                var local=inverse.Map(x+start+i+.5f,y+.5f);
                colors[i]=new(local.X,local.Y,0,0);
            }
            if(CpuOptions.EnableSimd&&shader is IPremultipliedBatchShader batch)batch.SampleBatch(colors[..count]);
            else for(int i=0;i<count;i++)colors[i]=shader.SamplePremultiplied(colors[i].X,colors[i].Y);
            for(int i=0;i<count;i++)colors[i]*=scale;
            BlendKernel.ApplyVaryingSourceOver(destination.Slice(start*4,count*4),colors[..count],target.Format,target.Alpha);
        }
    }
}
