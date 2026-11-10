using System.Buffers.Binary;
using SimdSkia.Avalonia;
using Avalonia.Media;

namespace SimdSkia.ControlValidation;

internal static class FontStreamValidation
{
    internal static int Run(string path)
    {
        int checks=0;
        void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);checks++;}
        var manager=new SimdSkia.Avalonia.FontManager([path]);
        byte[] bytes=File.ReadAllBytes(path);
        using var input=new MemoryStream(bytes,false);
        Check(manager.TryCreateGlyphTypeface(input,FontSimulations.None,out var first),"Registered stream font not found.");
        var font=((PlatformTypeface)first!).Data;
        input.Position=0;long start=GC.GetAllocatedBytesForCurrentThread();
        Check(manager.TryCreateGlyphTypeface(input,FontSimulations.None,out var second),"Repeated stream font not found.");
        long allocated=GC.GetAllocatedBytesForCurrentThread()-start;
        Check(allocated<512*1024,"Matching registered font allocated a font-sized snapshot.");
        Check(ReferenceEquals(((PlatformTypeface)second!).Data,font)&&input.Position==input.Length&&input.CanRead,"Font stream reuse changed ownership or consumption.");
        first!.Dispose();second!.Dispose();
        var prefixed=new byte[bytes.Length+17];bytes.CopyTo(prefixed,17);
        using var offset=new MemoryStream(prefixed,false);offset.Position=17;
        Check(manager.TryCreateGlyphTypeface(offset,FontSimulations.None,out var atOffset)&&ReferenceEquals(((PlatformTypeface)atOffset!).Data,font),"Seekable font stream ignored its starting position.");
        atOffset!.Dispose();
        using var sequential=new SequentialFontStream(new MemoryStream(bytes,false));
        Check(manager.TryCreateGlyphTypeface(sequential,FontSimulations.None,out var nonSeeking)&&ReferenceEquals(((PlatformTypeface)nonSeeking!).Data,font),"Non-seekable font stream failed to reuse registered data.");
        nonSeeking!.Dispose();
        int hhea=-1,count=BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4));
        for(int i=0;i<count;i++)if(bytes.AsSpan(12+i*16,4).SequenceEqual("hhea"u8))hhea=BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20+i*16));
        Check(hhea>=0,"Fixture lacks horizontal metrics.");
        short ascender=(short)(font.Font.Ascender+1);BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(hhea+4),ascender);
        using var changed=new MemoryStream(bytes,false);
        Check(manager.TryCreateGlyphTypeface(changed,FontSimulations.None,out var independent),"Unregistered seekable font was not snapshotted.");
        var retained=((PlatformTypeface)independent!).Data;
        bytes.AsSpan().Clear();
        Check(!ReferenceEquals(retained,font)&&retained.Font.Ascender==ascender&&changed.CanRead&&changed.Position==changed.Length,"New font snapshot aliases caller memory or changed stream lifetime.");
        independent!.Dispose();return checks;
    }
    private sealed class SequentialFontStream(Stream source):Stream
    {
        public override bool CanRead=>true;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();
        public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] buffer,int offset,int count)=>source.Read(buffer,offset,Math.Min(count,701));
        public override int Read(Span<byte> buffer)=>source.Read(buffer[..Math.Min(buffer.Length,701)]);
        public override void Flush()=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing)source.Dispose();base.Dispose(disposing);}
    }
}
