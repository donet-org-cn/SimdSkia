using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SimdSkia.Gpu;

public sealed unsafe partial class VulkanCanvas
{
    private ulong CreateShaderModule(string resource)
    {
        using var stream=typeof(VulkanCanvas).Assembly.GetManifestResourceStream(resource)
            ??throw new InvalidOperationException("Embedded SPIR-V resource missing: "+resource);
        if(stream.Length is <20 or >4*1024*1024||stream.Length%4!=0)throw new InvalidDataException("Invalid SPIR-V size.");
        int length=(int)stream.Length;

        // vkCreateShaderModule consumes its input synchronously. The open stream
        // owns the assembly mapping until the call returns.
        if(stream is UnmanagedMemoryStream mapped)
        {
            byte* address;
            try{address=mapped.PositionPointer;}
            catch(NotSupportedException){address=null;} // SafeBuffer-backed stream
            if(address!=null&&((nuint)address&3)==0)return Create((uint*)address);
        }
        var words=GC.AllocateUninitializedArray<uint>(length/4);
        stream.ReadExactly(MemoryMarshal.AsBytes(words.AsSpan()));
        fixed(uint* address=words)return Create(address);

        ulong Create(uint* code)
        {
            if(BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(code,4))!=0x07230203)
                throw new InvalidDataException("Invalid SPIR-V magic.");
            var info=new Vk.ShaderInfo{Type=16,CodeSize=(nuint)length,Code=code};
            ulong module=0;
            Check(Vk.vkCreateShaderModule(device,&info,null,&module));
            return module;
        }
    }
}
