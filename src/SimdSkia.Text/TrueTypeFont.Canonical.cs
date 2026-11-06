using System.Buffers.Binary;
using SimdSkia.Text.Hinting;
namespace SimdSkia.Text;

public sealed partial class TrueTypeFont
{
    private sealed record CanonicalGlyph(GlyphPointF[] Points,ushort[] Ends,PathGeometry Geometry,long Bytes);
    private readonly Dictionary<ushort,LinkedListNode<(ushort Id,CanonicalGlyph Data)>> canonicalGlyphs=[];
    private readonly LinkedList<(ushort Id,CanonicalGlyph Data)> canonicalOrder=[];
    private readonly object canonicalGate=new();
    private long canonicalBytes;
    private TrueTypeProgramInterpreter? canonicalInterpreter;
    private const long CanonicalBudget=4*1024*1024;
    internal (int Count,long Bytes) CanonicalCacheUsage {get{lock(canonicalGate)return(canonicalGlyphs.Count,canonicalBytes);}}
    internal void PurgeCanonicalGlyphCache(){lock(canonicalGate){canonicalGlyphs.Clear();canonicalOrder.Clear();canonicalBytes=0;canonicalInterpreter=null;}}

    /// <summary>DirectWrite-compatible em outline, evaluated entirely in C#.
    /// Raw design outlines remain available through Outline.</summary>
    internal PathGeometry WindowsOutline(ushort glyph)
    {
        if(glyph>=GlyphCount)throw new ArgumentOutOfRangeException(nameof(glyph));
        PathGeometry result;lock(canonicalGate)
        {if(canonicalGlyphs.TryGetValue(glyph,out var hit)){canonicalOrder.Remove(hit);canonicalOrder.AddLast(hit);return hit.Value.Data.Geometry;}result=Canonical(glyph,new HashSet<ushort>(),0).Geometry;}
        FontCacheRegistry.Notify(this);return result;
    }
    private int LeftBearing(ushort glyph)
    {int at=glyph<metrics?glyph*4+2:metrics*4+(glyph-metrics)*2;return BinaryPrimitives.ReadInt16BigEndian(horizontalMetrics.Span[at..]);}
    private TrueTypeProgramInterpreter CanonicalInterpreter()
    {
        if(canonicalInterpreter is not null)return canonicalInterpreter;
        _=Table("maxp",32);
        int U(int at)=>BinaryPrimitives.ReadUInt16BigEndian(bytes.Read(Table("maxp",32).Offset+at,2).Span);
        var vm=new TrueTypeProgramInterpreter(U(24),U(18),U(20),U(22),U(16));
        byte[] Program(string tag)
        {if(!tables.TryGetValue(tag,out var t))return [];if(t.Length>1024*1024)throw new NotSupportedException("Font program exceeds 1 MiB.");return bytes.Read(t.Offset,t.Length).ToArray();}
        var fpgm=Program("fpgm");if(fpgm.Length!=0)vm.InitializeFunctionDefs(fpgm);
        int[] cvt=[];
        if(tables.TryGetValue("cvt ",out var cv))
        {
            if(cv.Length%2!=0||cv.Length>1024*1024)throw new InvalidDataException("Invalid font control value table.");cvt=new int[cv.Length/2];var data=bytes.Read(cv.Offset,cv.Length).Span;for(int i=0;i<cvt.Length;i++)cvt[i]=BinaryPrimitives.ReadInt16BigEndian(data[(i*2)..]);
        }
        vm.SetControlValueTable(cvt,1,UnitsPerEm,Program("prep"));canonicalInterpreter=vm;return vm;
    }
    private CanonicalGlyph Canonical(ushort glyph,HashSet<ushort> active,int depth)
    {
        if(depth>32||!active.Add(glyph))throw new InvalidDataException("Cyclic/deep composite glyph.");
        try
        {
            if(canonicalGlyphs.TryGetValue(glyph,out var hit)){canonicalOrder.Remove(hit);canonicalOrder.AddLast(hit);return hit.Value.Data;}
            int length=offsets[glyph+1]-offsets[glyph];if(length==0)return new([],[],new([]),0);
            var reader=new GlyphReader(bytes.Read(Table("glyf",0).Offset+offsets[glyph],length).Span);int contourCount=reader.Short();int minX=reader.Short();reader.Short();reader.Short();int maxY=reader.Short();
            GlyphPointF[] points;ushort[] ends;byte[] instructions=[];int advance=Advance(glyph),bearing=LeftBearing(glyph);
            if(contourCount>=0)
            {
                if(contourCount==0)return new([],[],new([]),0);ends=new ushort[contourCount];
                for(int i=0;i<ends.Length;i++){ends[i]=reader.UShort();if(i>0&&ends[i]<=ends[i-1])throw new InvalidDataException("Invalid contour endpoints.");}
                int n=ends[^1]+1;instructions=reader.ReadBytes(reader.UShort());var flags=new byte[n];
                for(int i=0;i<n;i++){byte f=reader.Byte();flags[i]=f;if((f&8)!=0){int repeat=reader.Byte();if(repeat>=n-i)throw new InvalidDataException("Invalid repeated point flags.");while(repeat-->0)flags[++i]=f;}}
                points=new GlyphPointF[n];int x=0,y=0;
                for(int i=0;i<n;i++){byte f=flags[i];x+=(f&2)!=0?((f&16)!=0?reader.Byte():-reader.Byte()):(f&16)!=0?0:reader.Short();points[i]=new(x,0,(f&1)!=0);}
                for(int i=0;i<n;i++){byte f=flags[i];y+=(f&4)!=0?((f&32)!=0?reader.Byte():-reader.Byte()):(f&32)!=0?0:reader.Short();points[i].UpdateY(y);}
            }
            else
            {
                var all=new List<GlyphPointF>();var contours=new List<ushort>();int flags,components=0;
                do
                {
                    if(++components>4096)throw new NotSupportedException("Composite glyph exceeds the component budget.");
                    flags=reader.UShort();ushort child=reader.UShort();if(child>=GlyphCount)throw new InvalidDataException("Invalid component glyph.");bool words=(flags&1)!=0,xy=(flags&2)!=0;
                    int a=words?(xy?reader.Short():reader.UShort()):(xy?reader.SByte():reader.Byte()),b=words?(xy?reader.Short():reader.UShort()):(xy?reader.SByte():reader.Byte());
                    float xx=1,yy=1,yx=0,xyScale=0;
                    if((flags&8)!=0)xx=yy=reader.Short()/16384f;else if((flags&64)!=0){xx=reader.Short()/16384f;yy=reader.Short()/16384f;}else if((flags&128)!=0){xx=reader.Short()/16384f;xyScale=reader.Short()/16384f;yx=reader.Short()/16384f;yy=reader.Short()/16384f;}
                    var data=Canonical(child,active,depth+1);var transformed=new GlyphPointF[data.Points.Length];for(int i=0;i<transformed.Length;i++){var p=data.Points[i];transformed[i]=new(xx*p.X+xyScale*p.Y,yx*p.X+yy*p.Y,p.onCurve);}
                    float dx,dy;
                    if(xy){dx=a;dy=b;if((flags&2048)!=0){dx=xx*a+xyScale*b;dy=yx*a+yy*b;}}
                    else{if((uint)a>=(uint)all.Count||(uint)b>=(uint)transformed.Length)throw new InvalidDataException("Invalid composite attachment point.");dx=all[a].X-transformed[b].X;dy=all[a].Y-transformed[b].Y;}
                    if((flags&4)!=0){dx=MathF.Floor(dx+.5f);dy=MathF.Floor(dy+.5f);}int first=all.Count;if((long)first+transformed.Length>65535)throw new NotSupportedException("Composite glyph exceeds 65535 points.");
                    for(int i=0;i<transformed.Length;i++){var p=transformed[i];all.Add(new(p.X+dx,p.Y+dy,p.onCurve));}foreach(int end in data.Ends)contours.Add(checked((ushort)(first+end)));
                    if((flags&512)!=0){advance=Advance(child);bearing=LeftBearing(child);}
                }while((flags&32)!=0);
                if((flags&256)!=0)instructions=reader.ReadBytes(reader.UShort());points=all.ToArray();ends=contours.ToArray();
            }
            if(instructions.Length!=0)
            {
                var input=new GlyphPointF[points.Length+4];points.CopyTo(input,0);float left=minX-bearing;input[^4]=new(left,0,true);input[^3]=new(left+advance,0,true);input[^2]=new(0,maxY,true);input[^1]=input[^2];
                try{CanonicalInterpreter().HintGlyph(input,ends,instructions);}catch(Exception e)when(e is InvalidFontException or IndexOutOfRangeException or OverflowException or DivideByZeroException){throw new InvalidDataException($"Invalid TrueType program in glyph {glyph}.",e);}
                Array.Copy(input,points,points.Length);
            }
            foreach(var p in points)if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))throw new InvalidDataException("Font program produced nonfinite points.");
            var geometry=CanonicalPath(points,ends);long size=(long)geometry.Segments.Length*32+points.LongLength*12+ends.LongLength*2+64;var result=new CanonicalGlyph(points,ends,geometry,size);
            if(size<=CanonicalBudget&&Volatile.Read(ref FontCacheRegistry.ByteLimit)>0&&Volatile.Read(ref FontCacheRegistry.CountLimit)>0)
            {
                while(canonicalOrder.Count>=256||canonicalBytes+size>CanonicalBudget){var old=canonicalOrder.First!;canonicalBytes-=old.Value.Data.Bytes;canonicalGlyphs.Remove(old.Value.Id);canonicalOrder.RemoveFirst();}
                var node=canonicalOrder.AddLast((glyph,result));canonicalGlyphs.Add(glyph,node);canonicalBytes+=size;
            }
            return result;
        }
        finally{active.Remove(glyph);}
    }
    private PathGeometry CanonicalPath(GlyphPointF[] points,ushort[] ends)
    {
        var result=new List<PathSegment>();int first=0;PointF P(int i)=>new(points[i].X/UnitsPerEm,points[i].Y/UnitsPerEm);
        // SkFont's canonical path strike is 64 px. Reduction must happen at
        // that scale: doing it at design units changes zero-adjacent ULP tests.
        PointF Scale(PointF p)=>p*64;
        PointF Design(PointF p)=>p*(UnitsPerEm/64f);
        // Match the float geometry-sink conversion/reduction in Skia m119
        // SkDWriteGeometrySink.cpp (Google 2012, BSD-3), with no native calls.
        static bool Near(float a,float b)
        {
            if(float.IsNaN(a)||float.IsNaN(b))return false;uint x=BitConverter.SingleToUInt32Bits(a),y=BitConverter.SingleToUInt32Bits(b);
            x=(x&0x80000000)!=0?~x+1:x|0x80000000;y=(y&0x80000000)!=0?~y+1:y|0x80000000;return(x>y?x-y:y-x)<=10;
        }
        void Quad(PointF current,PointF q,PointF end)
        {
            var a=Scale(current);var b=Scale(current+(q-current)*(2f/3));var c=Scale(end+(q-end)*(2f/3));var d=Scale(end);
            var mid=new PointF(a.X+(b.X-a.X)*3/2,a.Y+(b.Y-a.Y)*3/2);var other=new PointF((c.X-d.X)*3/2+d.X,(c.Y-d.Y)*3/2+d.Y);
            if(Near(mid.X,other.X)&&Near(mid.Y,other.Y))result.Add(new(PathVerb.Quadratic,Design(mid),Design(d)));else result.Add(new(PathVerb.Cubic,Design(b),Design(c),Design(d)));
        }
        foreach(int end in ends)
        {
            int count=end-first+1,index;PointF start;
            if(points[first].onCurve){start=P(first);index=first+1;count--;}
            else if(points[end].onCurve){start=P(end);index=first;count--;}
            else{start=(P(first)+P(end))*.5f;index=first;}
            result.Add(new(PathVerb.Move,Design(Scale(start))));var current=start;
            while(count>0)
            {
                int next=index>end?first:index;var p=P(next);
                if(points[next].onCurve){if(current!=p)result.Add(new(PathVerb.Line,Design(Scale(p))));current=p;index=next+1;count--;}
                else{int following=next==end?first:next+1;var target=count==1?start:points[following].onCurve?P(following):(p+P(following))*.5f;Quad(current,p,target);current=target;index=following;count--;if(count>0&&points[following].onCurve){index=following+1;count--;}}
            }
            result.Add(new(PathVerb.Close,default));first=end+1;
        }
        return new(result);
    }
}
