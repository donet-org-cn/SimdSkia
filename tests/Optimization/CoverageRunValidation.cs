using SimdSkia;
using SimdSkia.Cpu;

internal static class CoverageRunValidation
{
    internal static int Run()
    {
        var random=new Random(9321);int checks=0;
        foreach(bool simd in new[]{false,true})foreach(var format in Enum.GetValues<PixelFormat>())
        foreach(int width in new[]{31,65,159})foreach(bool gradient in new[]{false,true})
        foreach(var mode in new[]{CompositeMode.SourceOver,CompositeMode.Source})
        foreach(var offset in new[]{(-7,2),(3,-3),(173,1),(0,0),(-90,-20)})
        {
            CpuOptions.EnableSimd=simd;
            using var actual=new PixelBuffer(width,13,format,AlphaFormat.Premultiplied,width*4+11);
            random.NextBytes(actual.GetPixelSpan());using var expected=actual.Clone();
            var values=new byte[141*17];
            for(int y=0;y<17;y++)for(int x=0;x<141;x++)values[y*141+x]=x<6?(byte)(x*43):x<97?(byte)255:x<119?(byte)0:x<131?(byte)173:(byte)random.Next(256);
            var clip=new CoverageClip(offset.Item1,offset.Item2,141,17,values);
            var bounds=new RectF(1.1875f,.75f,width-2.375f,11.8125f);
            var shader=gradient?new LinearGradient(new(0,0),new(width,0),new(21,201,7,255),new(239,71,213,255)):null;
            var solid=new Rgba32(31,182,233,137);
            using(var canvas=new RasterCanvas(actual))canvas.Execute(new(CommandKind.FillPath,default,Matrix3.Identity,new(gradient?new(255,255,255,255):solid,mode,true,shader)){Path=PathGeometry.Rectangle(bounds),CoverageClip=clip});
            static int Mul(int a,int b)=>(a*b+127)/255;
            for(int y=0;y<13;y++)for(int x=0;x<width;x++)
            {
                // Independent 16x16 point-count oracle, then scalar PMA math.
                int xs=0,ys=0;
                for(int s=0;s<16;s++){float px=x+(s+.5f)/16,py=y+(s+.5f)/16;if(px>=bounds.Left&&px<bounds.Right)xs++;if(py>=bounds.Top&&py<bounds.Bottom)ys++;}
                int cx=x-offset.Item1,cy=y-offset.Item2;
                int clipAlpha=cx>=0&&cy>=0&&cx<141&&cy<17?values[cy*141+cx]:0;
                int coverage=Mul((xs*ys*255+128)/256,clipAlpha);if(coverage==0)continue;
                var color=shader?.Sample(x+.5f,y+.5f)??solid;int alpha=Mul(color.A,coverage);
                int remainder=mode==CompositeMode.Source?255-coverage:255-alpha;
                var pixel=expected.GetRowSpan(y).Slice(x*4,4);int ri=format==PixelFormat.Rgba8888?0:2;
                pixel[ri]=(byte)Math.Min(255,Mul(color.R,alpha)+Mul(pixel[ri],remainder));
                pixel[1]=(byte)Math.Min(255,Mul(color.G,alpha)+Mul(pixel[1],remainder));
                pixel[2-ri]=(byte)Math.Min(255,Mul(color.B,alpha)+Mul(pixel[2-ri],remainder));
                pixel[3]=(byte)Math.Min(255,alpha+Mul(pixel[3],remainder));
            }
            if(!actual.GetPixelSpan().SequenceEqual(expected.GetPixelSpan()))throw new InvalidOperationException($"Clip scan oracle: {simd}/{format}/{width}/{gradient}/{mode}/{offset}");
            checks++;
        }
        return checks;
    }
}
