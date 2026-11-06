namespace SkiaSharp;

public partial class SKSurface
{
    public SKPixmap PeekPixels(){ThrowIfDisposed();return gpuContext is null?bitmap.PeekPixels():null!;}
    public bool PeekPixels(SKPixmap pixmap){ThrowIfDisposed();return gpuContext is null&&bitmap.PeekPixels(pixmap);}
    public bool ReadPixels(SKImageInfo dstInfo,IntPtr dstPixels,int dstRowBytes,int srcX,int srcY)
    {
        ThrowIfDisposed();if(nullSurface)return false;var destination=PixelOperations.Wrap(dstInfo,dstPixels,dstRowBytes);
        bool wholePremultiplied=bitmap.AlphaType==SKAlphaType.Premul&&srcX==0&&srcY==0&&dstInfo.Width==bitmap.Width&&dstInfo.Height==bitmap.Height&&dstInfo.AlphaType==SKAlphaType.Premul&&(dstInfo.ColorSpace is null||dstInfo.ColorSpace.IsSrgb);
        if(wholePremultiplied&&dstInfo.ColorType==bitmap.ColorType)
        {
            if(vulkanCanvas is not null){vulkanCanvas.ReadTo(destination,dstRowBytes);return true;}
            if(importedVulkanCanvas?.ReadTo(destination,dstRowBytes)==true)return true;
        }
        if(glCanvas is not null){if(wholePremultiplied&&dstInfo.ColorType==SKColorType.Rgba8888&&glCanvas.ReadTo(dstPixels,dstRowBytes))return true;glCanvas.ReadTarget();}
        else if(gpuContext is not null)canvas.Flush();
        var source=bitmap.Buffer;return PixelOperations.Read(source.GetPixelSpan(),bitmap.Info,source.RowBytes,destination,dstInfo,dstRowBytes,srcX,srcY);
    }
}
