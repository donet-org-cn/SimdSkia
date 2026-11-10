namespace SimdSkia.Cpu;
public sealed partial class RasterImageFilter
{
    // C# implementation of the baseline SkMagnifierImageFilter's fitted zoom
    // rectangle and rounded-corner distortion (Skia 7dbfc07, BSD-3-Clause).
    public static RasterImageFilter Magnifier(RectF lens,float zoom,float inset,ImageSampling sampling,RasterImageFilter? input=null,RectF? crop=null)
    {
        if(!lens.IsFinite||lens.IsEmpty||!float.IsFinite(zoom)||zoom<=0||!float.IsFinite(inset)||inset<0)throw new ArgumentOutOfRangeException(nameof(zoom));
        if(!Enum.IsDefined(sampling))throw new ArgumentOutOfRangeException(nameof(sampling));
        return new((source,m,clip)=>
        {
            if(m.M01!=0||m.M10!=0)throw new NotSupportedException("Magnifier currently requires scale and translation; rotated filter-layer mapping is not implemented.");
            using var original=Input(input,source,m,clip);using var cropped=crop is {} c?Color(IdentityColor,null,c).Apply(original,m,clip):null;var image=cropped??original;
            if(zoom<=1)return image.Share();if(image.Pixels is not {} pixels)return new(null);
            // The filter source's allocated layer can contain transparent pixels
            // outside the drawn geometry; those pixels still belong to the lens.
            var available=crop is {} cr?Intersect(original.TileBounds,Map(cr,m)):original.TileBounds;
            var bounds=Map(lens,m);var visible=Intersect(bounds,clip);var output=Intersect(visible,available);var result=FilterFrame.Rent(output);
            try
            {
                if(result.Pixels is not {} p)return result;
                float cx=Math.Clamp((bounds.Left+bounds.Right)*.5f,available.Left,available.Right),cy=Math.Clamp((bounds.Top+bounds.Bottom)*.5f,available.Top,available.Bottom);
                float sx=1/zoom,sy=sx,tx=cx*(1-sx),ty=cy*(1-sy);
                if(visible.Left<available.Left||visible.Top<available.Top||visible.Right>available.Right||visible.Bottom>available.Bottom)
                {
                    float w=(visible.Right-visible.Left)*sx,h=(visible.Bottom-visible.Top)*sy;
                    if(available.Right-available.Left>=w&&available.Bottom-available.Top>=h)
                    {
                        float left=Math.Clamp(visible.Left*sx+tx,available.Left,available.Right-w),top=Math.Clamp(visible.Top*sy+ty,available.Top,available.Bottom-h);
                        tx=left-visible.Left*sx;ty=top-visible.Top*sy;
                    }
                }
                float ix=MathF.Abs(m.M00)*inset,iy=MathF.Abs(m.M11)*inset;var sampler=new ImageColorShader(pixels,3,3,sampling);
                for(int y=0;y<p.Height;y++)
                {
                    var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++)
                    {
                        float px=x+result.Left+.5f,py=y+result.Top+.5f,weight=1;
                        if(ix>0&&iy>0)
                        {
                            float ex=MathF.Min(px-bounds.Left,bounds.Right-px)/ix,ey=MathF.Min(py-bounds.Top,bounds.Bottom-py)/iy;
                            weight=ex<2&&ey<2?2-MathF.Sqrt((2-ex)*(2-ex)+(2-ey)*(2-ey)):MathF.Min(ex,ey);weight=Math.Clamp(weight,0,1);weight*=weight;
                        }
                        float u=px+(tx+sx*px-px)*weight-image.Left,v=py+(ty+sy*py-py)*weight-image.Top;
                        Write(row,x,sampler.SamplePremultiplied(u,v));
                    }
                }
                return result;
            }
            catch{result.Dispose();throw;}
        },input);
    }
}
