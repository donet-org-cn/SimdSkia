using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Avalonia.Media;
using SimdSkia.Avalonia;

internal static class FontDiscoveryValidation
{
    internal static int Run(string parent,byte[] fixture)
    {
        int checks=0;
        void Check(bool value,string description)
        {
            if(!value)throw new InvalidOperationException(description);
            checks++;
        }

        var dir=Directory.CreateDirectory(Path.Combine(parent,"cjk-culture")).FullName;
        var traditional=Path.Combine(dir,"00-zh-hant.ttf");
        var simplified=Path.Combine(dir,"01-zh-hans.ttf");
        var preferred=Path.Combine(dir,"02-interface.ttf");
        byte[] tc=WithName(fixture,3,1,0x409,Encoding.BigEndianUnicode.GetBytes("Heiti TC"));
        byte[] sc=WithName(fixture,3,1,0x409,Encoding.BigEndianUnicode.GetBytes("Heiti SC"));
        File.WriteAllBytes(traditional,tc);
        File.WriteAllBytes(simplified,sc);
        File.WriteAllBytes(preferred,WithName(fixture,3,1,0x409,Encoding.BigEndianUnicode.GetBytes("Fixture UI")));
        var hans=CultureInfo.GetCultureInfo("zh-CN");
        var hant=CultureInfo.GetCultureInfo("zh-HK");
        Check(UnixSystemFonts.Discover([dir],"Fixture UI",hans).Files.SequenceEqual([preferred,simplified]),
            "Simplified Chinese discovery must select the SC face even when TC is first.");
        Check(UnixSystemFonts.Discover([dir],"Fixture UI",hant).Files.SequenceEqual([preferred,traditional]),
            "Traditional Chinese discovery must select the TC face.");
        var manager=new SimdSkia.Avalonia.FontManager([traditional,simplified],"Heiti TC",supportedFacesOnly:true);
        Check(Match(manager,'中',null,hans)=="Heiti SC","Simplified fallback selects SC from an installed collection.");
        Check(Match(manager,'。',null,hant)=="Heiti TC","Traditional punctuation selects TC.");
        Check(Match(manager,'。',null,hans)=="Heiti SC","Simplified punctuation selects SC after a traditional request.");
        Check(Match(manager,'中',"Heiti TC",hans)=="Heiti TC","The explicitly requested family takes precedence.");
        Check(Match(manager,'A',null,hans)=="Heiti TC","CJK locale preference does not reorder Latin fallback.");

        var collections=Directory.CreateDirectory(Path.Combine(parent,"font-collections")).FullName;
        var path=Path.Combine(collections,"sample.ttc");
        byte[] mac=WithName(fixture,1,0,0,[(byte)'C',(byte)'a',(byte)'f',0x8e,(byte)' ',(byte)'S',(byte)'C']);
        byte[] unsupported=WithName(fixture,1,1,0,Encoding.ASCII.GetBytes("Unsupported"));
        byte[] ttc=BuildCollection(tc,unsupported,mac);
        File.WriteAllBytes(path,ttc);
        var discovery=UnixSystemFonts.Discover([collections],"Café SC",hans);
        Check(discovery.Files.SequenceEqual([path])&&discovery.DefaultFamily=="Café SC", "Mac Roman supports non-ASCII family names.");
        Check(discovery.Exclusions.Count==1&&discovery.Exclusions[0].Contains("face at"), "A face with no supported name does not exclude its TTC.");
        var fromTtc=new SimdSkia.Avalonia.FontManager([path],discovery.DefaultFamily,supportedFacesOnly:true);
        Check(fromTtc.GetInstalledFontFamilyNames().SequenceEqual(["Heiti TC","Café SC"]), "Valid faces on both sides of an invalid face remain enumerable.");
        Check(Match(fromTtc,'中',null,hans)=="Café SC", "The Mac Roman face loads through the lazy glyph typeface path.");
        string brokenOffset=Path.Combine(collections,"bad-offset.ttc");
        BinaryPrimitives.WriteUInt32BigEndian(ttc.AsSpan(16),uint.MaxValue);
        File.WriteAllBytes(brokenOffset,ttc);
        var partial=new SimdSkia.Avalonia.FontManager([brokenOffset],"Café SC",supportedFacesOnly:true);
        Check(partial.TryCreateGlyphTypeface("Heiti TC",FontStyle.Normal,FontWeight.Normal,FontStretch.Normal,out var face),
            "Invalid sibling offsets cannot prevent valid installed faces from loading.");
        using(face)
        {
            Check(face!.TryGetStream(out var standalone),"A valid face can be exported from a partially invalid TTC.");
            standalone!.Dispose();
        }
        return checks;
    }

    private static string? Match(SimdSkia.Avalonia.FontManager manager,int codepoint,string? family,CultureInfo culture)
    {
        if(!manager.TryMatchCharacter(codepoint,FontStyle.Normal,FontWeight.Normal,FontStretch.Normal,family,culture,out var match))return null;
        using(match)return match.FamilyName;
    }

    private static byte[] WithName(byte[] fixture,ushort platform,ushort encoding,ushort language,byte[] name)
    {
        var bytes=(byte[])fixture.Clone();
        int count=BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4));
        for(int index=0;index<count;index++)
        {
            var entry=bytes.AsSpan(12+index*16,16);
            if(!entry[..4].SequenceEqual("name"u8))continue;
            int offset=BinaryPrimitives.ReadInt32BigEndian(entry[8..]);
            int length=BinaryPrimitives.ReadInt32BigEndian(entry[12..]);
            if(length<18+name.Length)throw new InvalidOperationException("Fixture name table is too small.");
            var table=bytes.AsSpan(offset,length);
            table.Clear();
            BinaryPrimitives.WriteUInt16BigEndian(table[2..],1);
            BinaryPrimitives.WriteUInt16BigEndian(table[4..],18);
            BinaryPrimitives.WriteUInt16BigEndian(table[6..],platform);
            BinaryPrimitives.WriteUInt16BigEndian(table[8..],encoding);
            BinaryPrimitives.WriteUInt16BigEndian(table[10..],language);
            BinaryPrimitives.WriteUInt16BigEndian(table[12..],1);
            BinaryPrimitives.WriteUInt16BigEndian(table[14..],checked((ushort)name.Length));
            name.CopyTo(table[18..]);
            return bytes;
        }
        throw new InvalidOperationException("Fixture has no name table.");
    }

    private static byte[] BuildCollection(params byte[][] faces)
    {
        int first=12+faces.Length*4;
        var ttc=new byte[first+faces.Sum(face=>(face.Length+3)&~3)];
        "ttcf"u8.CopyTo(ttc);
        BinaryPrimitives.WriteUInt32BigEndian(ttc.AsSpan(4),0x10000);
        BinaryPrimitives.WriteInt32BigEndian(ttc.AsSpan(8),faces.Length);
        int at=first;
        for(int i=0;i<faces.Length;i++)
        {
            var face=faces[i];
            BinaryPrimitives.WriteInt32BigEndian(ttc.AsSpan(12+i*4),at);
            face.CopyTo(ttc,at);
            int count=BinaryPrimitives.ReadUInt16BigEndian(face.AsSpan(4));
            for(int table=0;table<count;table++)
            {
                int entry=12+16*table;
                int offset=BinaryPrimitives.ReadInt32BigEndian(face.AsSpan(entry+8));
                BinaryPrimitives.WriteInt32BigEndian(ttc.AsSpan(at+entry+8),at+offset);
            }
            at+=(face.Length+3)&~3;
        }
        return ttc;
    }
}
