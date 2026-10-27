using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SimdSkia.Cpu;
using CorePixel=SimdSkia.PixelBuffer;

namespace SimdSkia.ControlValidation;

internal static class CompletionValidation
{
    internal static int Run()
    {
        int count=0;void Check(bool value,string reason){count++;if(!value)throw new InvalidOperationException(reason);}
        var factory=AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();
        Color Pixel(IReadableBitmapImpl image,int x,int y)
        {using var locked=image.Lock();var p=new byte[4];Marshal.Copy(locked.Address+y*locked.RowBytes+x*4,p,0,4);return locked.Format==PixelFormats.Bgra8888?Color.FromArgb(p[3],p[2],p[1],p[0]):Color.FromArgb(p[3],p[0],p[1],p[2]);}
        using var source=new WriteableBitmap(new(2,2),new(96,96),PixelFormats.Rgba8888,global::Avalonia.Platform.AlphaFormat.Premul);
        using(var locked=source.Lock())Marshal.Copy(new byte[]{255,0,0,255,0,255,0,255,0,0,255,255,255,255,0,255},0,locked.Address,16);
        using var target=factory.CreateRenderTargetBitmap(new(32,24),new(96,96));
        foreach(var mode in Enum.GetValues<TileMode>())
        {
            var brush=new ImageBrush(source){Stretch=Stretch.Fill,TileMode=mode,DestinationRect=new(0,0,4,4,RelativeUnit.Absolute)};
            using(var c=target.CreateDrawingContext()){c.Clear(Colors.Transparent);c.PushRenderOptions(new(){BitmapInterpolationMode=BitmapInterpolationMode.None});c.DrawRectangle(brush,null,new Rect(0,0,12,12));c.PopRenderOptions();}
            Check(Pixel(target,0,0).R==255&&Pixel(target,2,0).G==255,"ImageBrush did not render actual source pixels.");
            if(mode==TileMode.None)Check(Pixel(target,5,0).A==0,"Nonrepeating image brush did not decal outside its viewport.");
            else{Check(Pixel(target,4,0)==Pixel(target,mode is TileMode.FlipX or TileMode.FlipXY?3:0,0),"Tile X wrap/mirror differs.");Check(Pixel(target,0,4)==Pixel(target,0,mode is TileMode.FlipY or TileMode.FlipXY?3:0),"Tile Y wrap/mirror differs.");}
        }
        var visual=new Border{Width=4,Height=4,Background=Brushes.Red};ControlScene.Prepare(visual,4,4);
        var visualBrush=new VisualBrush(visual){Stretch=Stretch.Fill,TileMode=TileMode.Tile,DestinationRect=new(0,0,4,4,RelativeUnit.Absolute)};
        using(var c=target.CreateDrawingContext()){c.Clear(Colors.Transparent);c.DrawRectangle(visualBrush,null,new Rect(0,0,12,12));}
        Check(Pixel(target,7,7).R==255&&Pixel(target,7,7).A==255,"VisualBrush did not render its real visual content.");
        visual.Background=Brushes.Blue;
        using(var c=target.CreateDrawingContext()){c.Clear(Colors.Transparent);c.DrawRectangle(visualBrush,null,new Rect(0,0,12,12));}
        Check(Pixel(target,7,7).B==255&&Pixel(target,7,7).R==0,"VisualBrush retained stale mutable visual content.");
        using(var c=target.CreateDrawingContext())
        {c.Clear(Colors.Transparent);((IDrawingContextImplWithEffects)c).PushEffect(null,new BlurEffect{Radius=4});c.DrawRectangle(Brushes.Red,null,new Rect(12,8,1,1));((IDrawingContextImplWithEffects)c).PopEffect();}
        var center=Pixel(target,12,8);var neighbor=Pixel(target,11,8);
        Check(center.A is >0 and <255&&neighbor.A>0&&Pixel(target,0,0).A==0,"Blur did not spread alpha with transparent extension.");
        Check(center.R==center.A&&center.G==0&&neighbor.R==neighbor.A,"Blur violated premultiplied color storage.");
        using(var c=target.CreateDrawingContext())
        {c.Clear(Colors.Transparent);((IDrawingContextImplWithEffects)c).PushEffect(null,new DropShadowEffect{BlurRadius=0,OffsetX=4,OffsetY=3,Color=Colors.Blue,Opacity=.5});c.DrawRectangle(Brushes.Red,null,new Rect(2,2,4,4));((IDrawingContextImplWithEffects)c).PopEffect();}
        Check(Pixel(target,3,3).R==255&&Pixel(target,8,7).B==127&&Pixel(target,8,7).A==127,"DropShadow ignored source/offset/color/opacity.");
        using(var c=target.CreateDrawingContext())
        {c.Clear(Colors.Transparent);c.DrawRectangle(Brushes.Red,null,new Rect(4,4,8,8),new BoxShadows(new BoxShadow{OffsetX=3,OffsetY=2,Color=Color.FromArgb(128,0,0,0)}));}
        Check(Pixel(target,6,6).R==255&&Pixel(target,14,8).A==128&&Pixel(target,3,3).A==0,"Outer BoxShadow did not exclude original ink bounds.");
        using(var c=target.CreateDrawingContext())
        {c.Clear(Colors.Transparent);c.DrawRectangle(Brushes.White,null,new Rect(4,4,8,8),new BoxShadows(new BoxShadow{IsInset=true,OffsetX=3,Color=Color.FromArgb(128,0,0,0)}));}
        Check(Pixel(target,5,8).R==127&&Pixel(target,9,8).R==255&&Pixel(target,3,8).A==0,"Inset BoxShadow ignored its hole or interior clip.");
        var random=new Random(1307);
        foreach(int width in new[]{1,2,7,8,15,31,32,65})foreach(var format in Enum.GetValues<SimdSkia.PixelFormat>())foreach(var alpha in Enum.GetValues<SimdSkia.AlphaFormat>())
        {
            using var input=new CorePixel(width,13,format,alpha,width*4+11);random.NextBytes(input.GetPixelSpan());
            var snapshot=input.CopyBytes();
            foreach(float sigma in new[]{0,.85f,2.5f,8f,150f})
            {
                bool prior=CpuOptions.EnableSimd;
                try
                {
                    CpuOptions.EnableSimd=false;using var scalar=ManagedFilters.Blur(input,sigma,sigma==0?0:1.25f,new(1,2,width,11));
                    CpuOptions.EnableSimd=true;using var vector=ManagedFilters.Blur(input,sigma,sigma==0?0:1.25f,new(1,2,width,11));
                    Check(scalar.GetPixelSpan().SequenceEqual(vector.GetPixelSpan()),"SIMD Gaussian differed from fixed-point scalar, including ROI/stride/alpha/tails.");
                    Check(input.GetPixelSpan().SequenceEqual(snapshot),"Gaussian mutated source/padding.");
                }
                finally{CpuOptions.EnableSimd=prior;}
            }
        }
        foreach(double scaling in new[]{1d,1.5})
        {
            using var uncut=factory.CreateRenderTargetBitmap(new(96,72),new(96*scaling,96*scaling));
            using var cropped=factory.CreateRenderTargetBitmap(new(96,72),new(96*scaling,96*scaling));
            using var expected=factory.CreateRenderTargetBitmap(new(96,72),new(96*scaling,96*scaling));
            void Content(IDrawingContextImpl c)
            {
                c.Transform=Matrix.CreateTranslation(3.25,2.5);c.PushLayer(new Rect(0,0,1,1));
                c.DrawRectangle(Brushes.Red,null,new Rect(8,7,32,29));
                c.PushOpacityMask(new LinearGradientBrush{StartPoint=new(0,0,RelativeUnit.Relative),EndPoint=new(1,0,RelativeUnit.Relative),GradientStops=[new(Colors.Transparent,0),new(Colors.White,1)]},new Rect(16,12,30,24));
                c.DrawRectangle(Brushes.Blue,null,new RoundedRect(new Rect(10,9,36,30),7));c.PopOpacityMask();c.PopLayer();
                ((IDrawingContextImplWithEffects)c).PushEffect(null,new BlurEffect{Radius=4});c.DrawRectangle(Brushes.Green,null,new Rect(10,14,4,20));((IDrawingContextImplWithEffects)c).PopEffect();
                c.Transform=Matrix.Identity;
            }
            using(var c=uncut.CreateDrawingContext()){c.Clear(Colors.Transparent);Content(c);}
            var clipping=new RoundedRect(new Rect(15,10,34,27),5);
            using(var c=cropped.CreateDrawingContext()){c.Clear(Colors.Transparent);c.PushClip(clipping);c.PushOpacity(.5,null);Content(c);c.PopOpacity();c.PopClip();}
            using(var c=expected.CreateDrawingContext()){c.Clear(Colors.Transparent);c.PushClip(clipping);c.DrawBitmap(uncut,.5,new Rect(0,0,96,72),new Rect(0,0,96/scaling,72/scaling));c.PopClip();}
            using var a=cropped.Lock();using var b=expected.Lock();var aa=new byte[a.RowBytes*72];var bb=new byte[b.RowBytes*72];Marshal.Copy(a.Address,aa,0,aa.Length);Marshal.Copy(b.Address,bb,0,bb.Length);
            Check(aa.SequenceEqual(bb),$"Cropped nested layers differ from full-size layer + final clip at scale {scaling} (transform, opacity, mask, blur, hint bounds).");
        }
        // Opacity 1 is a scope, not an offscreen layer. Wrapping any mixture of
        // real layers, clipped clears and draws in such scopes must preserve
        // both pixels and dirty propagation to every enclosing real layer.
        foreach(double scaling in new[]{1d,1.5})foreach(bool clipped in new[]{false,true})
        {
            using var reference=factory.CreateRenderTargetBitmap(new(48,36),new(96*scaling,96*scaling));
            using var wrapped=factory.CreateRenderTargetBitmap(new(48,36),new(96*scaling,96*scaling));
            void Render(IRenderTargetBitmapImpl bitmap,bool scopes)
            {
                using var c=bitmap.CreateDrawingContext();
                void Push(){if(scopes)for(int i=0;i<5;i++)c.PushOpacity(1,new Rect(0,0,1,1));}
                void Pop(){if(scopes)for(int i=0;i<5;i++)c.PopOpacity();}
                Push();c.Clear(Colors.White);
                if(clipped)c.PushClip(new RoundedRect(new Rect(2,3,24,17),3));
                c.PushOpacity(.5,null);Push();c.Clear(Colors.Transparent);
                c.DrawRectangle(Brushes.Red,null,new Rect(1,1,14,13));
                c.PushLayer(new Rect(0,0,1,1));Push();
                c.DrawRectangle(Brushes.Blue,null,new Rect(8,4,14,12));
                Pop();c.PopLayer();Pop();c.PopOpacity();
                c.DrawRectangle(Brushes.Green,null,new Rect(3,14,9,6));
                if(clipped)c.PopClip();
                Pop();
            }
            Render(reference,false);Render(wrapped,true);
            using var a=reference.Lock();using var b=wrapped.Lock();
            var aa=new byte[a.RowBytes*36];var bb=new byte[b.RowBytes*36];
            Marshal.Copy(a.Address,aa,0,aa.Length);Marshal.Copy(b.Address,bb,0,bb.Length);
            Check(aa.SequenceEqual(bb),$"Opacity 1 changed nested layers, clear or clip at scale {scaling}, clip={clipped}.");
        }
        foreach(var format in Enum.GetValues<SimdSkia.PixelFormat>())foreach(var alpha in Enum.GetValues<SimdSkia.AlphaFormat>())
        {
            using var input=new CorePixel(13,11,format,alpha,13*4+11);
            for(int y=0;y<11;y++)for(int x=0;x<13;x++)input.WritePixel(x,y,new((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256)));
            using var output=ManagedFilters.Blur(input,.85f,1.25f);
            static double[] Kernel(double sigma){int r=(int)Math.Ceiling(sigma*3);var result=Enumerable.Range(-r,2*r+1).Select(i=>Math.Exp(-i*i/(2*sigma*sigma))).ToArray();double total=result.Sum();return result.Select(v=>v/total).ToArray();}
            var kx=Kernel(.85f);var ky=Kernel(1.25f);int rx=kx.Length/2,ry=ky.Length/2;
            for(int y=0;y<11;y++)for(int x=0;x<13;x++)
            {
                double[] expected=new double[4];
                for(int dy=-ry;dy<=ry;dy++)for(int dx=-rx;dx<=rx;dx++)
                {
                    if(x+dx<0||x+dx>=13||y+dy<0||y+dy>=11)continue;var p=input.ReadPixel(x+dx,y+dy);double a=alpha==SimdSkia.AlphaFormat.Opaque?255:p.A,w=kx[dx+rx]*ky[dy+ry];
                    expected[0]+=Math.Floor(p.R*a/255+.5)*w;expected[1]+=Math.Floor(p.G*a/255+.5)*w;expected[2]+=Math.Floor(p.B*a/255+.5)*w;expected[3]+=a*w;
                }
                var pma=output.GetRowSpan(y).Slice(x*4,4);int ri=format==SimdSkia.PixelFormat.Rgba8888?0:2;
                Check(Math.Abs(pma[ri]-expected[0])<=1.1&&Math.Abs(pma[1]-expected[1])<=1.1&&Math.Abs(pma[2-ri]-expected[2])<=1.1&&Math.Abs(pma[3]-expected[3])<=1.1,"Managed Gaussian diverged from independent double-precision 2D convolution.");
            }
        }
        return count;
    }
}
