using System.Globalization;

namespace SimdSkia.Avalonia;

internal enum CjkLocale { Unspecified, SimplifiedChinese, TraditionalChinese, Japanese, Korean }

internal static class CjkFontPreference
{
    internal static CjkLocale ForCulture(CultureInfo culture)
    {
        string name=culture.Name;
        if(name.StartsWith("zh",StringComparison.OrdinalIgnoreCase))
            return name.StartsWith("zh-Hant",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("zh-TW",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("zh-HK",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("zh-MO",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("zh-CHT",StringComparison.OrdinalIgnoreCase)
                ? CjkLocale.TraditionalChinese : CjkLocale.SimplifiedChinese;
        if(name.Equals("ja",StringComparison.OrdinalIgnoreCase)||name.StartsWith("ja-",StringComparison.OrdinalIgnoreCase))
            return CjkLocale.Japanese;
        if(name.Equals("ko",StringComparison.OrdinalIgnoreCase)||name.StartsWith("ko-",StringComparison.OrdinalIgnoreCase))
            return CjkLocale.Korean;
        return CjkLocale.Unspecified;
    }

    internal static CjkLocale ForFamily(string name)
    {
        const StringComparison ignoreCase=StringComparison.OrdinalIgnoreCase;
        if(name.EndsWith(" SC",ignoreCase)||name.EndsWith(" GB",ignoreCase)||
           name.StartsWith("Microsoft YaHei",ignoreCase)||name.Equals("SimHei",ignoreCase)||name.Equals("SimSun",ignoreCase))
            return CjkLocale.SimplifiedChinese;
        if(name.EndsWith(" TC",ignoreCase)||name.EndsWith(" HK",ignoreCase)||
           name.StartsWith("Microsoft JhengHei",ignoreCase)||name.Equals("MingLiU",ignoreCase))
            return CjkLocale.TraditionalChinese;
        if(name.EndsWith(" JP",ignoreCase)||name.StartsWith("Hiragino",ignoreCase)||name.StartsWith("Yu Gothic",ignoreCase))
            return CjkLocale.Japanese;
        if(name.EndsWith(" KR",ignoreCase)||name.StartsWith("Apple SD Gothic",ignoreCase)||name.StartsWith("Malgun Gothic",ignoreCase))
            return CjkLocale.Korean;
        return CjkLocale.Unspecified;
    }

    internal static bool AppliesTo(int codepoint) => codepoint is
        >= 0x2E80 and <= 0x312F or >= 0x3130 and <= 0x318F or >= 0x31A0 and <= 0x31FF or
        >= 0x3400 and <= 0x9FFF or >= 0xAC00 and <= 0xD7AF or >= 0xF900 and <= 0xFAFF or
        >= 0xFE30 and <= 0xFE4F or >= 0xFF01 and <= 0xFFEF or >= 0x20000 and <= 0x323AF;

    internal static int Rank(CjkLocale requested,CjkLocale family) =>
        requested==CjkLocale.Unspecified||requested==family ? 0 : family==CjkLocale.Unspecified ? 1 : 2;
}
