using System.Buffers;
using System.Runtime.InteropServices;
using SimdSkia.Cpu;
namespace SkiaSharp;
public static unsafe class SKSwizzle
{
    public static void SwapRedBlue(Span<byte> pixels)
    {if((pixels.Length&3)!=0)throw new ArgumentException("Whole 32-bit pixels are required.",nameof(pixels));Swap(pixels,pixels);}
    public static void SwapRedBlue(ReadOnlySpan<byte> pixels,int count)=>SwapRedBlue(pixels,pixels,count);
    // The baseline labels the writable destination ReadOnlySpan. Preserve its
    // signature; callers must provide writable memory, just like the native API.
    public static void SwapRedBlue(ReadOnlySpan<byte> dest,ReadOnlySpan<byte> src,int count)
    {
        int bytes=checked(count*4);if(count<0||bytes>dest.Length||bytes>src.Length)throw new ArgumentOutOfRangeException(nameof(count));
        var writable=MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(dest),bytes);Swap(src[..bytes],writable);
    }
    public static void SwapRedBlue(IntPtr pixels,int count)=>SwapRedBlue(pixels,pixels,count);
    public static void SwapRedBlue(IntPtr dest,IntPtr src,int count)
    {
        if(dest==IntPtr.Zero)throw new ArgumentException(nameof(dest));if(src==IntPtr.Zero)throw new ArgumentException(nameof(src));
        if(count<0)throw new ArgumentOutOfRangeException(nameof(count));int bytes=checked(count*4);Swap(new ReadOnlySpan<byte>((void*)src,bytes),new Span<byte>((void*)dest,bytes));
    }
    private static void Swap(ReadOnlySpan<byte> src,Span<byte> dest)
    {
        if(src.Overlaps(dest,out int offset)&&offset!=0)
        {
            var temp=ArrayPool<byte>.Shared.Rent(src.Length);
            try{src.CopyTo(temp);PixelTransferKernel.SwapRedBlue(temp.AsSpan(0,src.Length),dest,CpuOptions.EnableSimd);}
            finally{ArrayPool<byte>.Shared.Return(temp);}
        }
        else PixelTransferKernel.SwapRedBlue(src,dest,CpuOptions.EnableSimd);
    }
}
