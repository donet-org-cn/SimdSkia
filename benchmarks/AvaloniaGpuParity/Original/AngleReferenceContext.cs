using System.Runtime.InteropServices;
using SkiaSharp;

// Isolated reference only: the ANGLE package/version is exactly the dependency
// in the default Avalonia 12.1.4 application. No production project uses it.
internal static unsafe class AngleReferenceContext
{
    private const string Library="av_libglesv2";
    [DllImport(Library,EntryPoint="EGL_GetProcAddress")] private static extern nint eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)]string name);
    [DllImport(Library,EntryPoint="EGL_Initialize")] private static extern int eglInitialize(nint display,out int major,out int minor);
    [DllImport(Library,EntryPoint="EGL_BindAPI")] private static extern int eglBindAPI(int api);
    [DllImport(Library,EntryPoint="EGL_ChooseConfig")] private static extern int eglChooseConfig(nint display,int* attributes,out nint config,int capacity,out int count);
    [DllImport(Library,EntryPoint="EGL_CreatePbufferSurface")] private static extern nint eglCreatePbufferSurface(nint display,nint config,int* attributes);
    [DllImport(Library,EntryPoint="EGL_CreateContext")] private static extern nint eglCreateContext(nint display,nint config,nint share,int* attributes);
    [DllImport(Library,EntryPoint="EGL_MakeCurrent")] private static extern int eglMakeCurrent(nint display,nint draw,nint read,nint context);
    [DllImport(Library,EntryPoint="EGL_DestroyContext")] private static extern int eglDestroyContext(nint display,nint context);
    [DllImport(Library,EntryPoint="EGL_DestroySurface")] private static extern int eglDestroySurface(nint display,nint surface);
    [DllImport(Library,EntryPoint="EGL_Terminate")] private static extern int eglTerminate(nint display);
    [DllImport(Library,EntryPoint="EGL_GetError")] private static extern int eglGetError();
    private static void Check(int result){if(result==0)throw new InvalidOperationException($"EGL failed: 0x{eglGetError():X}");}
    internal static int Run(Func<GRContext,string,int> run)
    {
        var getDisplay=(delegate* unmanaged[Stdcall]<int,nint,int*,nint>)eglGetProcAddress("eglGetPlatformDisplayEXT");
        if(getDisplay==null)throw new InvalidOperationException("ANGLE display extension missing.");
        // EGL_PLATFORM_ANGLE_ANGLE / TYPE_D3D11. No WARP/software fallback.
        int* attributes=stackalloc int[]{0x3203,0x3208,0x3209,0x320A,0x3038};
        nint display=getDisplay(0x3202,0,attributes);
        if(display==0)throw new InvalidOperationException("D3D11 hardware EGL display unavailable.");
        Check(eglInitialize(display,out _,out _));nint surface=0,context=0;
        try
        {
            Check(eglBindAPI(0x30A0));
            int* configAttributes=stackalloc int[]{0x3024,8,0x3023,8,0x3022,8,0x3021,8,0x3033,1,0x3040,4,0x3038};
            Check(eglChooseConfig(display,configAttributes,out nint config,1,out int count));
            if(count!=1)throw new InvalidOperationException("RGBA8888 EGL configuration unavailable.");
            int* size=stackalloc int[]{0x3057,1,0x3056,1,0x3038};surface=eglCreatePbufferSurface(display,config,size);
            if(surface==0)throw new InvalidOperationException("EGL pbuffer unavailable.");
            int* version=stackalloc int[]{0x3098,2,0x3038};context=eglCreateContext(display,config,0,version);
            if(context==0)throw new InvalidOperationException("EGL GLES context unavailable.");
            Check(eglMakeCurrent(display,surface,surface,context));
            var getString=(delegate* unmanaged[Stdcall]<uint,nint>)eglGetProcAddress("glGetString");
            string renderer=Marshal.PtrToStringAnsi(getString(0x1F01))??throw new InvalidOperationException("GL renderer missing.");
            if(renderer.Contains("WARP",StringComparison.OrdinalIgnoreCase)||renderer.Contains("Microsoft Basic",StringComparison.OrdinalIgnoreCase)||renderer.Contains("SwiftShader",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Software adapter cannot pass the GPU gate.");
            using var api=GRGlInterface.CreateGles(eglGetProcAddress)??throw new InvalidOperationException("Skia GLES interface missing.");
            using var gpu=GRContext.CreateGl(api)??throw new InvalidOperationException("Skia GPU context missing.");
            return run(gpu,renderer);
        }
        finally
        {
            eglMakeCurrent(display,0,0,0);
            if(context!=0)eglDestroyContext(display,context);
            if(surface!=0)eglDestroySurface(display,surface);
            eglTerminate(display);
        }
    }
}
