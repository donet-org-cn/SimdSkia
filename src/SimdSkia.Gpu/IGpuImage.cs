namespace SimdSkia.Gpu;
internal interface IGpuImage:IDisposable
{
    PixelBuffer ReadPixels();
}
