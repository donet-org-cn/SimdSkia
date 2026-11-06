using SkiaSharp;

string output=args.FirstOrDefault()??"artifacts/gradients.png";
using var bitmap=new SKBitmap(512,384);using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(35,39,47));
SKColor[] colors=[new(255,60,100),new(40,230,170),new(50,110,255)];
for(int row=0;row<3;row++)for(int col=0;col<4;col++)
{
    int saved=canvas.Save();canvas.Translate(col*128,row*128);var mode=(SKShaderTileMode)col;
    using var shader=row switch
    {
        0=>SKShader.CreateLinearGradient(new(24,20),new(98,92),colors,[0,.35f,1],mode),
        1=>SKShader.CreateRadialGradient(new(64,64),42,colors,[0,.35f,1],mode),
        _=>SKShader.CreateSweepGradient(new(64,64),colors,[0,.35f,1],mode,45,240)
    };
    using var paint=new SKPaint{Shader=shader};canvas.DrawRect(new(4,4,124,124),paint);canvas.RestoreToCount(saved);
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
using var encoded=bitmap.Encode(SKEncodedImageFormat.Png,100);File.WriteAllBytes(output,encoded.ToArray());
Console.WriteLine("Wrote "+Path.GetFullPath(output)+" (rows: linear/radial/sweep; columns: clamp/repeat/mirror/decal)");
