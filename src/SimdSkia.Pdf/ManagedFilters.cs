using SimdSkia.Imaging;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Tokens;

namespace SimdSkia.Pdf;

internal sealed class ManagedFilters : BaseFilterProvider
{
    public ManagedFilters():base(Create()) { }
    private static Dictionary<string,IFilter> Create()
    {
        string[] names=["ASCII85Decode","A85","ASCIIHexDecode","AHx","CCITTFaxDecode","CCF","DCTDecode","DCT","JBIG2Decode","JPXDecode","RunLengthDecode","RL","LZWDecode","LZW"];
        var filters=names.ToDictionary(n=>n,n=>DefaultFilterProvider.Instance.GetNamedFilters([NameToken.Create(n)])[0]);var flate=new ManagedFlate();filters["FlateDecode"]=flate;filters["Fl"]=flate;return filters;
    }
    private sealed class ManagedFlate : IFilter
    {
        public bool IsSupported=>true;
        public Memory<byte> Decode(Memory<byte> input,DictionaryToken streamDictionary,IFilterProvider filterProvider,int filterIndex)
        {
            var p=DecodeParameterResolver.GetFilterParameters(streamDictionary,filterIndex);
            int Number(string name,int fallback)=>p.TryGet(NameToken.Create(name),out NumericToken n)?n.Int:fallback;
            int predictor=Number("Predictor",1),colors=Number("Colors",1),bits=Number("BitsPerComponent",8),columns=Number("Columns",1);
            var bytes=ManagedZlib.DecodeBounded(input.Span);
            if(predictor<=1)return bytes;
            if(colors is <1 or >32||bits is not (1 or 2 or 4 or 8 or 16)||columns<1)throw new InvalidDataException("Invalid PDF predictor parameters.");
            int row=checked((int)((checked((long)colors*bits*columns)+7)/8)),bpp=Math.Max(1,(colors*bits+7)/8);
            if(predictor==2)
            {
                if(bits!=8)throw new NotSupportedException("TIFF PDF predictor currently requires 8-bit samples.");
                if(bytes.Length%row!=0)throw new InvalidDataException("Truncated TIFF predictor row.");
                for(int start=0;start<bytes.Length;start+=row)for(int x=colors;x<row;x++)bytes[start+x]=unchecked((byte)(bytes[start+x]+bytes[start+x-colors]));return bytes;
            }
            if(predictor is <10 or >15)throw new NotSupportedException("Unknown PDF predictor.");
            if(bytes.Length%(row+1)!=0)throw new InvalidDataException("Truncated PNG predictor row.");
            var result=new byte[checked(bytes.Length/(row+1)*row)];
            for(int y=0;y<result.Length/row;y++)
            {
                int kind=bytes[y*(row+1)];if(kind>4)throw new InvalidDataException("Invalid PNG predictor.");
                for(int x=0;x<row;x++)
                {
                    int a=x>=bpp?result[y*row+x-bpp]:0,b=y>0?result[(y-1)*row+x]:0,c=y>0&&x>=bpp?result[(y-1)*row+x-bpp]:0;
                    int prediction=kind switch{0=>0,1=>a,2=>b,3=>(a+b)/2,_=>Paeth(a,b,c)};
                    result[y*row+x]=unchecked((byte)(bytes[y*(row+1)+1+x]+prediction));
                }
            }return result;
        }
        private static int Paeth(int a,int b,int c){int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;}
    }
}
