namespace SimdSkia.Cpu;

// Optional CPU sampling capability; public drawing commands still carry the
// backend-independent IColorShader. False must leave destination untouched.
internal interface IOpaqueRowShader:IColorShader
{
    bool TryCopyOpaqueRow(Span<byte> destination,PixelFormat format,Matrix3 deviceToShader,int x,int y);
}
