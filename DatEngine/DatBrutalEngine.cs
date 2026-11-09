using Brutal.SdlApi;
using DatEngine.Gpu;
using DatEngine.Util;

namespace DatEngine;

/// <summary>
/// The entry point of the engine
/// </summary>
/// <param name="gpu">The GPU class to use for this engine</param>
public class DatBrutalEngine(IGpu gpu) {
    public IGpu gpu { get; } = gpu;
    // public CVarManager;

    public DatBrutalEngine Init() {
        Logger.EngineLogger.Info("Initializing Engine");
        InitialiseSdl();
        InitialiseGpu();
        // Create primary window

        return this;
    }

    public void InitialiseSdl() {
        Logger.EngineLogger.Trace("Initializing Sdl");
        Sdl.Init.Initialize(SdlInitFlags.Video | SdlInitFlags.Events);
    }

    private void InitialiseGpu() {
        Logger.EngineLogger.Trace("Initializing Gpu");
        // Make primary window
        gpu.Init(null);
    }

    public void StartLoop() {
        Logger.EngineLogger.Trace("Starting Main Loop");


        while (true) {
            SdlEvent gameEvent;
            while (Sdl.Events.PollEvent(out gameEvent)) {

            }
        }
    }
}