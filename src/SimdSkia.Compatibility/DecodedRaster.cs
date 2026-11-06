using SimdSkia;
using SimdSkia.Imaging;
namespace SkiaSharp;
internal static class DecodedRaster
{
    internal static (SKImageInfo Info,PixelBuffer Pixels) Read(ReadOnlySpan<byte> encoded)
    {
        using var decoded=ImageCodec.Decode(encoded);bool opaque=true;var bytes=decoded.GetPixelSpan();for(int i=3;i<bytes.Length;i+=4)if(bytes[i]!=255){opaque=false;break;}
        // A lazy native image reports header transparency even if its RGBA samples
        // all happen to be opaque. Do not infer that such a PNG has no alpha channel.
        ReadOnlySpan<byte> signature=[137,80,78,71,13,10,26,10];
        if(encoded.Length>=29&&encoded[..8].SequenceEqual(signature)&&encoded[25] is 4 or 6)opaque=false;
        for(int i=0;i<bytes.Length;i+=4)
        {
            byte r=bytes[i],g=bytes[i+1],b=bytes[i+2],a=bytes[i+3];
            if(!opaque){r=PixelBuffer.Multiply(r,a);g=PixelBuffer.Multiply(g,a);b=PixelBuffer.Multiply(b,a);}
            bytes[i]=b;bytes[i+1]=g;bytes[i+2]=r;
        }
        var alpha=opaque?SKAlphaType.Opaque:SKAlphaType.Premul;
        return (new(decoded.Width,decoded.Height,SKColorType.Bgra8888,alpha),decoded.Reinterpret(PixelFormat.Bgra8888,opaque?AlphaFormat.Opaque:AlphaFormat.Premultiplied));
    }
}
