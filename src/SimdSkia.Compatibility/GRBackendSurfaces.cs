namespace SkiaSharp;

// These are borrowed resource descriptions, not drivers or drawing contexts.
// Constructing one never creates, verifies, or destroys the external GPU image.
// Importing it into a rendering surface remains a separately tracked API gap.
public class GRBackendTexture : SKObject
{
    private sealed record Description(int Width,int Height,GRBackend Backend,bool Mipmapped,object Info);
    private Description? description;
    private Description Value {get{ThrowIfDisposed();return description!;}}
    public GRBackendTexture(int width,int height,bool mipmapped,GRGlTextureInfo glInfo)=>description=new(width,height,GRBackend.OpenGL,mipmapped,glInfo);
    public GRBackendTexture(int width,int height,GRVkImageInfo vkInfo)=>description=new(width,height,GRBackend.Vulkan,vkInfo.LevelCount>1,vkInfo);
    public GRBackendTexture(int width,int height,GRD3DTextureResourceInfo d3dTextureInfo)
    {ArgumentNullException.ThrowIfNull(d3dTextureInfo);description=new(width,height,GRBackend.Direct3D,d3dTextureInfo.LevelCount>1,d3dTextureInfo.Copy());}
    public GRBackendTexture(int width,int height,bool mipmapped,GRMtlTextureInfo mtlInfo)=>description=new(width,height,GRBackend.Metal,mipmapped,mtlInfo);
    public bool IsValid {get{ThrowIfDisposed();return description is not null;}}
    public int Width=>Value.Width;
    public int Height=>Value.Height;
    public bool HasMipMaps=>Value.Mipmapped;
    public GRBackend Backend=>Value.Backend;
    internal GRVkImageInfo VulkanInfo=>Value.Info is GRVkImageInfo info?info:throw new InvalidOperationException("This texture is not Vulkan.");
    internal GRD3DTextureResourceInfo D3DInfo=>Value.Info as GRD3DTextureResourceInfo??throw new InvalidOperationException("This texture is not Direct3D12.");
    internal GRMtlTextureInfo MetalInfo=>Value.Info is GRMtlTextureInfo info?info:throw new InvalidOperationException("This texture is not Metal.");
    public SKSizeI Size=>new(Width,Height);
    public SKRectI Rect=>new(0,0,Width,Height);
    public GRGlTextureInfo GetGlTextureInfo()=>GetGlTextureInfo(out var info)?info:default;
    public bool GetGlTextureInfo(out GRGlTextureInfo glInfo)
    {var d=Value;glInfo=d.Info is GRGlTextureInfo g?g:default;return d.Backend==GRBackend.OpenGL;}
    protected override void Dispose(bool disposing)=>description=null;
}
public class GRBackendRenderTarget : SKObject
{
    private sealed record Description(int Width,int Height,GRBackend Backend,int Samples,int Stencil,object Info);
    private Description? description;
    private Description Value {get{ThrowIfDisposed();return description!;}}
    public GRBackendRenderTarget(int width,int height,int sampleCount,int stencilBits,GRGlFramebufferInfo glInfo)=>description=new(width,height,GRBackend.OpenGL,Math.Max(1,sampleCount),stencilBits,glInfo);
    public GRBackendRenderTarget(int width,int height,GRVkImageInfo vkImageInfo)=>description=new(width,height,GRBackend.Vulkan,checked((int)Math.Max(1,vkImageInfo.SampleCount)),0,vkImageInfo);
    public GRBackendRenderTarget(int width,int height,GRD3DTextureResourceInfo d3dTextureInfo)
    {ArgumentNullException.ThrowIfNull(d3dTextureInfo);description=new(width,height,GRBackend.Direct3D,checked((int)Math.Max(1,d3dTextureInfo.SampleCount)),0,d3dTextureInfo.Copy());}
    // Metal resources are borrowed: a descriptor cannot query their size or sample
    // count without a Metal device import. Skia uses a non-MSAA descriptor here.
    public GRBackendRenderTarget(int width,int height,GRMtlTextureInfo mtlInfo)=>description=new(width,height,GRBackend.Metal,1,0,mtlInfo);
    [Obsolete("Use GRBackendRenderTarget(int width, int height, GRVkImageInfo vkImageInfo) instead.")]
    public GRBackendRenderTarget(int width,int height,int sampleCount,GRVkImageInfo vkImageInfo):this(width,height,vkImageInfo){}
    public bool IsValid {get{ThrowIfDisposed();return description is not null;}}
    public int Width=>Value.Width;
    public int Height=>Value.Height;
    public int SampleCount=>Value.Samples;
    public int StencilBits=>Value.Stencil;
    public GRBackend Backend=>Value.Backend;
    internal GRVkImageInfo VulkanInfo=>Value.Info is GRVkImageInfo info?info:throw new InvalidOperationException("This render target is not Vulkan.");
    internal GRD3DTextureResourceInfo D3DInfo=>Value.Info as GRD3DTextureResourceInfo??throw new InvalidOperationException("This render target is not Direct3D12.");
    internal GRMtlTextureInfo MetalInfo=>Value.Info is GRMtlTextureInfo info?info:throw new InvalidOperationException("This render target is not Metal.");
    public SKSizeI Size=>new(Width,Height);
    public SKRectI Rect=>new(0,0,Width,Height);
    public GRGlFramebufferInfo GetGlFramebufferInfo()=>GetGlFramebufferInfo(out var info)?info:default;
    public bool GetGlFramebufferInfo(out GRGlFramebufferInfo glInfo)
    {var d=Value;glInfo=d.Info is GRGlFramebufferInfo g?g:default;return d.Backend==GRBackend.OpenGL;}
    protected override void Dispose(bool disposing)=>description=null;
}
public partial struct GRGlFramebufferInfo
{
    public GRGlFramebufferInfo(uint fboId):this(fboId,0){}
    public GRGlFramebufferInfo(uint fboId,uint format){this=default;fFBOID=fboId;fFormat=format;}
}
public partial struct GRGlTextureInfo
{
    public GRGlTextureInfo(uint target,uint id):this(target,id,0){}
    public GRGlTextureInfo(uint target,uint id,uint format){this=default;fTarget=target;fID=id;fFormat=format;}
}
