using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var backing=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("Image operation assertion "+checks);}
void Capture(SKImage image,string kind,int scene,int transform)
{using var read=new SKBitmap(new SKImageInfo(image.Width,image.Height,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind,scene,transform,offset=pixels.Position,length=bytes.Length,width=image.Width,height=image.Height});pixels.Write(bytes);}
for(int alpha=0;alpha<2;alpha++)for(int mode=0;mode<4;mode++)for(int filter=0;filter<2;filter++)
{
    using var source=new SKBitmap(new SKImageInfo(9,7,SKColorType.Rgba8888,alpha==0?SKAlphaType.Premul:SKAlphaType.Unpremul,SKColorSpace.CreateSrgbLinear()));for(int y=0;y<7;y++)for(int x=0;x<9;x++)source.SetPixel(x,y,new SKColor((byte)(x*27),(byte)(y*35),(byte)(x*9+y*11),(byte)(40+(x*7+y*13)%216)));
    using var image=SKImage.FromBitmap(source);using var shader=image.ToRawShader((SKShaderTileMode)mode,(SKShaderTileMode)mode,new SKSamplingOptions((SKFilterMode)filter),new SKMatrix(1.1f,.1f,2,.2f,.9f,3,0,0,1));image.Dispose();source.Dispose();
    using var surface=SKSurface.Create(new SKImageInfo(40,32,SKColorType.Rgba8888,SKAlphaType.Premul).WithColorSpace(SKColorSpace.CreateSrgb()))!;surface.Canvas.Clear(new SKColor(30,40,50));using(var paint=new SKPaint{Shader=shader})surface.Canvas.DrawPaint(paint);using var result=surface.Snapshot();Capture(result,"raw-image",alpha,mode*2+filter);
}
using(var bitmap=new SKBitmap(new SKImageInfo(40,32,SKColorType.Rgba8888,SKAlphaType.Premul)))
{
    using(var c=new SKCanvas(bitmap)){c.Clear(SKColors.Transparent);using var paint=new SKPaint{Color=new SKColor(210,60,130,200)};c.DrawRect(new SKRect(7,5,34,28),paint);}
    using var image=SKImage.FromBitmap(bitmap);Check(image.IsValid((GRContext)null!));Check(image.IsValid((GRRecordingContext)null!));
    using(var subset=image.Subset(null,new SKRectI(3,4,32,27)))Check(subset.Width==29&&subset.Height==23);
    for(int scene=0;scene<6;scene++)for(int subsetIndex=0;subsetIndex<2;subsetIndex++)
    {
        using var color=SKColorFilter.CreateLighting(new SKColor(128,200,255),new SKColor(10,20,30));using var filter=scene switch{0=>SKImageFilter.CreateOffset(3,-2),1=>SKImageFilter.CreateBlur(2,2),2=>SKImageFilter.CreateColorFilter(color),3=>SKImageFilter.CreateDilate(2,3),4=>SKImageFilter.CreateOffset(-7,5),_=>SKImageFilter.CreateOffset(0,0,null,new SKRect(4,6,30,26))};
        var subset=subsetIndex==0?new SKRectI(0,0,40,32):new SKRectI(5,3,35,30);using var result=image.ApplyImageFilter(filter,subset,new SKRectI(-8,-9,48,42),out var outSubset,out SKPointI offset);backing[$"filter-{scene}-{subsetIndex}"]=result is null?null:new object[]{result.Width,result.Height,new[]{outSubset.Left,outSubset.Top,outSubset.Right,outSubset.Bottom},new[]{offset.X,offset.Y}};
        values[$"filter-{scene}-{subsetIndex}"]=result is null?null:new object[]{outSubset.Width,outSubset.Height,offset.X,offset.Y};if(result is not null){using var valid=result.Subset(outSubset);Capture(valid,"image-filter",scene,subsetIndex);}
    }
}
File.WriteAllText(Path.Combine(output,"backing-layout.json"),JsonSerializer.Serialize(backing));
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} image operation images, {values.Count} metadata entries and {checks} lifetime/validation checks");
