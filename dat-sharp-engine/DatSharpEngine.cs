using dat_sharp_engine.AssetManagement;
using dat_sharp_engine.Rendering;
using dat_sharp_engine.Rendering.Vulkan;
using dat_sharp_engine.Threading;
using dat_sharp_engine.Util;
using Silk.NET.SDL;
using Thread = System.Threading.Thread;

namespace dat_sharp_engine;

public class DatSharpEngine {
    // CVars
    private static readonly CVar<bool> ResizableCvar = new("bWindowResizable", "Allow resizing the game window", false, CVarCategory.Graphics, CVarFlags.None);
    private static readonly CVar<int> WindowModeCvar = new("eWindowMode", "The window mode, 0 for windowed, 1 for fullscreen, 2 for borderless", 0, CVarCategory.Graphics, CVarFlags.None);
    private static readonly CVar<int> WindowWidthCvar = new("uWindowWidth", "The width of the game window", 1366, CVarCategory.Graphics, CVarFlags.None);
    private static readonly CVar<int> WindowHeightCvar = new("uWindowHeight", "The height of the game window", 768, CVarCategory.Graphics, CVarFlags.None);

    internal readonly Sdl sdl = Sdl.GetApi();

    public static DatSharpEngine Instance { get; } = new();
    public DatRenderer? renderer { get; private set; }
    public ApplicationSettings? appSettings { get; private set; }
    public unsafe Window* window;

    public bool ShouldClose { get; set; } = false;

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

    private unsafe void InitialiseSdl() {
        Logger.EngineLogger.Debug("Initialising SDL");
        if (sdl.Init(Sdl.InitEverything) != 0) {
            throw new Exception("Failed to initialise SDL");
        }

        var windowFlags = renderer!.GetWindowFlags();
        if (ResizableCvar.value) windowFlags |= (uint) WindowFlags.Resizable;
        switch (WindowModeCvar.value) {
            case 1:
                windowFlags |= (uint) WindowFlags.Fullscreen;
                break;
            case 2:
                windowFlags |= (uint) WindowFlags.Borderless;
                break;
        }

        window = sdl.CreateWindow(appSettings!.name,
            Sdl.WindowposUndefined,
            Sdl.WindowposUndefined,
            WindowWidthCvar.value,
            WindowHeightCvar.value,
            windowFlags);

        if (window == null) {
            throw new DatEngineException("Failed to create window");
        }

        Logger.EngineLogger.Debug("Created Window ({}, {})", WindowWidthCvar.value, WindowHeightCvar.value);
    }

    public void StartLoop() {
        if (appSettings == null) throw new DatEngineException("Engine Loop Started without setting up");
        Logger.EngineLogger.Info("Starting main loop");

        var lastTime = sdl.GetTicks64();
        while (!ShouldClose) {
            var currentTime = sdl.GetTicks64();
            var deltaTime = (currentTime - lastTime) / 1000f;
            
            // SDL.SDL.SDL_PollEvent()
            
            renderer!.Draw(deltaTime, currentTime);
            
            lastTime = currentTime;
        }
        
        Logger.EngineLogger.Info("Cleaning up");
        
        renderer!.Cleanup();

        unsafe {
            sdl.DestroyWindow(window);
        }

        sdl.Quit();
        Logger.EngineLogger.Debug("Bye!");
    }
}