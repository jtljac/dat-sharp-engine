using dat_sharp_engine.Mesh;
using dat_sharp_engine.Rendering.Vulkan.GpuStructures;
using dat_sharp_engine.Util;

namespace dat_sharp_engine.Rendering;

public abstract class DatRenderer {
    static DatRenderer() { }

    ~DatRenderer() {
        Cleanup();
    }
    
    /// <summary>
    /// Get the SDL window flags required for this renderer during window creation
    /// <para/>
    /// Usually specifies the API that SDL needs to setup
    /// </summary>
    /// <returns>SDL Window flags</returns>
    public abstract uint GetWindowFlags();

    /// <summary>
    /// Initialise the renderer
    /// </summary>
    public virtual void Initialise() {
        Logger.EngineLogger.Info("Initialising Renderer");
    }

    /// <summary>
    /// Draw a frame with the renderer
    /// </summary>
    /// <param name="deltaTime">The amount of time taken to execute the last frame</param>
    /// <param name="gameTime">The total running time of the game</param>
    public abstract void Draw(float deltaTime, float gameTime);

    public abstract ulong UploadMesh(Mesh3d mesh);

    public abstract void DestroyMesh(ulong meshId);

    public void UnloadMesh(Mesh3d mesh) {
        if (mesh.isGpuLoaded) DestroyMesh(mesh.gpuIndex);
    }

    /// <summary>
    /// Cleanup the renderer
    /// </summary>
    public abstract void Cleanup();
}