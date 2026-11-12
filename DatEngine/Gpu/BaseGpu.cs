using Brutal.SdlApi;
using DatEngine.Util;

namespace DatEngine.Gpu;

/// <summary>
/// Generic Interface for GPU operations
/// </summary>
public abstract class BaseGpu {
 [CVar("IBufferedFrames", "The Minimum number of frames to use in a swapchain", CVarFlags.Persistent)]
 public static CVar<int> bufferedFrames = 2;
 [CVar("BVsync", "Whether to enable VSync", CVarFlags.Persistent)]
 public static CVar<bool> enableVsync = true;

    /// <summary>
     /// Set up the GPU ready for use
     /// </summary>
     public abstract void Init();

    /// <summary>
     /// Clean up the GPU resources
     /// </summary>
     public abstract void Cleanup();

      /// <summary>
      /// Set up a new window for the GPU to render to.
      /// </summary>
      /// <param name="windowId">The id of the window to register</param>
      /// <param name="sdlWindow">The SDL Window handle to use for setup</param>
     public abstract void RegisterWindow(uint windowId, SdlWindow sdlWindow);
}