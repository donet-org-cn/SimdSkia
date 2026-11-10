using SimdSkia;
namespace SkiaSharp;
public partial class SKBitmap
{
    public bool ExtractAlpha(SKBitmap destination)=>ExtractAlpha(destination,null!,out _);
    public bool ExtractAlpha(SKBitmap destination,out SKPointI offset)=>ExtractAlpha(destination,null!,out offset);
    public bool ExtractAlpha(SKBitmap destination,SKPaint paint)=>ExtractAlpha(destination,paint,out _);
    public bool ExtractAlpha(SKBitmap destination,SKPaint? paint,out SKPointI offset)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(destination);destination.ThrowIfDisposed();paint?.ThrowIfDisposed();offset=default;
        if(buffer is null||info.IsEmpty)return false;
        if((long)Width*Height>16_777_216)throw new NotSupportedException("Alpha extraction exceeds the 16 megapixel mask budget.");
        var alpha=new byte[checked(Width*Height)];for(int y=0;y<Height;y++){var row=buffer.GetRowSpan(y);for(int x=0;x<Width;x++)alpha[y*Width+x]=row[x*BytesPerPixel+BytesPerPixel-1];}
        var filter=paint?.ToCore(true).MaskFilter;
        var mask=new CoverageClip(0,0,Width,Height,alpha);if(filter is not null)mask=filter.Filter(mask,Matrix3.Identity);
        if(mask.Width==0||mask.Height==0)return false;
        int stride=filter is SimdSkia.Cpu.RasterMaskFilter {IsBlur:true}?mask.Width:checked((mask.Width+3)&~3);var storage=new PixelBuffer(mask.Width,mask.Height,PixelFormat.Alpha8,AlphaFormat.Premultiplied,stride);
        try{for(int y=0;y<mask.Height;y++)mask.GetRow(mask.Top+y).CopyTo(storage.GetRowSpan(y));destination.ReplacePixels(new(mask.Width,mask.Height,SKColorType.Alpha8,SKAlphaType.Premul),storage);offset=new(mask.Left,mask.Top);return true;}
        catch{storage.Dispose();throw;}
    }
}
