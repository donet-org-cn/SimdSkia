namespace SimdSkia;

internal enum ComputeGradientKind { Radial = 3, Conic = 4 }

internal interface IComputeGradientShader
{
    ComputeGradient? ComputeGradient { get; }
}

internal sealed record ComputeGradient(ComputeGradientKind Kind, PointF Center, PointF Origin,
    PointF Radius, float AngleTurns, Matrix3 SampleTransform, GradientExtendMode Extend,
    ReadOnlyMemory<LinearColorStop> Stops)
{
    internal static bool CanDraw(in DrawingCommand command)
        => (command.Kind!=CommandKind.FillRectangle||command.ShaderTransform is not {} brush||brush==command.Transform)
            &&command.Paint.Shader is IComputeGradientShader {ComputeGradient:not null};
}
