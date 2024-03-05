using Buffer = Silk.NET.Vulkan.Buffer;

namespace dat_sharp_engine.Rendering.Vulkan.GpuStructures;

public struct AllocatedBuffer(Buffer buffer, VMASharp.Allocation allocation) {
    public Buffer buffer = buffer;
    public VMASharp.Allocation allocation = allocation;
}