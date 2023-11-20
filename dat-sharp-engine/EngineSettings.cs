using Silk.NET.SDL;

namespace dat_sharp_engine; 

public class EngineSettings {
    /// <summary>
    /// The width of the game window
    /// </summary>
    public int width = 1280;
    
    /// <summary>
    /// The height of the game window
    /// </summary>
    public int height = 720;
    
    /// <summary>
    /// The display mode of the window
    /// </summary>
    public WindowMode windowMode = WindowMode.Windowed;
    
    /// <summary>
    /// The gpu to use for rendering
    /// </summary>
    public uint? gpu;

    public bool vsync = false;

    /// <summary>
    /// The minimum number of buffered frames in the swapchain
    /// <para/>
    /// Note this is the minimum, the driver may demand more
    /// </summary>
    public uint bufferedFrames = 2;

    public WindowFlags getWindowFlags() {
        switch (windowMode) {
            case WindowMode.Fullscreen:
                return WindowFlags.Fullscreen;
            case WindowMode.Borderless:
                return WindowFlags.Borderless;
            case WindowMode.Windowed:
                return WindowFlags.None;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public enum WindowMode {
    Fullscreen,
    Borderless,
    Windowed
}