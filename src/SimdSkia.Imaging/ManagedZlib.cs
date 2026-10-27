using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Imaging;

/// <summary>Pure C# RFC 1950/1951 decoder. No System.IO.Compression/native zlib.</summary>
public static class ManagedZlib
{
    private static readonly int[] LengthBase=[3,4,5,6,7,8,9,10,11,13,15,17,19,23,27,31,35,43,51,59,67,83,99,115,131,163,195,227,258];
    private static readonly int[] LengthBits=[0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0];
    private static readonly int[] DistanceBase=[1,2,3,4,5,7,9,13,17,25,33,49,65,97,129,193,257,385,513,769,1025,1537,2049,3073,4097,6145,8193,12289,16385,24577];
    private static readonly int[] DistanceBits=[0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13];
    private static readonly Huffman FixedLiteral=new(Enumerable.Range(0,288).Select(i=>i<144?8:i<256?9:i<280?7:8).ToArray());
    private static readonly Huffman FixedDistance=new(Enumerable.Repeat(5,32).ToArray());
    public static byte[] Decode(ReadOnlySpan<byte> encoded,int expectedLength)
    {
        if(expectedLength<0||expectedLength>256*1024*1024)throw new NotSupportedException("Image inflation exceeds the configured 256 MiB limit.");
        var output=new byte[expectedLength];Decode(encoded,output);return output;
    }
    private sealed class OutputLimitException : Exception { }
    internal static void Decode(ReadOnlySpan<byte> encoded,Span<byte> output,bool prefix=false)
    {
        try{DecodeInto(encoded,output,prefix,true);}
        catch(OutputLimitException){throw new InvalidDataException("Inflation exceeds declared image size.");}
    }
    /// <summary>Inflates an unknown-length PDF stream with a strict output budget and checksum validation.</summary>
    public static byte[] DecodeBounded(ReadOnlySpan<byte> encoded,int maximumLength=64*1024*1024)
    {
        if(maximumLength<0||maximumLength>256*1024*1024)throw new ArgumentOutOfRangeException(nameof(maximumLength));
        int capacity=Math.Min(65536,maximumLength);
        while(true)
        {
            var output=new byte[capacity];
            try{int length=DecodeInto(encoded,output,false,false);return length==output.Length?output:output.AsSpan(0,length).ToArray();}
            catch(OutputLimitException){if(capacity==maximumLength)throw new NotSupportedException("Inflated stream exceeds the output budget.");capacity=(int)Math.Min(maximumLength,Math.Max(1L,capacity*2L));}
        }
    }
    private static int DecodeInto(ReadOnlySpan<byte> encoded,Span<byte> output,bool prefix,bool exact)
    {
        int expectedLength=output.Length;
        if(encoded.Length<6||(encoded[0]&15)!=8||(encoded[0]>>4)>7||((encoded[0]<<8)|encoded[1])%31!=0)throw new InvalidDataException("Invalid zlib header.");
        if((encoded[1]&32)!=0)throw new NotSupportedException("Preset DEFLATE dictionaries are not supported for PNG.");
        int window=1<<((encoded[0]>>4)+8);
        var reader=new Bits(encoded[2..^4]);int position=0;bool final;
        do
        {
            final=reader.Read(1)!=0;int kind=reader.Read(2);
            if(kind==0)
            {
                reader.Align();int size=reader.Read(16),inverse=reader.Read(16);if((size^inverse)!=65535)throw new InvalidDataException("Invalid stored DEFLATE block.");
                if(size>output.Length-position){if(prefix){reader.ReadBytes(output[position..]);return position;}throw new OutputLimitException();}
                reader.ReadBytes(output.Slice(position,size));position+=size;if(prefix&&position==output.Length)return position;continue;
            }
            Huffman literal,distance;
            if(kind==1)
            {
                literal=FixedLiteral;distance=FixedDistance;
            }
            else if(kind==2)
            {
                int literals=257+reader.Read(5),distances=1+reader.Read(5),codes=4+reader.Read(4);
                if(literals>286)throw new InvalidDataException("Invalid DEFLATE literal alphabet.");
                int[] order=[16,17,18,0,8,7,9,6,10,5,11,4,12,3,13,2,14,1,15];var codeLengths=new int[19];
                for(int i=0;i<codes;i++)codeLengths[order[i]]=reader.Read(3);
                var tree=new Huffman(codeLengths);var lengths=new int[literals+distances];int index=0;
                while(index<lengths.Length)
                {
                    int symbol=tree.Decode(ref reader);
                    if(symbol<16){lengths[index++]=symbol;continue;}
                    int value=0,repeat;
                    if(symbol==16){if(index==0)throw new InvalidDataException("DEFLATE repeat without prior length.");value=lengths[index-1];repeat=3+reader.Read(2);}
                    else if(symbol==17)repeat=3+reader.Read(3);else if(symbol==18)repeat=11+reader.Read(7);else throw new InvalidDataException("Invalid code-length symbol.");
                    if(repeat>lengths.Length-index)throw new InvalidDataException("DEFLATE length repeat overflow.");
                    while(repeat-->0)lengths[index++]=value;
                }
                if(lengths[256]==0)throw new InvalidDataException("Missing DEFLATE end-of-block symbol.");
                literal=new(lengths[..literals]);distance=new(lengths[literals..]);
            }
            else throw new InvalidDataException("Reserved DEFLATE block type.");
            while(true)
            {
                int symbol=literal.Decode(ref reader);if(symbol==256)break;
                if(symbol<256){if(position==output.Length){if(prefix)return position;throw new OutputLimitException();}output[position++]=(byte)symbol;if(prefix&&position==output.Length)return position;continue;}
                if(symbol>285)throw new InvalidDataException("Reserved length symbol.");
                int length=LengthBase[symbol-257]+reader.Read(LengthBits[symbol-257]);int ds=distance.Decode(ref reader);
                if(ds>=30)throw new InvalidDataException("Reserved distance symbol.");
                int offset=DistanceBase[ds]+reader.Read(DistanceBits[ds]);
                if(offset>window||offset>position)throw new InvalidDataException("Invalid DEFLATE back-reference.");
                if(length>output.Length-position){if(prefix)length=output.Length-position;else throw new OutputLimitException();}
                if(offset==1){output.Slice(position,length).Fill(output[position-1]);position+=length;if(prefix&&position==output.Length)return position;continue;}
                int copied=0;
                while(copied<length)
                {int count=Math.Min(offset+copied,length-copied);output.Slice(position-offset,count).CopyTo(output.Slice(position+copied,count));copied+=count;}
                position+=length;if(prefix&&position==output.Length)return position;
            }
        }while(!final);
        reader.Align();if(reader.BytePosition!=encoded.Length-6||(exact&&position!=expectedLength))throw new InvalidDataException("Unexpected DEFLATE length or trailing payload.");
        if(Adler(output[..position])!=BinaryPrimitives.ReadUInt32BigEndian(encoded[^4..]))throw new InvalidDataException("zlib Adler-32 mismatch.");
        return position;
    }
    public static byte[] EncodeStored(ReadOnlySpan<byte> data)
    {
        int blocks=Math.Max(1,(data.Length+65534)/65535);var output=new byte[checked(data.Length+blocks*5+6)];
        output[0]=0x78;output[1]=0x01;int position=0,destination=2;
        do
        {
            int size=Math.Min(65535,data.Length-position);bool last=position+size==data.Length;output[destination++]=last?(byte)1:(byte)0;
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(destination),(ushort)size);
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(destination+2),(ushort)(size^65535));destination+=4;
            data.Slice(position,size).CopyTo(output.AsSpan(destination));position+=size;destination+=size;
        }while(position<data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(destination),Adler(data));return output;
    }
    /// <summary>Bounded-window greedy LZ77 with RFC 1951 fixed Huffman codes, entirely managed.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> data,int searchDepth=32)
    {
        if(searchDepth is <1 or >256)throw new ArgumentOutOfRangeException(nameof(searchDepth));
        var heads=System.Buffers.ArrayPool<int>.Shared.Rent(65536);var previous=System.Buffers.ArrayPool<int>.Shared.Rent(32768);
        try
        {
            heads.AsSpan(0,65536).Fill(-1);var bits=new DeflateWriter();bits.Byte(0x78);bits.Byte(0x01);bits.Write(3,3); // final, fixed Huffman block
            int position=0;
            while(position<data.Length)
            {
                int length=0,distance=0;
                if(position<=data.Length-3)
                {
                    int hash=Hash(data,position),candidate=heads[hash];previous[position&32767]=candidate;heads[hash]=position;
                    int limit=Math.Min(258,data.Length-position),attempts=searchDepth;
                    while(candidate>=Math.Max(0,position-32768)&&attempts-->0)
                    {
                        if(candidate>=position)break;int count=0;
                        while(count<=limit-8&&BinaryPrimitives.ReadUInt64LittleEndian(data[(candidate+count)..])==BinaryPrimitives.ReadUInt64LittleEndian(data[(position+count)..]))count+=8;
                        while(count<limit&&data[candidate+count]==data[position+count])count++;
                        if(count>length&&count>=3){length=count;distance=position-candidate;if(count==limit)break;}
                        int next=previous[candidate&32767];if(next>=candidate)break;candidate=next;
                    }
                }
                if(length<3){bits.Symbol(data[position]);position++;continue;}
                int li=0;while(li<LengthBase.Length-1&&length>=LengthBase[li+1])li++;
                bits.Symbol(257+li);bits.Write(length-LengthBase[li],LengthBits[li]);
                int di=0;while(di<DistanceBase.Length-1&&distance>=DistanceBase[di+1])di++;
                bits.Write(Reverse(di,5),5);bits.Write(distance-DistanceBase[di],DistanceBits[di]);
                for(int i=1;i<length&&position+i<=data.Length-3;i++)
                {int hash=Hash(data,position+i);previous[(position+i)&32767]=heads[hash];heads[hash]=position+i;}
                position+=length;
            }
            bits.Symbol(256);bits.Align();uint checksum=Adler(data);bits.Byte((byte)(checksum>>24));bits.Byte((byte)(checksum>>16));bits.Byte((byte)(checksum>>8));bits.Byte((byte)checksum);
            var result=bits.Result();long storedLength=data.Length+Math.Max(1,(data.Length+65534L)/65535)*5+6;
            return result.Length<storedLength?result:EncodeStored(data);
        }
        finally{System.Buffers.ArrayPool<int>.Shared.Return(heads);System.Buffers.ArrayPool<int>.Shared.Return(previous);}
    }
    private static int Hash(ReadOnlySpan<byte> data,int p)=>(int)(unchecked(((uint)data[p]|((uint)data[p+1]<<8)|((uint)data[p+2]<<16))*2654435761u)>>16);
    private static int Reverse(int code,int length){int result=0;for(int i=0;i<length;i++){result=(result<<1)|(code&1);code>>=1;}return result;}
    private sealed class DeflateWriter
    {
        private readonly System.Buffers.ArrayBufferWriter<byte> output=new(16384);private uint buffer;private int count;
        internal void Byte(byte value){var span=output.GetSpan(1);span[0]=value;output.Advance(1);}
        internal void Write(int value,int length)
        {buffer|=(uint)value<<count;count+=length;while(count>=8){Byte((byte)buffer);buffer>>=8;count-=8;}}
        internal void Symbol(int symbol)
        {int length=symbol<144?8:symbol<256?9:symbol<280?7:8,code=symbol<144?0x30+symbol:symbol<256?0x190+symbol-144:symbol<280?symbol-256:0xc0+symbol-280;Write(Reverse(code,length),length);}
        internal void Align(){if(count!=0){Byte((byte)buffer);buffer=0;count=0;}}
        internal byte[] Result()=>output.WrittenSpan.ToArray();
    }
    private static uint Adler(ReadOnlySpan<byte> data)
    {
        uint a=1,b=0;
        while(!data.IsEmpty)
        {
            int count=Math.Min(5552,data.Length),at=0;
            if(Avx2.IsSupported&&count>=32)
            {
                var weights=Vector256.Create((sbyte)32,31,30,29,28,27,26,25,24,23,22,21,20,19,18,17,16,15,14,13,12,11,10,9,8,7,6,5,4,3,2,1);
                var sums=Vector256<ulong>.Zero;var prefix=Vector256<ulong>.Zero;var weighted=Vector256<int>.Zero;
                for(;at<=count-32;at+=32)
                {
                    var values=Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(data),(nuint)at);
                    prefix=Avx2.Add(prefix,sums);
                    sums=Avx2.Add(sums,Avx2.SumAbsoluteDifferences(values,Vector256<byte>.Zero).AsUInt64());
                    weighted=Avx2.Add(weighted,Avx2.MultiplyAddAdjacent(Avx2.MultiplyAddAdjacent(values,weights),Vector256.Create((short)1)));
                }
                ulong prior=prefix.GetElement(0)+prefix.GetElement(1)+prefix.GetElement(2)+prefix.GetElement(3);
                uint weight=0;for(int j=0;j<8;j++)weight+=unchecked((uint)weighted.GetElement(j));
                b+=unchecked((uint)((uint)at*a+32*prior+weight));
                a+=unchecked((uint)(sums.GetElement(0)+sums.GetElement(1)+sums.GetElement(2)+sums.GetElement(3)));
            }
            for(;at<count;at++){a+=data[at];b+=a;}
            a%=65521;b%=65521;data=data[count..];
        }
        return (b<<16)|a;
    }
    private ref struct Bits
    {
        private readonly ReadOnlySpan<byte> data;
        public Bits(ReadOnlySpan<byte> data){this.data=data;cursor=0;count=0;buffer=0;}
        private int cursor,count;
        private ulong buffer;
        public int BytePosition=>cursor-(count+7)/8;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(int requested,out int value)
        {
            if(count<requested&&cursor<=data.Length-4)
            {buffer|=(ulong)BinaryPrimitives.ReadUInt32LittleEndian(data[cursor..])<<count;cursor+=4;count+=32;}
            while(count<requested&&cursor<data.Length){buffer|=(ulong)data[cursor++]<<count;count+=8;}
            value=(int)(buffer&((1ul<<requested)-1));return count>=requested;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Drop(int amount){buffer>>=amount;count-=amount;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(int amount)
        {
            if(amount<0||amount>16||!TryPeek(amount,out int value))throw new InvalidDataException("Truncated DEFLATE bits.");
            Drop(amount);return value;
        }
        public void Align()=>Drop(count&7);
        public void ReadBytes(Span<byte> destination)
        {
            if((count&7)!=0)throw new InvalidDataException("Unaligned stored block.");
            int offset=0;while(count>=8&&offset<destination.Length)destination[offset++]=(byte)Read(8);
            int remaining=destination.Length-offset;
            if(remaining>data.Length-cursor)throw new InvalidDataException("Truncated stored block.");
            data.Slice(cursor,remaining).CopyTo(destination[offset..]);cursor+=remaining;
        }
    }
    private sealed class Huffman
    {
        private readonly int[] lookup=new int[512];
        private readonly int[] counts=new int[16],firstCode=new int[16],firstSymbol=new int[16],symbols;
        public Huffman(int[] lengths)
        {
            foreach(int length in lengths){if(length<0||length>15)throw new InvalidDataException("Invalid Huffman length.");if(length!=0)counts[length]++;}
            int code=0,index=0,left=1;
            for(int i=1;i<16;i++){left=left*2-counts[i];if(left<0)throw new InvalidDataException("Oversubscribed Huffman tree.");code=(code+counts[i-1])<<1;firstCode[i]=code;firstSymbol[i]=index;index+=counts[i];}
            symbols=new int[index];var positions=(int[])firstSymbol.Clone();for(int i=0;i<lengths.Length;i++)if(lengths[i]>0)symbols[positions[lengths[i]]++]=i;
            for(int length=1;length<=9;length++)for(int i=0;i<counts[length];i++)
            {
                int canonical=firstCode[length]+i,reverse=0;
                for(int bit=0;bit<length;bit++)reverse=(reverse<<1)|((canonical>>bit)&1);
                int entry=(symbols[firstSymbol[length]+i]<<4)|length;
                for(int j=reverse;j<lookup.Length;j+=1<<length)lookup[j]=entry;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decode(ref Bits bits)
        {
            if(bits.TryPeek(9,out int prefix)&&lookup[prefix]!=0){int entry=lookup[prefix];bits.Drop(entry&15);return entry>>4;}
            return DecodeLong(ref bits);
        }
        private int DecodeLong(ref Bits bits)
        {
            int code=0;for(int length=1;length<16;length++){code=(code<<1)|bits.Read(1);int offset=code-firstCode[length];if(offset>=0&&offset<counts[length])return symbols[firstSymbol[length]+offset];}
            throw new InvalidDataException("Invalid Huffman code.");
        }
    }
}
