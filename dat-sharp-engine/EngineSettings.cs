using Silk.NET.SDL;

namespace dat_sharp_engine;

public class EngineSettings {
    /// <summary>
    /// The width of the game window
    /// </summary>
    public uint width = 1280;

    /// <summary>
    /// The height of the game window
    /// </summary>
    public uint height = 720;

    /// <summary>
    /// The display mode of the window
    /// </summary>
    public WindowMode windowMode = WindowMode.Windowed;

    /// <summary>
    /// The gpu to use for rendering
    /// </summary>
    public uint? gpu;

    /// <summary>
    /// Whether VSync is enabled
    /// </summary>
    public bool vsync = true;

    /// <summary>
    /// The number of images to use for rendering too.
    /// <para/>
    /// Values > 1 can eliminate tearing as the renderer will be able to render to a different frame than the one being
    /// presented.
    /// </summary>
    public uint bufferedFrames = 2;

    /// <summary>
    /// Whether debug mode is enabled
    /// <para/>
    /// This enables
    /// </summary>
    public bool debug = true;

    public WindowFlags getWindowFlags() {
        return windowMode switch {
            WindowMode.Fullscreen => WindowFlags.Fullscreen,
            WindowMode.Borderless => WindowFlags.Borderless,
            WindowMode.Windowed => WindowFlags.None,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public enum WindowMode {
    Fullscreen,
    Borderless,
    Windowed
}
