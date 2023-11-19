using SDL;

namespace dat_sharp_engine.Rendering; 

public class EngineSettings {
    public int width = 1280;
    public int height = 720;
    public WindowMode windowMode = WindowMode.Windowed;

    public SDL_WindowFlags getWindowFlags() {
        switch (windowMode) {
            case WindowMode.Fullscreen:
                return SDL_WindowFlags.Fullscreen;
            case WindowMode.Borderless:
                return SDL_WindowFlags.Borderless;
            case WindowMode.Windowed:
                return SDL_WindowFlags.None;;
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