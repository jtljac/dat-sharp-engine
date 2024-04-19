using dat_sharp_engine.Rendering.Object;
using dat_sharp_engine.Rendering.Vulkan.GpuStructures;
using dat_sharp_engine.Util;

namespace dat_sharp_engine.Rendering;

public abstract class DatRenderer {
    // CVars
    protected static readonly CVar<int> GpuUuidCvar = new("iGpuUuid", "The UUID of the GPU to use for rendering", 0, CVarCategory.Graphics, CVarFlags.None);
    protected static readonly CVar<uint> BufferedFramesCvar = new("iBufferedFrames", "The number of buffered frames to render with", 2, CVarCategory.Graphics, CVarFlags.None);
    protected static readonly CVar<bool> VsyncCvar = new("bVsync", "Enable VSync", true, CVarCategory.Graphics, CVarFlags.None);

    protected readonly CVar<int> widthCvar = CVars.instance.GetIntCVar("uWindowWidth")!;
    protected readonly CVar<int> heightCvar = CVars.instance.GetIntCVar("uWindowHeight")!;

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

    public abstract AllocatedMesh UploadMesh(Mesh mesh);
    
    /// <summary>
    /// Cleanup the renderer
    /// </summary>
    public abstract void Cleanup();
}