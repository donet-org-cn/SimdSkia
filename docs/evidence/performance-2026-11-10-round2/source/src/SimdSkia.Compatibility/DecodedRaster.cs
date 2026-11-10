using SimdSkia;
using SimdSkia.Imaging;
namespace SkiaSharp;
internal static class DecodedRaster
{
    internal static (SKImageInfo Info,PixelBuffer Pixels) Read(ReadOnlySpan<byte> encoded)
    {
        using var decoded=ImageCodec.Decode(encoded);var bytes=decoded.GetPixelSpan();bool allOpaque=SimdSkia.Cpu.PixelTransferKernel.IsOpaque(bytes,SimdSkia.Cpu.CpuOptions.EnableSimd),opaque=allOpaque;
        // A lazy native image reports header transparency even if its RGBA samples
        // all happen to be opaque. Do not infer that such a PNG has no alpha channel.
        ReadOnlySpan<byte> signature=[137,80,78,71,13,10,26,10];
        if(encoded.Length>=29&&encoded[..8].SequenceEqual(signature)&&encoded[25] is 4 or 6)opaque=false;
        if(allOpaque)SimdSkia.Cpu.PixelTransferKernel.SwapRedBlue(bytes,bytes,SimdSkia.Cpu.CpuOptions.EnableSimd);
        else SimdSkia.Cpu.PixelTransferKernel.Premultiply(bytes,true,SimdSkia.Cpu.CpuOptions.EnableSimd);
        var alpha=opaque?SKAlphaType.Opaque:SKAlphaType.Premul;
        return (new(decoded.Width,decoded.Height,SKColorType.Bgra8888,alpha),decoded.Reinterpret(PixelFormat.Bgra8888,opaque?AlphaFormat.Opaque:AlphaFormat.Premultiplied));
    }
}
