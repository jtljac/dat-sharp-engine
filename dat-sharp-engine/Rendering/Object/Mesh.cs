using dat_sharp_engine.AssetManager;

namespace dat_sharp_engine.Rendering.Object;

public class Mesh(Vertex[] vertices, uint[] indices) : GpuAsset {
    public readonly Vertex[] vertices = vertices;
    public readonly uint[] indices = indices;

    public ulong GpuIndex { get; set; }
}