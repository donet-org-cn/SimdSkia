using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);using var bitmap=new SKBitmap(new SKImageInfo(256,256,SKColorType.Rgba8888,SKAlphaType.Premul));var source=bitmap.GetPixelSpan();
for(int alpha=0;alpha<256;alpha++)for(int c=0;c<256;c++){int p=(alpha*256+c)*4;source[p]=(byte)c;source[p+1]=(byte)Math.Min(alpha,c);source[p+2]=(byte)(alpha/2);source[p+3]=(byte)alpha;}
using var image=SKImage.FromBitmap(bitmap);using var target=new SKBitmap(new SKImageInfo(256,256,SKColorType.Rgba8888,SKAlphaType.Unpremul));if(!image.ReadPixels(target.Info,target.GetPixels(),target.RowBytes,0,0))throw new Exception("ReadPixels failed.");File.WriteAllBytes(Path.Combine(output,"pixels.bin"),target.GetPixelSpan().ToArray());
#if MANAGED
var expected=target.GetPixelSpan().ToArray();
foreach(bool simd in new[]{false,true})foreach(bool swap in new[]{false,true})foreach(int length in new[]{1,2,3,4,7,8,9,15,16,17,255,256,257,65536})
{
    var converted=new byte[length*4];SimdSkia.Cpu.PixelTransferKernel.Unpremultiply(source[..(length*4)],converted,swap,simd);
    for(int p=0;p<length;p++)for(int c=0;c<4;c++){int from=swap&&c is 0 or 2?2-c:c;if(converted[p*4+c]!=expected[p*4+from])throw new Exception($"PMA scalar/SIMD mismatch at {length}, {p}, {c}");}
}
for(int formula=0;formula<8;formula++)
{
    byte[] data=new byte[256*256*4];
    for(int a=0;a<256;a++)for(int c=0;c<256;c++)
    {
        for(int channel=0;channel<3;channel++)
        {
            int v=channel==0?c:channel==1?Math.Min(a,c):a/2;
            float f=formula switch{0=>v*255f/a,1=>v*(1f/a)*255,2=>(v*(1f/255))*(1/(a*(1f/255)))*255,3=>v*(1/(255*(a*(1f/255))))*255,4=>v*(1f/255)/(a*(1f/255))*255,5=>v*(255f/a),6=>v*(1/(a*(1f/255))),_=>v*(1f/255)*(255f/a)*255};
            int q=a==0?0:formula==6?(int)MathF.Floor(f+.5f):(int)MathF.Round(f,MidpointRounding.ToEven);data[(a*256+c)*4+channel]=(byte)Math.Clamp(q,0,255);
        }
        data[(a*256+c)*4+3]=(byte)a;
    }
    File.WriteAllBytes(Path.Combine(output,"formula-"+formula+".bin"),data);
}
#endif
Console.WriteLine("Captured all 32896 valid premultiplied channel pairs and invalid-RGB boundary probes");
