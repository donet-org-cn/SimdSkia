namespace SkiaSharp;

public class SKSurfaceProperties : SKObject
{
    private readonly SKSurfacePropsFlags flags;
    private readonly SKPixelGeometry geometry;
    public SKSurfaceProperties(SKPixelGeometry pixelGeometry):this(SKSurfacePropsFlags.None,pixelGeometry){ }
    public SKSurfaceProperties(uint flags,SKPixelGeometry pixelGeometry):this((SKSurfacePropsFlags)flags,pixelGeometry){ }
    public SKSurfaceProperties(SKSurfacePropsFlags flags,SKPixelGeometry pixelGeometry)
    {this.flags=flags;geometry=pixelGeometry;}
    public SKSurfacePropsFlags Flags{get{ThrowIfDisposed();return flags;}}
    public SKPixelGeometry PixelGeometry{get{ThrowIfDisposed();return geometry;}}
    public bool IsUseDeviceIndependentFonts=>(Flags&SKSurfacePropsFlags.UseDeviceIndependentFonts)!=0;
    internal SKSurfaceProperties Copy()=>new(Flags,PixelGeometry);
}
