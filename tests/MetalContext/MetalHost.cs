using System.Runtime.InteropServices;
internal sealed unsafe partial class MetalHost:IDisposable
{
 internal nint Device,Queue;
 internal string Name="";
 private nint pool;
 [LibraryImport("/System/Library/Frameworks/Metal.framework/Metal")]private static partial nint MTLCreateSystemDefaultDevice();
 [LibraryImport("/usr/lib/libobjc.A.dylib",StringMarshalling=StringMarshalling.Utf8)]private static partial nint sel_registerName(string selector);
 [LibraryImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")]private static partial nint Send(nint obj,nint selector);
 [LibraryImport("/usr/lib/libobjc.A.dylib")]private static partial void objc_release(nint obj);
 [LibraryImport("/usr/lib/libobjc.A.dylib")]private static partial nint objc_autoreleasePoolPush();
 [LibraryImport("/usr/lib/libobjc.A.dylib")]private static partial void objc_autoreleasePoolPop(nint pool);
 internal MetalHost()
 {
  if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException();pool=objc_autoreleasePoolPush();
  try{Device=MTLCreateSystemDefaultDevice();if(Device==0)throw new InvalidOperationException("No real Metal device.");Queue=Send(Device,sel_registerName("newCommandQueue"));if(Queue==0)throw new InvalidOperationException("No Metal command queue.");Name=Marshal.PtrToStringUTF8(Send(Send(Device,sel_registerName("name")),sel_registerName("UTF8String")))??"";}
  catch{Dispose();throw;}
 }
 public void Dispose(){if(Queue!=0){objc_release(Queue);Queue=0;}if(Device!=0){objc_release(Device);Device=0;}if(pool!=0){objc_autoreleasePoolPop(pool);pool=0;}}
}
