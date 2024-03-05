using Silk.NET.Maths;

namespace dat_sharp_engine.Rendering.Vulkan;

public struct DrawPushConstants(Matrix4X4<float> worldMatrix = new(), ulong vertexBuffer = new()) {
    public Matrix4X4<float> worldMatrix = worldMatrix;
    public ulong vertexBuffer = vertexBuffer;
}