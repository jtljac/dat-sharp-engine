using System.Numerics;
using Silk.NET.Maths;

namespace dat_sharp_engine.Rendering.Vulkan;

public struct ComputePushConstants(
    Vector4D<float> data1 = new(),
    Vector4D<float> data2 = new(),
    Vector4D<float> data3 = new(),
    Vector4D<float> data4 = new()) {
    public Vector4D<float> data1 = data1;
    public Vector4D<float> data2 = data2;
    public Vector4D<float> data3 = data3;
    public Vector4D<float> data4 = data4;
}