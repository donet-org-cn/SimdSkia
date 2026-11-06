#pragma warning disable CS0618
using SkiaSharp;
using System.Text;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);using var pixels=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new SortedDictionary<string,object?>();int offset=0;
void Draw(SKTextBlob? blob,object metadata,string key)
{
 values[key+"-null"]=blob is null;if(blob is not null){var b=blob.Bounds;values[key+"-bounds"]=new[]{b.Left,b.Top,b.Right,b.Bottom};values[key+"-id-valid"]=blob.UniqueId!=0;
 foreach(var strip in new[]{(-40f,-30f),(-10f,-8f),(-3f,1f),(2f,5f),(0f,0f),(-100f,100f)})
 {
  var gaps=blob.GetIntercepts(strip.Item1,strip.Item2);values[key+$"-gaps-{strip}"]=gaps;values[key+$"-gap-count-{strip}"]=blob.CountIntercepts(strip.Item1,strip.Item2);var buffer=new float[gaps.Length];blob.GetIntercepts(strip.Item1,strip.Item2,buffer);values[key+$"-gap-buffer-{strip}"]=buffer;
 }
}
 using var bitmap=new SKBitmap(new SKImageInfo(160,96,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);using var paint=new SKPaint{Color=SKColors.Black};if(blob is not null)canvas.DrawText(blob,8,48,paint);var bytes=bitmap.GetPixelSpan();pixels.Write(bytes);cases.Add(new{metadata,offset,length=bytes.Length});offset+=bytes.Length;
}
using var face=SKTypeface.FromFile("baseline/fonts/ZCOOLXiaoWei-Regular.ttf");
foreach(float size in new[]{12f,27f})foreach(float skew in new[]{0f,.2f})foreach(bool antialias in new[]{false,true})
{
 using var font=new SKFont(face,size,1.1f,skew){Hinting=SKFontHinting.None,LinearMetrics=true,Subpixel=true,BaselineSnap=false,Edging=antialias?SKFontEdging.Antialias:SKFontEdging.Alias};const string text="Ab中g";var glyphs=font.GetGlyphs(text);var utf8=Encoding.UTF8.GetBytes(text);float[] x=[1,20,43,78];SKPoint[] xy=[new(1,0),new(20,3),new(43,-2),new(78,4)];var rs=Enumerable.Range(0,4).Select(i=>SKRotationScaleMatrix.CreateDegrees(.7f+i*.2f,-20+i*15,x[i],i%2*3,0,0)).ToArray();uint[] clusters=[0,1,2,5];
 foreach(float pathSize in new[]{size,64f})
 {
  using var pf=new SKFont(face,pathSize,1.1f,skew){Hinting=SKFontHinting.None,Subpixel=true,LinearMetrics=true};using var gp=pf.GetGlyphPath(glyphs[0]);using var it=gp.CreateRawIterator();var pts=new SKPoint[4];var commands=new List<float>();for(SKPathVerb v;(v=it.Next(pts.AsSpan()))!=SKPathVerb.Done;){commands.Add((int)v);int count=v==SKPathVerb.Move?1:v==SKPathVerb.Line?2:v is SKPathVerb.Quad or SKPathVerb.Conic?3:v==SKPathVerb.Cubic?4:0;for(int i=0;i<count;i++){commands.Add(pts[i].X);commands.Add(pts[i].Y);}}
  values[$"path-diagnostic-{size}-{skew}-{antialias}-{pathSize}"]=commands.ToArray();
 }
 using(var legacy=new SKPaint(font){IsAntialias=antialias})
 foreach(var encoding in new[]{SKTextEncoding.Utf8,SKTextEncoding.Utf16,SKTextEncoding.Utf32})
 {
  legacy.TextEncoding=encoding;var encoded=StringUtilities.GetEncodedText(text,encoding);var pin=System.Runtime.InteropServices.GCHandle.Alloc(encoded,System.Runtime.InteropServices.GCHandleType.Pinned);
  try
  {
   foreach(var strip in new[]{(-10f,-8f),(-3f,1f),(2f,5f),(-100f,100f)})
   {
    string key=$"legacy-{size}-{skew}-{antialias}-{encoding}-{strip}";
    values[key+"-ordinary"]=legacy.GetTextIntercepts(text,1,0,strip.Item1,strip.Item2);values[key+"-byte"]=legacy.GetTextIntercepts(encoded,1,0,strip.Item1,strip.Item2);values[key+"-span"]=legacy.GetTextIntercepts(encoded.AsSpan(),1,0,strip.Item1,strip.Item2);values[key+"-pointer"]=legacy.GetTextIntercepts(pin.AddrOfPinnedObject(),encoded.Length,1,0,strip.Item1,strip.Item2);
    values[key+"-xy"]=legacy.GetPositionedTextIntercepts(text,xy,strip.Item1,strip.Item2);values[key+"-xy-byte"]=legacy.GetPositionedTextIntercepts(encoded,xy,strip.Item1,strip.Item2);values[key+"-xy-pointer"]=legacy.GetPositionedTextIntercepts(pin.AddrOfPinnedObject(),encoded.Length,xy,strip.Item1,strip.Item2);
    values[key+"-horizontal"]=legacy.GetHorizontalTextIntercepts(text,x,0,strip.Item1,strip.Item2);values[key+"-horizontal-byte"]=legacy.GetHorizontalTextIntercepts(encoded,x,0,strip.Item1,strip.Item2);values[key+"-horizontal-pointer"]=legacy.GetHorizontalTextIntercepts(pin.AddrOfPinnedObject(),encoded.Length,x,0,strip.Item1,strip.Item2);
    using var blob=SKTextBlob.Create(text,font,new SKPoint(1,0));values[key+"-blob"]=legacy.GetTextIntercepts(blob!,strip.Item1,strip.Item2);
   }
  }
  finally{pin.Free();}
 }
 foreach(int kind in new[]{0,1,2,3})foreach(bool textRun in new[]{false,true})foreach(bool raw in new[]{false,true})foreach(bool explicitBounds in new[]{false,true})
 {
  string key=$"{size}-{skew}-{antialias}-{kind}-{textRun}-{raw}-{explicitBounds}";SKRect? bounds=explicitBounds?new SKRect(-20,-40,145,40):null;using var builder=new SKTextBlobBuilder();
  if(raw)
  {
   if(kind<2){var run=kind==0?(textRun?builder.AllocateRawTextRun(font,4,1,0,utf8.Length,bounds):builder.AllocateRawRun(font,4,1,0,bounds)):(textRun?builder.AllocateRawHorizontalTextRun(font,4,0,utf8.Length,bounds):builder.AllocateRawHorizontalRun(font,4,0,bounds));glyphs.CopyTo(run.Glyphs);if(kind==1)x.CopyTo(run.Positions);if(textRun){utf8.CopyTo(run.Text);clusters.CopyTo(run.Clusters);values[key+"-text"]=run.Text.ToArray();values[key+"-clusters"]=run.Clusters.ToArray();}values[key+"-pos-length"]=run.Positions.Length;}
   else if(kind==2){var run=textRun?builder.AllocateRawPositionedTextRun(font,4,utf8.Length,bounds):builder.AllocateRawPositionedRun(font,4,bounds);glyphs.CopyTo(run.Glyphs);xy.CopyTo(run.Positions);if(textRun){utf8.CopyTo(run.Text);clusters.CopyTo(run.Clusters);}values[key+"-pos-length"]=run.Positions.Length;}
   else{var run=textRun?builder.AllocateRawRotationScaleTextRun(font,4,utf8.Length,bounds):builder.AllocateRawRotationScaleRun(font,4,bounds);glyphs.CopyTo(run.Glyphs);rs.CopyTo(run.Positions);if(textRun){utf8.CopyTo(run.Text);clusters.CopyTo(run.Clusters);}values[key+"-pos-length"]=run.Positions.Length;}
  }
  else
  {
   SKRunBuffer run=(kind,textRun) switch{(0,false)=>builder.AllocateRun(font,4,1,0,bounds),(0,true)=>builder.AllocateTextRun(font,4,1,0,utf8.Length,bounds),(1,false)=>builder.AllocateHorizontalRun(font,4,0,bounds),(1,true)=>builder.AllocateHorizontalTextRun(font,4,0,utf8.Length,bounds),(2,false)=>builder.AllocatePositionedRun(font,4,bounds),(2,true)=>builder.AllocatePositionedTextRun(font,4,utf8.Length,bounds),(3,false)=>builder.AllocateRotationScaleRun(font,4,bounds),_=>builder.AllocateRotationScaleTextRun(font,4,utf8.Length,bounds)};run.SetGlyphs(glyphs);switch(run){case SKHorizontalRunBuffer r:r.SetPositions(x);break;case SKHorizontalTextRunBuffer r:r.SetPositions(x);break;case SKPositionedRunBuffer r:r.SetPositions(xy);break;case SKPositionedTextRunBuffer r:r.SetPositions(xy);break;case SKRotationScaleRunBuffer r:r.SetPositions(rs);break;case SKRotationScaleTextRunBuffer r:r.SetPositions(rs);break;}if(run is SKTextRunBuffer t){t.SetText(utf8);t.SetClusters(clusters);values[key+"-text"]=t.Text.ToArray();values[key+"-clusters"]=t.Clusters.ToArray();values[key+"-textsize"]=t.TextSize;}values[key+"-size"]=run.Size;
  }
  using var blob=builder.Build();Draw(blob,new{size,skew,antialias,kind,textRun,raw,explicitBounds},key);values[key+"-builder-empty"]=builder.Build() is null;
 }
 // Attribute font raster differences independently from the new run buffers.
 using(var outline=new SKPath())
 {
  var widths=font.GetGlyphWidths(glyphs.AsSpan());float cursor=1;for(int i=0;i<glyphs.Length;i++){using var g=font.GetGlyphPath(glyphs[i]);g.Transform(SKMatrix.CreateTranslation(cursor,0));outline.AddPath(g);cursor+=widths[i];}
  using var b=new SKBitmap(new SKImageInfo(160,96,SKColorType.Rgba8888,SKAlphaType.Premul));using var c=new SKCanvas(b);c.Clear(SKColors.White);using var p=new SKPaint{Color=SKColors.Black,IsAntialias=antialias};c.Translate(8,48);c.DrawPath(outline,p);var bytes=b.GetPixelSpan();pixels.Write(bytes);cases.Add(new{metadata=(object)new{size,skew,antialias,outlineDiagnostic=true},offset,length=bytes.Length});offset+=bytes.Length;
 }
 foreach(var align in Enum.GetValues<SKTextAlign>())
 {
  using var b=new SKBitmap(new SKImageInfo(160,96,SKColorType.Rgba8888,SKAlphaType.Premul));using var c=new SKCanvas(b);c.Clear(SKColors.White);using var p=new SKPaint{Color=SKColors.Black,IsAntialias=antialias};c.DrawText(text,70,48,align,font,p);var bytes=b.GetPixelSpan();pixels.Write(bytes);cases.Add(new{metadata=(object)new{size,skew,antialias,align=(int)align,ordinaryText=true},offset,length=bytes.Length});offset+=bytes.Length;
 }
 foreach(bool warp in new[]{false,true})foreach(var align in Enum.GetValues<SKTextAlign>())
 {
  using var pth=new SKPath();pth.MoveTo(0,-8);pth.CubicTo(45,-45,80,30,140,-8);using var b=new SKBitmap(new SKImageInfo(160,96,SKColorType.Rgba8888,SKAlphaType.Premul));using var c=new SKCanvas(b);c.Clear(SKColors.White);using var p=new SKPaint{Color=SKColors.Black,IsAntialias=antialias};c.Translate(8,48);c.DrawTextOnPath(text,pth,new SKPoint(0,2),warp,align,font,p);var bytes=b.GetPixelSpan();pixels.Write(bytes);cases.Add(new{metadata=(object)new{size,skew,antialias,warp,align=(int)align,textOnPath=true},offset,length=bytes.Length});offset+=bytes.Length;
 }
 for(int scene=0;scene<7;scene++)
 {
  using var builder=new SKTextBlobBuilder();using var path=new SKPath();path.MoveTo(0,-8);path.CubicTo(45,-45,80,30,140,-8);SKTextBlob? blob;
  if(scene==0){builder.AddRun(glyphs,font,new(1,0));blob=builder.Build();}else if(scene==1){builder.AddHorizontalRun(glyphs,font,x,0);blob=builder.Build();}else if(scene==2){builder.AddPositionedRun(glyphs,font,xy);blob=builder.Build();}else if(scene==3){builder.AddRotationScaleRun(glyphs,font,rs);blob=builder.Build();}else if(scene==4)blob=SKTextBlob.CreateRotationScale(text,font,rs);else blob=SKTextBlob.CreatePathPositioned(text,font,path,scene==5?SKTextAlign.Left:SKTextAlign.Center,new(0,2));
  using(blob)Draw(blob,new{size,skew,antialias,scene},$"factory-{size}-{skew}-{antialias}-{scene}");
 }
}
File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {cases.Count} text blob images, {values.Count} metadata cases");
