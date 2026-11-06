namespace SimdSkia.Cpu;
public sealed partial class RasterCanvas
{
    private Dictionary<GlyphRunGeometry,GlyphRunGeometry>? lcdRuns;
    private bool DrawLcdText(in DrawingCommand command)
    {
        if(command.TextPixelGeometry is not (1 or 2)||target.Format!=PixelFormat.Bgra8888||!command.Paint.Antialias||command.Paint.Composite!=CompositeMode.SourceOver||command.Paint.Shader is not null||command.Paint.ColorFilter is not null||command.Paint.Blender is not null||command.Paint.FloatingColor is not null)return false;
        bool vertical=command.TextPixelGeometry>=3,bgr=command.TextPixelGeometry is 2 or 4;var original=command.GlyphRun!;
        lcdRuns??=[];if(!lcdRuns.TryGetValue(original,out var run)){run=new(original.Glyphs.ToArray(),original.FontTransform,GlyphPositioning.Exact);if(lcdRuns.Count>=16)lcdRuns.Clear();lcdRuns.Add(original,run);}
        for(int channel=0;channel<3;channel++)
        {
            float delta=(1-channel)/3f;var shift=vertical?new Matrix3(1,0,0,0,1,delta,0,0,1):new Matrix3(1,0,delta,0,1,0,0,0,1);var matrix=shift*command.Transform;
            int luminance=command.TextMaskPreblend?GlyphMaskGamma.Luminance(command.Paint):-1;
            var batch=GlyphCoverageCache.Resolve(run,matrix,true,command.CurveTolerance,command.SnapGlyphBaseline,luminance,out int ox,out int oy);
            int colorChannel=bgr?2-channel:channel,at=target.Format==PixelFormat.Rgba8888?colorChannel:2-colorChannel;var color=command.Paint.Color;byte source=colorChannel switch{0=>color.R,1=>color.G,_=>color.B};
            foreach(var glyph in batch.Glyphs)
            {
                var mask=glyph.Mask;int left=ox+glyph.X+mask.Left,top=oy+glyph.Y+mask.Top;int x0=Math.Max(0,left),y0=Math.Max(0,top),x1=Math.Min(target.Width,left+mask.Width),y1=Math.Min(target.Height,top+mask.Height);
                if(command.Clip is {} clip){x0=Math.Max(x0,SampleEdge(clip.Left,target.Width));x1=Math.Min(x1,SampleEdge(clip.Right,target.Width));y0=Math.Max(y0,SampleEdge(clip.Top,target.Height));y1=Math.Min(y1,SampleEdge(clip.Bottom,target.Height));}
                if(x0>=x1||y0>=y1)continue;
                if(mask.Dense)
                {for(int y=y0;y<y1;y++){var row=target.GetRowSpan(y);for(int x=x0;x<x1;x++){byte coverage=ClipCoverage(mask.Data[(y-top)*mask.Width+x-left],x,y,command.CoverageClip);BlendLcd(row.Slice(x*4,4),at,source,color.A,coverage);}}}
                else foreach(var r in mask.Runs){int y=oy+glyph.Y+r.Y;if(y<y0||y>=y1)continue;int a=Math.Max(x0,ox+glyph.X+r.X),b=Math.Min(x1,ox+glyph.X+r.X+r.Length);var row=target.GetRowSpan(y);for(int x=a;x<b;x++)BlendLcd(row.Slice(x*4,4),at,source,color.A,ClipCoverage(r.Coverage,x,y,command.CoverageClip));}
            }
        }
        return true;
    }
    private static void BlendLcd(Span<byte> pixel,int channel,byte source,byte opacity,byte coverage)
    {byte a=PixelBuffer.Multiply(opacity,coverage);pixel[channel]=(byte)Math.Min(255,PixelBuffer.Multiply(source,a)+PixelBuffer.Multiply(pixel[channel],(byte)(255-a)));pixel[3]=255;}
}
