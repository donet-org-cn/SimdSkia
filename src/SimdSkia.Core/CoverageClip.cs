namespace SimdSkia;

/// <summary>Immutable device-pixel coverage. The outer rectangle is half-open.</summary>
public sealed class CoverageClip
{
    private readonly byte[] data;
    public int Left {get;}
    public int Top {get;}
    public int Width {get;}
    public int Height {get;}
    public RectF Bounds=>new(Left,Top,Left+Width,Top+Height);
    public CoverageClip(int left,int top,int width,int height,ReadOnlySpan<byte> coverage)
    {
        if(width<0||height<0||coverage.Length!=checked(width*height))throw new ArgumentException("Clip dimensions do not match coverage.");
        _=checked(left+width);_=checked(top+height);
        Left=left;Top=top;Width=width;Height=height;data=coverage.ToArray();
    }
    public ReadOnlySpan<byte> GetRow(int y)=>y<Top||y>=Top+Height?ReadOnlySpan<byte>.Empty:data.AsSpan((y-Top)*Width,Width);
    public byte At(int x,int y)=>x<Left||y<Top||x>=Left+Width||y>=Top+Height?(byte)0:data[(y-Top)*Width+x-Left];
    public static CoverageClip Intersect(CoverageClip a,CoverageClip b)
    {
        int l=Math.Max(a.Left,b.Left),t=Math.Max(a.Top,b.Top),r=Math.Min(a.Left+a.Width,b.Left+b.Width),bottom=Math.Min(a.Top+a.Height,b.Top+b.Height);
        int w=Math.Max(0,r-l),h=Math.Max(0,bottom-t);var data=new byte[checked(w*h)];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)data[y*w+x]=PixelBuffer.Multiply(a.At(l+x,t+y),b.At(l+x,t+y));
        return new(l,t,w,h,data);
    }
}
