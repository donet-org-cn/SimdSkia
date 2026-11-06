#pragma warning disable CS0618
using SkiaSharp;
using System.Runtime.InteropServices;
int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("check "+checks);}
if(args.Contains("--require-aot"))Check(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
using(var data=SKData.CreateCopy(new byte[]{1,2,3,4,5})){using var subset=data.Subset(1,3);using var stream=subset.AsStream();data.Dispose();Check(stream.ReadByte()==2);stream.WriteByte(9);Check(subset.AsSpan()[1]==9);Check(subset.Data!=IntPtr.Zero);GC.Collect();Check(subset.AsSpan()[2]==4);}
using(var writer=new SKDynamicMemoryWStream()){Check(writer.WritePackedUInt32(65536));using var data=writer.DetachAsData();using var stream=new SKMemoryStream(data);Check(stream.ReadByte()==255&&stream.ReadUInt32()==65536);Check(writer.BytesWritten==0);}
using(var path=SKPath.ParseSvgPathData("M2 4A30 40 15 0 1 70 80Z")){Check(path is not null);using var it=path!.CreateIterator(false);Span<SKPoint> points=stackalloc SKPoint[4];int conics=0;for(SKPathVerb v;(v=it.Next(points))!=SKPathVerb.Done;)if(v==SKPathVerb.Conic)conics++;Check(conics==2);using var measure=new SKPathMeasure(path);Check(measure.Length>100);Check(measure.GetPositionAndTangent(12,out var p,out var t)&&float.IsFinite(p.X)&&Math.Abs(t.Length-1)<.0001f);using var segment=measure.GetSegment(5,50,true);Check(segment is not null&&segment.VerbCount>1);}
using(var line=new SKPath()){line.MoveTo(0,0);line.LineTo(3,4);using var measure=new SKPathMeasure(line);Check(measure.Length==5);Check(measure.GetPosition(2.5f)==new SKPoint(1.5f,2));}
var bytes=new byte[128];var handle=GCHandle.Alloc(bytes,GCHandleType.Pinned);int releases=0;
try{
    var info=new SKImageInfo(4,4,SKColorType.Rgba8888,SKAlphaType.Premul);var ptr=handle.AddrOfPinnedObject();
    var bitmap=new SKBitmap();Check(bitmap.InstallPixels(info,ptr,32,(p,c)=>releases++,new object()));var pixmap=bitmap.PeekPixels();bitmap.Dispose();Check(releases==0);Check(pixmap.Erase(SKColors.Red));Check(bytes[0]==255&&bytes[3]==255);pixmap.Dispose();Check(releases==1);
    using var props=new SKSurfaceProperties(SKSurfacePropsFlags.UseDeviceIndependentFonts,SKPixelGeometry.Unknown);using(var surface=SKSurface.Create(info,ptr,32,props)){surface.Canvas.Clear(SKColors.Blue);using var image=surface.Snapshot(new SKRectI(1,1,3,3));Check(image.Width==2&&image.Height==2);Check(surface.SurfaceProperties.IsUseDeviceIndependentFonts);}Check(bytes[2]==255&&bytes[3]==255);
}finally{handle.Free();}
using(var face=SKTypeface.FromFile("baseline/fonts/ZCOOLXiaoWei-Regular.ttf"))using(var font=new SKFont(face,16))
{
    string text="Hello 中文";var ids=font.GetGlyphs(text);Check(ids.Length==8&&ids.All(g=>g!=0));var widths=new float[ids.Length];font.GetGlyphOffsets(text,widths.AsSpan());Check(widths[1]>0&&font.MeasureText(text)>widths[^1]);Check(font.Metrics.Ascent<0&&font.Spacing>0);Check(face.GetTableData(0x68656164).Length>=54);using var blob=SKTextBlob.Create(text,font);Check(blob is not null&&!blob.Bounds.IsEmpty);using var paint=new SKPaint(font);Check(paint.GetGlyphs(text).SequenceEqual(ids));using var clone=paint.Clone();paint.TextSize=24;Check(clone.TextSize==16);using var manager=SKFontManager.CreateDefault();using var custom=manager.CreateTypeface("baseline/fonts/ZCOOLXiaoWei-Regular.ttf");Check(custom.GlyphCount==face.GlyphCount);
}
var matrix=SKMatrix.CreateScaleTranslation(2,3,4,5);var values=new float[9];matrix.GetValues(values);var mapped=new SKMatrix(values).Invert().MapPoint(6,8);Check(Math.Abs(mapped.X-1)<.00001f&&Math.Abs(mapped.Y-1)<.00001f);
Check(SKColorType.RgbaF32.GetBytesPerPixel()==16);Check(SKPixelGeometry.BgrVertical.IsVertical());
using(var region=new SKRegion(new SKRectI(2,3,24,26)))
{Check(region.Op(new SKRectI(8,10,16,20),SKRegionOperation.Difference));Check(region.Contains(3,4)&&!region.Contains(10,12));using var boundary=region.GetBoundaryPath();using var restored=new SKRegion(boundary);Check(restored.Contains(region)&&region.Contains(restored));using var bitmap=new SKBitmap(32,32);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);canvas.Translate(100,200);canvas.ClipRegion(region);canvas.ResetMatrix();using var paint=new SKPaint{Color=SKColors.Red};canvas.DrawRect(new SKRect(0,0,32,32),paint);Check(bitmap.GetPixel(3,4)==SKColors.Red&&bitmap.GetPixel(10,12)==SKColors.White);}
using(var rounded=new SKRoundRect(new SKRect(0,0,40,30),5,7))using(var path=new SKPath()){path.AddRoundRect(rounded,SKPathDirection.Clockwise,3);Check(path.IsRoundRect&&path.SegmentMasks==(SKPathSegmentMask.Line|SKPathSegmentMask.Conic));using var data=path.GetRoundRect();Check(data.Rect==rounded.Rect&&data.Radii.SequenceEqual(rounded.Radii));}
var shaderPixels=Marshal.AllocHGlobal(16);int shaderReleases=0;
try
{
    var b=new SKBitmap();Check(b.InstallPixels(new SKImageInfo(2,2,SKColorType.Rgba8888,SKAlphaType.Premul),shaderPixels,8,(p,c)=>shaderReleases++));b.Erase(SKColors.Blue);b.SetImmutable();var sh=b.ToShader();b.Dispose();Check(shaderReleases==0);var p=new SKPaint{Shader=sh};var cloned=p.Clone();sh.Dispose();p.Dispose();Check(shaderReleases==0);using var dst=new SKBitmap(2,2);using var cv=new SKCanvas(dst);cv.DrawRect(0,0,2,2,cloned);Check(dst.GetPixel(0,0)==SKColors.Blue);cloned.Dispose();Check(shaderReleases==1);
}
finally{Marshal.FreeHGlobal(shaderPixels);}
// Raster shader, retained paints, filtered bitmap and arithmetic remain real in AOT.
using(var bitmap=new SKBitmap(new SKImageInfo(2,2,SKColorType.Rgba8888,SKAlphaType.Premul)))
{
    bitmap.Erase(SKColors.Red);bitmap.SetImmutable();using var shader=bitmap.ToShader(SKShaderTileMode.Repeat,SKShaderTileMode.Mirror,new SKSamplingOptions(SKFilterMode.Linear));using var filter=SKColorFilter.CreateLighting(new SKColor(128,255,255),new SKColor(0,20,0));using var filtered=shader.WithColorFilter(filter);using var paint=new SKPaint{Shader=filtered};using var clone=paint.Clone();shader.Dispose();filtered.Dispose();bitmap.Dispose();paint.Dispose();using var target=new SKBitmap(8,8);using var canvas=new SKCanvas(target);canvas.Clear(SKColors.Black);canvas.DrawRect(0,0,8,8,clone);var c=target.GetPixel(4,4);Check(Math.Abs(c.Red-128)<=1&&Math.Abs(c.Green-20)<=1&&c.Alpha==255);
    using var ar=SKBlender.CreateArithmetic(0,.5f,.5f,0,true);using var a=SKShader.CreateColor(SKColors.Blue);using var b=SKShader.CreateColor(SKColors.Red);using var composed=SKShader.CreateBlend(ar,a,b);clone.Shader=composed;canvas.DrawRect(0,0,8,8,clone);c=target.GetPixel(4,4);Check(Math.Abs(c.Red-128)<=1&&Math.Abs(c.Blue-128)<=1);
    clone.Shader=null;clone.Color=SKColors.White;clone.ColorFilter=SKColorFilter.CreateLumaColor();canvas.DrawRect(0,0,8,8,clone);Check(target.GetPixel(4,4)==SKColors.Black);
}
using(var face=SKTypeface.FromFile("baseline/fonts/ZCOOLXiaoWei-Regular.ttf"))using(var font=new SKFont(face,18){Hinting=SKFontHinting.None,Subpixel=true,LinearMetrics=true,BaselineSnap=false})
{
    var ids=font.GetGlyphs("Ab中g");using var glyph=font.GetGlyphPath(ids[0]);Check(!glyph.IsEmpty);using var builder=new SKTextBlobBuilder();var utf8=System.Text.Encoding.UTF8.GetBytes("Ab中g");var run=builder.AllocateRawHorizontalTextRun(font,ids.Length,0,utf8.Length);ids.CopyTo(run.Glyphs);font.GetGlyphOffsets(ids.AsSpan(),run.Positions);utf8.CopyTo(run.Text);new uint[]{0,1,2,5}.CopyTo(run.Clusters);Check(run.Text.SequenceEqual(utf8)&&run.Clusters.Length==ids.Length);using var blob=builder.Build()!;Check(blob is not null&&builder.Build() is null);bool stale=false;try{_=run.Glyphs.Length;}catch(ObjectDisposedException){stale=true;}Check(stale);var intervals=blob!.GetIntercepts(-3,1);Check(intervals.Length==blob.CountIntercepts(-3,1)&&intervals.Length>0);var intervalBuffer=new float[intervals.Length];blob.GetIntercepts(-3,1,intervalBuffer);Check(intervalBuffer.AsSpan().SequenceEqual(intervals));
    using var target=new SKBitmap(180,80);using var canvas=new SKCanvas(target);using var paint=new SKPaint{Color=SKColors.Black,IsAntialias=true};canvas.Clear(SKColors.White);canvas.DrawText(blob,5,40,paint);Check(target.GetPixelSpan().Contains((byte)0));using var path=new SKPath();path.MoveTo(0,40);path.QuadTo(90,0,170,40);canvas.DrawTextOnPath("Ab中g",path,new SKPoint(0,0),true,SKTextAlign.Left,font,paint);Check(target.GetPixelSpan().Contains((byte)0));
}
using(var bitmap=new SKBitmap(40,32))using(var canvas=new SKCanvas(bitmap))
{
    using var cf=SKColorFilter.CreateLighting(SKColors.White,SKColors.Black);using var color=SKImageFilter.CreateColorFilter(cf);using var offset=SKImageFilter.CreateOffset(3,2,color);using var paint=new SKPaint{Color=SKColors.Red,ImageFilter=offset};color.Dispose();offset.Dispose();canvas.DrawRect(4,4,12,10,paint);Check(bitmap.GetPixel(8,8)==SKColors.Red);using var clone=paint.Clone();paint.Dispose();canvas.Clear();canvas.DrawRect(4,4,12,10,clone);Check(bitmap.GetPixel(8,8)==SKColors.Red);
    using var blur=SKImageFilter.CreateBlur(2,1);clone.ImageFilter=blur;canvas.Clear();canvas.DrawRect(12,8,4,4,clone);Check(bitmap.GetPixel(11,9).Alpha>0);clone.Reset();Check(clone.ImageFilter is null);
    using var outline=SKImageFilter.CreateDilate(1,1);clone.ImageFilter=outline;clone.Color=SKColors.Blue;canvas.Clear();canvas.DrawRect(12,8,4,4,clone);Check(bitmap.GetPixel(11,9)==SKColors.Blue);
}
var swizzle=new byte[]{10,20,30,40,50,60,70,80};SKSwizzle.SwapRedBlue(swizzle.AsSpan());Check(swizzle.AsSpan().SequenceEqual(new byte[]{30,20,10,40,70,60,50,80}));var textBytes=StringUtilities.GetEncodedText("Ab中",SKTextEncoding.Utf8);Check(StringUtilities.GetString(textBytes,SKTextEncoding.Utf8)=="Ab中");
using(var linear=SKColorSpace.CreateSrgbLinear())using(var rgb=SKColorSpace.CreateRgb(SKColorSpaceTransferFn.TwoDotTwo,SKColorSpaceXyz.DisplayP3))
{
    Check(linear.GammaIsLinear&&!linear.IsSrgb);using var profile=rgb.ToProfile();using var restored=SKColorSpace.CreateIcc(profile);Check(SKColorSpace.Equal(rgb,restored)&&profile.ToColorSpaceXyz()==SKColorSpaceXyz.DisplayP3);
    using var source=new SKBitmap(new SKImageInfo(8,4,SKColorType.Rgba8888,SKAlphaType.Unpremul,linear));source.Erase(new SKColor(128,128,128));using var image=SKImage.FromBitmap(source);using var target=new SKBitmap(new SKImageInfo(8,4,SKColorType.Rgba8888,SKAlphaType.Unpremul,SKColorSpace.CreateSrgb()));Check(image.ReadPixels(target.Info,target.GetPixels(),target.RowBytes,0,0));Check(Math.Abs(target.GetPixel(1,1).Red-128)<=1);
}
using(var bitmap=new SKBitmap(64,48))using(var canvas=new SKCanvas(bitmap))using(var path=new SKPath())
{
    path.AddRect(new SKRect(15,15,45,35));using var corner=SKPathEffect.CreateCorner(4);using var paint=new SKPaint{PathEffect=corner,Color=SKColors.Red,IsAntialias=true};using(var filled=paint.GetFillPath(path))Check(filled.VerbCount>path.VerbCount);corner.Dispose();canvas.DrawPath(path,paint);Check(bitmap.GetPixel(30,25)==SKColors.Red&&bitmap.GetPixel(15,15).Alpha<255);
    using var dash=SKPathEffect.CreateDash([4,4],0);paint.PathEffect=dash;paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=2;using(var filled=paint.GetFillPath(path))Check(filled.VerbCount>10);
    paint.PathEffect=null;paint.Style=SKPaintStyle.Fill;using var mask=SKMaskFilter.CreateBlur(SKBlurStyle.Normal,2);paint.MaskFilter=mask;mask.Dispose();canvas.Clear();canvas.DrawRect(new SKRect(20,18,40,32),paint);Check(bitmap.GetPixel(19,25).Alpha>0);paint.Reset();Check(paint.PathEffect is null&&paint.MaskFilter is null);
    using var map=bitmap.PeekPixels();using var png=map.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.Sub,1));using var decoded=SKBitmap.Decode(png);Check(decoded.Width==64&&decoded.Height==48);using var jpeg=map.Encode(new SKJpegEncoderOptions(80,SKJpegEncoderDownsample.Downsample420,SKJpegEncoderAlphaOption.BlendOnBlack));Check(jpeg.AsSpan()[0]==255&&jpeg.AsSpan()[1]==216);
    using var stream=new SKMemoryStream(png.ToArray());using var codec=SKCodec.Create(stream,out var codecResult)!;Check(codecResult==SKCodecResult.Success&&codec.EncodedOrigin==SKEncodedOrigin.TopLeft);using var output=new SKBitmap(codec.Info);Check(codec.StartIncrementalDecode(output.Info,output.GetPixels(),output.RowBytes)==SKCodecResult.Success&&codec.IncrementalDecode(out var rows)==SKCodecResult.Success&&rows==48);Check(codec.GetScaledDimensions(.5f)==codec.Info.Size);
    using var texture=new GRBackendTexture(64,48,true,new GRGlTextureInfo(3553,42,32856));Check(texture.IsValid&&texture.HasMipMaps&&texture.GetGlTextureInfo().Id==42);Check(canvas.DeviceClipBounds.Width==64&&canvas.IsClipRect);
    var pixel=new byte[]{7,14,21,42};var straight=new byte[4];SimdSkia.Cpu.PixelTransferKernel.Unpremultiply(pixel,straight);Check(straight[0]==42&&straight[3]==42);
}
Console.WriteLine($"{{\"checks\":{checks},\"passed\":true}}");
