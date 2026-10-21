using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SimdSkia.ControlValidation;

internal static class FeatureProbe
{
#if MANAGED
    internal static int ValidateTileFixtures(string output)
    {
        string directory=Path.Combine(output,"tile-regression");Directory.CreateDirectory(directory);Run(directory);
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","tiles-native-3.119.4.json")));
        int count=0;
        foreach(var fixture in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            string name=fixture.GetProperty("name").GetString()!;
            using var input=new MemoryStream(Convert.FromBase64String(fixture.GetProperty("zlibRgba").GetString()!));
            using var decoder=new System.IO.Compression.ZLibStream(input,System.IO.Compression.CompressionMode.Decompress);
            using var decoded=new MemoryStream();decoder.CopyTo(decoded);byte[] expected=decoded.ToArray();
            byte[] actual=File.ReadAllBytes(Path.Combine(directory,name+".rgba"));
            if(actual.Length!=expected.Length)throw new InvalidOperationException("Tile fixture dimensions differ: "+name);
            int maximum=0;long squared=0;
            for(int i=0;i<actual.Length;i++){int d=Math.Abs(actual[i]-expected[i]);maximum=Math.Max(maximum,d);squared+=(long)d*d;}
            double rmse=Math.Sqrt(squared/(double)actual.Length);
            if(maximum>fixture.GetProperty("maximum").GetInt32()||rmse>fixture.GetProperty("rmse").GetDouble())
                throw new InvalidOperationException($"Original tile regression {name}: maximum={maximum}, RMSE={rmse}.");
            count++;
        }
        return count;
    }
#endif
    internal static void Run(string output)
    {
        var factory=AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();var cases=new List<object>();
        using var source=ControlScene.ImageFixture();
        void Render(string name,string feature,double dpi,Action<IDrawingContextImpl> draw,int maximum,double rmse)
        {
            int width=(int)(128*dpi),height=(int)(96*dpi);using var image=factory.CreateRenderTargetBitmap(new(width,height),new(96*dpi,96*dpi));
            using(var context=image.CreateDrawingContext()){context.Clear(Colors.Transparent);draw(context);}
            using(var stream=File.Create(Path.Combine(output,name+".png")))image.Save(stream,PngBitmapEncoderOptions.Default);
            using var framebuffer=((IReadableBitmapImpl)image).Lock();var bytes=new byte[width*height*4];
            for(int y=0;y<height;y++)Marshal.Copy(framebuffer.Address+y*framebuffer.RowBytes,bytes,y*width*4,width*4);
            if(framebuffer.Format==PixelFormats.Bgra8888)for(int i=0;i<bytes.Length;i+=4)(bytes[i],bytes[i+2])=(bytes[i+2],bytes[i]);
            File.WriteAllBytes(Path.Combine(output,name+".rgba"),bytes);cases.Add(new{name,feature,width,height,maximum,rmse});
        }
        foreach(double dpi in new[]{1d,1.5})
        {
            foreach(var mode in Enum.GetValues<TileMode>())foreach(var stretch in Enum.GetValues<Stretch>())
            {
                string name=$"image-{mode}-{stretch}-{dpi}";
                Render(name,"ImageBrush",dpi,c=>
                {
                    c.PushRenderOptions(new(){BitmapInterpolationMode=BitmapInterpolationMode.None});
                    var brush=new ImageBrush(source){TileMode=mode,Stretch=stretch,DestinationRect=new(.125,.125,.5,.5,RelativeUnit.Relative),SourceRect=new(.125,.125,.75,.75,RelativeUnit.Relative),AlignmentX=AlignmentX.Right,AlignmentY=AlignmentY.Bottom};
                    c.DrawRectangle(brush,null,new Rect(8,8,104,72));c.PopRenderOptions();
                },2,.5);
            }
            var visual=new Border{Width=32,Height=16,Background=Brushes.Red,Child=new Border{Background=Brushes.Blue,Width=9,Height=7,HorizontalAlignment=global::Avalonia.Layout.HorizontalAlignment.Right,VerticalAlignment=global::Avalonia.Layout.VerticalAlignment.Bottom}};ControlScene.Prepare(visual,32,16);
            foreach(var mode in Enum.GetValues<TileMode>())
                Render($"visual-{mode}-{dpi}","VisualBrush",dpi,c=>c.DrawRectangle(new VisualBrush(visual){TileMode=mode,Stretch=Stretch.Uniform,DestinationRect=new(.1,.1,.5,.5,RelativeUnit.Relative)},null,new Rect(8,8,104,72)),2,.5);
            foreach(double radius in new[]{0d,2,8,20})
            {
                Render($"blur-{radius}-{dpi}","Blur",dpi,c=>{var effects=(IDrawingContextImplWithEffects)c;effects.PushEffect(null,new BlurEffect{Radius=radius});c.DrawRectangle(new SolidColorBrush(Color.FromArgb(177,230,70,20)),null,new Rect(32,24,48,32));effects.PopEffect();},8,1.5);
                Render($"drop-{radius}-{dpi}","DropShadow",dpi,c=>{var effects=(IDrawingContextImplWithEffects)c;effects.PushEffect(null,new DropShadowEffect{BlurRadius=radius,OffsetX=9.25,OffsetY=5.5,Color=Colors.Blue,Opacity=.65});c.DrawRectangle(new SolidColorBrush(Color.FromArgb(177,230,70,20)),null,new Rect(32,24,48,32));effects.PopEffect();},8,1.5);
            }
            foreach(bool inset in new[]{false,true})foreach(bool rounded in new[]{false,true})foreach(double spread in new[]{-3d,0,3})foreach(double blur in new[]{0d,8})
                Render($"box-{inset}-{rounded}-{spread}-{blur}-{dpi}","BoxShadow",dpi,c=>c.DrawRectangle(Brushes.White,null,new RoundedRect(new Rect(24,20,64,48),rounded?8:0),new BoxShadows(new BoxShadow{IsInset=inset,Spread=spread,Blur=blur,OffsetX=4,OffsetY=3,Color=Color.FromArgb(157,0,0,200)})),32,2);
        }
        File.WriteAllText(Path.Combine(output,"features.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Wrote {cases.Count} actual tile/effect/shadow scenes.");
    }
}
