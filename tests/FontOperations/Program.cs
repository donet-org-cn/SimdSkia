#pragma warning disable CS0618
using SkiaSharp;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
var output=args[0];Directory.CreateDirectory(output);var values=new SortedDictionary<string,object?>();
float[] Rect(SKRect r)=>[r.Left,r.Top,r.Right,r.Bottom];
foreach(string file in new[]{"ZCOOLXiaoWei-Regular.ttf","ZenOldMincho-Regular.ttf"})
foreach(float size in new[]{0f,12f,23.5f})
foreach(float sx in new[]{1f,1.25f})
{
    using var face=SKTypeface.FromFile("baseline/fonts/"+file);using var font=new SKFont(face,size,sx,.125f){Hinting=SKFontHinting.None,LinearMetrics=true,Subpixel=true,BaselineSnap=false};
    string key=$"{file}-{size}-{sx}";
    var m=font.Metrics;values[key+"-metrics"]=new float?[]{m.Top,m.Ascent,m.Descent,m.Bottom,m.Leading,m.AverageCharacterWidth,m.MaxCharacterWidth,m.XMin,m.XMax,m.XHeight,m.CapHeight,m.UnderlineThickness,m.UnderlinePosition,m.StrikeoutThickness,m.StrikeoutPosition,font.Spacing};
    values[key+"-postscript"]=face.PostScriptName;values[key+"-fixed"]=face.IsFixedPitch;values[key+"-tags"]=face.GetTableTags().Order().ToArray();foreach(uint tag in face.GetTableTags())values[key+"-table-"+tag]=Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(face.GetTableData(tag)));
    using var faceStream=face.OpenStream(out int faceIndex);using var reloaded=SKTypeface.FromStream(faceStream);values[key+"-reload"]=new[]{reloaded.GlyphCount,reloaded.UnitsPerEm,faceIndex};
    var kern=face.GetKerningPairAdjustments(face.GetGlyphs("AVATAR To WA"));values[key+"-kern"]=kern;values[key+"-has-kern"]=face.HasGetKerningPairAdjustments;
    string[] texts=["","A","AVATAR To WA","中文测试 Hello 123","🚀AB","缺字\u0378","a\u0301"];
    foreach(var text in texts)
    {
        string prefix=key+"-"+text;var ids=font.GetGlyphs(text);values[prefix+"-glyphs"]=ids;values[prefix+"-count"]=font.CountGlyphs(text);values[prefix+"-contains"]=font.ContainsGlyphs(text);
        foreach(var enc in new[]{SKTextEncoding.Utf8,SKTextEncoding.Utf16,SKTextEncoding.Utf32,SKTextEncoding.GlyphId})
        {
            byte[] bytes=enc switch{SKTextEncoding.Utf8=>Encoding.UTF8.GetBytes(text),SKTextEncoding.Utf16=>Encoding.Unicode.GetBytes(text),SKTextEncoding.Utf32=>Encoding.UTF32.GetBytes(text),_=>MemoryMarshal.AsBytes(ids.AsSpan()).ToArray()};
            values[prefix+"-glyphs-"+enc]=font.GetGlyphs(bytes,enc);values[prefix+"-width-"+enc]=font.MeasureText(bytes,enc,out var b);values[prefix+"-bounds-"+enc]=Rect(b);
            values[prefix+"-break-"+enc]=new[]{(float)font.BreakText(bytes,enc,35,out var mw),mw};
        }
        values[prefix+"-widths"]=font.GetGlyphWidths(ids,out var bounds);values[prefix+"-glyph-bounds"]=bounds.Select(Rect).ToArray();values[prefix+"-offsets"]=font.GetGlyphOffsets(text,2.5f);values[prefix+"-positions"]=font.GetGlyphPositions(text,new SKPoint(2.5f,8)).SelectMany(p=>new[]{p.X,p.Y}).ToArray();
        values[prefix+"-break"]=new[]{(float)font.BreakText(text,35,out var width),width};
        using var paint=new SKPaint(font);values[prefix+"-paint-width"]=paint.MeasureText(text);values[prefix+"-paint-glyphs"]=paint.GetGlyphs(text);values[prefix+"-paint-break"]=new object[]{paint.BreakText(text,35,out var paintedWidth,out var measuredText),paintedWidth,measuredText};using var blob=SKTextBlob.Create(text,font,new SKPoint(3,12));values[prefix+"-blob-bounds"]=blob is null?null:Rect(blob.Bounds);
        using var path=font.GetTextPath(text,new SKPoint(3,12));values[prefix+"-path-bounds"]=Rect(path.TightBounds);
    }
}
foreach(string file in new[]{"ZCOOLXiaoWei-Regular.ttf","ZenOldMincho-Regular.ttf"})
{
    using var face=SKTypeface.FromFile("baseline/fonts/"+file);using var font=new SKFont(face);var codepoints=Enumerable.Range(0,65536).Where(cp=>cp<0xd800||cp>0xdfff).Concat(Enumerable.Range(0,8192).Select(i=>65536+i*127)).ToArray();
    var glyphs=font.GetGlyphs(codepoints);File.WriteAllBytes(Path.Combine(output,file+".glyphs.bin"),MemoryMarshal.AsBytes(glyphs.AsSpan()).ToArray());values[file+"-full-cmap-count"]=codepoints.Length;
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {values.Count} font behavior cases");
