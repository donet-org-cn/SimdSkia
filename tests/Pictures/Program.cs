using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("Picture assertion "+checks);}
void Scene(SKCanvas canvas,int scene,List<IDisposable> deferred)
{
    using var paint=new SKPaint{Color=new SKColor(220,70,100,210),IsAntialias=false};using var path=new SKPath();path.AddRect(new(8,7,47,31));
    switch(scene)
    {
        case 0:canvas.DrawRect(new(8,7,47,31),paint);paint.Color=SKColors.Blue;canvas.DrawRect(new(20,20,55,44),paint);break;
        case 1:canvas.Save();canvas.Translate(7,8);canvas.ClipRect(new(0,0,32,24));canvas.DrawPath(path,paint);canvas.Restore();canvas.DrawRect(new(58,7,66,15),paint);break;
        case 2:path.Reset();path.AddCircle(30,28,19);canvas.ClipPath(path,SKClipOperation.Intersect,true);canvas.DrawRect(new(0,0,70,60),paint);break;
        case 3:canvas.DrawRect(new(0,0,60,48),paint);canvas.ClipRect(new(4,3,40,32));canvas.Clear(new SKColor(20,70,90,170));break;
        case 4:using(var layer=new SKPaint{Color=new SKColor(255,255,255,120)}){canvas.SaveLayer(layer);canvas.DrawRect(new(7,6,45,32),paint);paint.Color=SKColors.Blue;canvas.DrawRect(new(20,20,56,45),paint);canvas.Restore();}break;
        case 5:using(var shader=SKShader.CreateLinearGradient(new(0,0),new(64,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp)){paint.Shader=shader;canvas.DrawRect(new(3,4,62,49),paint);}break;
        case 6:using(var bitmap=new SKBitmap(7,5)){bitmap.Erase(SKColors.Green);bitmap.SetPixel(3,2,SKColors.Red);canvas.DrawBitmap(bitmap,new SKRect(5,6,54,41),paint);bitmap.Erase(SKColors.Blue);canvas.DrawBitmap(bitmap,new SKRect(60,6,74,16));}break;
        case 7:using(var bitmap=new SKBitmap(7,5)){bitmap.Erase(SKColors.Green);using var image=SKImage.FromBitmap(bitmap);canvas.DrawImage(image,new SKRect(4,3,67,48),new SKSamplingOptions(SKFilterMode.Linear),paint);}break;
        case 8:canvas.Save();canvas.Translate(11,13);paint.Shader=SKShader.CreateLinearGradient(new(0,0),new(64,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);canvas.DrawPaint(paint);paint.Shader.Dispose();canvas.Restore();break;
        case 9:canvas.Save();canvas.Translate(12,7);canvas.RotateDegrees(15);canvas.DrawRect(new(4,5,43,32),paint);canvas.Restore();break;
        case 10:using(var inner=new SKPictureRecorder()){var c=inner.BeginRecording(new(0,0,64,48));c.DrawRect(new(5,6,35,30),paint);using var picture=inner.EndRecording();canvas.DrawPicture(picture,8,5);canvas.DrawPicture(picture,25,26);}break;
        case 11:{var drawable=new DemoDrawable();deferred.Add(drawable);canvas.DrawDrawable(drawable,3,4);using var snapshot=drawable.Snapshot();canvas.DrawPicture(snapshot,29,25);uint before=drawable.GenerationId;drawable.NotifyDrawingChanged();Check(before!=drawable.GenerationId);Check(drawable.Bounds==new SKRect(0,0,24,20));}break;
        case 12:canvas.Save();canvas.DrawRect(new(1,1,40,40),paint);using(var path2=new SKPath()){path2.AddRect(new(5,5,20,20));canvas.ClipPath(path2,SKClipOperation.Difference);canvas.Clear(SKColors.Green);}canvas.Restore();break;
        case 13:using(var layer=new SKPaint{Color=new SKColor(255,255,255,150),BlendMode=SKBlendMode.Multiply}){canvas.SaveLayer(new SKRect(5,6,45,37),layer);canvas.DrawRect(new(0,0,60,50),paint);canvas.Restore();}break;
        default:throw new Exception();
    }
}
for(int scene=0;scene<14;scene++)
{
    using var recorder=new SKPictureRecorder();var recording=recorder.BeginRecording(new(0,0,80,64));Check(ReferenceEquals(recording,recorder.RecordingCanvas));var deferred=new List<IDisposable>();Scene(recording,scene,deferred);using var picture=recorder.EndRecording();foreach(var resource in deferred)resource.Dispose();Check(recorder.RecordingCanvas is null);Check(picture.ApproximateBytesUsed>0);Check(picture.UniqueId!=0);values[$"cull-{scene}"]=new[]{picture.CullRect.Left,picture.CullRect.Top,picture.CullRect.Right,picture.CullRect.Bottom};
    recorder.Dispose();
    for(int transform=0;transform<4;transform++)
    {
        using var surface=SKSurface.Create(new SKImageInfo(144,120,SKColorType.Rgba8888,SKAlphaType.Premul))!;var c=surface.Canvas;c.Clear(new SKColor(30,40,50));c.ClipRect(new(2,3,138,116));
        if(transform==1)c.Translate(13,11);if(transform==2){c.Translate(8,6);c.Scale(1.5f);}if(transform==3){c.Translate(18,5);c.RotateDegrees(10);}
        int count=c.SaveCount;var matrix=c.TotalMatrix;using var parent=new SKPaint{Color=new SKColor(255,255,255,180)};
        if(transform==3)c.DrawPicture(picture,parent);else picture.Playback(c);Check(c.SaveCount==count);Check(c.TotalMatrix==matrix);
        using var image=surface.Snapshot();using var read=new SKBitmap(new SKImageInfo(144,120,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind="picture",scene,transform,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);
    }
}
for(int mode=0;mode<4;mode++)for(int filter=0;filter<2;filter++)
{
    using var recorder=new SKPictureRecorder();var c=recorder.BeginRecording(new(0,0,16,12));using(var paint=new SKPaint{Color=SKColors.Red}){c.DrawRect(new SKRect(0,0,8,12),paint);paint.Color=SKColors.Blue;c.DrawRect(new SKRect(8,0,16,12),paint);}
    using var picture=recorder.EndRecording();using var shader=picture.ToShader((SKShaderTileMode)mode,(SKShaderTileMode)mode,(SKFilterMode)filter);using var target=SKSurface.Create(new SKImageInfo(144,120,SKColorType.Rgba8888,SKAlphaType.Premul))!;using var p=new SKPaint{Shader=shader};picture.Dispose();target.Canvas.Clear(SKColors.Green);target.Canvas.DrawPaint(p);
    using var image=target.Snapshot();using var read=new SKBitmap(new SKImageInfo(144,120,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind="picture-shader",scene=mode,transform=filter,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);
}
using(var recorder=new SKPictureRecorder()){var c=recorder.BeginRecording(new(0,0,80,64));using(var paint=new SKPaint{Color=SKColors.Red})c.DrawRect(new SKRect(4,5,30,25),paint);using var drawable=recorder.EndRecordingAsDrawable();using var snapshot=drawable.Snapshot();Check(snapshot.CullRect==new SKRect(0,0,80,64));Check(drawable.ApproximateBytesUsed>0);drawable.Dispose();using var b=new SKBitmap(80,64);using var canvas=new SKCanvas(b);snapshot.Playback(canvas);Check(b.GetPixel(10,10)==SKColors.Red);}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} picture images, {values.Count} metadata entries and {checks} lifetime/validation checks");
sealed class DemoDrawable : SKDrawable
{
    protected override SKRect OnGetBounds()=>new(0,0,24,20);
    protected override int OnGetApproximateBytesUsed()=>123;
    protected override void OnDraw(SKCanvas canvas){using var paint=new SKPaint{Color=SKColors.Gold};canvas.DrawRect(new SKRect(0,0,24,20),paint);}
}
