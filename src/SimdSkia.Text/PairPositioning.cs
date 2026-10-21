using System.Buffers.Binary;
using System.Numerics;

namespace SimdSkia.Text;

public readonly record struct GlyphPositionAdjustment(int XPlacement,int YPlacement,int XAdvance,int YAdvance)
{
    public static GlyphPositionAdjustment operator +(GlyphPositionAdjustment a,GlyphPositionAdjustment b)=>new(a.XPlacement+b.XPlacement,a.YPlacement+b.YPlacement,a.XAdvance+b.XAdvance,a.YAdvance+b.YAdvance);
}
public readonly record struct GlyphPairAdjustment(GlyphPositionAdjustment First,GlyphPositionAdjustment Second);

/// <summary>Default Latin/DFLT horizontal kern feature: GPOS pair formats 1/2 and legacy kern format 0.</summary>
internal sealed class PairPositioning
{
    private readonly ReadOnlyMemory<byte> gpos,kern;
    private readonly int[] lookups=[];
    private readonly bool hasGposKerning;
    internal PairPositioning(TrueTypeFont font)
    {
        font.TryGetTable("GPOS",out gpos);font.TryGetTable("kern",out kern);
        if(gpos.IsEmpty)return;
        var r=new Reader(gpos.Span);if(r.U16(0)!=1)throw new NotSupportedException("Unsupported GPOS version.");
        int scriptList=r.U16(4),features=r.U16(6),lookupList=r.U16(8);
        if(scriptList==0||features==0||lookupList==0)throw new InvalidDataException("Required GPOS offsets must be nonzero.");
        int script=r.Record(scriptList,0x6c61746e);if(script==0)script=r.Record(scriptList,0x44464c54);if(script==0)return;
        int defaultLanguage=r.U16(script);if(defaultLanguage==0)return;int language=checked(script+defaultLanguage);
        int required=r.U16(language+2),count=r.U16(language+4);var indices=new SortedSet<int>();
        for(int j=-1;j<count;j++)
        {
            int index=j<0?required:r.U16(language+6+j*2);if(index==65535)continue;
            if(index>=r.U16(features))throw new InvalidDataException("Invalid GPOS feature index.");
            int record=features+2+index*6;if(r.U32(record)!=0x6b65726e)continue;hasGposKerning=true;
            int feature=checked(features+r.U16(record+4));int n=r.U16(feature+2);
            for(int k=0;k<n;k++){int lookup=r.U16(feature+4+k*2);if(lookup>=r.U16(lookupList))throw new InvalidDataException("Invalid GPOS lookup index.");indices.Add(lookup);}
        }
        lookups=new int[indices.Count];int position=0;foreach(int index in indices)lookups[position++]=checked(lookupList+r.U16(lookupList+2+index*2));
    }
    internal GlyphPairAdjustment Get(ushort first,ushort second)
    {
        if(!hasGposKerning)return new(new(0,0,Legacy(first,second),0),default);
        var r=new Reader(gpos.Span);GlyphPositionAdjustment a=default,b=default;
        foreach(int lookup in lookups)
        {
            int type=r.U16(lookup),flags=r.U16(lookup+2),count=r.U16(lookup+4);
            // Basic shaping rejects combining/RTL text before reaching these lookups.
            if((flags&~8)!=0)throw new NotSupportedException("This kern lookup needs GDEF mark filtering or RTL positioning.");
            if(type is not (2 or 9))throw new NotSupportedException($"Kern lookup type {type} is not implemented.");
            for(int i=0;i<count;i++)
            {
                int table=checked(lookup+r.U16(lookup+6+i*2));
                if(type==9){if(r.U16(table)!=1||r.U16(table+2)!=2)throw new NotSupportedException("Only GPOS pair-position extension lookups are implemented.");table=checked(table+(int)r.U32(table+4));}
                if(!TryPair(ref r,table,first,second,out var adjustment))continue;
                a+=adjustment.First;b+=adjustment.Second;break; // Subtables within one lookup are alternatives.
            }
        }
        return new(a,b);
    }
    private static bool TryPair(ref Reader r,int table,ushort first,ushort second,out GlyphPairAdjustment pair)
    {
        pair=default;int coverage=Coverage(ref r,checked(table+r.U16(table+2)),first);if(coverage<0)return false;
        int format=r.U16(table),v1=r.U16(table+4),v2=r.U16(table+6),size1=ValueSize(v1),size2=ValueSize(v2);
        if(v2!=0)throw new NotSupportedException("Kern lookups that consume both glyphs require run-level GPOS processing.");
        int at;
        if(format==1)
        {
            if(coverage>=r.U16(table+8))throw new InvalidDataException("Invalid pair-set coverage.");
            int set=checked(table+r.U16(table+10+coverage*2)),count=r.U16(set),stride=checked(2+size1+size2);at=-1;int lo=0,hi=count-1;
            while(lo<=hi){int mid=lo+(hi-lo)/2,p=checked(set+2+mid*stride),value=r.U16(p);if(value<second)lo=mid+1;else if(value>second)hi=mid-1;else{at=p+2;break;}}
            if(at<0)return false;
        }
        else if(format==2)
        {
            int c1=Class(ref r,checked(table+r.U16(table+8)),first),c2=Class(ref r,checked(table+r.U16(table+10)),second),n1=r.U16(table+12),n2=r.U16(table+14);
            if(c1>=n1||c2>=n2)throw new InvalidDataException("Invalid pair class.");at=checked(table+16+(c1*n2+c2)*(size1+size2));
        }
        else throw new NotSupportedException($"Pair positioning format {format} is not implemented.");
        var a=Value(ref r,at,v1);var b=Value(ref r,checked(at+size1),v2);pair=new(a,b);return true;
    }
    private static int ValueSize(int format)
    {if((format&~255)!=0)throw new InvalidDataException("Reserved GPOS value flags.");return BitOperations.PopCount((uint)format)*2;}
    private static GlyphPositionAdjustment Value(ref Reader r,int at,int format)
    {
        int x=0,y=0,ax=0,ay=0;
        for(int bit=1;bit<=128;bit<<=1)if((format&bit)!=0)
        {
            int value=r.S16(at);at+=2;
            switch(bit){case 1:x=value;break;case 2:y=value;break;case 4:ax=value;break;case 8:ay=value;break;default:if(value!=0)throw new NotSupportedException("Device/variation adjustments in GPOS kern values are not implemented.");break;}
        }
        return new(x,y,ax,ay);
    }
    private static int Coverage(ref Reader r,int table,ushort glyph)
    {
        int format=r.U16(table),count=r.U16(table+2),lo=0,hi=count-1;
        if(format==1){while(lo<=hi){int mid=lo+(hi-lo)/2,value=r.U16(table+4+mid*2);if(value<glyph)lo=mid+1;else if(value>glyph)hi=mid-1;else return mid;}return -1;}
        if(format!=2)throw new NotSupportedException("Unknown GPOS coverage format.");
        while(lo<=hi){int mid=lo+(hi-lo)/2,p=table+4+mid*6,start=r.U16(p),end=r.U16(p+2);if(glyph<start)hi=mid-1;else if(glyph>end)lo=mid+1;else return r.U16(p+4)+glyph-start;}return -1;
    }
    private static int Class(ref Reader r,int table,ushort glyph)
    {
        int format=r.U16(table);
        if(format==1){int start=r.U16(table+2),count=r.U16(table+4);return glyph>=start&&glyph-start<count?r.U16(table+6+(glyph-start)*2):0;}
        if(format!=2)throw new NotSupportedException("Unknown GPOS class format.");
        int lo=0,hi=r.U16(table+2)-1;
        while(lo<=hi){int mid=lo+(hi-lo)/2,p=table+4+mid*6,start=r.U16(p),end=r.U16(p+2);if(glyph<start)hi=mid-1;else if(glyph>end)lo=mid+1;else return r.U16(p+4);}return 0;
    }
    private int Legacy(ushort first,ushort second)
    {
        if(kern.IsEmpty)return 0;var r=new Reader(kern.Span);if(r.U16(0)!=0)throw new NotSupportedException("Apple kern tables are not implemented.");
        int at=4,result=0;uint key=((uint)first<<16)|second;
        for(int i=0,count=r.U16(2);i<count;i++)
        {
            int length=r.U16(at+2),flags=r.U16(at+4);if(length<6)throw new InvalidDataException("Invalid kern subtable length.");r.Check(at,length);
            if((flags&1)!=0)
            {
                if((flags>>8)!=0||(flags&6)!=0)throw new NotSupportedException("Only horizontal format-0 kern pairs are implemented.");
                int pairs=r.U16(at+6);if(14L+pairs*6>length)throw new InvalidDataException("Truncated kern pairs.");int lo=0,hi=pairs-1;
                while(lo<=hi){int mid=lo+(hi-lo)/2,p=at+14+mid*6;uint value=r.U32(p);if(value<key)lo=mid+1;else if(value>key)hi=mid-1;else{int k=r.S16(p+4);result=(flags&8)!=0?k:result+k;break;}}
            }
            at=checked(at+length);
        }
        return result;
    }
    private readonly ref struct Reader
    {
        private readonly ReadOnlySpan<byte> data;
        internal Reader(ReadOnlySpan<byte> data)=>this.data=data;
        internal void Check(int offset,int length){if(offset<0||length<0||offset>data.Length-length)throw new InvalidDataException("OpenType positioning table offset exceeds its bounds.");}
        internal int U16(int offset){Check(offset,2);return BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);}
        internal int S16(int offset){Check(offset,2);return BinaryPrimitives.ReadInt16BigEndian(data[offset..]);}
        internal uint U32(int offset){Check(offset,4);return BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);}
        internal int Record(int list,uint tag)
        {int count=U16(list);for(int i=0;i<count;i++){int p=checked(list+2+i*6);if(U32(p)==tag)return checked(list+U16(p+4));}return 0;}
    }
}
