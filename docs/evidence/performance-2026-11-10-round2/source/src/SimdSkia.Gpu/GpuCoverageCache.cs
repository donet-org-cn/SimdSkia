using SimdSkia.Cpu;
namespace SimdSkia.Gpu;

// Device coverage has a separate small budget. In particular, recreating the
// same immutable clip on every draw prevents reuse of its uploaded GPU mask.
internal sealed class GpuCoverageCache
{
    private readonly record struct Key(PathGeometry Path,Matrix3 Matrix,bool Antialias,bool Quad);
    private readonly Dictionary<Key,CoverageClip> entries=[];
    private long bytes;
    private long limit=2*1024*1024;
    internal CoverageClip Get(PathGeometry path,in DrawingCommand command,int width,int height)
    {
        bool quad=command.Kind==CommandKind.DrawImage&&command.Paint.Antialias&&command.Transform.M01==0&&command.Transform.M10==0;
        var key=new Key(path,command.Transform,command.Paint.Antialias,quad);if(entries.TryGetValue(key,out var cached))return cached;
        CoverageClip result;
        if(quad)
        {
            var a=command.Transform.Map(command.Rectangle.Left,command.Rectangle.Top);var b=command.Transform.Map(command.Rectangle.Right,command.Rectangle.Bottom);
            float left=MathF.Min(a.X,b.X),top=MathF.Min(a.Y,b.Y),right=MathF.Max(a.X,b.X),bottom=MathF.Max(a.Y,b.Y);
            int x0=(int)Math.Clamp(MathF.Floor(left),0,width),y0=(int)Math.Clamp(MathF.Floor(top),0,height),x1=(int)Math.Clamp(MathF.Ceiling(right),x0,width),y1=(int)Math.Clamp(MathF.Ceiling(bottom),y0,height);var values=new byte[checked((x1-x0)*(y1-y0))];
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){float cx=Math.Clamp(MathF.Min(x+1,right)-MathF.Max(x,left),0,1),cy=Math.Clamp(MathF.Min(y+1,bottom)-MathF.Max(y,top),0,1);values[(y-y0)*(x1-x0)+x-x0]=(byte)MathF.Floor(MathF.Min(cx,cy)*255+.5f);}
            result=new(x0,y0,x1-x0,y1-y0,values);
        }
        else result=RasterCanvas.CreateClipForBounds(path,command.Transform,width,height,command.Paint.Antialias);
        long length=(long)result.Width*result.Height+(long)path.Segments.Length*32;
        if(limit>0&&length<=limit){if(entries.Count>=32||bytes+length>limit)Clear();entries.Add(key,result);bytes+=length;}return result;
    }
    internal void Configure(long budget,bool enabled){limit=enabled?Math.Clamp(budget,0,2*1024*1024):0;if(bytes>limit)Clear();}
    internal void Clear(){entries.Clear();bytes=0;}
}
