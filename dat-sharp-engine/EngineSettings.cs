using Silk.NET.SDL;

namespace dat_sharp_engine; 

public class EngineSettings {
    public int width = 1280;
    public int height = 720;
    public WindowMode windowMode = WindowMode.Windowed;
    public uint? gpu;

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