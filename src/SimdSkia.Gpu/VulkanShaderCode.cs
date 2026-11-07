namespace SimdSkia.Gpu;

// SPIR-V is produced offline. Keep one immutable copy per process instead of
// allocating a MemoryStream and another array for every window/device/resize.
internal static class VulkanShaderCode
{
    internal static readonly byte[] Renderer=Read("renderer.spv");
    internal static readonly byte[] RectangleBatch=Read("renderer-batch.spv");
    private static byte[] Read(string name)
    {
        using var stream=typeof(VulkanCanvas).Assembly.GetManifestResourceStream("SimdSkia.Gpu.Shaders."+name)
            ??throw new InvalidOperationException("Embedded SPIR-V resource missing: "+name);
        if(stream.Length<20||stream.Length%4!=0||stream.Length>4*1024*1024)throw new InvalidDataException("Invalid SPIR-V size.");
        var bytes=new byte[checked((int)stream.Length)];stream.ReadExactly(bytes);
        if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=0x07230203)throw new InvalidDataException("Invalid SPIR-V magic.");
        return bytes;
    }
}
