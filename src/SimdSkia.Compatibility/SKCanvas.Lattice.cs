using SimdSkia;
namespace SkiaSharp;

public partial class SKCanvas
{
    public void DrawBitmapNinePatch(SKBitmap bitmap,SKRectI center,SKRect dst,SKPaint? paint=null)=>DrawBitmapNinePatch(bitmap,center,dst,SKFilterMode.Nearest,paint);
    public void DrawBitmapNinePatch(SKBitmap bitmap,SKRectI center,SKRect dst,SKFilterMode filterMode,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(bitmap);RequireSrgb(bitmap.ColorSpace);DrawNinePatch(bitmap.Buffer,center,dst,filterMode,paint);}
    public void DrawImageNinePatch(SKImage image,SKRectI center,SKRect dst,SKPaint? paint=null)=>DrawImageNinePatch(image,center,dst,SKFilterMode.Nearest,paint);
    public void DrawImageNinePatch(SKImage image,SKRectI center,SKRect dst,SKFilterMode filterMode=SKFilterMode.Nearest,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(image);RequireSrgb(image.ColorSpace);DrawNinePatch(image.Buffer,center,dst,filterMode,paint);}
    private void DrawNinePatch(PixelBuffer pixels,SKRectI center,SKRect dst,SKFilterMode filterMode,SKPaint? paint)
    {
        if(center.Left<0||center.Top<0||center.Right>pixels.Width||center.Bottom>pixels.Height||center.Right<center.Left||center.Bottom<center.Top)throw new ArgumentException("Center rectangle must be contained inside the image bounds.",nameof(center));
        DrawLattice(pixels,new(){XDivs=[center.Left,center.Right],YDivs=[center.Top,center.Bottom]},dst,filterMode,paint);
    }
    public void DrawBitmapLattice(SKBitmap bitmap,int[] xDivs,int[] yDivs,SKRect dst,SKPaint? paint=null)=>DrawBitmapLattice(bitmap,xDivs,yDivs,dst,SKFilterMode.Nearest,paint);
    public void DrawBitmapLattice(SKBitmap bitmap,int[] xDivs,int[] yDivs,SKRect dst,SKFilterMode filterMode,SKPaint? paint=null)=>DrawBitmapLattice(bitmap,new(){XDivs=xDivs,YDivs=yDivs},dst,filterMode,paint);
    public void DrawBitmapLattice(SKBitmap bitmap,SKLattice lattice,SKRect dst,SKPaint? paint=null)=>DrawBitmapLattice(bitmap,lattice,dst,SKFilterMode.Nearest,paint);
    public void DrawBitmapLattice(SKBitmap bitmap,SKLattice lattice,SKRect dst,SKFilterMode filterMode,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(bitmap);RequireSrgb(bitmap.ColorSpace);DrawLattice(bitmap.Buffer,lattice,dst,filterMode,paint);}
    public void DrawImageLattice(SKImage image,int[] xDivs,int[] yDivs,SKRect dst,SKPaint? paint=null)=>DrawImageLattice(image,xDivs,yDivs,dst,SKFilterMode.Nearest,paint);
    public void DrawImageLattice(SKImage image,int[] xDivs,int[] yDivs,SKRect dst,SKFilterMode filterMode,SKPaint? paint=null)=>DrawImageLattice(image,new(){XDivs=xDivs,YDivs=yDivs},dst,filterMode,paint);
    public void DrawImageLattice(SKImage image,SKLattice lattice,SKRect dst,SKPaint? paint=null)=>DrawImageLattice(image,lattice,dst,SKFilterMode.Nearest,paint);
    public void DrawImageLattice(SKImage image,SKLattice lattice,SKRect dst,SKFilterMode filterMode,SKPaint? paint=null)
    {ArgumentNullException.ThrowIfNull(image);RequireSrgb(image.ColorSpace);DrawLattice(image.Buffer,lattice,dst,filterMode,paint);}

