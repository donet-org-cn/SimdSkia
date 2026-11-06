using SimdSkia;
namespace SkiaSharp;
public partial class SKShader
{internal static SKShader Runtime(IColorShader shader,ShaderResources? resources)=>new(shader){resources=resources};}
