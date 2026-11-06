using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using SimdSkia.Cpu;
namespace SimdSkia.Gpu;

/// <summary>GL/GLES framebuffer renderer. Immutable coverage masks are made in
/// C#; paint sampling, image filtering and pixel composition execute on the GPU.
/// Readback is explicit; Flush completes device work.</summary>
internal sealed unsafe class GlCanvas:IGpuCanvas
{
    private readonly GlDevice gl;
    private readonly PixelBuffer pixels;
    private readonly int thread=Environment.CurrentManagedThreadId;
    private readonly GlyphMaskCommands glyphMasks=new();
    private readonly Dictionary<RectF,PathGeometry> rectangles=[];
    private readonly GpuCoverageCache coverageCache=new();
    private HashSet<ResourceLease>? pendingImages;
    private readonly bool ownsTarget,ownsFramebuffer,flip;
    internal GlDevice Device=>gl;
    internal bool Accepts(GlImage image)=>ReferenceEquals(image.Device,gl);
    private readonly Dictionary<string,int> uniforms=[];
    private readonly Dictionary<CoverageClip,(uint Texture,long Bytes,long Touched)> masks=[];
    private readonly Dictionary<PixelBuffer,(uint Texture,byte[] Bytes,long Touched)> images=[];
    private long maskBytes,imageBytes,limit=96*1024*1024;
    private uint target,framebuffer,vertices,array,dummy,destination;
    private bool disposed,abandoned,dirty,pending,drawing,cacheMasks=true;
    private readonly List<uint> retired=[];
    private PixelBuffer? readback;
    internal GlCanvas(GlDevice gl,PixelBuffer pixels,uint texture=0,uint? externalFramebuffer=null,bool flip=false)
    {
        if(pixels.Format==PixelFormat.Alpha8||pixels.Alpha==AlphaFormat.Straight)throw new NotSupportedException("GL render targets require RGBA/BGRA premultiplied or opaque storage.");
        if(pixels.Width>gl.MaximumSize||pixels.Height>gl.MaximumSize||pixels.ByteCount>128*1024*1024)throw new NotSupportedException("GL target exceeds the device/dimension/memory budget.");
        this.gl=gl;this.pixels=pixels.Share();this.flip=flip;ownsTarget=texture==0&&externalFramebuffer is null;ownsFramebuffer=externalFramebuffer is null;gl.Check();
        try
        {
            target=ownsTarget?gl.Upload(pixels):texture;destination=gl.Texture(pixels.Width,pixels.Height,0x1908,0);byte* black=stackalloc byte[4];new Span<byte>(black,4).Clear();dummy=gl.Texture(1,1,0x1908,(nint)black);
            uint fbo=0;if(ownsFramebuffer)((delegate* unmanaged[Stdcall]<int,uint*,void>)gl.P("glGenFramebuffers"))(1,&fbo);framebuffer=externalFramebuffer??fbo;Bind();if(ownsFramebuffer)((delegate* unmanaged[Stdcall]<uint,uint,uint,uint,int,void>)gl.P("glFramebufferTexture2D"))(0x8D40,0x8CE0,0x0DE1,target,0);
            if(((delegate* unmanaged[Stdcall]<uint,uint>)gl.P("glCheckFramebufferStatus"))(0x8D40)!=0x8CD5)throw new NotSupportedException("GL framebuffer is incomplete.");
            uint buffer=0;((delegate* unmanaged[Stdcall]<int,uint*,void>)gl.P("glGenBuffers"))(1,&buffer);vertices=buffer;
            nint vao=gl.Optional("glGenVertexArrays");if(vao!=0){uint value=0;((delegate* unmanaged[Stdcall]<int,uint*,void>)vao)(1,&value);array=value;}
            gl.CheckError();dirty=!ownsTarget;
        }
        catch{Dispose();throw;}
    }
    public bool ContextAbandoned=>abandoned||gl.Abandoned;
    public bool ResourceDisposed=>disposed;
    public int MaximumImageSize=>gl.MaximumSize;
    public (int Count,long Bytes,long Purgeable) ResourceUsage=>disposed||abandoned?default:(masks.Count+images.Count+(ownsTarget?3:2),maskBytes+imageBytes+(long)pixels.Width*pixels.Height*(ownsTarget?8:4)+4,maskBytes+imageBytes);
    private void Check(){ObjectDisposedException.ThrowIf(disposed,this);if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("Use GL surfaces on their creating thread.");gl.Check();if(abandoned)throw new InvalidOperationException("The GL surface has been abandoned.");}
    private void Bind()=>((delegate* unmanaged[Stdcall]<uint,uint,void>)gl.P("glBindFramebuffer"))(0x8D40,framebuffer);
    private int U(string name){if(!uniforms.TryGetValue(name,out int location)){location=gl.Uniform(name);uniforms.Add(name,location);}return location;}
    private void I(string name,int value)=>((delegate* unmanaged[Stdcall]<int,int,void>)gl.P("glUniform1i"))(U(name),value);
    private void V2(string name,float x,float y)=>((delegate* unmanaged[Stdcall]<int,float,float,void>)gl.P("glUniform2f"))(U(name),x,y);
    private void V4(string name,float x,float y,float z,float w)=>((delegate* unmanaged[Stdcall]<int,float,float,float,float,void>)gl.P("glUniform4f"))(U(name),x,y,z,w);
    private void Matrix(Matrix3 value){float* matrix=stackalloc float[9]{value.M00,value.M10,value.M20,value.M01,value.M11,value.M21,value.M02,value.M12,value.M22};((delegate* unmanaged[Stdcall]<int,int,byte,float*,void>)gl.P("glUniformMatrix3fv"))(U("inverseMatrix"),1,0,matrix);}
    public void Execute(in DrawingCommand command)
    {
        Check();if(!command.Transform.IsFinite||!command.Transform.IsAffine)throw new NotSupportedException("GL drawing requires finite affine transforms.");
        if(command.Paint.Blender is not null||command.Paint.ColorFilter is not null||command.Paint.ImageFilter is not null||command.Paint.MaskFilter is not null||command.Paint.Dither)throw new NotSupportedException("This GL pipeline requires explicit CPU fallback for custom blending, filters and dithering.");
        if(command.Paint.Composite is not (CompositeMode.SourceOver or CompositeMode.Source or CompositeMode.Clear))throw new NotSupportedException("This GL pipeline implements SourceOver, Source and Clear composition.");
        if(command.TextPixelGeometry is 1 or 2&&pixels.Format==PixelFormat.Bgra8888)throw new NotSupportedException("GL LCD glyph composition requires explicit raster fallback.");
        if(command.Kind==CommandKind.Clear)
        {
            Bind();((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glDisable"))(0x0C11);((delegate* unmanaged[Stdcall]<byte,byte,byte,byte,void>)gl.P("glColorMask"))(1,1,1,1);var c=command.Paint.Color;float a=c.A/255f;((delegate* unmanaged[Stdcall]<float,float,float,float,void>)gl.P("glClearColor"))(c.R/255f*a,c.G/255f*a,c.B/255f*a,a);((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glClear"))(0x4000);dirty=pending=true;return;
        }
        if(command.Kind==CommandKind.DrawGlyphRun){foreach(var mask in glyphMasks.Resolve(command))Draw(command,mask);return;}
        PathGeometry Rectangle(RectF r){if(!rectangles.TryGetValue(r,out var result)){if(rectangles.Count>=64)rectangles.Clear();rectangles.Add(r,result=PathGeometry.Rectangle(r));}return result;}
        PathGeometry geometry=command.Kind switch{CommandKind.FillRectangle or CommandKind.DrawImage=>Rectangle(command.Rectangle),CommandKind.FillPath=>command.Path??throw new ArgumentException("GL path geometry is missing."),_=>throw new NotSupportedException("Unknown GL command.")};
        Draw(command,coverageCache.Get(geometry,command,pixels.Width,pixels.Height));
    }
    private uint Image(PixelBuffer image,bool immutable)
    {
        if(images.TryGetValue(image,out var entry)&&(immutable||image.GetPixelSpan().SequenceEqual(entry.Bytes))){images[image]=entry with{Touched=Stopwatch.GetTimestamp()};return entry.Texture;}
        var bytes=image.CopyBytes();uint texture=gl.Upload(image);
        if(images.Remove(image,out var old)){uint value=old.Texture;gl.DeleteTexture(ref value);imageBytes-=old.Bytes.Length;}
        if(bytes.Length>limit){retired.Add(texture);return texture;}if(images.Count>=64||imageBytes+maskBytes+bytes.Length>limit)PurgeCachedResources();images.Add(image,(texture,bytes,Stopwatch.GetTimestamp()));imageBytes+=bytes.Length;return texture;
    }
    private uint Mask(CoverageClip mask)
    {
        if(masks.TryGetValue(mask,out var cached)){masks[mask]=cached with{Touched=Stopwatch.GetTimestamp()};return cached.Texture;}uint texture;
        int count=checked(mask.Width*mask.Height);var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(count);
        try{for(int y=0;y<mask.Height;y++)mask.GetRow(mask.Top+y).CopyTo(bytes.AsSpan(y*mask.Width,mask.Width));fixed(byte* data=bytes)texture=gl.Texture(mask.Width,mask.Height,0x1909,(nint)data);}
        finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes);}
        if(!cacheMasks||count>limit){retired.Add(texture);return texture;}if(masks.Count>=64||maskBytes+imageBytes+count>limit)PurgeCachedResources();masks.Add(mask,(texture,count,Stopwatch.GetTimestamp()));maskBytes+=count;return texture;
    }
    private void Draw(in DrawingCommand command,CoverageClip coverage)
    {drawing=true;try{if(DrawCore(command,coverage)&&command.ImageResource is GlImage{Lease:{} lease}&&(pendingImages??=[]).Add(lease))lease.Retain();}finally{drawing=false;foreach(uint id in retired){uint value=id;gl.DeleteTexture(ref value);}retired.Clear();}}
    private bool DrawCore(in DrawingCommand command,CoverageClip coverage)
    {
        if(command.CoverageClip is {} inherited)coverage=CoverageClip.Intersect(coverage,inherited);if(coverage.Width==0||coverage.Height==0)return false;
        var p=command.Paint;var color=p.FloatingColor??new Vector4(p.Color.R/255f,p.Color.G/255f,p.Color.B/255f,p.Color.A/255f);LinearGradientDescription? gradient=null;
        if(p.Shader is LinearGradient simple)gradient=new(simple.Start,simple.End,[new(0,simple.StartColor),new(1,simple.EndColor)]);else if(p.Shader is not null&&!p.Shader.TryGetLinearGradient(out gradient))throw new NotSupportedException("This GL pipeline supports solid and linear-gradient brushes.");
        if(gradient is not null&&gradient.Stops.Length>16)throw new NotSupportedException("The GLES2 uniform gradient pipeline accepts at most 16 stops.");
        if(!(command.ShaderTransform??command.Transform).TryInvert(out var inverse))return false;if(gradient is not null)inverse=gradient.SampleTransform*inverse;
        Bind();((delegate* unmanaged[Stdcall]<int,int,int,int,void>)gl.P("glViewport"))(0,0,pixels.Width,pixels.Height);((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glUseProgram"))(gl.Program);
        ((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glDisable"))(0x0B71);((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glDisable"))(0x0B44);((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glDisable"))(0x0B90);((delegate* unmanaged[Stdcall]<byte,byte,byte,byte,void>)gl.P("glColorMask"))(1,1,1,1);
        if(command.Clip is {} clip){int l=Math.Clamp((int)MathF.Ceiling(clip.Left-.5f),0,pixels.Width),t=Math.Clamp((int)MathF.Ceiling(clip.Top-.5f),0,pixels.Height),r=Math.Clamp((int)MathF.Ceiling(clip.Right-.5f),l,pixels.Width),b=Math.Clamp((int)MathF.Ceiling(clip.Bottom-.5f),t,pixels.Height);((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glEnable"))(0x0C11);((delegate* unmanaged[Stdcall]<int,int,int,int,void>)gl.P("glScissor"))(l,flip?pixels.Height-b:t,r-l,b-t);}else ((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glDisable"))(0x0C11);
        V2("targetSize",pixels.Width,pixels.Height);((delegate* unmanaged[Stdcall]<int,float,void>)gl.P("glUniform1f"))(U("yScale"),flip?-1:1);I("targetFlip",flip?1:0);V4("maskBounds",coverage.Left,coverage.Top,coverage.Width,coverage.Height);V4("color",color.X,color.Y,color.Z,color.W);Matrix(inverse);
        uint mask=Mask(coverage);gl.BindTexture(1,mask);I("maskTexture",1);gl.BindTexture(0,dummy);I("imageTexture",0);gl.BindTexture(2,destination);I("destinationTexture",2);
        int mode=p.Composite==CompositeMode.SourceOver?0:p.Composite==CompositeMode.Source?1:2;I("mode",mode);
        if(mode==0){((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glEnable"))(0x0BE2);((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glBlendEquation"))(0x8006);((delegate* unmanaged[Stdcall]<uint,uint,void>)gl.P("glBlendFunc"))(1,0x0303);}
        else{((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glDisable"))(0x0BE2);((delegate* unmanaged[Stdcall]<uint,int,int,int,int,int,int,int,void>)gl.P("glCopyTexSubImage2D"))(0x0DE1,0,0,0,0,0,pixels.Width,pixels.Height);}
        if(command.Kind==CommandKind.DrawImage)
        {
            if(p.Shader is not null||command.Image?.Format==PixelFormat.Alpha8||command.Sampling==ImageSampling.Mitchell)throw new NotSupportedException("GL A8/image-brush/cubic sampling requires an additional shader variant.");
            var resource=command.ImageResource as GlImage;var image=command.Image;if(resource is not null&&!Accepts(resource))throw new InvalidOperationException("GL image belongs to a different context.");int width=resource?.Width??image?.Width??throw new ArgumentException("GL source image is missing."),height=resource?.Height??image!.Height;var source=command.ImageSource??new(0,0,width,height);if(!source.IsFinite||source.IsEmpty||source.Left<0||source.Top<0||source.Right>width||source.Bottom>height)throw new ArgumentOutOfRangeException(nameof(command.ImageSource));
            I("imageFlip",resource?.Flip==true?1:0);I("imageStraight",resource?.Alpha==AlphaFormat.Straight?1:0);I("imageOpaque",resource?.Alpha==AlphaFormat.Opaque?1:0);uint texture=resource?.Texture??Image(image!,command.ImageIsImmutable);gl.BindTexture(0,texture);gl.Parameter(0x2801,command.Sampling==ImageSampling.Nearest?0x2600:0x2601);gl.Parameter(0x2800,command.Sampling==ImageSampling.Nearest?0x2600:0x2601);I("kind",2);V2("imageSize",width,height);V4("imageSource",source.Left,source.Top,source.Right-source.Left,source.Bottom-source.Top);V4("imageDestination",command.Rectangle.Left,command.Rectangle.Top,command.Rectangle.Right-command.Rectangle.Left,command.Rectangle.Bottom-command.Rectangle.Top);
        }
        else if(gradient is not null)
        {
            I("kind",1);I("gradientCount",gradient.Stops.Length);I("tileMode",(int)gradient.Extend);V4("gradientLine",gradient.Start.X,gradient.Start.Y,gradient.End.X,gradient.End.Y);float* colors=stackalloc float[64];float* positions=stackalloc float[16];int i=0;foreach(var stop in gradient.Stops.Span){positions[i]=stop.Offset;colors[i*4]=stop.Color.R/255f;colors[i*4+1]=stop.Color.G/255f;colors[i*4+2]=stop.Color.B/255f;colors[i*4+3]=stop.Color.A/255f;i++;}((delegate* unmanaged[Stdcall]<int,int,float*,void>)gl.P("glUniform4fv"))(U("gradientColors[0]"),i,colors);((delegate* unmanaged[Stdcall]<int,int,float*,void>)gl.P("glUniform1fv"))(U("gradientPositions[0]"),i,positions);
        }
        else I("kind",0);
        if(array!=0)((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glBindVertexArray"))(array);((delegate* unmanaged[Stdcall]<uint,uint,void>)gl.P("glBindBuffer"))(0x8892,vertices);float l0=coverage.Left,t0=coverage.Top,r0=l0+coverage.Width,b0=t0+coverage.Height;float* quad=stackalloc float[8]{l0,t0,r0,t0,l0,b0,r0,b0};((delegate* unmanaged[Stdcall]<uint,nint,void*,uint,void>)gl.P("glBufferData"))(0x8892,32,quad,0x88E8);((delegate* unmanaged[Stdcall]<uint,int,uint,byte,int,nint,void>)gl.P("glVertexAttribPointer"))(0,2,0x1406,0,8,0);((delegate* unmanaged[Stdcall]<uint,void>)gl.P("glEnableVertexAttribArray"))(0);((delegate* unmanaged[Stdcall]<uint,int,int,void>)gl.P("glDrawArrays"))(5,0,4);dirty=pending=true;
        return true;
    }
    private void ReleasePendingImages()
    {
        if(pendingImages is null||pendingImages.Count==0)return;
        Exception? error=null;foreach(var lease in pendingImages)try{lease.Dispose();}catch(Exception e){error??=e;}pendingImages.Clear();
        if(error is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    public void Flush()
    {
        Check();if(!pending)return;((delegate* unmanaged[Stdcall]<void>)gl.P("glFinish"))();gl.CheckError();pending=false;ReleasePendingImages();
    }
    internal bool ReadTo(nint address,int rowBytes)
    {
        Check();if(flip||rowBytes!=pixels.Width*4)return false;Bind();gl.PixelStorage(true);((delegate* unmanaged[Stdcall]<int,int,int,int,uint,uint,nint,void>)gl.P("glReadPixels"))(0,0,pixels.Width,pixels.Height,0x1908,0x1401,address);gl.CheckError();pending=false;ReleasePendingImages();return true;
    }
    internal void ReadTarget()
    {
        Check();if(!dirty)return;Bind();readback??=PixelBuffer.Rent(pixels.Width,pixels.Height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);using(var pin=readback.Pin()){gl.PixelStorage(true);((delegate* unmanaged[Stdcall]<int,int,int,int,uint,uint,nint,void>)gl.P("glReadPixels"))(0,0,pixels.Width,pixels.Height,0x1908,0x1401,pin.Address);}gl.CheckError();pending=false;
        for(int y=0;y<pixels.Height;y++){var source=readback.GetRowSpan(flip?pixels.Height-1-y:y);var dest=pixels.GetRowSpan(y)[..(pixels.Width*4)];if(pixels.Format==PixelFormat.Rgba8888)source.CopyTo(dest);else PixelTransferKernel.SwapRedBlue(source,dest,CpuOptions.EnableSimd);}dirty=false;ReleasePendingImages();
    }
    internal GlImage Snapshot()
    {
        Check();Bind();uint texture=gl.Texture(pixels.Width,pixels.Height,0x1908,0);
        try{Bind();((delegate* unmanaged[Stdcall]<uint,int,int,int,int,int,int,int,void>)gl.P("glCopyTexSubImage2D"))(0x0DE1,0,0,0,0,0,pixels.Width,pixels.Height);gl.CheckError();pending=true;return new(gl,texture,pixels.Width,pixels.Height,flip,pixels.Alpha,true,false);}catch{gl.DeleteTexture(ref texture);throw;}
    }
    public void ConfigureResourceCache(long limit,bool pathMasks){Check();this.limit=limit;cacheMasks=pathMasks;coverageCache.Configure(limit,pathMasks);if(!pathMasks)PurgeCachedResources(scratchOnly:true);if(maskBytes+imageBytes>limit)PurgeCachedResources();}
    public void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)
    {
        Check();if(bytesToPurge<=0)return;if(minimumAgeMilliseconds==0)coverageCache.Clear();long freed=0;
        void Delete(uint texture){if(drawing)retired.Add(texture);else gl.DeleteTexture(ref texture);}
        foreach(var item in masks.OrderBy(p=>p.Value.Touched).ToArray()){if(Stopwatch.GetElapsedTime(item.Value.Touched).TotalMilliseconds<minimumAgeMilliseconds)continue;masks.Remove(item.Key);maskBytes-=item.Value.Bytes;freed+=item.Value.Bytes;Delete(item.Value.Texture);if(freed>=bytesToPurge)return;}
        if(!scratchOnly)foreach(var item in images.OrderBy(p=>p.Value.Touched).ToArray()){if(Stopwatch.GetElapsedTime(item.Value.Touched).TotalMilliseconds<minimumAgeMilliseconds)continue;images.Remove(item.Key);imageBytes-=item.Value.Bytes.Length;freed+=item.Value.Bytes.Length;Delete(item.Value.Texture);if(freed>=bytesToPurge)return;}
    }
    private void Release(bool destroy=true)
    {
        void Delete(uint texture){if(destroy){uint value=texture;gl.DeleteTexture(ref value);}}
        foreach(var item in masks.Values)Delete(item.Texture);masks.Clear();foreach(var image in images.Values)Delete(image.Texture);images.Clear();imageBytes=maskBytes=0;
        if(ownsTarget)Delete(target);target=0;Delete(destination);Delete(dummy);destination=dummy=0;
        if(destroy&&!gl.Abandoned){uint value=framebuffer;if(value!=0&&ownsFramebuffer)((delegate* unmanaged[Stdcall]<int,uint*,void>)gl.P("glDeleteFramebuffers"))(1,&value);value=vertices;if(value!=0)((delegate* unmanaged[Stdcall]<int,uint*,void>)gl.P("glDeleteBuffers"))(1,&value);value=array;if(value!=0)((delegate* unmanaged[Stdcall]<int,uint*,void>)gl.P("glDeleteVertexArrays"))(1,&value);}
        framebuffer=vertices=array=0;glyphMasks.Clear();rectangles.Clear();coverageCache.Clear();ReleasePendingImages();
    }
    public void Abandon(bool releaseResources){if(disposed||abandoned)return;if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("Abandon GL surfaces on their creating thread.");if(releaseResources){Check();if(dirty)Flush();}Release(releaseResources);abandoned=true;}
    public void Dispose(){if(disposed)return;if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("Dispose GL surfaces on their creating thread.");try{if(!abandoned&&!gl.Abandoned&&dirty)Flush();Release();}finally{disposed=true;readback?.Dispose();pixels.Dispose();}}
}
