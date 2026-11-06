using System.Buffers.Binary;
namespace SimdSkia.Text;
public sealed partial class TrueTypeFont
{
    // OpenType EBLC/EBDT formats, implemented from Microsoft's public table
    // specification. Monochrome strike pixels become exact filled pixel runs;
    // no platform font renderer or native image library is involved.
    internal bool TryEmbeddedOutline(ushort glyph,float pixelsX,float pixelsY,out PathGeometry geometry)
    {
        geometry=null!;
        if(!tables.TryGetValue("EBLC",out var location)||!tables.TryGetValue("EBDT",out var bitmap)||pixelsX<1||pixelsY<1||pixelsX>255||pixelsY>255||pixelsX!=MathF.Round(pixelsX)||pixelsY!=MathF.Round(pixelsY))return false;
        ReadOnlyMemory<byte> Read((int Offset,int Length) table,int at,int count)
        {if(at<0||count<0||at>table.Length-count)throw new InvalidDataException("Embedded bitmap reference is outside its font table.");return bytes.Read(checked(table.Offset+at),count);}
        ushort U16((int Offset,int Length) table,int at)=>BinaryPrimitives.ReadUInt16BigEndian(Read(table,at,2).Span);
        uint U32((int Offset,int Length) table,int at)=>BinaryPrimitives.ReadUInt32BigEndian(Read(table,at,4).Span);
        int Offset((int Offset,int Length) table,int at){uint value=U32(table,at);if(value>int.MaxValue)throw new InvalidDataException("Invalid embedded bitmap offset.");return (int)value;}
        if(U32(location,0)!=0x20000||U32(bitmap,0)!=0x20000)throw new NotSupportedException("This embedded bitmap table version is not implemented.");
        int count=Offset(location,4);if(count>4096||8L+count*48>location.Length)throw new InvalidDataException("Invalid embedded bitmap strike count.");
        for(int strike=0;strike<count;strike++)
        {
            var size=Read(location,8+48*strike,48).Span;if(size[44]!=(int)pixelsX||size[45]!=(int)pixelsY||glyph<BinaryPrimitives.ReadUInt16BigEndian(size[40..])||glyph>BinaryPrimitives.ReadUInt16BigEndian(size[42..]))continue;
            if(size[46]!=1)throw new NotSupportedException("Grayscale embedded glyph strikes need a per-pixel coverage glyph representation; only monochrome EBDT is implemented.");
            if((size[47]&1)==0)throw new NotSupportedException("Vertical EBDT metrics are not implemented.");
            int list=checked((int)BinaryPrimitives.ReadUInt32BigEndian(size)),subtables=checked((int)BinaryPrimitives.ReadUInt32BigEndian(size[8..]));if(subtables<0||subtables>65536||list<0||(long)list+subtables*8>location.Length)throw new InvalidDataException("Invalid embedded bitmap index list.");
            for(int index=0;index<subtables;index++)
            {
                int item=list+index*8,first=U16(location,item),last=U16(location,item+2);if(last<first)throw new InvalidDataException("Invalid bitmap glyph range.");if(glyph<first||glyph>last)continue;
                int sub=checked(list+Offset(location,item+4)),format=U16(location,sub),imageFormat=U16(location,sub+2),data=Offset(location,sub+4),relative=glyph-first,start,end;ReadOnlyMemory<byte> implicitMetrics=default;
                switch(format)
                {
                    case 1:start=Offset(location,checked(sub+8+relative*4));end=Offset(location,checked(sub+12+relative*4));break;
                    case 3:start=U16(location,checked(sub+8+relative*2));end=U16(location,checked(sub+10+relative*2));break;
                    case 2:{int length=Offset(location,sub+8);start=checked(relative*length);end=checked(start+length);implicitMetrics=Read(location,sub+12,8);break;}
                    case 4:
                        int entries=Offset(location,sub+8);if(entries>65536)throw new InvalidDataException("Invalid sparse bitmap count.");start=end=-1;
                        for(int j=0;j<entries;j++)if(U16(location,checked(sub+12+j*4))==glyph){start=U16(location,sub+14+j*4);end=U16(location,sub+18+j*4);break;}if(start<0)return false;break;
                    case 5:
                        int imageSize=Offset(location,sub+8);implicitMetrics=Read(location,sub+12,8);int glyphs=Offset(location,sub+20);if(glyphs>65536)throw new InvalidDataException("Invalid constant bitmap glyph count.");start=end=-1;
                        for(int j=0;j<glyphs;j++)if(U16(location,checked(sub+24+j*2))==glyph){start=checked(j*imageSize);end=checked(start+imageSize);break;}if(start<0)return false;break;
                    default:throw new NotSupportedException($"EBLC index format {format} is not implemented.");
                }
                if(start==end)return false;if(start<0||end<start)throw new InvalidDataException("Invalid EBDT glyph extent.");var payload=Read(bitmap,checked(data+start),end-start);int metricBytes=imageFormat is 1 or 2?5:imageFormat is 6 or 7?8:imageFormat==5?0:throw new NotSupportedException($"EBDT image format {imageFormat} is not implemented.");
                var metrics=metricBytes==0?implicitMetrics.Span:payload.Span[..Math.Min(metricBytes,payload.Length)];if(metrics.Length<5||payload.Length<metricBytes)throw new InvalidDataException("Truncated embedded glyph metrics.");
                int height=metrics[0],width=metrics[1],bearingX=(sbyte)metrics[2],bearingY=(sbyte)metrics[3];bool aligned=imageFormat is 1 or 6;int rowBytes=(width+7)/8,needed=aligned?rowBytes*height:(width*height+7)/8;if(payload.Length-metricBytes<needed)throw new InvalidDataException("Truncated embedded glyph pixels.");
                var bits=payload.Span[metricBytes..];var segments=new List<PathSegment>();float sx=UnitsPerEm/pixelsX,sy=UnitsPerEm/pixelsY;
                for(int y=0;y<height;y++)for(int x=0;x<width;)
                {
                    int at=aligned?y*rowBytes*8+x:y*width+x;if((bits[at/8]&(128>>(at&7)))==0){x++;continue;}int endX=x+1;while(endX<width){int next=aligned?y*rowBytes*8+endX:y*width+endX;if((bits[next/8]&(128>>(next&7)))==0)break;endX++;}
                    float left=(bearingX+x)*sx,right=(bearingX+endX)*sx,top=(bearingY-y)*sy,bottom=(bearingY-y-1)*sy;segments.Add(new(PathVerb.Move,new(left,bottom)));segments.Add(new(PathVerb.Line,new(right,bottom)));segments.Add(new(PathVerb.Line,new(right,top)));segments.Add(new(PathVerb.Line,new(left,top)));segments.Add(new(PathVerb.Close,default));x=endX;
                }
                geometry=new(segments);return true;
            }
            return false;
        }
        return false;
    }
}
