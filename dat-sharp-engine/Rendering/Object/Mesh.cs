using dat_sharp_engine.AssetManagement;
using dat_sharp_vfs;

namespace dat_sharp_engine.Rendering.Object;

public class Mesh : GpuAsset {
    public byte[]? vertices;
    public uint[]? indices;

    public Mesh(string? path, DVfsFile? file) : base(path, file) { }

    public Mesh(byte[] vertices, uint[] indices) {
        this.vertices = vertices;
        this.indices = indices;
    }

    public ulong gpuIndex { get; set; }

    protected override void LoadAssetImpl(Stream assetData) {
        throw new NotImplementedException();
    }

    protected override void UnloadAssetImpl() {
        vertices = null;
        indices = null;
    }
}