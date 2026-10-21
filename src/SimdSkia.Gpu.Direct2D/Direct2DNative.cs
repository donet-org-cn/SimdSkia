using System.Runtime.InteropServices;
namespace SimdSkia.Gpu.Direct2D;

// Fixed Windows SDK ABI. No reflection-based COM activation or bundled native component.
internal static unsafe partial class D2DNative
{
    internal static readonly Guid DxgiDevice=new("54ec77fa-1377-44e6-8c32-88fd5f44c84c"), DxgiSurface=new("cafcb56c-6ac3-4889-bf47-9e23bbd260ec"),
        DxgiFactory2=new("50c83a1c-e072-4c48-87b0-3630fa36a6d0"), Texture2D=new("6f15aaf2-d208-4e89-9ab4-489535d34f9c"), Factory1=new("bb12d362-daee-4b9a-aa1d-14ba401cfa1f");
    internal static void* Slot(nint obj,int slot)=>((void**)(*(nint*)obj))[slot];
    internal static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
    internal static nint Query(nint obj,Guid iid){nint result=0;Check(((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)Slot(obj,0))(obj,&iid,&result));return result;}
    internal static void Release(ref nint obj){nint value=Interlocked.Exchange(ref obj,0);if(value!=0)((delegate* unmanaged[Stdcall]<nint,uint>)Slot(value,2))(value);}
    internal static nint CreateDevice(){nint device=0,context=0;uint level=0xb000,actual=0;Check(D3D11CreateDevice(0,1,0,32,&level,1,7,&device,&actual,&context));Release(ref context);return device;}
    internal static nint Immediate(nint device){nint result=0;((delegate* unmanaged[Stdcall]<nint,nint*,void>)Slot(device,40))(device,&result);return result;}
    internal static nint Adapter(nint dxgi){nint result=0;Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Slot(dxgi,7))(dxgi,&result));return result;}
    internal static string AdapterName(nint adapter)
    {
        nint adapter1=Query(adapter,new Guid("29038f61-3839-4626-91fd-086879011a05"));
        try{byte* desc=stackalloc byte[312];Check(((delegate* unmanaged[Stdcall]<nint,void*,int>)Slot(adapter1,10))(adapter1,desc));if((*(uint*)(desc+304)&2)!=0)throw new NotSupportedException("A software DXGI adapter cannot be reported as Direct2D hardware acceleration.");return new string((char*)desc,0,128).TrimEnd('\0');}
        finally{Release(ref adapter1);}
    }
    internal static nint Parent(nint obj,Guid iid){nint result=0;Check(((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)Slot(obj,6))(obj,&iid,&result));return result;}
    internal static nint CreateFactory(){nint result=0;Guid iid=Factory1;Check(D2D1CreateFactory(0,&iid,null,&result));return result;}
    internal static nint DrawingDevice(nint factory,nint dxgi){nint result=0;Check(((delegate* unmanaged[Stdcall]<nint,nint,nint*,int>)Slot(factory,17))(factory,dxgi,&result));return result;}
    internal static nint DrawingContext(nint device){nint result=0;Check(((delegate* unmanaged[Stdcall]<nint,uint,nint*,int>)Slot(device,4))(device,0,&result));return result;}
    internal static nint Texture(nint device,int width,int height,bool staging){var d=new TextureDescription{Width=(uint)width,Height=(uint)height,Mips=1,Array=1,Format=87,SampleCount=1,Usage=staging?3u:0,Bind=staging?0u:40,Cpu=staging?0x20000u:0};nint r=0;Check(((delegate* unmanaged[Stdcall]<nint,TextureDescription*,void*,nint*,int>)Slot(device,5))(device,&d,null,&r));return r;}
    internal static nint Swapchain(nint factory,nint device,nint hwnd,int width,int height){var d=new SwapDescription{Width=(uint)width,Height=(uint)height,Format=87,SampleCount=1,Usage=0x20,BufferCount=1,Effect=1,Alpha=3};nint r=0;Check(((delegate* unmanaged[Stdcall]<nint,nint,nint,SwapDescription*,void*,nint,nint*,int>)Slot(factory,15))(factory,device,hwnd,&d,null,0,&r));return r;}
    internal static nint Backbuffer(nint swapchain){nint r=0;Guid iid=Texture2D;Check(((delegate* unmanaged[Stdcall]<nint,uint,Guid*,nint*,int>)Slot(swapchain,9))(swapchain,0,&iid,&r));return r;}
    internal static void Present(nint swapchain)=>Check(((delegate* unmanaged[Stdcall]<nint,uint,uint,int>)Slot(swapchain,8))(swapchain,0,0));
    internal static nint QueryEvent(nint device){uint* d=stackalloc uint[2]{0,0};nint r=0;Check(((delegate* unmanaged[Stdcall]<nint,void*,nint*,int>)Slot(device,24))(device,d,&r));return r;}
    internal static void EndEvent(nint context,nint query)=>((delegate* unmanaged[Stdcall]<nint,nint,void>)Slot(context,28))(context,query);
    internal static int EventReady(nint context,nint query)=>((delegate* unmanaged[Stdcall]<nint,nint,nint,uint,uint,int>)Slot(context,29))(context,query,0,0,0);
    internal static void Flush(nint context)=>((delegate* unmanaged[Stdcall]<nint,void>)Slot(context,111))(context);
    internal static void ClearState(nint context)=>((delegate* unmanaged[Stdcall]<nint,void>)Slot(context,110))(context);
    internal static void Upload(nint context,nint texture,nint data,int stride)=>((delegate* unmanaged[Stdcall]<nint,nint,uint,void*,nint,uint,uint,void>)Slot(context,48))(context,texture,0,null,data,(uint)stride,0);
    internal static void Copy(nint context,nint destination,nint source)=>((delegate* unmanaged[Stdcall]<nint,nint,nint,void>)Slot(context,47))(context,destination,source);
    internal static Mapping Map(nint context,nint resource){Mapping r=default;Check(((delegate* unmanaged[Stdcall]<nint,nint,uint,uint,uint,Mapping*,int>)Slot(context,14))(context,resource,0,1,0,&r));return r;}
    internal static void Unmap(nint context,nint resource)=>((delegate* unmanaged[Stdcall]<nint,nint,uint,void>)Slot(context,15))(context,resource,0);
    internal static nint Target(nint context,nint surface){var p=new BitmapProperties1{Format=87,Alpha=1,DpiX=96,DpiY=96,Options=3};nint r=0;Check(((delegate* unmanaged[Stdcall]<nint,nint,BitmapProperties1*,nint*,int>)Slot(context,62))(context,surface,&p,&r));return r;}
    internal static void SetTarget(nint context,nint target)=>((delegate* unmanaged[Stdcall]<nint,nint,void>)Slot(context,74))(context,target);
    internal static void Begin(nint context)=>((delegate* unmanaged[Stdcall]<nint,void>)Slot(context,48))(context);
    internal static void End(nint context)=>Check(((delegate* unmanaged[Stdcall]<nint,ulong*,ulong*,int>)Slot(context,49))(context,null,null));
    internal static void Transform(nint context,Matrix3 m){float* v=stackalloc float[6]{m.M00,m.M10,m.M01,m.M11,m.M02,m.M12};((delegate* unmanaged[Stdcall]<nint,float*,void>)Slot(context,30))(context,v);}
    internal static void Blend(nint context,CompositeMode mode)=>((delegate* unmanaged[Stdcall]<nint,uint,void>)Slot(context,78))(context,mode==CompositeMode.SourceOver?0u:1u);
    internal static void Antialias(nint context,bool enabled)=>((delegate* unmanaged[Stdcall]<nint,uint,void>)Slot(context,32))(context,enabled?0u:1u);
    internal static void PushClip(nint context,RectF rect)=>((delegate* unmanaged[Stdcall]<nint,RectF*,uint,void>)Slot(context,45))(context,&rect,1);
    internal static void PopClip(nint context)=>((delegate* unmanaged[Stdcall]<nint,void>)Slot(context,46))(context);
    internal static nint OpacityBrush(nint context,nint bitmap,int left,int top)
    {
        uint* bitmapProps=stackalloc uint[3]{0,0,0};float* brushProps=stackalloc float[7]{1,1,0,0,1,left,top};nint result=0;
        Check(((delegate* unmanaged[Stdcall]<nint,nint,uint*,float*,nint*,int>)Slot(context,7))(context,bitmap,bitmapProps,brushProps,&result));return result;
    }
    internal static void PushCoverage(nint context,RectF bounds,nint brush)
    {var p=new LayerParameters{Bounds=bounds,Antialias=1,M11=1,M22=1,Opacity=1,Brush=brush};((delegate* unmanaged[Stdcall]<nint,LayerParameters*,nint,void>)Slot(context,86))(context,&p,0);}
    internal static void PopLayer(nint context)=>((delegate* unmanaged[Stdcall]<nint,void>)Slot(context,41))(context);
    internal static Color ColorOf(Rgba32 c)=>new(){R=c.R/255f,G=c.G/255f,B=c.B/255f,A=c.A/255f};
    internal static void Clear(nint context,Rgba32 color){var c=ColorOf(color);((delegate* unmanaged[Stdcall]<nint,Color*,void>)Slot(context,47))(context,&c);}
    internal static nint Solid(nint context,Rgba32 color){var c=ColorOf(color);nint r=0;Check(((delegate* unmanaged[Stdcall]<nint,Color*,void*,nint*,int>)Slot(context,8))(context,&c,null,&r));return r;}
    internal static nint Gradient(nint context,LinearGradient gradient,byte opacity)
        =>Gradient(context,new LinearGradientDescription(gradient.Start,gradient.End,[new(0,gradient.StartColor),new(1,gradient.EndColor)]),opacity);
    internal static nint Gradient(nint context,LinearGradientDescription gradient,byte opacity)
    {
        var values=gradient.Stops.Span;if(values.Length>256||values.ToArray().Any(s=>s.Offset<0||s.Offset>1))throw new NotSupportedException("Direct2D gradients accept up to 256 stops in [0,1].");
        if(!gradient.SampleTransform.TryInvert(out var transform))throw new NotSupportedException("Singular gradient transform.");
        Stop* stops=stackalloc Stop[values.Length];for(int i=0;i<values.Length;i++)stops[i]=new(){Position=values[i].Offset,Color=ColorOf(values[i].Color)};
        nint collection=0,r=0;
        // D2D1.1 straight-color interpolation in sRGB, matching managed gradient semantics.
        Check(((delegate* unmanaged[Stdcall]<nint,Stop*,uint,uint,uint,uint,uint,uint,nint*,int>)Slot(context,64))(context,stops,(uint)values.Length,1,1,1,(uint)gradient.Extend,0,&collection));
        try
        {
            PointF* points=stackalloc PointF[2]{gradient.Start,gradient.End};Check(((delegate* unmanaged[Stdcall]<nint,PointF*,void*,nint,nint*,int>)Slot(context,10))(context,points,null,collection,&r));
            ((delegate* unmanaged[Stdcall]<nint,float,void>)Slot(r,4))(r,opacity/255f);
            float* m=stackalloc float[6]{transform.M00,transform.M10,transform.M01,transform.M11,transform.M02,transform.M12};((delegate* unmanaged[Stdcall]<nint,float*,void>)Slot(r,5))(r,m);return r;
        }
        finally{Release(ref collection);}
    }
    internal static void FillRect(nint context,RectF rectangle,nint brush)=>((delegate* unmanaged[Stdcall]<nint,RectF*,nint,void>)Slot(context,17))(context,&rectangle,brush);
    internal static void FillPath(nint context,nint path,nint brush)=>((delegate* unmanaged[Stdcall]<nint,nint,nint,nint,void>)Slot(context,23))(context,path,brush,0);
    internal static nint Bitmap(nint context,int width,int height,nint bytes,int stride){var size=new Size{Width=(uint)width,Height=(uint)height};var p=new BitmapProperties{Format=87,Alpha=1,DpiX=96,DpiY=96};nint r=0;Check(((delegate* unmanaged[Stdcall]<nint,Size,nint,uint,BitmapProperties*,nint*,int>)Slot(context,4))(context,size,bytes,(uint)stride,&p,&r));return r;}
    internal static void DrawBitmap(nint context,nint bitmap,RectF destination,RectF source,byte opacity,ImageSampling sampling)=>((delegate* unmanaged[Stdcall]<nint,nint,RectF*,float,uint,RectF*,void*,void>)Slot(context,85))(context,bitmap,&destination,opacity/255f,sampling==ImageSampling.Nearest?0u:1u,&source,null);
    internal static nint Path(nint factory){nint r=0;Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Slot(factory,10))(factory,&r));return r;}
    internal static nint Open(nint geometry){nint r=0;Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Slot(geometry,17))(geometry,&r));return r;}
    internal static void FillRule(nint sink,bool winding)=>((delegate* unmanaged[Stdcall]<nint,uint,void>)Slot(sink,3))(sink,winding?1u:0u);
    internal static void BeginFigure(nint sink,PointF start)=>((delegate* unmanaged[Stdcall]<nint,PointF,uint,void>)Slot(sink,5))(sink,start,0);
    internal static void EndFigure(nint sink,bool closed)=>((delegate* unmanaged[Stdcall]<nint,uint,void>)Slot(sink,8))(sink,closed?1u:0u);
    internal static void Line(nint sink,PointF end)=>((delegate* unmanaged[Stdcall]<nint,PointF,void>)Slot(sink,10))(sink,end);
    internal static void Bezier(nint sink,PathSegment s){PointF* p=stackalloc PointF[3]{s.A,s.B,s.C};((delegate* unmanaged[Stdcall]<nint,PointF*,void>)Slot(sink,s.Verb==PathVerb.Quadratic?12:11))(sink,p);}
    internal static void Close(nint sink)=>Check(((delegate* unmanaged[Stdcall]<nint,int>)Slot(sink,9))(sink));
    [StructLayout(LayoutKind.Sequential)] internal struct TextureDescription{internal uint Width,Height,Mips,Array,Format,SampleCount,SampleQuality,Usage,Bind,Cpu,Misc;}
    [StructLayout(LayoutKind.Sequential)] internal struct SwapDescription{internal uint Width,Height,Format,Stereo,SampleCount,SampleQuality,Usage,BufferCount,Scaling,Effect,Alpha,Flags;}
    [StructLayout(LayoutKind.Sequential)] internal struct Mapping{internal nint Data;internal uint RowPitch,DepthPitch;}
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapProperties{internal uint Format,Alpha;internal float DpiX,DpiY;}
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapProperties1{internal uint Format,Alpha;internal float DpiX,DpiY;internal uint Options;internal nint ColorContext;}
    [StructLayout(LayoutKind.Sequential)] internal struct Size{internal uint Width,Height;}
    [StructLayout(LayoutKind.Sequential)] internal struct Color{internal float R,G,B,A;}
    [StructLayout(LayoutKind.Sequential)] internal struct Stop{internal float Position;internal Color Color;}
    [StructLayout(LayoutKind.Sequential)] internal struct LayerParameters{internal RectF Bounds;internal nint Geometry;internal uint Antialias;internal float M11,M12,M21,M22,Dx,Dy,Opacity;internal nint Brush;internal uint Options;}
    internal static void ValidateAbi(){if(IntPtr.Size!=8||!BitConverter.IsLittleEndian)throw new PlatformNotSupportedException("Direct2D requires 64-bit little-endian Windows.");if(sizeof(TextureDescription)!=44||sizeof(SwapDescription)!=48||sizeof(BitmapProperties1)!=32||sizeof(LayerParameters)!=72||sizeof(Stop)!=20||sizeof(PointF)!=8||sizeof(RectF)!=16)throw new PlatformNotSupportedException("Windows graphics ABI layout mismatch.");}
    [LibraryImport("d3d11.dll")] private static partial int D3D11CreateDevice(nint adapter,uint driver,nint software,uint flags,uint* levels,uint levelCount,uint sdk,nint* device,uint* actual,nint* context);
    [LibraryImport("d2d1.dll")] private static partial int D2D1CreateFactory(uint type,Guid* iid,void* options,nint* factory);
}
