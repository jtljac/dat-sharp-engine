using dat_sharp_engine.AssetManagement;
using dat_sharp_engine.Rendering;
using dat_sharp_engine.Rendering.Vulkan;
using dat_sharp_engine.Threading;
using dat_sharp_engine.Util;
using Silk.NET.SDL;

namespace dat_sharp_engine;

/// <summary>
/// The entry point of the engine
/// </summary>
public class DatSharpEngine {
    /// <summary>The engines reference to SDL2</summary>
    internal readonly Sdl sdl = Sdl.GetApi();

    /// <summary>The static instance of the engine</summary>
    public static DatSharpEngine instance { get; } = new();
    /// <summary>The instance of the renderer being used by the engine</summary>
    public DatRenderer renderer { get; private set; } = null!;

    /// <summary>The application settings defining game to the engine</summary>
    public ApplicationSettings appSettings { get; private set; } = null!;
    
    /// <summary>The window the game is in</summary>
    public unsafe Window* window;

    /// <summary>Whether he game should try to close</summary>
    private bool shouldClose { get; set; }

    static DatSharpEngine() { }

    /// <summary>
    /// Setup the engine and the engine subsystems
    /// <list type="bullet">
    ///     <item>CVars</item>
    ///     <item>Renderer</item>
    ///     <item>Localisation</item>
    ///     <item>Asset Manager</item>
    /// </list>
    /// </summary>
    /// <param name="appSettings"></param>
    /// <param name="renderer"></param>
    public void Initialise(ApplicationSettings appSettings, DatRenderer? renderer = null) {
        this.appSettings = appSettings;
        this.renderer = renderer ?? new VulkanRenderer();

        Logger.EngineLogger.Info("Initialising engine");

        CVars.instance.Initialise();
        InitialiseSdl();
        ThreadManager.instance.Initialise();
        AssetManager.instance.Initialise();
        this.renderer.Initialise();
        Localisation.Initialise();
    }

    /// <summary>
    /// Initialise SDL
    /// </summary>
    /// <exception cref="SdlException">Thrown if SDL fails to initialise</exception>
    /// <exception cref="DatEngineException">Thrown if the engine is unable to get a window from SDL</exception>
    private unsafe void InitialiseSdl() {
        Logger.EngineLogger.Debug("Initialising SDL");
        if (sdl.Init(Sdl.InitEverything) != 0) {
            throw new SdlException("Failed to initialise SDL");
        }

        var windowFlags = renderer.GetWindowFlags();
        if (EngineCVars.ResizableCvar.value) windowFlags |= (uint) WindowFlags.Resizable;
        switch (EngineCVars.WindowModeCvar.value) {
            case 1:
                windowFlags |= (uint) WindowFlags.Fullscreen;
                break;
            case 2:
                windowFlags |= (uint) WindowFlags.Borderless;
                break;
        }

        window = sdl.CreateWindow(appSettings.name,
            Sdl.WindowposUndefined,
            Sdl.WindowposUndefined,
            EngineCVars.WindowWidthCvar.value,
            EngineCVars.WindowHeightCvar.value,
            windowFlags);

        if (window == null) {
            throw new DatEngineException("Failed to create window");
        }

        Logger.EngineLogger.Debug("Created Window ({}, {})", EngineCVars.WindowWidthCvar.value, EngineCVars.WindowHeightCvar.value);
    }

    /// <summary>
    /// Start the main loop and hand off the application to the engine
    /// </summary>
    /// <exception cref="DatEngineException">Thrown when there is a failure with the engine</exception>
    public void StartLoop() {
        if (appSettings == null) throw new DatEngineException("Engine Loop Started without first initialising");
        Logger.EngineLogger.Info("Starting main loop");

        var lastTime = sdl.GetTicks64();
        while (!shouldClose) {
            var currentTime = sdl.GetTicks64();
            var deltaTime = (currentTime - lastTime) / 1000f;
            
            // SDL.SDL.SDL_PollEvent()
            
            renderer.Draw(deltaTime, currentTime);
            
            lastTime = currentTime;
        }
        
        Logger.EngineLogger.Info("Cleaning up");
        
        renderer.Cleanup();

        unsafe {
            sdl.DestroyWindow(window);
        }

        sdl.Quit();
        Logger.EngineLogger.Debug("Bye!");
    }

    /// <summary>
    /// Tell the engine to shut down gracefully
    /// </summary>
    public void Exit() {
        shouldClose = true;
    }
}