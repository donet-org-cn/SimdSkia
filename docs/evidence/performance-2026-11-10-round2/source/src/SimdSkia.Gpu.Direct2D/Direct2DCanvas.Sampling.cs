namespace SimdSkia.Gpu.Direct2D;
public sealed partial class Direct2DCanvas
{
    private static bool SafeNearestCoordinates(in DrawingCommand command)
    {
        // With integral source edges and integral device magnification, pixel
        // centers cannot hit a texel boundary. Both nearest tie policies agree.
        if(command.Sampling!=ImageSampling.Nearest)return true;
        var m=command.Transform;if(m.M01!=0||m.M10!=0||!m.IsAffine)return false;
        var source=command.ImageSource??new RectF(0,0,command.Image!.Width,command.Image.Height);var destination=command.Rectangle;if(source.IsEmpty||destination.IsEmpty)return false;
        static bool Integer(float value)=>float.IsFinite(value)&&value==MathF.Truncate(value);
        float sx=MathF.Abs((destination.Right-destination.Left)*m.M00/(source.Right-source.Left)),sy=MathF.Abs((destination.Bottom-destination.Top)*m.M11/(source.Bottom-source.Top));
        return sx>=1&&sy>=1&&Integer(sx)&&Integer(sy)&&Integer(source.Left)&&Integer(source.Top)&&Integer(source.Right)&&Integer(source.Bottom)&&Integer(destination.Left*m.M00+m.M02)&&Integer(destination.Top*m.M11+m.M12)&&Integer(destination.Right*m.M00+m.M02)&&Integer(destination.Bottom*m.M11+m.M12);
    }
}
