using System.Numerics;
using System.Runtime.CompilerServices;

namespace SimdSkia;

/// <summary>Immutable device-pixel coverage. The outer rectangle is half-open.</summary>
public sealed class CoverageClip
{
    private readonly byte[] data;
    private object? identity;
    // Device caches keep this token independently of the managed pixels.
    internal object CacheIdentity
    {
        get
        {
            if (Volatile.Read(ref identity) is { } existing) return existing;
            var token = new object();
            return Interlocked.CompareExchange(ref identity, token, null) ?? token;
        }
    }
    public int Left {get;}
    public int Top {get;}
    public int Width {get;}
    public int Height {get;}
    internal ReadOnlySpan<byte> StoredPixels => data;
    // A band of identical scanlines keeps one physical row. Coordinates exposed
    // to renderers and callers always refer to the full device-space mask.
    internal int RepeatStart { get; }
    internal int ExtraRows { get; }
    public RectF Bounds=>new(Left,Top,Left+Width,Top+Height);
    public CoverageClip(int left,int top,int width,int height,ReadOnlySpan<byte> coverage)
    {
        if(width<0||height<0||coverage.Length!=checked(width*height))throw new ArgumentException("Clip dimensions do not match coverage.");
        _=checked(left+width);_=checked(top+height);
        Left=left;Top=top;Width=width;Height=height;data=coverage.ToArray();
    }
    // Internal immutable raster masks have no caller-owned writable aliases.
    // Integer translations can retain their bytes without copying full masks.
    internal static CoverageClip BorrowImmutable(int left,int top,int width,int height,byte[] coverage)
        =>new(left,top,width,height,coverage,0,0);
    internal static CoverageClip BorrowRepeatedRows(int left,int top,int width,int height,byte[] coverage,int repeatStart,int extraRows)
        =>new(left,top,width,height,coverage,repeatStart,extraRows);
    private CoverageClip(int left,int top,int width,int height,byte[] coverage,int repeatStart,int extraRows)
    {
        if(width<0||height<0||repeatStart<0||extraRows<0||extraRows>0&&repeatStart>=(long)height-extraRows||
            coverage.Length!=checked(width*(height-extraRows)))throw new ArgumentException("Clip dimensions do not match coverage.");
        _=checked(left+width);_=checked(top+height);Left=left;Top=top;Width=width;Height=height;data=coverage;
        RepeatStart=repeatStart;ExtraRows=extraRows;
    }
    /// <summary>The stored bytes: masks that share them (integer translations)
    /// share one device upload.</summary>
    internal object StorageIdentity=>data;
    /// <summary>The same coverage moved by whole device pixels, without copying.</summary>
    internal CoverageClip Offset(int dx,int dy)=>dx==0&&dy==0?this:new(checked(Left+dx),checked(Top+dy),Width,Height,data,RepeatStart,ExtraRows);
    private int RowOffset(int row) => (ExtraRows == 0 ? row : row - Math.Clamp(row - RepeatStart, 0, ExtraRows)) * Width;
    public ReadOnlySpan<byte> GetRow(int y)=>y<Top||y>=Top+Height?ReadOnlySpan<byte>.Empty:data.AsSpan(RowOffset(y-Top),Width);
    public byte At(int x,int y)=>x<Left||y<Top||x>=Left+Width||y>=Top+Height?(byte)0:data[RowOffset(y-Top)+(x-Left)];
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
        int firstRepeated = Math.Max(t, Math.Max(a.Top + a.RepeatStart, b.Top + b.RepeatStart));
        int lastRepeated = Math.Min(bottom - 1, Math.Min(a.Top + a.RepeatStart + a.ExtraRows, b.Top + b.RepeatStart + b.ExtraRows));
        int extra = lastRepeated > firstRepeated ? lastRepeated - firstRepeated : 0;
        int start = extra == 0 ? 0 : firstRepeated - t;
        var result=GC.AllocateUninitializedArray<byte>(w*(h-extra));
        for(int row=0;row<h-extra;row++)
        {
            int y=t+row+(row>start?extra:0);
            var first=a.GetRow(y).Slice(l-a.Left,w);
            var second=b.GetRow(y).Slice(l-b.Left,w);
            var dest=result.AsSpan(row*w,w);
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
        var product=BorrowRepeatedRows(l,t,w,h,result,start,extra);
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

    internal bool IsOpaqueOver(int left,int top,int width,int height)
    {
        if (left < Left || top < Top || (long)left + width > Left + Width || (long)top + height > Top + Height)
            return false;
        int bandStart = Top + RepeatStart, bandEnd = bandStart + ExtraRows;
        for(int y=top;y<top+height;y++)
        {
            if(GetRow(y).Slice(left-Left,width).ContainsAnyExcept((byte)255))return false;
            if(y>=bandStart && y<bandEnd)y=bandEnd;
        }
        return true;
    }

    private static Vector<ushort> Multiply(Vector<ushort> a,Vector<ushort> b)
    {
        var rounded=a*b+new Vector<ushort>(128);
        return (rounded+(rounded>>8))>>8;
    }
}
