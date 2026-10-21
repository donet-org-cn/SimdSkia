using SimdSkia;
using SimdSkia.Cpu;

internal static class TextureCacheValidation
{
    internal static int Run(ICanvasBackend gpu,PixelBuffer target,Func<long> uploads,Func<long> hits)
    {
        int count=0;
        void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);count++;}
        using var expected=new PixelBuffer(target.Width,target.Height,target.Format,target.Alpha);
        using var cpu=new RasterCanvas(expected);
        foreach(var format in Enum.GetValues<PixelFormat>())foreach(var alpha in Enum.GetValues<AlphaFormat>())
        {
            using var source=new PixelBuffer(3,2,format,alpha,17);
            for(int y=0;y<2;y++)for(int x=0;x<3;x++)source.WritePixel(x,y,new((byte)(53+x*71),(byte)(31+y*153),97,alpha==AlphaFormat.Opaque?(byte)255:(byte)137));
            if(alpha==AlphaFormat.Opaque)for(int y=0;y<2;y++)for(int x=0;x<3;x++)source.GetRowSpan(y)[x*4+3]=19;
            gpu.Execute(DrawingCommand.Clear(default));cpu.Execute(DrawingCommand.Clear(default));
            void Draw(PixelBuffer image,int x)
            {
                var command=new DrawingCommand(CommandKind.DrawImage,new(x,0,x+3,2),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver)){Image=image};
                gpu.Execute(command);cpu.Execute(command);
            }
            Draw(source,0);long uploaded=uploads(),hit=hits();
            using(var clone=source.Clone()){Draw(clone,4);Check(uploads()==uploaded&&hits()==hit+1,"Identical wrappers must reuse a native texture, without relying on object identity.");}
            source.WritePixel(0,0,new(3,240,11,255));Draw(source,8);
            Check(uploads()==uploaded+1,"Changed image bytes must upload a fresh queued snapshot.");
            source.Dispose();gpu.Flush();
            for(int y=0;y<2;y++)for(int x=0;x<11;x++)
            {
                var a=target.ReadPixel(x,y);var b=expected.ReadPixel(x,y);
                Check(Math.Abs(a.R-b.R)<=2&&Math.Abs(a.G-b.G)<=2&&Math.Abs(a.B-b.B)<=2&&a.A==b.A,"Content reuse must preserve queued versions, format, alpha, padding and disposed-source lifetime.");
            }
        }
        for(int i=0;i<300;i++)
        {
            using var image=new PixelBuffer(1,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);
            image.WritePixel(0,0,new((byte)i,(byte)(i>>8),217,255));
            gpu.Execute(new(CommandKind.DrawImage,new(0,3,1,4),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver)){Image=image});
        }
        gpu.Flush();Check(target.ReadPixel(0,3)==new Rgba32(43,1,217,255),"Texture eviction must retain the final submitted version.");
        return count;
    }
}
