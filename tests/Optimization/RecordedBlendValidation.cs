using System.Buffers.Binary;
using SimdSkia;
using SimdSkia.Cpu;

internal static class RecordedBlendValidation
{
    internal static int Run()
    {
        byte[] fixture=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","linear-blend-native-3.119.4.bin"));
        if(!fixture.AsSpan(0,8).SequenceEqual("SSBLEND1"u8))throw new InvalidDataException("Native blend fixture header");
        int count=BinaryPrimitives.ReadInt32LittleEndian(fixture.AsSpan(8,4));
        if(count!=12288||fixture.Length!=12+count*16)throw new InvalidDataException("Native blend fixture length");
        int checks=0;
        foreach(bool simd in new[]{false,true})
        foreach(var sourceFormat in Enum.GetValues<PixelFormat>())
        foreach(var destinationFormat in Enum.GetValues<PixelFormat>())
        foreach(var sampling in new[]{ImageSampling.Nearest,ImageSampling.Bilinear})
        foreach(int width in new[]{2,9})
        {
            CpuOptions.EnableSimd=simd;
            using var pma=new PixelBuffer(2,2,sourceFormat,AlphaFormat.Premultiplied,13);
            using var straight=new PixelBuffer(2,2,sourceFormat,AlphaFormat.Straight,13);
            using var target=new PixelBuffer(width,2,destinationFormat,AlphaFormat.Premultiplied,width*4+5);
            using var canvas=new RasterCanvas(target);
            int si=sourceFormat==PixelFormat.Rgba8888?0:2,di=destinationFormat==PixelFormat.Rgba8888?0:2;
            for(int i=0;i<count;i++)
            {
                var row=fixture.AsSpan(12+i*16,16);var source=row[0]==0?pma:straight;
                for(int y=0;y<2;y++)for(int x=0;x<2;x++)
                {var pixel=source.GetRowSpan(y).Slice(x*4,4);pixel[si]=row[4];pixel[1]=row[5];pixel[2-si]=row[6];pixel[3]=row[7];}
                for(int y=0;y<2;y++)for(int x=0;x<width;x++){var pixel=target.GetRowSpan(y).Slice(x*4,4);pixel[di]=row[8];pixel[1]=row[9];pixel[2-di]=row[10];pixel[3]=row[11];}
                canvas.Execute(new DrawingCommand(CommandKind.DrawImage,new(0,0,width,2),Matrix3.Identity,new(new(255,255,255,row[1]),CompositeMode.SourceOver))
                    {Image=source,ImageSource=new(0,0,2,2),Sampling=sampling});
                for(int y=0;y<2;y++)for(int x=0;x<width;x++)
                {var pixel=target.GetRowSpan(y).Slice(x*4,4);if(pixel[di]!=row[12]||pixel[1]!=row[13]||pixel[2-di]!=row[14]||pixel[3]!=row[15])throw new InvalidOperationException($"Native filtered blend {i}/{x}/{y}/{simd}/{sourceFormat}/{destinationFormat}");}
                checks++;
            }
        }
        CpuOptions.EnableSimd=true;return checks;
    }
}
