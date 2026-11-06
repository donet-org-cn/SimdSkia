using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
namespace SimdSkia.Cpu;
public static partial class ManagedFilters
{
    private static void BoxAxisVector(PixelBuffer source,PixelBuffer destination,float sigma,bool vertical,int offset)
    {
        int window=Math.Max(1,(int)Math.Floor(sigma*3*Math.Sqrt(2*Math.PI)/4+.5));
        if(window==1)
        {
            if(vertical)
            {for(int y=0;y<destination.Height;y++)if((uint)(y+offset)<(uint)source.Height)source.GetRowSpan(y+offset)[..(source.Width*4)].CopyTo(destination.GetRowSpan(y));}
            else
            {
                int l=Math.Max(0,-offset),r=Math.Min(destination.Width,source.Width-offset);
                if(l<r)for(int y=0;y<source.Height;y++)source.GetRowSpan(y).Slice((l+offset)*4,(r-l)*4).CopyTo(destination.GetRowSpan(y).Slice(l*4));
            }
            return;
        }
        int passes=3,last=(window&1)==0?window+1:window;
        if(window>=255){window=3*window/2;passes=2;last=window;}
        int extra=(passes-1)*(window-1)+last-1,border=extra/2,n=vertical?source.Height:source.Width,lines=vertical?source.Width:source.Height,outputCount=vertical?destination.Height:destination.Width;
        uint divisor=1;for(int i=0;i<passes;i++)divisor*=(uint)(i==passes-1?last:window);
        ulong reciprocal=((1UL<<32)+divisor-1)/divisor;
        int ring0=window-1,ring1=window-1,ring2=passes==3?last-1:0,total=ring0+ring1+ring2;
        var ring=ArrayPool<Vector128<uint>>.Shared.Rent(total);
        var sourceData=source.GetPixelSpan();var destinationData=destination.GetPixelSpan();
        try
        {
            for(int line=0;line<lines;line++)
            {
                ring.AsSpan(0,total).Clear();int c0=0,c1=ring0,c2=ring0+ring1;
                var s0=Vector128<uint>.Zero;var s1=s0;var s2=s0;
                // All boxes advance together. The three short rings contain
                // trailing edges; no full scanline intermediate or early rounding.
                for(int i=0;i<n+extra;i++)
                {
                    var leading=Vector128<uint>.Zero;
                    if(i<n)
                    {
                        int at=vertical?i*source.RowBytes+line*4:line*source.RowBytes+i*4;uint pixel=BinaryPrimitives.ReadUInt32LittleEndian(sourceData.Slice(at,4));
                        leading=Vector128.WidenLower(Vector128.WidenLower(Vector128.CreateScalar(pixel).AsByte()));
                    }
                    s0+=leading;s1+=s0;if(passes==3)s2+=s1;
                    int output=i-border-offset;
                    if((uint)output<(uint)outputCount)
                    {
                        uint pixel=PackRounded(passes==3?s2:s1,divisor,(uint)reciprocal);
                        int at=vertical?output*destination.RowBytes+line*4:line*destination.RowBytes+output*4;BinaryPrimitives.WriteUInt32LittleEndian(destinationData.Slice(at,4),pixel);
                    }
                    if(passes==3){s2-=ring[c2];ring[c2]=s1;if(++c2==total)c2=ring0+ring1;}
                    s1-=ring[c1];ring[c1]=s0;if(++c1==ring0+ring1)c1=ring0;
                    s0-=ring[c0];ring[c0]=leading;if(++c0==ring0)c0=0;
                }
            }
        }
        finally{ArrayPool<Vector128<uint>>.Shared.Return(ring);}
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackRounded(Vector128<uint> value,uint divisor,uint reciprocal)
    {
        var numerator=value+Vector128.Create(divisor/2);
        if(Sse2.IsSupported)
        {
            var factor=Vector128.Create(reciprocal);
            var even=Sse2.ShiftRightLogical(Sse2.Multiply(numerator,factor),32).AsUInt32();
            var odd=Sse2.ShiftRightLogical(Sse2.Multiply(Sse2.ShiftRightLogical128BitLane(numerator.AsByte(),4).AsUInt32(),factor),32).AsUInt32();
            var quotient=Sse2.Or(even,Sse2.ShiftLeftLogical128BitLane(odd.AsByte(),4).AsUInt32());
            quotient-=Vector128.GreaterThan(quotient*Vector128.Create(divisor),numerator)&Vector128.Create(1u);
            return Vector128.Narrow(Vector128.Narrow(quotient,Vector128<uint>.Zero),Vector128<ushort>.Zero).AsUInt32().GetElement(0);
        }
        uint packed=0;
        for(int c=0;c<4;c++)
        {
            uint n=numerator.GetElement(c),q=(uint)((ulong)n*reciprocal>>32);
            if((ulong)q*divisor>n)q--;packed|=q<<(c*8);
        }
        return packed;
    }
}
