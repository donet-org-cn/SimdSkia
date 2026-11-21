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
        CheckSupplementaryFallback(parent,fixture,Check);
        return checks;
    }

    private static void CheckSupplementaryFallback(string parent,byte[] fixture,Action<bool,string> check)
    {
        var dir=Directory.CreateDirectory(Path.Combine(parent,"supplementary-fallback")).FullName;
        string Write(string file,string family,int codepoint,ushort glyph,bool format12=false)
        {
            string path=Path.Combine(dir,file);
            var bytes=WithName(fixture,3,1,0x409,Encoding.BigEndianUnicode.GetBytes(family));
            SetCharacterMap(bytes,codepoint,glyph,format12);
            File.WriteAllBytes(path,bytes);
            return path;
        }
        string primary=Write("00-ui.ttf","Fixture UI",'A',3);
        string noGlyph=Write("10-no-glyph.ttf","No Glyph",0x2713,0);
        string badGlyph=Write("11-bad-glyph.ttf","Bad Glyph",0x2713,65535);
        string disappears=Write("12-removed.ttf","Removed Symbols",0x2713,5);
        string symbol=Write("13-symbols.ttf","Fallback Symbols",0x2713,6);
        string music=Write("21-music.ttf","Fallback Music",0x1d11e,4,format12:true);
        string traditional=Write("30-tc.ttf","Fixture TC",'中',3);
        string simplified=Write("31-sc.ttf","Fixture SC",'中',4);
        var hans=CultureInfo.GetCultureInfo("zh-CN");
        var hant=CultureInfo.GetCultureInfo("zh-HK");
        var discovered=UnixSystemFonts.DiscoverWithFallbacks([dir],"Fixture UI",hans);
        check(discovered.Primary.Files.SequenceEqual([primary,simplified])&&discovered.Fallbacks.Contains(traditional),
            "Automatic system discovery retains supplementary sources outside the UI/CJK selection.");
        var manager=new SimdSkia.Avalonia.FontManager(discovered.Primary.Files,discovered.Primary.DefaultFamily,true,discovered.Fallbacks);
        check(!manager.SupplementaryIndexCreated&&manager.LoadedFaceCount==0,"Supplementary metadata is deferred at startup.");
        check(Match(manager,'A',"Fixture UI",hans)=="Fixture UI"&&!manager.SupplementaryIndexCreated,
            "Common primary glyphs do not initialize the supplementary index.");
        check(manager.GetInstalledFontFamilyNames().Contains("Fallback Music")&&manager.LoadedFaceCount==1,
            "Enumerating supplementary families does not load their outlines.");
        File.Delete(disappears);
        check(Match(manager,'\u2714',"Fixture UI",hans) is null&&manager.LoadedFaceCount==2,
            "A missing symbol does not open outlines of supplementary faces on the same page.");
        check(Match(manager,'\u2713',"Fixture UI",hans)=="Fallback Symbols"&&manager.LoadedFaceCount==3,
            "The format-4 glyph fallback skips zero glyphs, invalid indices and removed fonts.");
        check(Match(manager,0x1d11e,null,hans)=="Fallback Music"&&manager.LoadedFaceCount==4,
            "Supplementary Unicode format-12 glyphs resolve without eagerly loading other faces.");
        check(Match(manager,'中',null,hant)=="Fixture TC"&&Match(manager,'中',null,hans)=="Fixture SC",
            "Text culture can select a supplementary TC file even when SC was selected at startup.");
        check(Match(manager,'中',"Fixture TC",hans)=="Fixture TC","Explicit supplementary CJK fonts take priority.");
        check(manager.TryGetFamilyTypefaces("Fallback Symbols",out var styles)&&styles.Count==1,
            "Supplementary family styles are available from metadata.");
        check(manager.TryCreateGlyphTypeface("Fallback Symbols",FontStyle.Normal,FontWeight.Normal,FontStretch.Normal,out var direct),
            "Supplementary families are selectable directly.");
        using(direct)
        {
            check(direct!.TryGetStream(out var stream),"Fallback exports the original font.");
            using(stream) check(stream!.Length==fixture.Length,"Fallback exports the complete font.");
        }
        var explicitFiles=new SimdSkia.Avalonia.FontManager([primary]);
        check(Match(explicitFiles,'\u2713',null,hans) is null,"Explicit FontFiles remain isolated from the installed catalogue.");
    }

    private static void SetCharacterMap(byte[] font,int codepoint,ushort glyph,bool format12)
    {
        int count=BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for(int i=0;i<count;i++)
        {
            var entry=font.AsSpan(12+i*16,16);
            if(!entry[..4].SequenceEqual("cmap"u8))continue;
            int offset=(int)BinaryPrimitives.ReadUInt32BigEndian(entry[8..]);
            var table=font.AsSpan(offset,44);
            table.Clear();
            static void U16(Span<byte> data,int at,int value)=>BinaryPrimitives.WriteUInt16BigEndian(data[at..],(ushort)value);
            static void U32(Span<byte> data,int at,int value)=>BinaryPrimitives.WriteUInt32BigEndian(data[at..],(uint)value);
            U16(table,2,1);U16(table,4,3);U16(table,6,format12?10:1);U32(table,8,12);
            var sub=table[12..];
            if(format12)
            {
                U16(sub,0,12);U32(sub,4,28);U32(sub,12,1);
                U32(sub,16,codepoint);U32(sub,20,codepoint);U32(sub,24,glyph);
            }
            else
            {
                U16(sub,0,4);U16(sub,2,32);U16(sub,6,4);U16(sub,8,4);U16(sub,10,1);
                U16(sub,14,codepoint);U16(sub,16,65535);U16(sub,20,codepoint);U16(sub,22,65535);
                U16(sub,24,glyph-codepoint);U16(sub,26,1);
            }
            return;
        }
        throw new InvalidOperationException("Fixture has no cmap table.");
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
