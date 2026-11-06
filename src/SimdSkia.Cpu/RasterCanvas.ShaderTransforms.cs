namespace SimdSkia.Cpu;
public sealed partial class RasterCanvas
{
    private Dictionary<(PremultipliedTransformedShader,Matrix3),IColorShader>? shaderTransforms;
    private bool PrepareShader(in DrawingCommand command,out DrawingCommand prepared)
    {
        prepared=default;
        if(command.Paint.Shader is not PremultipliedTransformedShader shader||!(command.ShaderTransform??command.Transform).TryInvert(out var inverse))return false;
        shaderTransforms??=[];var key=(shader,inverse);
        if(!shaderTransforms.TryGetValue(key,out var device)){device=shader.ForDevice(inverse);if(shaderTransforms.Count>=16)shaderTransforms.Clear();shaderTransforms.Add(key,device);}
        prepared=command with{Paint=command.Paint with{Shader=device},ShaderTransform=Matrix3.Identity};return true;
    }
}
