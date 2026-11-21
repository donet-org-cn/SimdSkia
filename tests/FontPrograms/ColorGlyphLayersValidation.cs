using System.Buffers.Binary;
using SimdSkia;
using SimdSkia.Text;

internal static class ColorGlyphLayersValidation
{
    internal static int Run()
    {
        // One base glyph, with a palette-colored back layer and an explicit
        // foreground top layer. Palette zero begins at the second BGRA record.
        const int colrSize=28,cpalSize=26;
        var bytes=new byte[colrSize+cpalSize];
        Put16(2,1);Put32(4,14);Put32(8,20);Put16(12,2);
        Put16(14,2);Put16(18,2);
        Put16(20,3);Put16(24,4);Put16(26,65535);
        Put16(30,1);Put16(32,1);Put16(34,2);Put32(36,14);Put16(40,1);
        bytes[46]=29;bytes[47]=81;bytes[48]=163;bytes[49]=207;
        var parsed=Read().Get(2);
        if(parsed.Length!=2||parsed[0]!=new ColorGlyphLayer(3,new Rgba32(163,81,29,207))||parsed[1]!=new ColorGlyphLayer(4,null))
            throw new InvalidOperationException("COLR output must preserve palette zero, BGRA order, alpha and foreground layers.");
        if(!Read().Get(5).IsEmpty)throw new InvalidOperationException("A monochrome glyph inherited unrelated color layers.");
        Put16(26,1);Reject();Put16(26,65535);
        Put16(18,3);Reject();Put16(18,2);
        Put32(8,29);Reject();Put32(8,20);
        Put32(36,22);Reject();Put32(36,14);
        return 6;

        ColorGlyphLayers Read()=>ColorGlyphLayers.Read(new FontSource(bytes),0,colrSize,colrSize,cpalSize,8);
        void Reject()
        {
            try{Read();}catch(InvalidDataException){return;}
            throw new InvalidOperationException("An invalid color glyph table was accepted.");
        }
        void Put16(int at,int value)=>BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(at),(ushort)value);
        void Put32(int at,int value)=>BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at),(uint)value);
    }
}
