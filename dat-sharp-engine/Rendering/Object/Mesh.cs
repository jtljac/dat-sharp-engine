using dat_sharp_engine.AssetManagement;

namespace dat_sharp_engine.Rendering.Object;

public class Mesh : GpuAsset {
    public byte[]? vertices;
    public uint[]? indices;

    /// <summary>
    /// Create a
    /// This constructor will create a unique non-virtual instance of a Mesh. It should be preferred to
    /// access an asset with <see cref="AssetManager.GetAsset{T}(string)"/>
    /// </summary>
    /// <param name="path">The path to the asset</param>
    public Mesh(string path) : base(path) {}

    public Mesh(byte[] vertices, uint[] indices) : base(null) {
        this.vertices = vertices;
        this.indices = indices;
    }

    public ulong gpuIndex { get; set; }

    public override void LoadAsset(Stream assetData) {
        throw new NotImplementedException();
    }

    protected override void UnloadAssetImpl() {
        vertices = null;
        indices = null;
    }
}