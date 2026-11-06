using System.Runtime.InteropServices;
using SkiaSharp;
internal static unsafe class WglContext
{
 internal static void WithoutCurrent(Action action){nint dc=wglGetCurrentDC(),context=wglGetCurrentContext();if(wglMakeCurrent(0,0)==0)throw new InvalidOperationException("WGL suspend failed.");try{action();}finally{if(wglMakeCurrent(dc,context)==0)throw new InvalidOperationException("WGL restore failed.");}}
 internal static void Run(bool core,Action<GRGlInterface,string> action)
 {
  nint window=CreateWindowExW(0,"STATIC","SimdSkia GL validation",0,0,0,1,1,0,0,0,0);if(window==0)throw new InvalidOperationException("Unable to create hidden WGL host.");nint dc=GetDC(window),context=0,library=NativeLibrary.Load("opengl32.dll");
  try
  {
   var pfd=new PixelFormat{Size=40,Version=1,Flags=0x24,ColorBits=32,AlphaBits=8};int format=ChoosePixelFormat(dc,&pfd);if(format==0||SetPixelFormat(dc,format,&pfd)==0)throw new InvalidOperationException("No WGL pixel format.");context=wglCreateContext(dc);if(context==0||wglMakeCurrent(dc,context)==0)throw new InvalidOperationException("No current WGL context.");
   nint Lookup(string name){nint address=wglGetProcAddress(name);if(address is 0 or 1 or 2 or 3 or -1)NativeLibrary.TryGetExport(library,name,out address);return address;}
   if(core){nint create=Lookup("wglCreateContextAttribsARB");if(create==0)throw new InvalidOperationException("OpenGL core context unavailable.");int* attributes=stackalloc int[]{0x2091,3,0x2092,3,0x9126,1,0};nint next=((delegate* unmanaged[Stdcall]<nint,nint,int*,nint>)create)(dc,0,attributes);if(next==0)throw new InvalidOperationException("OpenGL 3.3 core unavailable.");wglMakeCurrent(0,0);wglDeleteContext(context);context=next;if(wglMakeCurrent(dc,context)==0)throw new InvalidOperationException("Core context activation failed.");}
   string renderer=Marshal.PtrToStringAnsi(((delegate* unmanaged[Stdcall]<uint,nint>)Lookup("glGetString"))(0x1F01))??"";if(renderer.Contains("GDI Generic",StringComparison.OrdinalIgnoreCase)||renderer.Contains("llvmpipe",StringComparison.OrdinalIgnoreCase)||renderer.Contains("Microsoft Basic",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Software adapter cannot pass the GL GPU gate.");
   using var api=GRGlInterface.CreateOpenGl(Lookup)??throw new InvalidOperationException("No GL interface.");action(api,renderer);
  }
  finally{wglMakeCurrent(0,0);if(context!=0)wglDeleteContext(context);NativeLibrary.Free(library);ReleaseDC(window,dc);DestroyWindow(window);}
 }
 [StructLayout(LayoutKind.Sequential)]private struct PixelFormat{internal ushort Size,Version;internal uint Flags;internal byte PixelType,ColorBits,RedBits,RedShift,GreenBits,GreenShift,BlueBits,BlueShift,AlphaBits,AlphaShift,AccumBits,AccumRed,AccumGreen,AccumBlue,AccumAlpha,Depth,Stencil,Aux,Layer,Reserved;internal uint LayerMask,Visible,Damage;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern nint CreateWindowExW(uint ex,string name,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint parameter);
 [DllImport("user32.dll")]private static extern nint GetDC(nint window);
 [DllImport("user32.dll")]private static extern int ReleaseDC(nint window,nint dc);
 [DllImport("user32.dll")]private static extern int DestroyWindow(nint window);
 [DllImport("gdi32.dll")]private static extern int ChoosePixelFormat(nint dc,PixelFormat* value);
 [DllImport("gdi32.dll")]private static extern int SetPixelFormat(nint dc,int format,PixelFormat* value);
 [DllImport("opengl32.dll")]private static extern nint wglCreateContext(nint dc);
 [DllImport("opengl32.dll")]private static extern int wglMakeCurrent(nint dc,nint context);
 [DllImport("opengl32.dll")]private static extern int wglDeleteContext(nint context);
 [DllImport("opengl32.dll")]private static extern nint wglGetCurrentDC();
 [DllImport("opengl32.dll")]private static extern nint wglGetCurrentContext();
 [DllImport("opengl32.dll",CharSet=CharSet.Ansi)]private static extern nint wglGetProcAddress(string name);
}
