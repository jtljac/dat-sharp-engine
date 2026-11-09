using Brutal.SdlApi;
using DatEngine.Gpu;

namespace DatEngine.Engine;

/**
 * <summary>A window in the application, with its own input queue and render target</summary>
 */
public class Window {
    private IGpu _gpu;
    private SdlWindow _window;

    public void Init(String title, int width, int height, SdlWindowFlags flags) {

        _window = Sdl.Video.CreateWindow(title, width, height, flags);
        _gpu.RegisterWindow(_window);
    }

    // Size
    // Resizable
    // FullScreen (Or windowed or borderless)
    //
}