    private void DrawLattice(PixelBuffer pixels,SKLattice lattice,SKRect dst,SKFilterMode filterMode,SKPaint? paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(lattice.XDivs);ArgumentNullException.ThrowIfNull(lattice.YDivs);
        if(paint?.Shader is not null)throw new NotSupportedException("Lattice image drawing does not accept a paint shader override.");
        if(!Enum.IsDefined(filterMode))throw new ArgumentOutOfRangeException(nameof(filterMode));
        if(!dst.ToCore().IsFinite)throw new ArgumentException("Destination must be finite.",nameof(dst));
        var bounds=lattice.Bounds??new SKRectI(0,0,pixels.Width,pixels.Height);
        if(bounds.Left<0||bounds.Top<0||bounds.Right>pixels.Width||bounds.Bottom>pixels.Height||bounds.Width<=0||bounds.Height<=0)throw new ArgumentException("Invalid lattice source bounds.",nameof(lattice));
        int nx=lattice.XDivs.Length+1,ny=lattice.YDivs.Length+1;
        if((long)nx*ny>65536)throw new NotSupportedException("Lattices are limited to 65536 cells.");
        if(lattice.RectTypes is {} types&&(types.Length<nx*ny||types.Any(t=>!Enum.IsDefined(t))))throw new ArgumentException("Invalid lattice cell types.",nameof(lattice));
        if(lattice.RectTypes?.Contains(SKLatticeRectType.FixedColor)==true&&(lattice.Colors is null||lattice.Colors.Length<nx*ny))throw new ArgumentException("Fixed color cells require one color per cell.",nameof(lattice));
        // Validate before the first destination mutation. Invalid div sequences in
        // Skia fall back to an ordinary image draw; valid zero-edge divs toggle
        // stretch/fixed parity without adding a visible cell.
        if(!Valid(lattice.XDivs,bounds.Left,bounds.Right)||!Valid(lattice.YDivs,bounds.Top,bounds.Bottom))
        {Execute(new DrawingCommand(CommandKind.DrawImage,dst.ToCore(),matrix,(paint?.ToCore()??new Paint(new(255,255,255,255),CompositeMode.SourceOver)) with {Antialias=false}){Image=pixels,ImageSource=new(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom),Sampling=filterMode==SKFilterMode.Linear?ImageSampling.Bilinear:ImageSampling.Nearest,NearestLowerBoundary=true,LatticeRectangleEdges=true},paint);return;}
        if(dst.Width<=0||dst.Height<=0)return;
        Span<float> sx=nx<=64?stackalloc float[nx+1]:new float[nx+1];
        Span<float> sy=ny<=64?stackalloc float[ny+1]:new float[ny+1];
        Span<float> dx=nx<=64?stackalloc float[nx+1]:new float[nx+1];
        Span<float> dy=ny<=64?stackalloc float[ny+1]:new float[ny+1];
        Axis(lattice.XDivs,bounds.Left,bounds.Right,dst.Left,dst.Right,sx,dx);
        Axis(lattice.YDivs,bounds.Top,bounds.Bottom,dst.Top,dst.Bottom,sy,dy);
        using var snapshot=pixels.GetPixelSpan().Overlaps(target.GetPixelSpan())?pixels.Clone():null;pixels=snapshot??pixels;
        using var solid=paint?.Clone()??new SKPaint();solid.Shader=null;solid.Style=SKPaintStyle.Fill;
        for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
        {
            if(sx[x]>=sx[x+1]||sy[y]>=sy[y+1]||dx[x]>=dx[x+1]||dy[y]>=dy[y+1])continue;
            var type=lattice.RectTypes?[y*nx+x]??SKLatticeRectType.Default;
            if(type==SKLatticeRectType.Transparent)continue;
            var destination=new SKRect(dx[x],dy[y],dx[x+1],dy[y+1]);
            if(type==SKLatticeRectType.FixedColor)
            {var color=lattice.Colors![y*nx+x];solid.Color=color.WithAlpha(PixelBuffer.Multiply(color.Alpha,paint?.Color.Alpha??255));DrawRect(destination,solid);}
            else Execute(new DrawingCommand(CommandKind.DrawImage,destination.ToCore(),matrix,(paint?.ToCore()??new Paint(new(255,255,255,255),CompositeMode.SourceOver)) with {Antialias=false}){Image=pixels,ImageSource=new(sx[x],sy[y],sx[x+1],sy[y+1]),Sampling=filterMode==SKFilterMode.Linear?ImageSampling.Bilinear:ImageSampling.Nearest,NearestLowerBoundary=true,LatticeRectangleEdges=true},paint);
        }
        static bool Valid(int[] divs,int start,int end)
        {int previous=start;for(int i=0;i<divs.Length;i++){int d=divs[i];if(d<previous||d>=end||i>0&&d==previous)return false;previous=d;}return true;}
        static void Axis(int[] divs,int start,int end,float left,float right,Span<float> source,Span<float> destination)
        {
            source[0]=start;for(int i=0;i<divs.Length;i++)source[i+1]=divs[i];source[^1]=end;
            float fixedSize=0;for(int i=0;i<source.Length-1;i+=2)fixedSize+=source[i+1]-source[i];
            float stretchSize=end-start-fixedSize,available=right-left;
            float fixedScale=available<fixedSize?available/fixedSize:1;
            float stretchScale=available>fixedSize&&stretchSize>0?(available-fixedSize)/stretchSize:0;
            if(divs.Length==0)fixedScale=available/(end-start);
            destination[0]=left;for(int i=0;i<source.Length-1;i++)destination[i+1]=destination[i]+(source[i+1]-source[i])*((i&1)==0?fixedScale:stretchScale);
            destination[^1]=right;
        }
    }
}
