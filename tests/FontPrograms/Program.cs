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
var stable=em.Segments.ToArray();int limit=Math.Min(600,font.GlyphCount);
for(int i=0;i<limit;i++){var path=font.WindowsOutline((ushort)i);foreach(var s in path.Segments)Check(float.IsFinite(s.A.X)&&float.IsFinite(s.A.Y),"Finite em outline");}
var usage=font.CanonicalCacheUsage;Check(usage.Count<=256&&usage.Bytes<=4*1024*1024,"Cache count and bytes remain bounded");Check(stable.AsSpan().SequenceEqual(em.Segments),"Eviction never changes an existing outline snapshot");Check(font.WindowsOutline(glyph).Segments.SequenceEqual(stable),"Recomputed outline matches after eviction");
var rawStable=raw.Segments.ToArray();for(int i=0;i<Math.Min(1600,font.GlyphCount);i++){var outline=font.Outline((ushort)i);foreach(var s in outline.Segments)Check(float.IsFinite(s.A.X)&&float.IsFinite(s.A.Y),"Finite raw outline after bounded eviction");}
var rawUsage=font.RawCacheUsage;Check(rawUsage.Count<=512&&rawUsage.Bytes<=4*1024*1024,"Raw Chinese outline cache is bounded");Check(rawStable.AsSpan().SequenceEqual(raw.Segments),"Raw eviction does not mutate a live snapshot");Check(font.Outline(glyph).Segments.SequenceEqual(rawStable),"Raw reparse after eviction is identical");font.PurgeRawGlyphCache();Check(font.RawCacheUsage==(0,0),"Raw purge drops all cached payload");Check(font.Outline(glyph).Segments.SequenceEqual(rawStable),"Raw reparse after purge is identical");
Console.WriteLine($"PASS {checks} font program/spec/oracle/budget/cache checks; cache {usage.Count} glyphs, {usage.Bytes} bytes.");
