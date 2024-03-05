namespace dat_sharp_engine.Rendering.Vulkan.GpuStructures;

public class AllocatedMesh(AllocatedBuffer indexBuffer, AllocatedBuffer vertexBuffer, ulong vertexBufferAddress) {
    public AllocatedBuffer indexBuffer = indexBuffer;
    public AllocatedBuffer vertexBuffer = vertexBuffer;
    public ulong vertexBufferAddress = vertexBufferAddress;
}