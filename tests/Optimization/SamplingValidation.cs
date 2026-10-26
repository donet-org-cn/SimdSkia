using SimdSkia;
using SimdSkia.Cpu;

internal static class SamplingValidation
{
    internal static int Run()
    {
        var random=new Random(9326);int checks=0;
        foreach(var sourceFormat in Enum.GetValues<PixelFormat>())
        foreach(var destinationFormat in Enum.GetValues<PixelFormat>())
        foreach(var alpha in Enum.GetValues<AlphaFormat>())
        foreach(var mode in Enum.GetValues<CompositeMode>())
        foreach(bool aa in new[]{false,true})
        foreach(bool mirror in new[]{false,true})
        foreach(int width in new[]{1,7,8,9,15,16,17,33,129})
        {
            using var source=new PixelBuffer(11,7,sourceFormat,alpha,49);
            // Include invalid physical PMA and opaque physical alpha bytes:
            // these must retain the documented scalar interpretation.
            random.NextBytes(source.GetPixelSpan());
            using var expected=new PixelBuffer(width,17,destinationFormat,AlphaFormat.Premultiplied,width*4+13);
            random.NextBytes(expected.GetPixelSpan());using var actual=expected.Clone();
            var values=new byte[113*19];
            for(int y=0;y<19;y++)for(int x=0;x<113;x++)values[y*113+x]=x<9?(byte)random.Next(256):x<73?(byte)255:x<97?(byte)173:(byte)0;
            var clip=new CoverageClip(-5,1,113,19,values);
            var matrix=new Matrix3(mirror?-1.25f:1.25f,0,mirror?width+.125f:-.125f,0,1.125f,.375f,0,0,1);
            var command=new DrawingCommand(CommandKind.DrawImage,new(0,0,width,14),matrix,new(new(255,255,255,173),mode,aa))
            {Image=source,ImageSource=new(.25f,.125f,10.75f,6.875f),Sampling=ImageSampling.Bilinear,CoverageClip=clip,Clip=new(-1,.25f,width+.5f,16.5f)};
            CpuOptions.EnableSimd=false;using(var canvas=new RasterCanvas(expected))canvas.Execute(command);
            CpuOptions.EnableSimd=true;using(var canvas=new RasterCanvas(actual))canvas.Execute(command);
            if(!expected.GetPixelSpan().SequenceEqual(actual.GetPixelSpan()))throw new InvalidOperationException($"Bilinear SIMD/scalar: {sourceFormat}/{destinationFormat}/{alpha}/{mode}/{aa}/{mirror}/{width}");
            checks++;
        }
        // Exact half-integer results must round to even, with alpha limiting
        // after interpolation rather than before it. No shared oracle kernel.
        using var tieSource=new PixelBuffer(2,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);
        new byte[]{0,2,8,200,1,3,9,201}.CopyTo(tieSource.GetPixelSpan());
        foreach(bool simd in new[]{false,true})
        {
            CpuOptions.EnableSimd=simd;using var result=new PixelBuffer(3,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);
            using(var canvas=new RasterCanvas(result))canvas.Execute(new(CommandKind.DrawImage,new(0,0,3,1),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver))
            {Image=tieSource,Sampling=ImageSampling.Bilinear});
            if(!result.GetPixelSpan().Slice(4,4).SequenceEqual(new byte[]{0,2,8,200}))throw new InvalidOperationException("Bilinear ties-to-even changed.");
            checks++;
        }
        // Every byte/alpha pair: premultiplication must round before filtering,
        // not after. Expected values use an independent scalar formula.
        using var straightSource=new PixelBuffer(2,2,PixelFormat.Rgba8888,AlphaFormat.Straight,13);
        using var straightResult=new PixelBuffer(9,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied,41);
        using(var canvas=new RasterCanvas(straightResult))
        {
            var command=new DrawingCommand(CommandKind.DrawImage,new(0,0,9,1),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver))
                {Image=straightSource,Sampling=ImageSampling.Bilinear};
            for(int alpha=0;alpha<256;alpha++)for(int value=0;value<256;value++)
            {
                for(int y=0;y<2;y++)for(int x=0;x<2;x++)
                {var pixel=straightSource.GetRowSpan(y).Slice(x*4,4);pixel[0]=(byte)value;pixel[1]=(byte)(255-value);pixel[2]=(byte)((value*13)&255);pixel[3]=(byte)alpha;}
                straightResult.GetPixelSpan().Clear();CpuOptions.EnableSimd=true;canvas.Execute(command);
                var result=straightResult.GetRowSpan(0);
                for(int x=0;x<9;x++)
                    if(result[x*4]!=(value*alpha+127)/255||result[x*4+1]!=((255-value)*alpha+127)/255||result[x*4+2]!=(((value*13)&255)*alpha+127)/255||result[x*4+3]!=alpha)
                        throw new InvalidOperationException($"Straight-alpha interpolation rounding: {value}/{alpha}/{x}");
                checks++;
            }
        }
        checks+=RecordedBlendValidation.Run();
        CpuOptions.EnableSimd=true;return checks;
    }
}
