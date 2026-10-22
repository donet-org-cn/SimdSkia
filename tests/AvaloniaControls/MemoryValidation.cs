using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

namespace SimdSkia.ControlValidation;

internal static class MemoryValidation
{
    internal static int Run()
    {
        int checks=0;var factory=AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();
        void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);checks++;}
        byte[] Bytes(IReadableBitmapImpl bitmap)
        {using var locked=bitmap.Lock();var bytes=new byte[locked.RowBytes*locked.Size.Height];Marshal.Copy(locked.Address,bytes,0,bytes.Length);return bytes;}
        using var reused=factory.CreateRenderTargetBitmap(new(64,48),new(96,96));
        foreach(GradientBrush brush in new GradientBrush[]{new LinearGradientBrush(),new RadialGradientBrush(),new ConicGradientBrush()})
        {
            brush.GradientStops.Add(new GradientStop(Colors.Red,0));brush.GradientStops.Add(new GradientStop(Colors.Blue,1));
            for(int step=0;step<12;step++)
            {
                switch(step)
                {
                    case 1:brush.GradientStops[0].Color=Colors.Green;break;
                    case 2:brush.GradientStops[1].Offset=.7;break;
                    case 3:brush.GradientStops.Add(new GradientStop(Colors.Yellow,.4));break;
                    case 4:brush.SpreadMethod=GradientSpreadMethod.Reflect;break;
                    case 5:brush.Transform=new RotateTransform(17);break;
                    case 6:brush.TransformOrigin=new RelativePoint(.2,.6,RelativeUnit.Relative);break;
                    case 7:brush.Opacity=.37;break;
                    case 8:
                        if(brush is LinearGradientBrush linear)linear.EndPoint=new RelativePoint(.2,.8,RelativeUnit.Relative);
                        if(brush is RadialGradientBrush radial)radial.GradientOrigin=new RelativePoint(.25,.4,RelativeUnit.Relative);
                        if(brush is ConicGradientBrush conic)conic.Angle=73;
                        break;
                    case 9:
                        if(brush is LinearGradientBrush linear2)linear2.StartPoint=new RelativePoint(.1,.3,RelativeUnit.Relative);
                        if(brush is RadialGradientBrush radial2)radial2.RadiusX=new RelativeScalar(.3,RelativeUnit.Relative);
                        if(brush is ConicGradientBrush conic2)conic2.Center=new RelativePoint(.2,.3,RelativeUnit.Relative);
                        break;
                    case 10:brush.GradientStops.Clear();break;
                }
                var bounds=step==11?new Rect(4,7,33,25):new Rect(2,3,51,37);
                using(var context=reused.CreateDrawingContext()){context.Clear(Colors.White);context.DrawRectangle(brush,null,bounds);}
                using var fresh=factory.CreateRenderTargetBitmap(new(64,48),new(96,96));
                // Avalonia 12.1.4's radial copy constructor assigns RadiusX to
                // RadiusY. Pass both radii explicitly to preserve this fixture.
                IBrush snapshot=brush is RadialGradientBrush r
                    ?new ImmutableRadialGradientBrush(r.GradientStops.Select(s=>new ImmutableGradientStop(s.Offset,s.Color)).ToArray(),r.Opacity,r.Transform is {} t?new ImmutableTransform(t.Value):null,r.TransformOrigin,r.SpreadMethod,r.Center,r.GradientOrigin,r.RadiusX,r.RadiusY)
                    :brush.ToImmutable();
                using(var context=fresh.CreateDrawingContext()){context.Clear(Colors.White);context.DrawRectangle(snapshot,null,bounds);}
                Check(Bytes(reused).AsSpan().SequenceEqual(Bytes(fresh)), $"Reused {brush.GetType().Name} step {step} retained a stale stop, transform, opacity, geometry or bounds.");
            }
        }
        var owner=factory.CreateRenderTargetBitmap(new(8,8),new(96,96));
        var first=owner.CreateDrawingContext();var overlap=owner.CreateDrawingContext();
        first.Clear(Colors.Red);first.Dispose();owner.Dispose();
        overlap.DrawRectangle(Brushes.Blue,null,new Rect(0,0,4,4));overlap.Dispose();checks++;
        owner=factory.CreateRenderTargetBitmap(new(8,8),new(96,96));
        var retained=owner.CreateDrawingContext();owner.Dispose();retained.Clear(Colors.Green);retained.Dispose();checks++;
        var invalid=reused.CreateDrawingContext();invalid.PushClip(new Rect(0,0,2,2));
        bool rejected=false;try{invalid.Dispose();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Unbalanced reused bitmap context was accepted.");
        using(var context=reused.CreateDrawingContext()){context.Clear(Colors.Blue);context.DrawRectangle(Brushes.Red,null,new Rect(30,20,4,4));}
        var output=Bytes(reused);Check(output[(21*64+31)*4+2]==255,"Previous context clip leaked into the next frame.");
        return checks;
    }
}
