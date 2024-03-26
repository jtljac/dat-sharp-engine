using dat_sharp_engine.Rendering;
using dat_sharp_engine.Rendering.Vulkan;
using dat_sharp_engine.Util;
using Silk.NET.SDL;

namespace dat_sharp_engine;

public class DatSharpEngine {
    // CVars
    private static readonly CVar<bool> ResizableCvar = new("bWindowResizable", "Allow resizing the game window", false, CVarCategory.Graphics, CVarFlags.None);
    private static readonly CVar<int> WindowModeCvar = new("eWindowMode", "The window mode, 0 for windowed, 1 for fullscreen, 2 for borderless", 0, CVarCategory.Graphics, CVarFlags.None);
    private static readonly CVar<int> WindowWidthCvar = new("uWindowWidth", "The width of the game window", 1366, CVarCategory.Graphics, CVarFlags.None);
    private static readonly CVar<int> WindowHeightCvar = new("uWindowHeight", "The height of the game window", 768, CVarCategory.Graphics, CVarFlags.None);

    internal readonly Sdl sdl = Sdl.GetApi();
    
    public readonly DatRenderer renderer;
    public readonly ApplicationSettings appSettings;
    public unsafe Window* window;

    public bool ShouldClose { get; set; } = false;

    static DatSharpEngine() { }

    public DatSharpEngine(ApplicationSettings appSettings) {
        renderer = new VulkanRenderer(this);
        this.appSettings = appSettings;
    }

    public void StartLoop() {
        Logger.EngineLogger.Info("Initialising engine");
        Logger.EngineLogger.Debug("Initialising SDL");
        if (sdl.Init(Sdl.InitEverything) != 0) {
            throw new Exception("Failed to initialise SDL");
        }

        // Initialise SDL window
        unsafe {
            var windowFlags = renderer.GetWindowFlags();
            if (ResizableCvar.value) windowFlags |= (uint) WindowFlags.Resizable;
            switch (WindowModeCvar.value) {
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
                WindowWidthCvar.value,
                WindowHeightCvar.value,
                windowFlags);

            if (window == null) {
                throw new Exception("Failed to create window");
            }
        }
        Logger.EngineLogger.Debug("Created Window ({}, {})", WindowWidthCvar.value, WindowHeightCvar.value);

        Logger.EngineLogger.Info("Initialising Renderer");
        renderer.Initialise();
        
        Logger.EngineLogger.Info("Initialising finished, starting main loop");
        var lastTime = sdl.GetTicks() - 16;
        while (!ShouldClose) {
            var currentTime = sdl.GetTicks();
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
}