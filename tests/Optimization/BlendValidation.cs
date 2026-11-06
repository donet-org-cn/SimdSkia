using SimdSkia;
using SimdSkia.Cpu;
internal static class BlendValidation
{
    internal static int Run()
    {
        int checks=0;bool previous=CpuOptions.EnableSimd;var random=new Random(319734);
        try
        {
            foreach(var format in new[]{PixelFormat.Rgba8888,PixelFormat.Bgra8888})foreach(var alpha in Enum.GetValues<AlphaFormat>())foreach(byte coverage in new byte[]{1,91,193,255})for(int sample=0;sample<97;sample++)
            {
                int count=1+sample;var expected=new byte[count*4];random.NextBytes(expected);var actual=(byte[])expected.Clone();
                var color=new Rgba32((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256));
                CpuOptions.EnableSimd=false;BlendKernel.Apply(expected,format,alpha,color,CompositeMode.Multiply,coverage);
                CpuOptions.EnableSimd=true;BlendKernel.Apply(actual,format,alpha,color,CompositeMode.Multiply,coverage);
                if(!expected.AsSpan().SequenceEqual(actual))throw new Exception($"Multiply SIMD/scalar differs: {format}/{alpha}/{coverage}/{count}");checks++;
            }
        }
        finally{CpuOptions.EnableSimd=previous;}
        return checks;
    }
}
