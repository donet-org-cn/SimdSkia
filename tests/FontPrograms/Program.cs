using System.Buffers.Binary;
using SimdSkia.Text;
using SimdSkia.Text.Hinting;
int checks=0;void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
void Throws<T>(Action work,string name)where T:Exception{try{work();}catch(T){checks++;return;}throw new Exception(name);}
TrueTypeProgramInterpreter Vm(){var vm=new TrueTypeProgramInterpreter(32,8,4,0,8);vm.SetControlValueTable([],1,16,[]);return vm;}
var points=new[]{new GlyphPointF(.4f,.6f,true),new GlyphPointF(0,0,true),new GlyphPointF(0,0,true),new GlyphPointF(0,0,true),new GlyphPointF(0,0,true)};
// SVTCA[y], PUSHB point 0, MDAP[round]: a specified TrueType operation.
Vm().HintGlyph(points,[0],[0x00,0xb0,0x00,0x2f]);Check(points[0].Y==1&&points[0].X==.4f,"MDAP rounds only the projected axis");
Throws<InvalidTrueTypeFontException>(()=>Vm().HintGlyph((GlyphPointF[])points.Clone(),[0],[0x21]),"Stack underflow must fail");
Throws<IndexOutOfRangeException>(()=>Vm().HintGlyph((GlyphPointF[])points.Clone(),[0],[0,0xb0,255,0x2f]),"Point index remains bounds checked");
// PUSHW -3, JMPR returns to the push, with constant stack depth.
Throws<InvalidTrueTypeFontException>(()=>Vm().HintGlyph((GlyphPointF[])points.Clone(),[0],[0xb8,255,253,0x1c]),"Self-jumping program has a finite instruction budget");
var fixture=File.ReadAllBytes("baseline/fonts/ZCOOLXiaoWei-Regular.ttf");var font=new TrueTypeFont(fixture);ushort glyph=font.GetGlyph('A');var raw=font.Outline(glyph);var em=font.WindowsOutline(glyph);Check(raw.Segments[0].A.Y==-5&&Math.Abs(em.Segments[0].A.Y)<1e-4,"Independent original/dehinted oracle: em program adjusts baseline");Check(ReferenceEquals(em,font.WindowsOutline(glyph)),"Warm glyph retains immutable cached geometry");
// Replace the bytecode of the same simple glyph without changing its contours.
// A program that moves point 0 then reads CVT[-1] must leave the outline
// identical to a program that does nothing.
byte[] broken=(byte[])fixture.Clone(), noOp=(byte[])fixture.Clone();
var brokenProgram=GlyphProgram(broken,glyph);var noOpProgram=GlyphProgram(noOp,glyph);
brokenProgram.Fill(0x01);noOpProgram.Fill(0x01); // SVTCA[x], no point changes
byte[] invalid=[0x00,0xb1,0,64,0x48,0xb8,0xff,0xff,0x45]; // SCFS, then out-of-range RCVT
invalid.CopyTo(brokenProgram);
var fallbackFont=new TrueTypeFont(broken);var noHintFont=new TrueTypeFont(noOp);
var fallback=fallbackFont.WindowsOutline(glyph);
Check(fallback.Segments.SequenceEqual(noHintFont.WindowsOutline(glyph).Segments),"Invalid bytecode discards partially hinted points");
Check(ReferenceEquals(fallback,fallbackFont.WindowsOutline(glyph)),"Fallback outline is cached");
ushort next=font.GetGlyph('B');
Check(fallbackFont.WindowsOutline(next).Segments.SequenceEqual(noHintFont.WindowsOutline(next).Segments),"A bad glyph does not disable hinting on later glyphs");
var stable=em.Segments.ToArray();int limit=Math.Min(600,font.GlyphCount);
for(int i=0;i<limit;i++){var path=font.WindowsOutline((ushort)i);foreach(var s in path.Segments)Check(float.IsFinite(s.A.X)&&float.IsFinite(s.A.Y),"Finite em outline");}
var usage=font.CanonicalCacheUsage;Check(usage.Count<=256&&usage.Bytes<=4*1024*1024,"Cache count and bytes remain bounded");Check(stable.AsSpan().SequenceEqual(em.Segments),"Eviction never changes an existing outline snapshot");Check(font.WindowsOutline(glyph).Segments.SequenceEqual(stable),"Recomputed outline matches after eviction");
var rawStable=raw.Segments.ToArray();for(int i=0;i<Math.Min(1600,font.GlyphCount);i++){var outline=font.Outline((ushort)i);foreach(var s in outline.Segments)Check(float.IsFinite(s.A.X)&&float.IsFinite(s.A.Y),"Finite raw outline after bounded eviction");}
var rawUsage=font.RawCacheUsage;Check(rawUsage.Count<=512&&rawUsage.Bytes<=4*1024*1024,"Raw Chinese outline cache is bounded");Check(rawStable.AsSpan().SequenceEqual(raw.Segments),"Raw eviction does not mutate a live snapshot");Check(font.Outline(glyph).Segments.SequenceEqual(rawStable),"Raw reparse after eviction is identical");font.PurgeRawGlyphCache();Check(font.RawCacheUsage==(0,0),"Raw purge drops all cached payload");Check(font.Outline(glyph).Segments.SequenceEqual(rawStable),"Raw reparse after purge is identical");
checks+=ColorGlyphLayersValidation.Run();
Console.WriteLine($"PASS {checks} font program/spec/oracle/budget/cache checks; cache {usage.Count} glyphs, {usage.Bytes} bytes.");

static Span<byte> GlyphProgram(byte[] font,ushort glyph)
{
    int Table(ReadOnlySpan<byte> tag)
    {
        int count=BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for(int i=0;i<count;i++)
        {
            var entry=font.AsSpan(12+16*i,16);
            if(entry[..4].SequenceEqual(tag))return BinaryPrimitives.ReadInt32BigEndian(entry[8..]);
        }
        throw new InvalidDataException("Missing test font table.");
    }
    int loca=Table("loca"u8);
    bool shortOffsets=BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(Table("head"u8)+50))==0;
    int offset=shortOffsets ? 2*BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(loca+2*glyph)) :
        BinaryPrimitives.ReadInt32BigEndian(font.AsSpan(loca+4*glyph));
    int start=Table("glyf"u8)+offset;
    int contours=BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(start));
    if(contours<=0)throw new InvalidDataException("Expected a simple test glyph.");
    int at=start+10+2*contours;
    int length=BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at));
    return font.AsSpan(at+2,length);
}
