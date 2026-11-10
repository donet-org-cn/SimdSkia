// CSS3 scoring adapted from Skia m119 SkFontMgr.cpp (Google, BSD-3).
namespace SimdSkia.Text;
internal static class FontStyleMatcher
{
    internal static int Score(int weight,int width,int slant,int targetWeight,int targetWidth,int targetSlant)
    {
        int score=targetWidth<=5?(width<=targetWidth?10-targetWidth+width:10-width):(width>targetWidth?10+targetWidth-width:width);
        score<<=8;
        score+=targetSlant switch{0=>slant switch{0=>3,1=>1,_=>2},1=>slant switch{0=>1,1=>3,_=>2},_=>slant switch{0=>1,1=>2,_=>3}};
        score<<=8;
        score+=targetWeight==weight?1000:targetWeight<400?(weight<=targetWeight?1000-targetWeight+weight:1000-weight):targetWeight<=500?(weight>=targetWeight&&weight<=500?1000+targetWeight-weight:weight<=targetWeight?500+weight:1000-weight):(weight>targetWeight?1000+targetWeight-weight:weight);
        return -score;
    }
}
