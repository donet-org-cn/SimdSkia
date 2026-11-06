using SkiaSharp;
using System.Text.Json;
string output=args[0];Directory.CreateDirectory(output);using var bytes=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new SortedDictionary<string,object?>();int offset=0,checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
SKRect crop=new(5,4,56,41);
SKImageFilter Color(bool cropped=false)
{using var color=SKColorFilter.CreateLighting(new SKColor(180,255,140),new SKColor(8,20,40));return cropped?SKImageFilter.CreateColorFilter(color,null,crop):SKImageFilter.CreateColorFilter(color);}
SKImageFilter Compose()
{using var outer=SKImageFilter.CreateOffset(3,-2);using var inner=Color();return SKImageFilter.CreateCompose(outer,inner);}
SKImageFilter Blend(SKBlendMode mode)
{using var bg=SKImageFilter.CreateOffset(-4,2);using var fg=SKImageFilter.CreateOffset(5,-3);return SKImageFilter.CreateBlendMode(mode,bg,fg,crop);}
SKImageFilter Arithmetic()
{using var bg=SKImageFilter.CreateOffset(-4,2);using var fg=SKImageFilter.CreateOffset(5,-3);return SKImageFilter.CreateArithmetic(.3f,.5f,.7f,.05f,true,bg,fg,crop);}
SKImageFilter Matrix()
{var m=SKMatrix.CreateScale(1.2f,.7f);return SKImageFilter.CreateMatrix(in m,new SKSamplingOptions(SKFilterMode.Linear));}
SKImageFilter Merge()
{using var a=SKImageFilter.CreateOffset(-4,2);using var b=SKImageFilter.CreateOffset(5,-3);return SKImageFilter.CreateMerge([a,b],crop);}
var factories=new List<(string,Func<SKImageFilter>)>{("color",()=>Color()),("color-crop",()=>Color(true)),("offset",()=>SKImageFilter.CreateOffset(3,-2)),("fractional-offset",()=>SKImageFilter.CreateOffset(2.5f,-1.25f,null,crop)),("matrix",Matrix),("compose",Compose),("merge",Merge),("srcin",()=>Blend(SKBlendMode.SrcIn)),("multiply",()=>Blend(SKBlendMode.Multiply)),("arithmetic",Arithmetic)};
foreach(var mode in Enum.GetValues<SKShaderTileMode>()){var tile=mode;factories.Add(("blur-"+mode,()=>SKImageFilter.CreateBlur(1.2f,2.1f,tile,null,crop)));}
factories.Add(("zero-blur",()=>SKImageFilter.CreateBlur(0,0)));
factories.AddRange([
    ("dilate",()=>SKImageFilter.CreateDilate(2,3,null,crop)),("erode",()=>SKImageFilter.CreateErode(2,1,null,crop)),
    ("shadow",()=>SKImageFilter.CreateDropShadow(3,-2,1.2f,2.1f,new SKColor(20,50,200,160),null,crop)),
    ("shadow-only",()=>SKImageFilter.CreateDropShadowOnly(3,-2,1.2f,2.1f,new SKColor(20,50,200,160),null,crop)),
    ("convolution-pma",()=>SKImageFilter.CreateMatrixConvolution(new(3,3),new float[]{0,1,0,1,4,1,0,1,0},.125f,3,new(1,1),SKShaderTileMode.Decal,true,null,crop)),
    ("convolution-straight",()=>SKImageFilter.CreateMatrixConvolution(new(3,3),new float[]{0,-1,0,-1,5,-1,0,-1,0},1,0,new(1,1),SKShaderTileMode.Clamp,false,null,crop)),
    ("displacement",()=>{using var map=Color();return SKImageFilter.CreateDisplacementMapEffect(SKColorChannel.R,SKColorChannel.A,8,map,null,crop);}),
    ("tile",()=>{using var source=Color();return SKImageFilter.CreateTile(new(14,11,26,21),crop,source);}),
    ("image-source",()=>{using var b=new SKBitmap(8,8);b.Erase(new SKColor(40,150,200,180));b.SetPixel(3,3,SKColors.Yellow);using var i=SKImage.FromBitmap(b);return SKImageFilter.CreateImage(i,new(0,0,8,8),new(10,8,40,32),new SKSamplingOptions(SKFilterMode.Linear));}),
    ("shader-source",()=>{using var shader=SKShader.CreateLinearGradient(new(0,0),new(60,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);return SKImageFilter.CreateShader(shader,false,crop);}),
    ("distant-diffuse",()=>SKImageFilter.CreateDistantLitDiffuse(new(1,-1,2),SKColors.White,2,.8f,null,crop)),
    ("point-diffuse",()=>SKImageFilter.CreatePointLitDiffuse(new(10,4,20),SKColors.White,2,.8f,null,crop)),
    ("spot-diffuse",()=>SKImageFilter.CreateSpotLitDiffuse(new(10,4,20),new(30,20,0),2,45,SKColors.White,2,.8f,null,crop)),
    ("distant-specular",()=>SKImageFilter.CreateDistantLitSpecular(new(1,-1,2),SKColors.White,2,.8f,3,null,crop)),
    ("point-specular",()=>SKImageFilter.CreatePointLitSpecular(new(10,4,20),SKColors.White,2,.8f,3,null,crop)),
    ("spot-specular",()=>SKImageFilter.CreateSpotLitSpecular(new(10,4,20),new(30,20,0),2,45,SKColors.White,2,.8f,3,null,crop))
]);
factories.Add(("magnifier",()=>SKImageFilter.CreateMagnifier(new(10,8,48,38),2.2f,4,new SKSamplingOptions(SKFilterMode.Linear),null,crop)));
factories.Add(("magnifier-flat",()=>SKImageFilter.CreateMagnifier(new(10,8,48,38),2.2f,0,new SKSamplingOptions(SKFilterMode.Nearest),null,crop)));
foreach(var (name,create) in factories)foreach(int transform in new[]{0,1})foreach(int shape in new[]{0,1,2})foreach(byte alpha in new byte[]{90,255})foreach(bool clipped in new[]{false,true})
{
    using var filter=create();values[name+"-nonnull"]=filter is not null;
    using var bitmap=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(25,110,160,180));
    if(clipped){canvas.ClipRect(new(8,7,53,39));}if(transform==1){canvas.Translate(4,-2);canvas.Scale(.85f,1.1f);}
    using var paint=new SKPaint{Color=new SKColor(210,80,30,alpha),ImageFilter=filter};
    if(shape==0)canvas.DrawRect(new SKRect(14,11,38,31),paint);
    else if(shape==1)
    {using var source=new SKBitmap(20,14);for(int y=0;y<14;y++)for(int x=0;x<20;x++)source.SetPixel(x,y,new SKColor((byte)(x*11),(byte)(y*17),90,(byte)(x%3==0?0:190)));canvas.DrawBitmap(source,new SKRect(14,11,38,31),paint);}
    else{int save=canvas.SaveLayer(paint);using var p=new SKPaint{Color=new SKColor(210,80,30,alpha)};canvas.DrawRect(new SKRect(14,11,38,31),p);p.Color=new SKColor(40,200,90,180);canvas.DrawRect(new SKRect(30,24,46,35),p);canvas.RestoreToCount(save);}
    var span=bitmap.GetPixelSpan();bytes.Write(span);cases.Add(new{metadata=new{name,transform,shape,alpha,clipped},offset,length=span.Length});offset+=span.Length;
}
foreach(bool directReference in new[]{false,true})foreach(bool previous in new[]{false,true})foreach(bool backdrop in new[]{false,true})foreach(bool bounded in new[]{false,true})foreach(byte opacity in new byte[]{110,255})
{
    using var bitmap=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(30,70,130,200));using var p=new SKPaint{Color=new SKColor(210,80,30,210)};canvas.DrawRect(3,3,22,28,p);
    using var color=SKColorFilter.CreateLighting(new SKColor(110,200,255),new SKColor(10,0,30));using var filter=SKImageFilter.CreateColorFilter(color);using var layerPaint=new SKPaint{Color=SKColors.White.WithAlpha(opacity)};
    var rec=new SKCanvasSaveLayerRec{Paint=layerPaint,Backdrop=backdrop?filter:null,Bounds=bounded?new SKRect(5,5,54,40):null,Flags=previous?SKCanvasSaveLayerRecFlags.InitializeWithPrevious:SKCanvasSaveLayerRecFlags.None};
    int count;
    #if ORIGINAL
    count=directReference?NativeLayerProbe.Save(canvas,rec):canvas.SaveLayer(in rec);
    #else
    count=canvas.SaveLayer(in rec);
    #endif
    p.Color=new SKColor(40,200,90,150);canvas.DrawRect(20,14,38,26,p);canvas.RestoreToCount(count);var span=bitmap.GetPixelSpan();bytes.Write(span);cases.Add(new{metadata=(object)new{name=directReference?"layer-rec-stable-pointer-reference":"layer-rec",previous,backdrop,bounded,opacity},offset,length=span.Length});offset+=span.Length;
}
foreach(bool bounded in new[]{false,true})foreach(byte opacity in new byte[]{110,255})
{
    using var bitmap=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(30,70,130,200));using var p=new SKPaint{Color=new SKColor(210,80,30,210)};canvas.DrawRect(3,3,22,28,p);using var layerPaint=new SKPaint{Color=SKColors.White.WithAlpha(opacity)};
    int count=bounded?canvas.SaveLayer(new SKRect(5,5,54,40),layerPaint):canvas.SaveLayer(layerPaint);p.Color=new SKColor(40,200,90,150);canvas.DrawRect(20,14,38,26,p);canvas.RestoreToCount(count);var span=bitmap.GetPixelSpan();bytes.Write(span);cases.Add(new{metadata=(object)new{name="layer-overload",bounded,opacity},offset,length=span.Length});offset+=span.Length;
}
foreach(float sigma in new[]{0f,.2f,.7f,.8f,1f,1.2f,2.1f,4f,12f,134f,136f,256f})
{
    using var bitmap=new SKBitmap(new SKImageInfo(257,1,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);using var filter=SKImageFilter.CreateBlur(sigma,0,null,new SKRect(0,0,257,1));using var paint=new SKPaint{Color=SKColors.White,ImageFilter=filter};canvas.DrawRect(128,0,1,1,paint);var span=bitmap.GetPixelSpan();bytes.Write(span);cases.Add(new{metadata=(object)new{name="impulse",sigma},offset,length=span.Length});offset+=span.Length;
}
// Dispose and Clone must preserve the immutable input graph.
using(var a=SKImageFilter.CreateOffset(2,1))using(var b=Color())using(var graph=SKImageFilter.CreateCompose(a,b))using(var paint=new SKPaint{ImageFilter=graph,Color=SKColors.Red})
{
    a.Dispose();b.Dispose();graph.Dispose();using var clone=paint.Clone();paint.Dispose();using var bitmap=new SKBitmap(32,24);using var canvas=new SKCanvas(bitmap);canvas.DrawRect(5,5,12,8,clone);Check(bitmap.GetPixel(9,9).Alpha>0,"Retained graph survives wrapper disposal");clone.Reset();Check(clone.ImageFilter is null,"Reset releases graph");
}
values["checks"]=checks;File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {cases.Count} images, {checks} lifecycle checks");
#if ORIGINAL
// Reference-only ABI probe: keep the optional bounds live until the C call
// returns. Production assemblies never reference the original native library.
static unsafe class NativeLayerProbe
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rec {public SKRect* Bounds;public IntPtr Paint,Backdrop;public uint Flags;}
    [System.Runtime.InteropServices.DllImport("libSkiaSharp",CallingConvention=System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern int sk_canvas_save_layer_rec(IntPtr canvas,Rec* rec);
    public static int Save(SKCanvas canvas,SKCanvasSaveLayerRec r)
    {
        SKRect bounds=r.Bounds??default;var native=new Rec{Bounds=r.Bounds is null?null:&bounds,Paint=r.Paint?.Handle??IntPtr.Zero,Backdrop=r.Backdrop?.Handle??IntPtr.Zero,Flags=(uint)r.Flags};
        return sk_canvas_save_layer_rec(canvas.Handle,&native);
    }
}
#endif
