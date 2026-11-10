using Brutal.Numerics;
using Brutal.SdlApi;
using DatEngine.Gpu;
using DatEngine.Util;

namespace DatEngine.Engine;

/**
 * <summary>A window in the application, with its own input queue and render target</summary>
 */
public class Window {
    [CVar("VPrimaryWindowSize", "The window size for the primary window", CVarFlags.Persistent)]
    public static CVar<int2> primaryWindowSize = new(new int2(1920, 1080));

    private BaseGpu _Gpu;
    private SdlWindow _window;

    public Window(String title, int width, int height, SdlWindowFlags flags) {
        _window = Sdl.Video.CreateWindow(title, width, height, flags);
        _Gpu.RegisterWindow(_window);
    }



    // Size
    // Resizable
    // FullScreen (Or windowed or borderless)
    //
}