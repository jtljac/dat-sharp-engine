using Silk.NET.Maths;

namespace dat_sharp_engine.Rendering.Object;

public struct Vertex(Vector3D<float> position, float uvX, Vector3D<float> normal, float uvY, Vector4D<float> color) {
    public Vector3D<float> position = position;
    public float uvX = uvX;
    public Vector3D<float> normal = normal;
    public float uvY = uvY;
    public Vector4D<float> color = color;
}