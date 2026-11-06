using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new SortedDictionary<string,object?>();int checks=0;
void Check(bool condition){checks++;if(!condition)throw new Exception("Lifetime check "+checks);}
byte[] Read(SKImage image){var info=new SKImageInfo(image.Width,image.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul);using var target=new SKBitmap(info);Check(image.ReadPixels(info,target.GetPixels(),target.RowBytes));return target.GetPixelSpan().ToArray();}
foreach(var type in new[]{SKColorType.Rgba8888,SKColorType.Bgra8888})foreach(var alpha in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul,SKAlphaType.Opaque})foreach(int padding in new[]{0,12})
{
    var info=new SKImageInfo(11,9,type,alpha);int stride=info.RowBytes+padding;using var source=new SKBitmap(info,stride);for(int y=0;y<9;y++)for(int x=0;x<11;x++)source.SetPixel(x,y,new SKColor((byte)(x*20),(byte)(y*25),45,alpha==SKAlphaType.Opaque?(byte)255:(byte)(x*20+y*5)));
    byte[] bytes=source.GetPixelSpan().ToArray();string key=$"{type}-{alpha}-{padding}";
    using(var copy=SKImage.FromPixelCopy(info,bytes,stride)){values[key+"-copy"]=Read(copy);using var part=copy.Subset(new(2,1,8,7));values[key+"-subset"]=Read(part);Check(copy.Subset(new(-1,0,3,3)) is null);using var raster=copy.ToRasterImage(true);values[key+"-raster"]=Read(raster);}
    using(var data=SKData.CreateCopy(bytes)){var image=SKImage.FromPixels(info,data,stride);data.Dispose();using(image){values[key+"-data"]=Read(image);using var map=image.PeekPixels();map.Dispose();values[key+"-after-map"]=Read(image);}}
    using(var stream=new MemoryStream(bytes))using(var image=SKImage.FromPixelCopy(info,stream,stride))values[key+"-stream"]=Read(image);
    var ptr=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,ptr,bytes.Length);int released=0;var ctx=new object();
    try
    {
        var bitmap=new SKBitmap();Check(bitmap.InstallPixels(info,ptr,stride,(address,context)=>{Check(address==ptr&&ReferenceEquals(context,ctx));released++;},ctx));
        using(var canvas=new SKCanvas(bitmap)){canvas.Clear(new SKColor(10,50,90,255));canvas.DrawRect(new SKRect(2,1,8,7),new SKPaint{Color=SKColors.Red});}
        using(var image=SKImage.FromBitmap(bitmap))values[key+"-external-draw"]=Read(image);
        var held=bitmap.PeekPixels();GC.Collect();GC.WaitForPendingFinalizers();Check(released==0);values[key+"-external-pixel"]=held.GetPixelColor(3,2).ToString();held.Dispose();bitmap.Dispose();Check(released==1);
    }
    finally{Marshal.FreeHGlobal(ptr);}
    ptr=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,ptr,bytes.Length);released=0;
    try{var surface=SKSurface.Create(info,ptr,stride,(address,context)=>{Check(address==ptr);released++;},ctx);surface.Canvas.Clear(SKColors.Blue);using(var image=surface.Snapshot())values[key+"-direct-surface"]=Read(image);surface.Dispose();Check(released==1);}
    finally{Marshal.FreeHGlobal(ptr);}
    using(var data=source.Encode(SKEncodedImageFormat.Png,100))using(var decoded=SKImage.FromEncodedData(data)){values[key+"-decoded"]=Read(decoded);Check(decoded.EncodedData.AsSpan().SequenceEqual(data.AsSpan()));using var roundtrip=decoded.Encode();Check(roundtrip.AsSpan().SequenceEqual(data.AsSpan()));}
}
values["null-direct"]=SKSurface.Create(new SKImageInfo(1,1),IntPtr.Zero) is null;
foreach(var pg in Enum.GetValues<SKPixelGeometry>())foreach(var flag in Enum.GetValues<SKSurfacePropsFlags>())
{using var props=new SKSurfaceProperties(flag,pg);using var surface=SKSurface.Create(new SKImageInfo(5,6),props);props.Dispose();using var current=surface.SurfaceProperties;values["surface-properties-"+pg+flag]=new[]{(int)current.Flags,(int)current.PixelGeometry,current.IsUseDeviceIndependentFonts?1:0};}
using(var bitmap=new SKBitmap(new SKImageInfo(5,6),SKBitmapAllocFlags.ZeroPixels))using(var subset=new SKBitmap())
{bitmap.Erase(SKColors.Red);Check(bitmap.ExtractSubset(subset,new SKRectI(1,2,4,5)));subset.SetPixel(0,0,SKColors.Blue);values["bitmap-subset-shared"]=bitmap.GetPixel(1,2).ToString();using var image=SKImage.FromBitmap(subset);values["bitmap-subset-pixels"]=Read(image);}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {values.Count} image factory cases; {checks} local lifetime checks");
