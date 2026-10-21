using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace SimdSkia.ControlValidation;

internal static class ControlScene
{
    internal static void Prepare(Control root,double width=1000,double height=720)
    {
        static void Style(Visual visual){if(visual is StyledElement styled)styled.ApplyStyling();if(visual is TemplatedControl control)control.ApplyTemplate();foreach(var child in visual.GetVisualChildren())Style(child);}
        Style(root);root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));Style(root);root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));
    }
    internal static Control Create(Button? button=null,bool fullOpacity=true)
    {
        var rows=new StackPanel{Spacing=5};
        for(int i=0;i<12;i++)rows.Children.Add(new Border
        {
            Background=i%2==0?new SolidColorBrush(Color.Parse("#E9F1FA")):Brushes.White,
            CornerRadius=new CornerRadius(5),Padding=new Thickness(10,4),
            Child=new TextBlock{Text=$"{i+1:00}  中文界面 | Avalonia rendering | SimdSkia CPU  123.45",FontSize=16,Foreground=Brushes.MidnightBlue}
        });
        var path=new global::Avalonia.Controls.Shapes.Path{Data=global::Avalonia.Media.Geometry.Parse("M 0 25 C 40 -20 65 80 100 20 Q 125 -10 160 20 L 180 50"),Stroke=Brushes.RoyalBlue,StrokeThickness=3,StrokeLineCap=PenLineCap.Round,Width=200,Height=60};
        var transparentGroup=new Border{Opacity=.65,Background=Brushes.LightBlue,CornerRadius=new CornerRadius(8),Padding=new Thickness(10),Child=new TextBlock{Text="分组透明度 / Group opacity",FontSize=17}};
        // Match true group compositing on both backends; Skia defaults to per-draw opacity.
        RenderOptions.SetRequiresFullOpacityHandling(transparentGroup,fullOpacity);
        var top=new StackPanel{Spacing=8,Children=
        {
            new TextBlock{Text="SimdSkia 托管渲染 / Managed Avalonia",FontSize=27,Foreground=Brushes.DarkSlateBlue},
            new TextBlock{Text="真实控件：布局、中文文字、圆角、路径、图片和透明分组",FontSize=17},
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,Children={button??new Button{Content="更新 / Update"},new TextBox{Text="输入中文和 English",Width=270},new CheckBox{Content="托管 CPU",IsChecked=true}}},
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=24,Children={path,new Image{Source=ImageFixture(),Width=112,Height=56}}},
            new Border{CornerRadius=new CornerRadius(16),ClipToBounds=true,Height=56,Background=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,0,RelativeUnit.Relative),GradientStops=new GradientStops{new(Colors.RoyalBlue,0),new(Colors.MediumPurple,.5),new(Colors.Coral,1)}},Child=new TextBlock{Text="多色渐变 / Gradient",Foreground=Brushes.White,FontSize=20,Margin=new Thickness(14)}},
            transparentGroup,
            rows
        }};
        var root=new Border{Background=Brushes.White,Padding=new Thickness(24),Child=top};
        TextOptions.SetTextOptions(root,new TextOptions{TextHintingMode=TextHintingMode.None,TextRenderingMode=TextRenderingMode.Antialias,BaselinePixelAlignment=BaselinePixelAlignment.Unaligned});
        return root;
    }
    internal static Control CreateList()
    {
        var rows=new StackPanel{Spacing=5};for(int i=0;i<24;i++)rows.Children.Add(new Border{Background=i%2==0?Brushes.AliceBlue:Brushes.White,Padding=new Thickness(10,3),Child=new TextBlock{Text=$"{i:00} 中文列表 / Avalonia List | 123.45",FontSize=18}});
        var root=new Border{Background=Brushes.White,ClipToBounds=true,Child=rows};TextOptions.SetTextOptions(root,new TextOptions{TextHintingMode=TextHintingMode.None,TextRenderingMode=TextRenderingMode.Antialias,BaselinePixelAlignment=BaselinePixelAlignment.Unaligned});return root;
    }
    internal static WriteableBitmap ImageFixture()
    {
        var bitmap=new WriteableBitmap(new PixelSize(32,16),new Vector(96,96),PixelFormats.Rgba8888,global::Avalonia.Platform.AlphaFormat.Premul);
        using var locked=bitmap.Lock();var bytes=new byte[locked.RowBytes*16];for(int y=0;y<16;y++)for(int x=0;x<32;x++){int p=y*locked.RowBytes+x*4;bytes[p]=(byte)(x*7);bytes[p+1]=(byte)(y*15);bytes[p+2]=80;bytes[p+3]=255;}Marshal.Copy(bytes,0,locked.Address,bytes.Length);return bitmap;
    }
}
