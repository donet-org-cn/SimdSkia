namespace SimdSkia.Cpu;
/// <summary>Immutable A8 transfer tables and blur styles. Color/alpha are never blurred into coverage.</summary>
public sealed partial class RasterMaskFilter : IMaskFilter
{
    private readonly byte[]? table;
    private readonly float sigma;
    private readonly int style;
    private readonly bool respectTransform;
    internal bool IsBlur=>table is null;
    private RasterMaskFilter(byte[] table)=>this.table=(byte[])table.Clone();
    private RasterMaskFilter(float sigma,int style,bool respectTransform){this.sigma=sigma;this.style=style;this.respectTransform=respectTransform;}
    public static RasterMaskFilter Table(byte[] table)
    {ArgumentNullException.ThrowIfNull(table);if(table.Length!=256)throw new ArgumentException("Expected 256 A8 transfer values.",nameof(table));return new(table);}
    public static RasterMaskFilter Blur(float sigma,int style,bool respectTransform)=>new(sigma,style,respectTransform);
    private float Sigma(Matrix3 m)
    {
        float value=sigma;
        if(respectTransform){double x=Math.Sqrt((double)m.M00*m.M00+(double)m.M10*m.M10),y=Math.Sqrt((double)m.M01*m.M01+(double)m.M11*m.M11);value*=(float)Math.Sqrt(x*y);}
        if(!float.IsFinite(value))throw new NotSupportedException("Device mask blur sigma is not finite.");return MathF.Min(value,128);
    }
    public int Margin(Matrix3 transform)=>table is null?(int)MathF.Ceiling(3*Sigma(transform)):0;
    public CoverageClip Filter(CoverageClip source,Matrix3 transform)
    {
        if(table is not null)
        {
            var bytes=new byte[checked(source.Width*source.Height)];
            for(int y=0;y<source.Height;y++){var row=source.GetRow(source.Top+y);for(int x=0;x<source.Width;x++)bytes[y*source.Width+x]=table[row[x]];}
            return new(source.Left,source.Top,source.Width,source.Height,bytes);
        }
        float s=Sigma(transform);int margin=Margin(transform);
        if(source.Width==0||source.Height==0)return source;
        if(3*s<=1)return style==2?new CoverageClip(source.Left,source.Top,0,0,[]):source;
        if(s<2)return ApplyStyle(source,SmallBlur(source,s));
        using var input=PixelBuffer.Rent(source.Width,source.Height);
        for(int y=0;y<source.Height;y++){var row=input.GetRowSpan(y);var coverage=source.GetRow(source.Top+y);for(int x=0;x<source.Width;x++){byte c=coverage[x];int p=x*4;row[p]=row[p+1]=row[p+2]=row[p+3]=c;}}
        using var frame=new FilterFrame(input.Share(),source.Left,source.Top);
        var bounds=new RectF(source.Left-margin,source.Top-margin,source.Left+source.Width+margin,source.Top+source.Height+margin);
        using var blurred=ManagedFilters.CompatibleBlur(frame,s,s,bounds);var image=blurred.Pixels!;
        var blurredCoverage=new byte[checked(image.Width*image.Height)];for(int y=0;y<image.Height;y++){var row=image.GetRowSpan(y);for(int x=0;x<image.Width;x++)blurredCoverage[y*image.Width+x]=row[x*4+3];}
        return ApplyStyle(source,new CoverageClip(blurred.Left,blurred.Top,image.Width,image.Height,blurredCoverage));
    }
    private CoverageClip ApplyStyle(CoverageClip source,CoverageClip blurred)
    {
        if(style==0)return blurred;
        int left=style==3?source.Left:blurred.Left,top=style==3?source.Top:blurred.Top,width=style==3?source.Width:blurred.Width,height=style==3?source.Height:blurred.Height;
        var result=new byte[checked(width*height)];
        for(int y=0;y<height;y++)
        {
            for(int x=0;x<width;x++)
            {
                byte b=blurred.At(left+x,top+y),original=source.At(left+x,top+y);
                result[y*width+x]=style switch{1=>(byte)(original+PixelBuffer.Multiply(b,(byte)(255-original))),2=>PixelBuffer.Multiply(b,(byte)(255-original)),3=>PixelBuffer.Multiply(b,original),_=>b};
            }
        }
        return new(left,top,width,height,result);
    }
}
