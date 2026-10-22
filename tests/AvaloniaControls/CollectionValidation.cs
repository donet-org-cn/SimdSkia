using System.Buffers.Binary;
using System.Text;
using System.Runtime.InteropServices;
using SimdSkia.Text;

namespace SimdSkia.ControlValidation;

internal static class CollectionValidation
{
    internal static int Run(string path)
    {
        var data=File.ReadAllBytes(path);var original=new TrueTypeFont(data);int checks=0;
        void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);checks++;}
        int aligned=(data.Length+3)&~3;var ttc=new byte[32+aligned*2];
        void U32(int at,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(ttc.AsSpan(at),value);
        U32(0,0x74746366);U32(4,0x20000);U32(8,2);U32(12,32);U32(16,(uint)(32+aligned));
        int hhea=-1;
        for(int face=0;face<2;face++)
        {
            int start=32+face*aligned;data.CopyTo(ttc,start);int count=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
            for(int i=0;i<count;i++)
            {
                int entry=start+12+i*16;uint offset=BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan(entry+8));U32(entry+8,offset+(uint)start);
                if(face==1&&BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan(entry))==0x68686561)hhea=start+(int)offset;
            }
        }
        Check(hhea>=0,"Real font missing hhea.");BinaryPrimitives.WriteInt16BigEndian(ttc.AsSpan(hhea+4),(short)(original.Ascender+1));
        Check(FontCollection.Count(ttc)==2,"TTC face count.");
        string collectionFile=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"simdskia-collection-"+Guid.NewGuid().ToString("N")+".ttc");
        File.WriteAllBytes(collectionFile,ttc);
        try
        {
        var fileSource=FontSource.OpenInstalled(collectionFile,ttc.Length,File.GetLastWriteTimeUtc(collectionFile));
        for(int face=0;face<2;face++)
        {
            var selected=new TrueTypeFont(ttc,face);Check(selected.Ascender==original.Ascender+face,"TTC selected the wrong face metrics.");
            var shared=TrueTypeFont.FromOwnedCollection(ttc,face);
            Check(shared.Ascender==selected.Ascender,"Shared TTC selected the wrong face metrics.");
            Check(shared.TryGetTable("glyf",out var glyf)&&MemoryMarshal.TryGetArray(glyf,out var segment)&&ReferenceEquals(segment.Array,ttc),"Shared TTC duplicated its file storage.");
            using var stream=selected.OpenStream();var standalone=new byte[stream.Length];stream.ReadExactly(standalone);
            using var exported=shared.OpenStream();var exportedBytes=new byte[exported.Length];exported.ReadExactly(exportedBytes);
            Check(exportedBytes.AsSpan().SequenceEqual(standalone),"Shared TTC stream changed the standalone font bytes/checksums.");
            var paged=TrueTypeFont.FromInstalledSource(fileSource,face);
            using var pagedStream=paged.OpenStream();var pagedBytes=new byte[pagedStream.Length];pagedStream.ReadExactly(pagedBytes);
            Check(pagedBytes.AsSpan().SequenceEqual(standalone),"Paged TTC export changed face selection, bytes or checksums.");
            uint checksum=0;for(int i=0;i<standalone.Length;i+=4)checksum=unchecked(checksum+BinaryPrimitives.ReadUInt32BigEndian(standalone.AsSpan(i)));
            Check(checksum==0xb1b0afba,"Extracted sfnt checksum adjustment.");Check(FontCollection.Count(standalone)==1,"Extracted stream still a collection.");
            foreach(var rune in "中文界面测试，OCR AVATAR To 12345".EnumerateRunes())
            {
                ushort glyph=original.GetGlyph(rune.Value);Check(selected.GetGlyph(rune.Value)==glyph,"TTC cmap selection.");
                Check(selected.Advance(glyph)==original.Advance(glyph),"TTC advance.");Check(selected.Outline(glyph).Segments.SequenceEqual(original.Outline(glyph).Segments),"TTC altered real CJK/Latin outlines.");
                Check(shared.GetGlyph(rune.Value)==glyph&&shared.Advance(glyph)==selected.Advance(glyph)&&shared.Outline(glyph).Segments.SequenceEqual(selected.Outline(glyph).Segments),"Shared TTC changed CJK/Latin glyphs or metrics.");
                Check(paged.GetGlyph(rune.Value)==glyph&&paged.Advance(glyph)==selected.Advance(glyph)&&paged.Outline(glyph).Segments.SequenceEqual(selected.Outline(glyph).Segments),"Paged TTC changed CJK/Latin glyphs or metrics.");
            }
        }
        }
        finally{File.Delete(collectionFile);}
        foreach(int index in new[]{-1,2}){bool rejected=false;try{_ = new TrueTypeFont(ttc,index);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"TTC invalid face index.");}
        var invalid=(byte[])ttc.Clone();BinaryPrimitives.WriteUInt32BigEndian(invalid.AsSpan(12),uint.MaxValue);bool badOffset=false;try{_ = FontCollection.Count(invalid);}catch(InvalidDataException){badOffset=true;}Check(badOffset,"TTC offset outside file accepted.");
        return checks;
    }
}
