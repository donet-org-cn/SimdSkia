using System.Globalization;

namespace SkiaSharp;

public readonly partial struct SKColor
{
    public static SKColor Parse(string hexString)
    {
        if(!TryParse(hexString,out var color))throw new ArgumentException("Invalid hexadecimal color string.",nameof(hexString));
        return color;
    }
    public static bool TryParse(string hexString,out SKColor color)
    {
        color=Empty;var text=hexString.AsSpan().Trim();if(text.IsEmpty)return false;
        if(text[0]=='#')text=text[1..];
        if(text.Length is not (3 or 4 or 6 or 8)||!uint.TryParse(text,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint value))return false;
        if(text.Length==3)value=0xff000000|((value>>8)&15)*0x110000|((value>>4)&15)*0x1100|(value&15)*0x11;
        else if(text.Length==4)value=((value>>12)&15)*0x11000000|((value>>8)&15)*0x110000|((value>>4)&15)*0x1100|(value&15)*0x11;
        else if(text.Length==6)value|=0xff000000;
        color=new(value);return true;
    }
}
