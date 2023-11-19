﻿using System.Reflection.Metadata;
using dat_sharp_engine.Rendering;
using dat_sharp_engine.Rendering.Vulkan;
using dat_sharp_engine.Util;
using Silk.NET.SDL;

namespace dat_sharp_engine;

public class DatSharpEngine {
    internal readonly Sdl _sdl = Sdl.GetApi();
    
    public readonly DatRenderer renderer;
    public readonly EngineSettings engineSettings = new EngineSettings();
    public readonly ApplicationSettings appSettings;
    public unsafe Window* window;

    public bool shouldClose { get; set; } = false;

    public DatSharpEngine(ApplicationSettings appSettings) {
        renderer = new VulkanRenderer(this);
        this.appSettings = appSettings;
    }

    public void StartLoop() {
        Logger.EngineLogger.Info("Initialising engine");
        if (_sdl.Init(Sdl.InitEverything) != 0) {
            throw new Exception("Failed to initialise SDL");
        }
        Logger.EngineLogger.Info("Initialised SDL");
        unsafe {
            window = _sdl.CreateWindow(appSettings.name,
                Sdl.WindowposUndefined,
                Sdl.WindowposUndefined,
                engineSettings.width,
                engineSettings.height,
                (uint)(engineSettings.getWindowFlags() | renderer.GetWindowFlags()));

            if (window == null) {
                throw new Exception("Failed to create window");
            }
        }

        Logger.EngineLogger.Info("Created Window ({}, {})", engineSettings.width, engineSettings.height);

        renderer.Initialise();
        Logger.EngineLogger.Info("Initialised Renderer");
        
        Logger.EngineLogger.Info("Initising finished, starting main loop");

        var lastTime = _sdl.GetTicks() - 16;
        while (!shouldClose) {
            var currentTime = _sdl.GetTicks();
            var deltaTime = (currentTime - lastTime) / 1000f;
            
            // SDL.SDL.SDL_PollEvent()
            
            renderer.Draw(deltaTime, currentTime);
            
            lastTime = currentTime;
        }
        
        Logger.EngineLogger.Info("Cleaning up");
        
        renderer.Cleanup();

        unsafe {
            _sdl.DestroyWindow(window);
        }

        _sdl.Quit();
        Logger.EngineLogger.Info("Bye!");
    }
}