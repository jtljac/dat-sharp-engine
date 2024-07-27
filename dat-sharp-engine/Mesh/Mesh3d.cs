using System.Runtime.InteropServices;
using dat_asset_handlers.DatMesh;
using dat_sharp_engine.AssetManagement.Util;
using dat_sharp_engine.Rendering;
using dat_sharp_vfs;

namespace dat_sharp_engine.Mesh;

public class Mesh3d : GpuAsset {
    public byte[]? vertices;
    public uint[]? indices;

    public Mesh3d(string? path, DVfsFile? file) : base(path, file) { }

    public Mesh3d(byte[] vertices, uint[] indices) {
        this.vertices = vertices;
        this.indices = indices;
    }

    public ulong gpuIndex { get; private set; }

    protected override void CpuLoadAssetImpl(Stream assetData) {
        using var reader = new BinaryReader(assetData);
        var signature = reader.ReadBytes(8);

        if (!DatMeshConstants.FileSignature.SequenceEqual(signature))
            throw new DatAssetException($"Asset is not a DatMesh: {path}");

        var version = reader.ReadByte();
        if (version != DatMeshConstants.FileVersion)
            throw new DatAssetException($"Version mismatch while parsing {path}");

        var vertexSize = reader.ReadByte();

        var vertexCount = reader.ReadInt32();
        var indexCount = reader.ReadInt32();

        // Skip Typehints
        var typeHintSize = reader.ReadByte();
        assetData.Seek(typeHintSize, SeekOrigin.Current);

        vertices = reader.ReadBytes(vertexCount * vertexSize);

        indices = new uint[indexCount];
        var bytes = MemoryMarshal.AsBytes(indices.AsSpan());
        var readIndices = reader.Read(bytes) / sizeof(uint);
        if (readIndices != indexCount) throw new DatAssetException("Asset stream contained less data than expected");
    }

    protected override void CpuUnloadAssetImpl() {
        vertices = null;
        indices = null;
    }

    protected override void GpuLoadAssetImpl() {
        Thread.Sleep(5000);
        gpuIndex = DatSharpEngine.instance.renderer.UploadMesh(this);
    }

    protected override void GpuUnloadAssetImpl() {
        DatSharpEngine.instance.renderer.DestroyMesh(gpuIndex);
        gpuIndex = 0;
    }
}