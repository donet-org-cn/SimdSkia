using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);using var pixels=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new Dictionary<string,object?>();
SKMaskFilter? Filter(string name)=>name switch
{
    "gamma-half"=>SKMaskFilter.CreateGamma(.5f),"gamma-two"=>SKMaskFilter.CreateGamma(2),"gamma-one"=>SKMaskFilter.CreateGamma(1),"clip"=>SKMaskFilter.CreateClip(70,185),"clip-reverse"=>SKMaskFilter.CreateClip(185,70),"table"=>SKMaskFilter.CreateTable(Enumerable.Range(0,256).Select(x=>(byte)(255-x)).ToArray()),
    "blur-normal"=>SKMaskFilter.CreateBlur(SKBlurStyle.Normal,2),"blur-solid"=>SKMaskFilter.CreateBlur(SKBlurStyle.Solid,2),"blur-outer"=>SKMaskFilter.CreateBlur(SKBlurStyle.Outer,2),"blur-inner"=>SKMaskFilter.CreateBlur(SKBlurStyle.Inner,2),"blur-fixed"=>SKMaskFilter.CreateBlur(SKBlurStyle.Normal,2,false),"blur-small"=>SKMaskFilter.CreateBlur(SKBlurStyle.Normal,.7f),_=>null
};
string[] names=["none","gamma-half","gamma-two","gamma-one","clip","clip-reverse","table","blur-normal","blur-solid","blur-outer","blur-inner","blur-fixed","blur-small"];
foreach(var name in names)for(int shape=0;shape<4;shape++)for(int transform=0;transform<3;transform++)foreach(bool shader in new[]{false,true})
{
    using var bitmap=new SKBitmap(new SKImageInfo(96,72,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(240,245,250));
    if(transform==1){canvas.Translate(7,5);canvas.Scale(.8f,.7f);}if(transform==2){canvas.ClipRect(new SKRect(15,9,79,61));canvas.RotateDegrees(8,48,36);}
    using var gradient=shader?SKShader.CreateLinearGradient(new SKPoint(10,0),new SKPoint(80,0),[new SKColor(245,20,40),new SKColor(10,100,220)],SKShaderTileMode.Clamp):null;
    using var filter=Filter(name);using var paint=new SKPaint{Color=new SKColor(22,77,155,173),IsAntialias=true,MaskFilter=filter,Shader=gradient};using var path=new SKPath();
    switch(shape){case 0:canvas.DrawRect(new SKRect(12.25f,11.5f,79.75f,57.25f),paint);break;case 1:path.AddCircle(43.3f,33.8f,21.4f);canvas.DrawPath(path,paint);break;case 2:path.MoveTo(-8,28);path.CubicTo(16,-13,40,75,84,12);path.LineTo(80,60);path.Close();canvas.DrawPath(path,paint);break;case 3:path.MoveTo(10,20);path.LineTo(78,51);paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=4;canvas.DrawPath(path,paint);break;}
    int offset=(int)pixels.Position;var bytes=bitmap.GetPixelSpan();pixels.Write(bytes);cases.Add(new{name,shape,transform,shader,offset,length=bytes.Length});
}
foreach(float radius in new[]{-1f,0,.2f,1,5,100})values["radius-"+radius]=new[]{SKMaskFilter.ConvertRadiusToSigma(radius),SKMaskFilter.ConvertSigmaToRadius(radius)};
for(int i=0;i<4;i++){using var f=SKMaskFilter.CreateBlur(SKBlurStyle.Normal,i switch{0=>0,1=>-1,2=>float.NaN,_=>float.PositiveInfinity});values["invalid-"+i]=f is null;}
using(var filter=Filter("blur-normal"))using(var paint=new SKPaint{MaskFilter=filter})using(var clone=paint.Clone())using(var bitmap=new SKBitmap(40,40))using(var canvas=new SKCanvas(bitmap))
{filter!.Dispose();paint.Dispose();canvas.Clear(SKColors.Transparent);canvas.DrawRect(new SKRect(15,15,25,25),clone);values["retained-after-dispose"]=bitmap.GetPixel(14,20).Alpha>0;clone.Reset();values["reset"]=clone.MaskFilter is null;}
File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {cases.Count} mask-filter images, {values.Count} metadata entries");
