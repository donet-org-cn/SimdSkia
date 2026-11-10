using System.Numerics;
namespace SimdSkia.Cpu;

/// <summary>Managed PMA filter graph. Local coordinates are mapped by the drawing CTM.</summary>
public sealed partial class RasterImageFilter : IImageFilter
{
    /// <summary>A managed drawing source. The callback owns a returned frame and
    /// must clip/rasterize at the requested transform, without native rendering.</summary>
    public static RasterImageFilter DrawingSource(Func<Matrix3,RectF,FilterFrame> draw)
    {ArgumentNullException.ThrowIfNull(draw);return new((source,m,clip)=>draw(m,clip));}
    private readonly Func<FilterFrame,Matrix3,RectF,FilterFrame> operation;
    private readonly int depth;
    private RasterImageFilter(Func<FilterFrame,Matrix3,RectF,FilterFrame> operation,params RasterImageFilter?[] inputs)
    {
        this.operation=operation;depth=1+inputs.Select(x=>x?.depth??0).DefaultIfEmpty().Max();
        if(depth>64)throw new NotSupportedException("Image-filter graph exceeds 64 levels.");
    }
    public FilterFrame Apply(FilterFrame source,Matrix3 transform,RectF outputClip)
    {
        ArgumentNullException.ThrowIfNull(source);if(!transform.IsFinite||!transform.IsAffine||!outputClip.IsFinite)throw new ArgumentException("Filters require finite affine coordinates.");
        return operation(source,transform,outputClip);
    }
    private static FilterFrame Input(RasterImageFilter? input,FilterFrame source,Matrix3 m,RectF clip)=>input?.Apply(source,m,clip)??source.Share();
    private static RectF Inflate(RectF b,float x,float y)=>b.IsEmpty?default:new(b.Left-x,b.Top-y,b.Right+x,b.Bottom+y);
    private static RectF Intersect(RectF a,RectF b)=>new(MathF.Max(a.Left,b.Left),MathF.Max(a.Top,b.Top),MathF.Min(a.Right,b.Right),MathF.Min(a.Bottom,b.Bottom));
    private static RectF Union(RectF a,RectF b)=>a.IsEmpty?b:b.IsEmpty?a:new(MathF.Min(a.Left,b.Left),MathF.Min(a.Top,b.Top),MathF.Max(a.Right,b.Right),MathF.Max(a.Bottom,b.Bottom));
    private static RectF Map(RectF b,Matrix3 m)
    {
        if(b.IsEmpty)return default;var a=m.Map(b.Left,b.Top);var c=m.Map(b.Right,b.Top);var d=m.Map(b.Left,b.Bottom);var e=m.Map(b.Right,b.Bottom);
        return new(MathF.Min(MathF.Min(a.X,c.X),MathF.Min(d.X,e.X)),MathF.Min(MathF.Min(a.Y,c.Y),MathF.Min(d.Y,e.Y)),MathF.Max(MathF.Max(a.X,c.X),MathF.Max(d.X,e.X)),MathF.Max(MathF.Max(a.Y,c.Y),MathF.Max(d.Y,e.Y)));
    }
    private static RectF Crop(RectF bounds,RectF? crop,Matrix3 m)=>crop is {} b?Intersect(bounds,Map(b,m)):bounds;
    private static Vector4 Read(FilterFrame source,int x,int y,int tile=3)
    {
        if(source.Pixels is not {} p)return default;
        if(tile!=3)
        {
            var b=source.TileBounds;int l=(int)MathF.Floor(b.Left),t=(int)MathF.Floor(b.Top),w=(int)MathF.Ceiling(b.Right)-l,h=(int)MathF.Ceiling(b.Bottom)-t;
            if(w<=0||h<=0)return default;x-=l;y-=t;Tile(ref x,w,tile);Tile(ref y,h,tile);x+=l;y+=t;
        }
        x-=source.Left;y-=source.Top;if((uint)x>=(uint)p.Width||(uint)y>=(uint)p.Height)return default;
        var row=p.GetRowSpan(y);int i=x*4,ri=p.Format==PixelFormat.Rgba8888?0:2;float a=p.Alpha==AlphaFormat.Opaque?1:row[i+3]/255f;
        var v=new Vector4(row[i+ri]/255f,row[i+1]/255f,row[i+2-ri]/255f,a);
        if(p.Alpha==AlphaFormat.Straight){v.X*=a;v.Y*=a;v.Z*=a;}return v;
    }
    private static bool Tile(ref int x,int n,int mode)
    {
        if((uint)x<(uint)n)return true;if(mode==3)return false;
        if(mode==0)x=Math.Clamp(x,0,n-1);else if(mode==1)x=(x%n+n)%n;
        else{int k=(x%(n*2)+n*2)%(n*2);x=k<n?k:n*2-1-k;}return true;
    }
    private static void Write(Span<byte> row,int x,Vector4 c)
    {int i=x*4;row[i]=ColorBlend.Byte(c.Z);row[i+1]=ColorBlend.Byte(c.Y);row[i+2]=ColorBlend.Byte(c.X);row[i+3]=ColorBlend.Byte(c.W);}
    private static void Copy(FilterFrame source,FilterFrame destination,CompositeMode blend=CompositeMode.SourceOver,Matrix3? transform=null,ImageSampling sampling=ImageSampling.Nearest)
    {
        if(source.Pixels is not {} p||destination.Pixels is not {} d)return;
        var tr=transform??Matrix3.Identity;
        if(tr.M00==1&&tr.M11==1&&tr.M01==0&&tr.M10==0&&tr.M02==MathF.Truncate(tr.M02)&&tr.M12==MathF.Truncate(tr.M12)&&p.Format==d.Format&&p.Alpha==AlphaFormat.Premultiplied&&d.Alpha==AlphaFormat.Premultiplied&&blend is CompositeMode.Source or CompositeMode.SourceOver)
        {
            int dx=checked(source.Left+(int)tr.M02-destination.Left),dy=checked(source.Top+(int)tr.M12-destination.Top);
            int l=Math.Max(0,dx),t=Math.Max(0,dy),r=(int)Math.Min(d.Width,(long)dx+p.Width),b=(int)Math.Min(d.Height,(long)dy+p.Height);
            if(l>=r||t>=b)return;
            for(int y=t;y<b;y++)
            {
                var from=p.GetRowSpan(y-dy).Slice((l-dx)*4,(r-l)*4);var to=d.GetRowSpan(y).Slice(l*4,(r-l)*4);
                if(blend==CompositeMode.Source)from.CopyTo(to);else ImageCompositor.SourceOver(from,to,255,false);
            }
            return;
        }
        var shift=new Matrix3(1,0,-destination.Left,0,1,-destination.Top,0,0,1);
        using var canvas=new RasterCanvas(d);canvas.Execute(new(CommandKind.DrawImage,source.Bounds,shift*(transform??Matrix3.Identity),new(new(255,255,255,255),blend,true)){Image=p,Sampling=sampling});
    }
    public static RasterImageFilter Color(IColorFilter color,RasterImageFilter? input=null,RectF? crop=null)
    {
        ArgumentNullException.ThrowIfNull(color);
        return new((source,m,clip)=>
        {
            using var a=Input(input,source,m,clip);bool changesTransparent=color.Filter(Vector4.Zero)!=Vector4.Zero;
            var bounds=changesTransparent?(crop is {} c?Map(c,m):clip):Crop(a.Bounds,crop,m);var result=FilterFrame.Rent(bounds);
            try{ColorRows(a,result,color);return result;}
            catch{result.Dispose();throw;}
        },input);
    }
    public static RasterImageFilter Compose(RasterImageFilter outer,RasterImageFilter inner)
    {ArgumentNullException.ThrowIfNull(outer);ArgumentNullException.ThrowIfNull(inner);return new((s,m,c)=>{using var a=inner.Apply(s,m,c);return outer.Apply(a,m,c);},outer,inner);}
    public static RasterImageFilter Offset(float dx,float dy,RasterImageFilter? input=null,RectF? crop=null)
    {
        if(!float.IsFinite(dx)||!float.IsFinite(dy))throw new ArgumentException("Offset must be finite.");
        return new((source,m,clip)=>
        {
            using var a=Input(input,source,m,clip);float x=m.M00*dx+m.M01*dy,y=m.M10*dx+m.M11*dy;
            var shift=new Matrix3(1,0,x,0,1,y,0,0,1);var result=FilterFrame.Rent(Crop(Map(a.Bounds,shift),crop,m));
            try{Copy(a,result,transform:shift,sampling:ImageSampling.Nearest);return result;}catch{result.Dispose();throw;}
        },input);
    }
    public static RasterImageFilter Matrix(Matrix3 matrix,ImageSampling sampling,RasterImageFilter? input=null)
    {
        if(!matrix.IsFinite||!matrix.IsAffine)throw new NotSupportedException("Image-filter matrix requires a finite affine transform.");
        return new((source,m,clip)=>
        {
            if(!m.TryInvert(out var inverse))return new(null);using var a=Input(input,source,m,clip);var transform=m*matrix*inverse;
            var result=FilterFrame.Rent(Map(a.Bounds,transform));try{Copy(a,result,transform:transform,sampling:sampling);return result;}catch{result.Dispose();throw;}
        },input);
    }
    public static RasterImageFilter Merge(RasterImageFilter?[] inputs,RectF? crop=null)
    {
        var nodes=inputs.ToArray();return new((source,m,clip)=>
        {
            var frames=new FilterFrame?[nodes.Length];FilterFrame? result=null;
            try{RectF bounds=default;for(int i=0;i<frames.Length;i++){frames[i]=Input(nodes[i],source,m,clip);bounds=Union(bounds,frames[i]!.Bounds);}result=FilterFrame.Rent(Crop(bounds,crop,m));foreach(var a in frames)Copy(a!,result);return result;}
            catch{result?.Dispose();throw;}finally{foreach(var a in frames)a?.Dispose();}
        },nodes);
    }
    public static RasterImageFilter Blend(Func<Vector4,Vector4,Vector4> blend,RasterImageFilter? background,RasterImageFilter? foreground,RectF? crop=null)
    {
        ArgumentNullException.ThrowIfNull(blend);return new((source,m,clip)=>
        {
            using var bg=Input(background,source,m,clip);using var fg=Input(foreground,source,m,clip);bool fills=blend(Vector4.Zero,Vector4.Zero)!=Vector4.Zero;
            var result=FilterFrame.Rent(fills?(crop is {} c?Map(c,m):clip):Crop(Union(bg.Bounds,fg.Bounds),crop,m));
            try{if(result.Pixels is {} p)for(int y=0;y<p.Height;y++){var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++)Write(row,x,blend(Read(fg,x+result.Left,y+result.Top),Read(bg,x+result.Left,y+result.Top)));}return result;}catch{result.Dispose();throw;}
        },background,foreground);
    }
    public static RasterImageFilter Blur(float sigmaX,float sigmaY,int tile,RasterImageFilter? input=null,RectF? crop=null)
    {
        ValidateSigma(sigmaX);ValidateSigma(sigmaY);if((uint)tile>3)throw new ArgumentOutOfRangeException(nameof(tile));
        return new((source,m,clip)=>
        {
            using var a=Input(input,source,m,clip);if(a.Pixels is null)return new(null);
            using var normalized=a.Pixels.Format!=PixelFormat.Bgra8888||a.Pixels.Alpha!=AlphaFormat.Premultiplied?Normalize(a):null;
            var image=normalized??a;
            float sx=MathF.Sqrt(m.M00*m.M00*sigmaX*sigmaX+m.M01*m.M01*sigmaY*sigmaY),sy=MathF.Sqrt(m.M10*m.M10*sigmaX*sigmaX+m.M11*m.M11*sigmaY*sigmaY);ValidateSigma(sx);ValidateSigma(sy);
            float rx=MathF.Ceiling(sx*3),ry=MathF.Ceiling(sy*3);var bounds=Crop(Inflate(a.Bounds,rx,ry),crop,m);
            if(tile==3)return ManagedFilters.CompatibleBlur(image,sx,sy,bounds);
            using var padded=FilterFrame.Rent(Inflate(bounds,rx,ry));if(padded.Pixels is not {} p)return new(null);
            if(tile==3)Copy(image,padded);
            else
            {
                using var tiled=new FilterFrame(image.Pixels?.Share(),image.Left,image.Top){TileBounds=crop is {} c?Map(c,m):image.TileBounds};
                for(int y=0;y<p.Height;y++){var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++)Write(row,x,Read(tiled,x+padded.Left,y+padded.Top,tile));}
            }
            using var filtered=new FilterFrame(ManagedFilters.CompatibleBlur(p,sx,sy),padded.Left,padded.Top);var result=FilterFrame.Rent(bounds);
            try{Copy(filtered,result);return result;}catch{result.Dispose();throw;}
        },input);
    }
    private static void ValidateSigma(float sigma){if(!float.IsFinite(sigma)||sigma<0||sigma>256)throw new ArgumentOutOfRangeException(nameof(sigma),"Managed Gaussian sigma supports 0..256 device pixels.");}
    private sealed class IdentityFilter:IColorFilter {public Vector4 Filter(Vector4 value)=>value;}
    private static readonly IColorFilter IdentityColor=new IdentityFilter();
    private static FilterFrame Normalize(FilterFrame source)
    {
        var allocated=FilterFrame.Rent(source.Bounds);
        try{ColorRows(source,allocated,IdentityColor);return new(allocated.Pixels?.Share(),allocated.Left,allocated.Top){TileBounds=source.TileBounds};}
        finally{allocated.Dispose();}
    }
    private static void ColorRows(FilterFrame source,FilterFrame destination,IColorFilter filter)
    {
        if(destination.Pixels is not {} target)return;
        static uint Pack(Vector4 c)=>(uint)(ColorBlend.Byte(c.Z)|(ColorBlend.Byte(c.Y)<<8)|(ColorBlend.Byte(c.X)<<16)|(ColorBlend.Byte(c.W)<<24));
        var transparent=filter.FilterRaster(Vector4.Zero);uint clear=Pack(transparent);
        if(clear!=0)for(int y=0;y<target.Height;y++)System.Runtime.InteropServices.MemoryMarshal.Cast<byte,uint>(target.GetRowSpan(y)).Fill(clear);
        if(source.Pixels is not {} input)return;
        if(input.Format!=PixelFormat.Bgra8888||input.Alpha!=AlphaFormat.Premultiplied)
        {for(int y=0;y<target.Height;y++){var row=target.GetRowSpan(y);for(int x=0;x<target.Width;x++)Write(row,x,filter.FilterRaster(Read(source,x+destination.Left,y+destination.Top)));}return;}
        int l=Math.Max(source.Left,destination.Left),t=Math.Max(source.Top,destination.Top),r=Math.Min(source.Left+input.Width,destination.Left+target.Width),b=Math.Min(source.Top+input.Height,destination.Top+target.Height);
        if(l>=r||t>=b)return;
        uint previous=0,filtered=clear;
        for(int y=t;y<b;y++)
        {
            var from=System.Runtime.InteropServices.MemoryMarshal.Cast<byte,uint>(input.GetRowSpan(y-source.Top)).Slice(l-source.Left,r-l);
            var to=System.Runtime.InteropServices.MemoryMarshal.Cast<byte,uint>(target.GetRowSpan(y-destination.Top)).Slice(l-destination.Left,r-l);
            for(int x=0;x<from.Length;)
            {
                uint value=from[x];int length=from[x..].IndexOfAnyExcept(value);if(length<0)length=from.Length-x;
                if(value!=previous){filtered=Pack(filter.FilterRaster(new((value>>16&255)/255f,(value>>8&255)/255f,(value&255)/255f,(value>>24)/255f)));previous=value;}
                to.Slice(x,length).Fill(filtered);x+=length;
            }
        }
    }
}
