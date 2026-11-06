#pragma warning disable CS0618
using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var data=new SortedDictionary<string,object?>();
void Texture(GRBackendTexture texture,string key)
{
    using(texture){bool ok=texture.GetGlTextureInfo(out var g);data[key+"-texture"]=new object[]{texture.IsValid,texture.Width,texture.Height,texture.Size.Width,texture.Rect.Right,texture.HasMipMaps,(int)texture.Backend,ok,g.Target,g.Id,g.Format,g.Protected,texture.GetGlTextureInfo()==g};}
}
void Target(GRBackendRenderTarget target,string key)
{
    using(target){bool ok=target.GetGlFramebufferInfo(out var g);data[key+"-target"]=new object[]{target.IsValid,target.Width,target.Height,target.Size.Height,target.Rect.Bottom,target.SampleCount,target.StencilBits,(int)target.Backend,ok,g.FramebufferObjectId,g.Format,g.Protected,target.GetGlFramebufferInfo()==g};}
}
foreach(int width in new[]{0,1,16})foreach(int height in new[]{0,1,9})foreach(uint id in new uint[]{0,17})foreach(uint format in new uint[]{0,0x8058})foreach(bool mip in new[]{false,true})
{
    string key=$"{width}-{height}-{id}-{format}-{mip}";var g=new GRGlTextureInfo(0x0de1,id,format){Protected=mip};Texture(new(width,height,mip,g),key);var fb=new GRGlFramebufferInfo(id,format){Protected=mip};Target(new(width,height,mip?4:0,mip?8:0,fb),key);
}
foreach(uint samples in new uint[]{0,1,4})foreach(uint levels in new uint[]{0,1,4})
{
    var vk=new GRVkImageInfo{Image=123,Format=37,SampleCount=samples,LevelCount=levels,ImageLayout=1};string key=$"vk-{samples}-{levels}";Texture(new(16,9,vk),key);Target(new(16,9,vk),key);Target(new(16,9,8,vk),key+"-old");
}
var a=new GRGlTextureInfo(0x0de1,27);var b=new GRGlFramebufferInfo(29);data["defaults"]=new uint[]{a.Target,a.Id,a.Format,b.FramebufferObjectId,b.Format};File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {data.Count} borrowed resource descriptions; no GPU drawing/import claim");
