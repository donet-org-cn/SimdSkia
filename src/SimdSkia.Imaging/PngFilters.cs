using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Imaging;

/// <summary>Lossless PNG byte predictors. SIMD lanes are channels, never dependent pixels.</summary>
internal static class PngFilters
{
    public static void Reconstruct(Span<byte> line, ReadOnlySpan<byte> above, int bpp, int filter)
    {
        bool first = above.IsEmpty;
        if (filter == 0 || (filter == 2 && first)) return;
        if (filter == 1 || (filter == 4 && first))
        {
            Sub(line, bpp);
            return;
        }
        if (filter == 2)
        {
            int i = 0;
            if (Vector.IsHardwareAccelerated)
                for (; i <= line.Length - Vector<byte>.Count; i += Vector<byte>.Count)
                    (new Vector<byte>(line[i..]) + new Vector<byte>(above[i..])).CopyTo(line[i..]);
            for (; i < line.Length; i++) line[i] = unchecked((byte)(line[i] + above[i]));
            return;
        }
        if (filter == 3)
        {
            for (int i = 0; i < bpp; i++) line[i] = unchecked((byte)(line[i] + (first ? 0 : above[i]) / 2));
            if (first)
                for (int i = bpp; i < line.Length; i++) line[i] = unchecked((byte)(line[i] + line[i - bpp] / 2));
            else
                for (int i = bpp; i < line.Length; i++) line[i] = unchecked((byte)(line[i] + (line[i - bpp] + above[i]) / 2));
            return;
        }
        if (filter != 4) throw new InvalidDataException("Invalid PNG row filter.");
        for (int i = 0; i < bpp; i++) line[i] = unchecked((byte)(line[i] + above[i]));
        int at = bpp;
        if (Ssse3.IsSupported && bpp is 3 or 4 && line.Length >= bpp + 4)
        {
            ref byte row = ref MemoryMarshal.GetReference(line);
            ref byte prior = ref MemoryMarshal.GetReference(above);
            var left = Widen(Unsafe.ReadUnaligned<uint>(ref row));
            var corner = Widen(Unsafe.ReadUnaligned<uint>(ref prior));
            for (; at <= line.Length - 4; at += bpp)
            {
                var up = Widen(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref prior, at)));
                var low = Sse2.Min(left, up);var high = Sse2.Max(left, up);
                var threshold = Sse2.Subtract(Sse2.Add(Sse2.Add(corner, corner), corner), Sse2.Add(left, up));
                var chooseHigh = Sse2.CompareEqual(Sse2.Min(threshold, low), threshold);
                var chooseLow = Sse2.CompareEqual(Sse2.Max(threshold, high), threshold);
                var other = Sse2.Or(Sse2.And(chooseLow, low), Sse2.AndNot(chooseLow, corner));
                var predictor = Sse2.Or(Sse2.And(chooseHigh, high), Sse2.AndNot(chooseHigh, other));
                // Wrapping byte addition is essential; predictors are not saturating.
                var encoded = Vector128.CreateScalar(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, at))).AsByte();
                var decoded = Sse2.Add(encoded, Sse2.PackUnsignedSaturate(predictor, Vector128<short>.Zero));
                uint packed = decoded.AsUInt32().ToScalar();
                if (bpp == 4) Unsafe.WriteUnaligned(ref Unsafe.Add(ref row, at), packed);
                else
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref row, at), (ushort)packed);
                    line[at + 2] = (byte)(packed >> 16);
                }
                left = Widen(packed);
                corner = up;
            }
        }
        for (; at < line.Length; at++)
            line[at] = unchecked((byte)(line[at] + Paeth(line[at - bpp], above[at], above[at - bpp])));
    }

    private static void Sub(Span<byte> row, int bpp)
    {
        int at = 0;
        if (Sse2.IsSupported && bpp is 1 or 2 or 4)
        {
            var carry = Vector128<byte>.Zero;
            for (; at <= row.Length - 16; at += 16)
            {
                var value = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(row), (nuint)at);
                if (bpp == 1) value = Sse2.Add(value, Sse2.ShiftLeftLogical128BitLane(value, 1));
                if (bpp <= 2) value = Sse2.Add(value, Sse2.ShiftLeftLogical128BitLane(value, 2));
                value = Sse2.Add(value, Sse2.ShiftLeftLogical128BitLane(value, 4));
                value = Sse2.Add(value, Sse2.ShiftLeftLogical128BitLane(value, 8));
                value = Sse2.Add(value, carry);
                value.StoreUnsafe(ref MemoryMarshal.GetReference(row), (nuint)at);
                carry = bpp == 4 ? Vector128.Create(value.AsUInt32().GetElement(3)).AsByte()
                    : bpp == 2 ? Vector128.Create(value.AsUInt16().GetElement(7)).AsByte()
                    : Vector128.Create(value.GetElement(15));
            }
        }
        else if (Ssse3.IsSupported && bpp == 3)
        {
            var carry = Vector128<byte>.Zero;
            var repeat = Vector128.Create((byte)9,10,11,9,10,11,9,10,11,9,10,11,9,10,11,9);
            ref byte data = ref MemoryMarshal.GetReference(row);
            for (; at <= row.Length - 16; at += 12)
            {
                var value = Vector128.LoadUnsafe(ref data, (nuint)at);
                value = Sse2.Add(value, Sse2.ShiftLeftLogical128BitLane(value, 3));
                value = Sse2.Add(value, Sse2.ShiftLeftLogical128BitLane(value, 6));
                value = Sse2.Add(value, Sse2.ShiftLeftLogical128BitLane(value, 12));
                value = Sse2.Add(value, carry);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref data, at), value.AsUInt64().ToScalar());
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref data, at + 8), value.AsUInt32().GetElement(2));
                carry = Ssse3.Shuffle(value, repeat);
            }
        }
        for (int i = Math.Max(bpp, at); i < row.Length; i++) row[i] = unchecked((byte)(row[i] + row[i - bpp]));
    }

    public static void ExpandRgb(ReadOnlySpan<byte> rgb, Span<byte> rgba)
    {
        int source = 0, target = 0;
        if (Ssse3.IsSupported)
        {
            var shuffle = Vector128.Create((byte)0,1,2,128,3,4,5,128,6,7,8,128,9,10,11,128);
            var alpha = Vector128.Create(0xff000000u).AsByte();
            for (; source <= rgb.Length - 16; source += 12, target += 16)
                Sse2.Or(Ssse3.Shuffle(Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(rgb), (nuint)source), shuffle), alpha)
                    .StoreUnsafe(ref MemoryMarshal.GetReference(rgba), (nuint)target);
        }
        for (; source < rgb.Length; source += 3, target += 4)
        {rgba[target] = rgb[source];rgba[target + 1] = rgb[source + 1];rgba[target + 2] = rgb[source + 2];rgba[target + 3] = 255;}
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Widen(uint bytes) => Sse2.UnpackLow(Vector128.CreateScalar(bytes).AsByte(), Vector128<byte>.Zero).AsInt16();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Paeth(int a, int b, int c)
    {int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - 2 * c);return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;}
}
