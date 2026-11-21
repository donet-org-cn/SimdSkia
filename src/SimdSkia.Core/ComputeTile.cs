namespace SimdSkia;

internal enum ComputeTileMode { None, Tile, FlipX, FlipY, FlipXY }

internal interface IComputeTileShader
{
    bool TryGetTile(out PixelBuffer pixels,out ComputeTilePlacement placement);
}

internal readonly record struct ComputeTilePlacement(int X,int Y,ComputeTileMode Mode)
{
    internal static bool TryResolve(in DrawingCommand command,out PixelBuffer? image,out ComputeTilePlacement placement)
    {
        image=null;placement=default;
        if(command.Kind is not (CommandKind.FillPath or CommandKind.FillRectangle)||
            command.Paint.Shader is not IComputeTileShader shader)return false;
        var m=command.ShaderTransform??command.Transform;
        if(m!=(Matrix3.Identity with {M02=m.M02,M12=m.M12})||!Integer(m.M02)||!Integer(m.M12)||
            !shader.TryGetTile(out var pixels,out var tile))return false;
        image=pixels;
        placement=tile with {X=tile.X-(int)m.M02,Y=tile.Y-(int)m.M12};
        return true;
    }

    internal static bool Integer(double value)=>Math.Abs(value)<65536&&value==Math.Floor(value);
}
