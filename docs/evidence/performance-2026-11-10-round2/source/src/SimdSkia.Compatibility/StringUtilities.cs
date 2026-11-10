// SkiaSharp v3.119.4 encoding and legacy validation contracts, MIT.
using System.Text;
namespace SkiaSharp;
public static unsafe class StringUtilities
{
    private static Encoding EncodingFor(SKTextEncoding encoding)=>encoding switch
    {SKTextEncoding.Utf8=>Encoding.UTF8,SKTextEncoding.Utf16=>Encoding.Unicode,SKTextEncoding.Utf32=>Encoding.UTF32,_=>throw new ArgumentOutOfRangeException(nameof(encoding),$"Encoding {encoding} is not supported.")};
    public static byte[] GetEncodedText(string text,SKTextEncoding encoding)=>GetEncodedText(text.AsSpan(),encoding);
    public static byte[] GetEncodedText(ReadOnlySpan<char> text,SKTextEncoding encoding)
    {var e=EncodingFor(encoding);var bytes=new byte[e.GetByteCount(text)];e.GetBytes(text,bytes);return bytes;}
    public static string GetString(byte[] data,SKTextEncoding encoding)=>GetString(data,0,data.Length,encoding);
    public static string GetString(byte[] data,int index,int count,SKTextEncoding encoding)
    {ArgumentNullException.ThrowIfNull(data);return EncodingFor(encoding).GetString(data,index,count);}
    public static string GetString(ReadOnlySpan<byte> data,SKTextEncoding encoding)=>GetString(data,0,data.Length,encoding);
    public static string GetString(ReadOnlySpan<byte> data,int index,int count,SKTextEncoding encoding)
    {data=data.Slice(index,count);return data.IsEmpty?string.Empty:EncodingFor(encoding).GetString(data);}
    public static string GetString(IntPtr data,int dataLength,SKTextEncoding encoding)=>GetString(new ReadOnlySpan<byte>((void*)data,dataLength),encoding);
    public static int GetUnicodeCharacterCode(string character,SKTextEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(character);
        int length=encoding switch{SKTextEncoding.Utf8 or SKTextEncoding.Utf16=>1,SKTextEncoding.Utf32=>2,_=>throw new ArgumentOutOfRangeException(nameof(encoding))};
        if(character.Length!=length)throw new ArgumentException(nameof(character),"Only a single character can be specified.");
        return BitConverter.ToInt32(GetEncodedText(character,encoding),0);
    }
}
