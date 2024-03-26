using Assimp;
using Assimp.Unmanaged;
using dat_asset_processor.Util;
using NLog;

namespace dat_asset_processor.AssetProcessors;

public class BasicMeshProcessor : IBaseAssetProcessor {
    private static readonly byte[] FileSignature = [0xB1, 0x44, 0x41, 0x54, 0x4D, 0x45, 0x53, 0x48];    // ±DATMESH
    private const byte FileVersion = 0x01; // 1
    private readonly AssimpContext _importer = new AssimpContext();

    protected virtual byte vertexSize => 48;

    /// <inheritdoc />
    public string GetProcessorName() {
        return "Basic Mesh Processor";
    }
    public string ProcessFile(string src, string destDir) {
        var destFile = Path.Join(destDir, $"{Path.GetFileNameWithoutExtension(src)}.datmesh");
        var model = _importer.ImportFile(src, PostProcessSteps.Triangulate | PostProcessSteps.FlipUVs | PostProcessSteps.JoinIdenticalVertices);

        if (!model.HasMeshes) {
            throw new Exception($"Failed to find mesh in {src}");
        }

        var fileStream = File.Open(destFile, FileMode.Create);
        using var writer = new BinaryWriter(fileStream);

        WriteHeader(writer);
        WriteTypeHints(writer);

        var mesh = model.Meshes[0];

        WriteVertices(mesh, writer);
        WriteIndices(mesh, writer);

        return destFile;
    }

    protected void WriteHeader(BinaryWriter writer) {
        writer.Write(FileSignature);    // Signature
        writer.Write(FileVersion);  // Version
        writer.Write(vertexSize);   // Vertex Size

        // Vertex, Index, and TypeHint count can be written when we know their size
        writer.Write((uint) 0); // Vertex count
        writer.Write((uint) 0); // Index Count
        writer.Write((byte) 0); // Typehint size
    }

    protected virtual void WriteTypeHints(BinaryWriter writer) {
        var curPos = writer.BaseStream.Position;
        writer.Seek(19, SeekOrigin.Begin);
        writer.Write((byte) 4);
        writer.Seek((int) curPos, SeekOrigin.Begin);

        writer.Write((byte) TypeHint.R32G32B32SFloat);      // Position
        writer.Write((byte) TypeHint.R32SFloat);            // UV.x
        writer.Write((byte) TypeHint.R32G32B32SFloat);      // Normal
        writer.Write((byte) TypeHint.R32SFloat);            // UV.y
        writer.Write((byte) TypeHint.R32G32B32A32SFloat);   // Colour
    }

    protected virtual void WriteVertices(Mesh mesh, BinaryWriter writer) {
        var curPos = writer.BaseStream.Position;
        writer.Seek(10, SeekOrigin.Begin);
        writer.Write((uint) mesh.Vertices.Count);
        writer.Seek((int) curPos, SeekOrigin.Begin);

        var texCoords = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0] : [];
        var vertexColours = mesh.HasVertexColors(0) ? mesh.VertexColorChannels[0] : [];

        for (var index = 0; index < mesh.VertexCount; index++) {
            var meshVertex = mesh.Vertices[index];
            var normal = mesh.HasNormals ? mesh.Normals[index] : new Vector3D(0, 0, 0);
            var uv = texCoords.Count == 0 ? new Vector3D(0, 0, 0) : texCoords[index];
            var colour = vertexColours.Count == 0 ? new Color4D(0, 0, 0, 0) : vertexColours[index];

            // Vertex
            writer.Write(meshVertex.X);
            writer.Write(meshVertex.Y);
            writer.Write(meshVertex.Z);
            // UV.x
            writer.Write(uv.X);

            // Normal
            writer.Write(normal.X);
            writer.Write(normal.Y);
            writer.Write(normal.Z);

            // UV.y
            writer.Write(uv.X);

            // Vertex
            writer.Write(colour.R);
            writer.Write(colour.G);
            writer.Write(colour.B);
            writer.Write(colour.A);
        }
    }

    protected virtual void WriteIndices(Mesh mesh, BinaryWriter writer) {
        var unsignedIndices = mesh.GetUnsignedIndices();

        var curPos = writer.BaseStream.Position;
        writer.Seek(14, SeekOrigin.Begin);
        writer.Write((uint) unsignedIndices.Length);
        writer.Seek((int) curPos, SeekOrigin.Begin);

        foreach (var unsignedIndex in unsignedIndices) {
            writer.Write(unsignedIndex);
        }
    }
}