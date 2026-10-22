using Avalonia;
using Avalonia.Media;
using SimdSkia.Cpu;
using SimdSkia.Avalonia;

namespace SimdSkia.ControlValidation;

internal static class TileRowValidation
{
    private sealed class ScalarShader(IColorShader shader):IColorShader
    {public Rgba32 Sample(float x,float y)=>shader.Sample(x,y);}
    internal static int Run()
    {
        int checks=0,accepted=0;var random=new Random(2112);
        foreach(var mode in Enum.GetValues<TileMode>())foreach(bool translucent in new[]{false,true})
        foreach(var sampling in new[]{ImageSampling.Nearest,ImageSampling.Bilinear})
        foreach(var transform in new[]{Matrix.Identity,Matrix.CreateTranslation(-13,3),Matrix.CreateTranslation(.25,.5),Matrix.CreateScale(1.5,1.5)})
        {
            using var pixels=new PixelBuffer(7,5,PixelFormat.Bgra8888,AlphaFormat.Premultiplied,35);
            random.NextBytes(pixels.GetPixelSpan());
            for(int y=0;y<5;y++)for(int x=0;x<7;x++)pixels.GetRowSpan(y)[x*4+3]=255;
            if(translucent)pixels.GetRowSpan(2)[3*4+3]=137;
            using var shader=new TileShader(pixels.Share(),transform,mode,sampling);
            foreach(var format in Enum.GetValues<PixelFormat>())foreach(int x in new[]{-17,-1,0,3,65537})
            {
                var row=new byte[33*4];row.AsSpan().Fill(71);var matrix=new Matrix3(1,0,-2,0,1,1,0,0,1);
                bool copied=shader.TryCopyOpaqueRow(row,format,matrix,x,1);
                if(copied)
                {
                    accepted++;
                    for(int i=0;i<33;i++)
                    {
                        var p=matrix.Map(x+i+.5f,1.5f);var c=shader.Sample(p.X,p.Y);int ri=format==PixelFormat.Rgba8888?0:2,at=i*4;
                        if(row[at+ri]!=c.R||row[at+1]!=c.G||row[at+2-ri]!=c.B||row[at+3]!=255||c.A!=255)throw new InvalidOperationException("Opaque tile row changed scalar sampling.");
                    }
                }
                else if(row.Any(b=>b!=71))throw new InvalidOperationException("Rejected tile row partially overwrote the destination.");
                checks++;
            }
            foreach(byte opacity in new byte[]{173,255})foreach(var blend in Enum.GetValues<CompositeMode>())
            {
                using var fast=new PixelBuffer(37,19,PixelFormat.Rgba8888,AlphaFormat.Premultiplied,157);
                random.NextBytes(fast.GetPixelSpan());using var reference=fast.Clone();
                var command=new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,new(new(255,255,255,opacity),blend,true,shader))
                {Path=PathGeometry.Rectangle(new(1.25f,1.5f,35.75f,17.25f))};
                using(var canvas=new RasterCanvas(fast))canvas.Execute(command);
                using(var canvas=new RasterCanvas(reference))canvas.Execute(command with{Paint=command.Paint with{Shader=new ScalarShader(shader)}});
                if(!fast.GetPixelSpan().SequenceEqual(reference.GetPixelSpan()))throw new InvalidOperationException($"Tile span/fallback blend differs: {mode}/{sampling}/{translucent}/{transform}/{opacity}/{blend}");
                checks++;
            }
        }
        if(accepted==0)throw new InvalidOperationException("No opaque tile fast path was exercised.");
        return checks+1;
    }
}
