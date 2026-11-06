using System.Runtime.InteropServices;
internal sealed unsafe partial class D3DHost:IDisposable
{
 internal nint Adapter,Device,Queue;
 internal string Name="";
 private static void* Slot(nint obj,int index)=>((void**)(*(nint*)obj))[index];
 private static void Check(int result){if(result<0)Marshal.ThrowExceptionForHR(result);}
 private static void Release(ref nint obj){nint value=obj;obj=0;if(value!=0)((delegate* unmanaged[Stdcall]<nint,uint>)Slot(value,2))(value);}
 internal D3DHost()
 {
  Guid factoryId=new("770aae78-f26f-4dba-a829-253c83d1b387"),deviceId=new("189819f1-1db6-4b57-be54-1821339b85f7"),queueId=new("0ec870a6-5d7e-4c22-8cfc-5baae07616ed");nint factory=0;Check(CreateDXGIFactory1(&factoryId,&factory));
  try
  {
   for(uint index=0;;index++)
   {
    nint adapter=0;int hr=((delegate* unmanaged[Stdcall]<nint,uint,nint*,int>)Slot(factory,12))(factory,index,&adapter);if(hr==unchecked((int)0x887A0002))break;Check(hr);
    try{byte* desc=stackalloc byte[312];Check(((delegate* unmanaged[Stdcall]<nint,void*,int>)Slot(adapter,10))(adapter,desc));if((*(uint*)(desc+304)&2)!=0)continue;nint device=0;if(D3D12CreateDevice(adapter,0xb000,&deviceId,&device)<0)continue;Device=device;Name=new string((char*)desc,0,128).TrimEnd('\0');Adapter=adapter;adapter=0;break;}
    finally{Release(ref adapter);}
   }
   if(Device==0)throw new InvalidOperationException("No hardware D3D12 device.");uint* description=stackalloc uint[4];new Span<uint>(description,4).Clear();nint queue=0;Check(((delegate* unmanaged[Stdcall]<nint,uint*,Guid*,nint*,int>)Slot(Device,8))(Device,description,&queueId,&queue));Queue=queue;
  }
  catch{Dispose();throw;}
  finally{Release(ref factory);}
 }
 public void Dispose(){Release(ref Queue);Release(ref Device);Release(ref Adapter);}
 [LibraryImport("dxgi.dll")]private static partial int CreateDXGIFactory1(Guid* id,nint* result);
 [LibraryImport("d3d12.dll")]private static partial int D3D12CreateDevice(nint adapter,uint level,Guid* id,nint* result);
}
