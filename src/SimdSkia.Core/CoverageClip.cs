using System.Numerics;
using System.Runtime.CompilerServices;

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
    // Internal immutable raster masks have no caller-owned writable aliases.
    // Integer translations can retain their bytes without copying full masks.
    internal static CoverageClip BorrowImmutable(int left,int top,int width,int height,byte[] coverage)=>new(left,top,width,height,coverage,true);
    private CoverageClip(int left,int top,int width,int height,byte[] coverage,bool immutable)
    {
        if(width<0||height<0||coverage.Length!=checked(width*height))throw new ArgumentException("Clip dimensions do not match coverage.");
        _=checked(left+width);_=checked(top+height);Left=left;Top=top;Width=width;Height=height;data=coverage;
    }
    public ReadOnlySpan<byte> GetRow(int y)=>y<Top||y>=Top+Height?ReadOnlySpan<byte>.Empty:data.AsSpan((y-Top)*Width,Width);
    public byte At(int x,int y)=>x<Left||y<Top||x>=Left+Width||y>=Top+Height?(byte)0:data[(y-Top)*Width+x-Left];
    public static CoverageClip Intersect(CoverageClip a,CoverageClip b)
    {
        int l=Math.Max(a.Left,b.Left),t=Math.Max(a.Top,b.Top),r=Math.Min(a.Left+a.Width,b.Left+b.Width),bottom=Math.Min(a.Top+a.Height,b.Top+b.Height);
        int w=r<=l?0:r-l,h=bottom<=t?0:bottom-t;
        if(w==0||h==0)return BorrowImmutable(l,t,w,h,[]);
        // A soft edge multiplied by itself is not the same edge. Only an
        // actually opaque covering rectangle makes its operand redundant.
        if(w==b.Width&&h==b.Height&&a.IsOpaqueOver(l,t,w,h))return b;
        if(w==a.Width&&h==a.Height&&b.IsOpaqueOver(l,t,w,h))return a;

        int length=checked(w*h);
        // Keep unreferenced masks collectible, including large-object-heap
        // results from old controls. Identity is sufficient for immutable clips.
        var slot=length>=85_000?ProductSlot.For(a,b):null;
        if(slot?.Find(a,b) is {} previous)return previous;
        var result=GC.AllocateUninitializedArray<byte>(length);
        for(int y=0;y<h;y++)
        {
            var first=a.GetRow(t+y).Slice(l-a.Left,w);
            var second=b.GetRow(t+y).Slice(l-b.Left,w);
            var dest=result.AsSpan(y*w,w);
            int x=0;
            if(Vector.IsHardwareAccelerated)
            {
                for(;x<=w-Vector<byte>.Count;x+=Vector<byte>.Count)
                {
                    Vector.Widen(new Vector<byte>(first[x..]),out var al,out var ah);
                    Vector.Widen(new Vector<byte>(second[x..]),out var bl,out var bh);
                    Vector.Narrow(Multiply(al,bl),Multiply(ah,bh)).CopyTo(dest[x..]);
                }
            }
            for(;x<w;x++)dest[x]=PixelBuffer.Multiply(first[x],second[x]);
        }
        var product=BorrowImmutable(l,t,w,h,result);
        slot?.Remember(a,b,product);
        return product;
    }

    private sealed class ProductSlot
    {
        private static readonly ProductSlot[] slots=[new(),new(),new(),new(),new(),new(),new(),new()];
        private readonly WeakReference<CoverageClip> first=new(null!),second=new(null!),result=new(null!);

        internal static ProductSlot For(CoverageClip a,CoverageClip b)
            =>slots[(RuntimeHelpers.GetHashCode(a)^RuntimeHelpers.GetHashCode(b))&(slots.Length-1)];

        internal CoverageClip? Find(CoverageClip a,CoverageClip b)
        {
            lock(this)
            {
                if(first.TryGetTarget(out var x)&&second.TryGetTarget(out var y)&&
                    ((x==a&&y==b)||(x==b&&y==a))&&result.TryGetTarget(out var product))return product;
                return null;
            }
        }

        internal void Remember(CoverageClip a,CoverageClip b,CoverageClip product)
        {
            lock(this){first.SetTarget(a);second.SetTarget(b);result.SetTarget(product);}
        }
    }

    private bool IsOpaqueOver(int left,int top,int width,int height)
    {
        for(int y=0;y<height;y++)
            if(GetRow(top+y).Slice(left-Left,width).ContainsAnyExcept((byte)255))return false;
        return true;
    }

    private static Vector<ushort> Multiply(Vector<ushort> a,Vector<ushort> b)
    {
        var rounded=a*b+new Vector<ushort>(128);
        return (rounded+(rounded>>8))>>8;
    }
}
