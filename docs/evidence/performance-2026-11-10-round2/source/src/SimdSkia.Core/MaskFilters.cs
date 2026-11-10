namespace SimdSkia;
/// <summary>Coverage-only filter, evaluated before paint color/shader and final clipping.</summary>
public interface IMaskFilter
{
    int Margin(Matrix3 transform);
    CoverageClip Filter(CoverageClip source,Matrix3 transform);
}
