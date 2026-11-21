namespace SimdSkia;

// Native radial drawing is optional. Shaders with a wider sampling contract
// are still evaluated by the managed sampler when required.
internal interface INativeRadialGradient
{
    RadialGradientDescription? Direct2DGradient { get; }
}

internal sealed record RadialGradientDescription(PointF Center,PointF OriginOffset,
    float RadiusX,float RadiusY,LinearColorStop[] Stops,GradientExtendMode Extend);
