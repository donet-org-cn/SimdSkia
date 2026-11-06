using SkiaSharp;
using System.Text.Json;
using System.Security.Cryptography;
int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Cache assertion "+checks);}
SKGraphics.Init();SKGraphics.PurgeAllCaches();long originalFont=SKGraphics.GetFontCacheLimit(),originalResource=SKGraphics.GetResourceCacheTotalByteLimit(),originalSingle=SKGraphics.GetResourceCacheSingleAllocationByteLimit();int originalCount=SKGraphics.GetFontCacheCountLimit();
var rows=new List<object>();using var typeface=SKTypeface.FromFile("baseline/fonts/ZenOldMincho-Regular.ttf");using var font=new SKFont(typeface,24){Edging=SKFontEdging.Alias,Hinting=SKFontHinting.None};
byte[] Render()
{using var bitmap=new SKBitmap(new SKImageInfo(320,100,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);using var clip=new SKPath();clip.AddCircle(140,45,120);canvas.ClipPath(clip,SKClipOperation.Intersect,true);using var paint=new SKPaint{Color=SKColors.Black};canvas.DrawText("中文缓存 Cache 123",7,43,font,paint);canvas.DrawText("中文缓存 Cache 123",7,76,font,paint);return bitmap.GetPixelSpan().ToArray();}
try
{
 var before=Render();for(int i=0;i<4;i++)Check(before.AsSpan().SequenceEqual(Render()));Check(SKGraphics.GetFontCacheUsed()>0);Check(SKGraphics.GetFontCacheCountUsed()>0);
 rows.Add(new{stage="warm",fontBytes=SKGraphics.GetFontCacheUsed(),fontOwners=SKGraphics.GetFontCacheCountUsed(),resourceBytes=SKGraphics.GetResourceCacheTotalBytesUsed()});
 using(var dump=new Dump()){SKGraphics.DumpMemoryStatistics(dump);Check(dump.Values>0);}
 SKGraphics.PurgeAllCaches();rows.Add(new{stage="purged",fontBytes=SKGraphics.GetFontCacheUsed(),fontOwners=SKGraphics.GetFontCacheCountUsed(),resourceBytes=SKGraphics.GetResourceCacheTotalBytesUsed()});Check(before.AsSpan().SequenceEqual(Render()));
 Check(SKGraphics.SetFontCacheLimit(8192)==originalFont);Check(SKGraphics.GetFontCacheLimit()==8192);Check(SKGraphics.SetFontCacheCountLimit(2)==originalCount);Check(SKGraphics.GetFontCacheCountLimit()==2);
 for(int i=0;i<4;i++)Check(before.AsSpan().SequenceEqual(Render()));Check(SKGraphics.GetFontCacheUsed()<=8192);Check(SKGraphics.GetFontCacheCountUsed()<=2);
 Check(SKGraphics.SetResourceCacheTotalByteLimit(4096)==originalResource);Check(SKGraphics.SetResourceCacheSingleAllocationByteLimit(1024)==originalSingle);Check(before.AsSpan().SequenceEqual(Render()));Check(SKGraphics.GetResourceCacheTotalBytesUsed()<=4096);
 SKGraphics.SetFontCacheLimit(0);SKGraphics.SetFontCacheCountLimit(0);for(int i=0;i<3;i++)Check(before.AsSpan().SequenceEqual(Render()));Check(SKGraphics.GetFontCacheUsed()==0);Check(SKGraphics.GetFontCacheCountUsed()==0);
 SKGraphics.SetResourceCacheTotalByteLimit(0);Check(before.AsSpan().SequenceEqual(Render()));Check(SKGraphics.GetResourceCacheTotalBytesUsed()==0);
 rows.Add(new{stage="disabled",fontBytes=SKGraphics.GetFontCacheUsed(),fontOwners=SKGraphics.GetFontCacheCountUsed(),resourceBytes=SKGraphics.GetResourceCacheTotalBytesUsed()});
 SKGraphics.SetFontCacheLimit(1024*1024);SKGraphics.SetFontCacheCountLimit(16);
 Parallel.For(0,24,i=>{using var face=SKTypeface.FromFile("baseline/fonts/ZenOldMincho-Regular.ttf");using var f=new SKFont(face,16+i%4);for(int j=0;j<3;j++){using var b=new SKBitmap(180,40);using var c=new SKCanvas(b);using var p=new SKPaint();c.DrawText("中文缓存abc",0,28,f,p);}});Check(SKGraphics.GetFontCacheUsed()<=1024*1024);
 Console.WriteLine(JsonSerializer.Serialize(new{checks,defaultFontLimit=originalFont,defaultCountLimit=originalCount,defaultResourceLimit=originalResource,defaultSingleLimit=originalSingle,renderSha256=Convert.ToHexString(SHA256.HashData(before)),rows},new JsonSerializerOptions{WriteIndented=true}));
}
finally{SKGraphics.SetFontCacheLimit(originalFont);SKGraphics.SetFontCacheCountLimit(originalCount);SKGraphics.SetResourceCacheTotalByteLimit(originalResource);SKGraphics.SetResourceCacheSingleAllocationByteLimit(originalSingle);SKGraphics.PurgeAllCaches();}
sealed class Dump:SKTraceMemoryDump
{public int Values;public Dump():base(true,true){}protected override void OnDumpNumericValue(string dumpName,string valueName,string units,ulong value)=>Values++;}
