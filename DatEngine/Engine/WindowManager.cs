using Brutal.Numerics;
using Brutal.SdlApi;
using DatEngine.Gpu;
using DatEngine.Util;

namespace DatEngine.Engine;

using WindowId = uint;

/// <summary>
/// Service for managing windows. Handles GPU surfaces, input queues
/// </summary>
/// <param name="gpu">The GPU service for creating new window surfaces</param>
public class WindowManager(BaseGpu gpu) {
    [CVar("VPrimaryWindowSize", "The window size for the primary window", CVarFlags.Persistent)]
    public static CVar<int2> primaryWindowSize = new int2(1920, 1080);

    [CVar("EPrimaryFullscreenMode", "The fullscreen mode for the primary window", CVarFlags.Persistent)]
    public static CVar<FullscreenMode> primaryFullscreenMode = FullscreenMode.WindowedBorderless;

    /// <summary>
    /// The next available ID for a window
    /// <para>
    /// IDs won't be reused, but we store windows as a dictionary, and there's probably more IDs than possible windows
    /// </para>
    /// </summary>
    /// <seealso cref="_windows"/>
    private uint _nextId = 1;

    /// <summary>
    /// A mapping of windowIds to the platform window handle
    /// </summary>
    private readonly Dictionary<WindowId, SdlWindow> _windows = new();

    /// <summary>
    /// A link to the GPU interface, we need to chat so we can create and destroy windows
    /// </summary>
    private BaseGpu _gpu = gpu;
    // Input system

    /// <summary>
    /// Create a new window
    /// <para>
    /// This will handle setting up the window in the GPU and InputManager too
    /// </para>
    /// </summary>
    /// <param name="windowId">The ID the window should use</param>
    /// <param name="config">A record with the information required to create the window</param>
    /// <exception cref="DatEngineException">The window with the provided ID already exists</exception>
    private void CreateWindow(WindowId windowId, WindowConfig config) {
        if (_windows.ContainsKey(windowId)) {
            throw new DatEngineException("Window already exists");
        }

        var window = Sdl.Video.CreateWindow(config.title, config.size.X, config.size.Y, ExtractFlags(config));

        _windows.Add(windowId, window);

        // Initialise in GPU
        _gpu.RegisterWindow(windowId, window);

        // Initialise in input manager
    }

    /// <summary>
    /// Create a new window
    /// </summary>
    /// <param name="windowConfig">The configuration for the window</param>
    /// <returns>The assigned handle for the window</returns>
    public WindowId CreateWindow(WindowConfig windowConfig) {
        var id = _nextId++;
        CreateWindow(id, windowConfig);

        return id;
    }

    /// <summary>
    /// Create the primary window
    /// </summary>
    /// <returns>The assigned handle for the window</returns>
    /// <exception cref="DatEngineException">The primary window already exists</exception>
    public WindowId CreatePrimaryWindow() {
        var config = new WindowConfig("Game", primaryWindowSize, primaryFullscreenMode);

        CreateWindow(0, config);

        return 0;
    }

    /// <summary>
    /// Extract the <see cref="SdlWindowFlags"/> from the <paramref name="config"/>
    /// </summary>
    /// <param name="config">The config object representing the flags</param>
    /// <returns>An SDL flags object representing the configured options</returns>
    protected SdlWindowFlags ExtractFlags(WindowConfig config) {
        SdlWindowFlags flags = 0;

        flags |= getFullscreenModeFlag(config.fullscreenMode);

        return flags;
    }

    /// <summary>
    /// Get an <see cref="SdlWindowFlags"/> that represents the provided <paramref name="fullscreenMode"/>
    /// </summary>
    /// <param name="fullscreenMode">The fullscreen mode to convert into an SDL window flag</param>
    /// <returns>An SDL flags object representing the fullscreen mode</returns>
    protected SdlWindowFlags getFullscreenModeFlag(FullscreenMode fullscreenMode) {
        return fullscreenMode switch {
            FullscreenMode.Fullscreen => SdlWindowFlags.Fullscreen,
            FullscreenMode.WindowedBorderless => SdlWindowFlags.Borderless,
            FullscreenMode.Windowed => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(fullscreenMode), fullscreenMode, null)
        };
    }
}

/// <summary>
/// The available options for fullscreen/windowed
/// </summary>
public enum FullscreenMode {
    /// <summary>
    /// Run the game in a window, with a task bar
    /// </summary>
    Windowed,
    /// <summary>
    /// Run the game in a window, but with the borders hidden so it looks fullscreen
    /// </summary>
    WindowedBorderless,
    /// <summary>
    /// Run the game in exclusive fullscreen
    /// </summary>
    Fullscreen
}

public record WindowConfig(string title, int2 size, FullscreenMode fullscreenMode = FullscreenMode.WindowedBorderless);