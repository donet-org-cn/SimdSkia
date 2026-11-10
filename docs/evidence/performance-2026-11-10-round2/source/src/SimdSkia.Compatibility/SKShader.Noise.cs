using SimdSkia;
namespace SkiaSharp;
public partial class SKShader
{
    public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX,float baseFrequencyY,int numOctaves,float seed)=>Noise(baseFrequencyX,baseFrequencyY,numOctaves,seed,default,true);
    public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX,float baseFrequencyY,int numOctaves,float seed,SKPointI tileSize)=>Noise(baseFrequencyX,baseFrequencyY,numOctaves,seed,new(tileSize.X,tileSize.Y),true);
    public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX,float baseFrequencyY,int numOctaves,float seed,SKSizeI tileSize)=>Noise(baseFrequencyX,baseFrequencyY,numOctaves,seed,tileSize,true);
    public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX,float baseFrequencyY,int numOctaves,float seed)=>Noise(baseFrequencyX,baseFrequencyY,numOctaves,seed,default,false);
    public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX,float baseFrequencyY,int numOctaves,float seed,SKPointI tileSize)=>Noise(baseFrequencyX,baseFrequencyY,numOctaves,seed,new(tileSize.X,tileSize.Y),false);
    public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX,float baseFrequencyY,int numOctaves,float seed,SKSizeI tileSize)=>Noise(baseFrequencyX,baseFrequencyY,numOctaves,seed,tileSize,false);
    private static SKShader Noise(float x,float y,int octaves,float seed,SKSizeI tile,bool fractal)
    {
        if(!(x>=0&&y>=0)||octaves is <0 or >255||!float.IsFinite(seed)||tile.Width<0||tile.Height<0)return null!;
        return octaves==0?CreateColor(fractal?new SKColorF(.5f,.5f,.5f,.5f):SKColorF.Empty,null):new(new PerlinNoiseShader(x,y,octaves,seed,tile.Width,tile.Height,fractal));
    }
}
