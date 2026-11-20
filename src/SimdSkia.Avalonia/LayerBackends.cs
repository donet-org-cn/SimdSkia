namespace SimdSkia.Avalonia;

/// <summary>A drawing backend that hosts offscreen group layers (opacity,
/// layers, masks, effects) on its own device instead of a CPU raster layer.</summary>
internal interface ILayerBackendProvider
{
    /// <summary>Creates zero-initialized premultiplied layer pixels and the
    /// backend drawing them, or returns false so a CPU layer is used.</summary>
    bool TryCreateLayer(int width,int height,PixelFormat format,out PixelBuffer pixels,out ICanvasBackend canvas);
}

/// <summary>A layer backend that can apply group effects on its device.</summary>
internal interface IFilterBackend
{
    /// <summary>Returns filtered pixels (blur, or drop shadow under the layer),
    /// or null when the CPU filter must be used.</summary>
    PixelBuffer? TryFilter(PixelBuffer layer,RectF region,float sigmaX,float sigmaY,float offsetX,float offsetY,Rgba32 color,bool shadow);
    /// <summary>Completes device work so the CPU may read or modify layer pixels.</summary>
    void SynchronizeForCpu();
}
