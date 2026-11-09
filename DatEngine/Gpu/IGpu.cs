using Brutal.SdlApi;

namespace DatEngine.Gpu;

/// <summary>
/// Generic Interface for GPU operations
/// </summary>
public interface IGpu {
     /// <summary>
     /// Set up the GPU ready for use
     /// </summary>
     public void Init(SdlWindow primaryWindow);

     /// <summary>
     /// Clean up the GPU resources
     /// </summary>
     public void Cleanup();

      /// <summary>
      /// Set up a new window for the GPU to render to.
      /// </summary>
     public void RegisterWindow(SdlWindow window);
}