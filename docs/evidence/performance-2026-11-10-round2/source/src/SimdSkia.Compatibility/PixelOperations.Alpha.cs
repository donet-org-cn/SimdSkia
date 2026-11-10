using System.Buffers;
namespace SkiaSharp;
internal static partial class AlphaPixelOperations
{
    internal static bool Transfer(ReadOnlySpan<byte> source,SKImageInfo srcInfo,int srcStride,Span<byte> destination,SKImageInfo dstInfo,int dstStride,int sx,int sy,int dx,int dy,int width,int height)
    {
        byte[]? rented=null;if(source.Overlaps(destination)){rented=ArrayPool<byte>.Shared.Rent(source.Length);source.CopyTo(rented);source=rented.AsSpan(0,source.Length);}
        try
        {
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int from=(sy+y)*srcStride+(sx+x)*srcInfo.BytesPerPixel,to=(dy+y)*dstStride+(dx+x)*dstInfo.BytesPerPixel;
                var color=PixelOperations.ReadColorF(source,from,srcInfo);PixelOperations.WriteColorF(destination,to,dstInfo,new(color.Red,color.Green,color.Blue,color.Alpha));
            }
            return true;
        }
        finally{if(rented is not null)ArrayPool<byte>.Shared.Return(rented);}
    }
}
