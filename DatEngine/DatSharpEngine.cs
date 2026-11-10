using Brutal.SdlApi;
using DatEngine.Gpu;
using DatEngine.Util;

namespace DatEngine;

/// <summary>
/// The entry point of the engine
/// </summary>
/// <param name="baseGpu">The GPU class to use for this engine</param>
public class DatSharpEngine(BaseGpu baseGpu) {
    public BaseGpu baseGpu { get; } = baseGpu;
    public readonly CVarManager cvarMan = new();

    public DatSharpEngine Init() {
        Logger.EngineLogger.Info("Initializing Engine");
        cvarMan.Init();

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
        baseGpu.Init(null);
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