using Brutal.SdlApi;

namespace DatEngine.Gpu;

/// <summary>
/// Generic Interface for GPU operations
/// </summary>
public abstract class BaseGpu {
     /// <summary>
     /// Set up the GPU ready for use
     /// </summary>
     public abstract void Init(SdlWindow primaryWindow);

     /// <summary>
     /// Clean up the GPU resources
     /// </summary>
     public abstract void Cleanup();

      /// <summary>
      /// Set up a new window for the GPU to render to.
      /// </summary>
     public abstract void RegisterWindow(SdlWindow window);
}