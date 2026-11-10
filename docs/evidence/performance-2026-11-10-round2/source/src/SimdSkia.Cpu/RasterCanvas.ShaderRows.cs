namespace SimdSkia.Cpu;
public sealed partial class RasterCanvas
{
    private readonly record struct RasterRowKey(ImageColorShader Shader,Matrix3 Inverse,int Start,int End,int Row);
    private Dictionary<RasterRowKey,byte[]>? rasterRows;
    private long rasterRowBytes;
    private bool TryCopyRasterShader(in DrawingCommand command,Matrix3 inverse,int left,int top,int right,int bottom)
    {
        var paint=command.Paint;
        if(paint.Blender is not null||paint.Dither||paint.ColorFilter is not null||paint.Composite!=CompositeMode.Source||paint.Color.A!=255||target.Alpha==AlphaFormat.Straight||paint.Shader is not IImageShader raster||!raster.TryGetImage(out var image,out var local)||!image.IsNearest)return false;
        inverse=local*inverse;if(!inverse.IsAffine||inverse.M01!=0||inverse.M10!=0||!inverse.IsFinite)return false;
        int length=checked((right-left)*4);rasterRows??=[];
        for(int y=top;y<bottom;y++)
        {
            var key=new RasterRowKey(image,inverse,left,right,image.Row(inverse.M11*(y+.5f)+inverse.M12));
            if(!rasterRows.TryGetValue(key,out var bytes))
            {
                if(length>4*1024*1024)return false;
                bytes=new byte[length];image.FillNearestRow(bytes,target.Format,inverse,left,key.Row);
                if(rasterRows.Count>=64||rasterRowBytes+length>4*1024*1024){rasterRows.Clear();rasterRowBytes=0;}rasterRows.Add(key,bytes);rasterRowBytes+=length;
            }
            bytes.AsSpan().CopyTo(target.GetRowSpan(y).Slice(left*4,length));
        }
        return true;
    }
}
