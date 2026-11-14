using Brutal.SdlApi;
using DatEngine.Engine;
using DatEngine.Gpu;
using DatEngine.Util;

namespace DatEngine;

/// <summary>
/// The entry point of the engine
/// </summary>
/// <param name="baseGpu">The GPU class to use for this engine</param>
public class DatSharpEngine(BaseGpu baseGpu) {
    /// <summary>
    /// The GPU instance for rendering and Compute
    /// </summary>
    public readonly BaseGpu baseGpu = baseGpu;

    /// <summary>
    /// The CVar manager instance for global values
    /// </summary>
    public readonly CVarManager cvarMan = new();

    /// <summary>
    /// The window manager instance for handling the lifetime of windows
    /// </summary>
    public readonly WindowManager winMan = new WindowManager(baseGpu);

    /// <summary>
    /// Initialise the engine and it's subsystems
    /// </summary>
    /// <returns>A reference to this for chaining</returns>
    public DatSharpEngine Init() {
        Logger.EngineLogger.Info("Initializing Engine");
        // Start with the CVars, everything depends on that
        cvarMan.Init();

        InitialiseSdl();
        InitialiseGpu();

        // Create primary window
        winMan.CreatePrimaryWindow();

        return this;
    }

    /// <summary>
    /// Initialise SDL systems for windows and inputs
    /// </summary>
    private void InitialiseSdl() {
        Logger.EngineLogger.Trace("Initializing Sdl");
        Sdl.Init.Initialize(SdlInitFlags.Video | SdlInitFlags.Events);
    }

    /// <summary>
    /// Set up the GPU for use
    /// </summary>
    private void InitialiseGpu() {
        Logger.EngineLogger.Trace("Initializing Gpu");
        // Make primary window
        baseGpu.Init();
    }

    /// <summary>
    /// Hand off to the engine to start the game Loop
    /// </summary>
    public void StartLoop() {
        Logger.EngineLogger.Trace("Starting Main Loop");


        while (true) {
            SdlEvent gameEvent;
            while (Sdl.Events.PollEvent(out gameEvent)) {

            }
        }
    }
}