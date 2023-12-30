using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace dat_sharp_engine.Rendering.Vulkan;

/// <summary>
/// A class containing resources used every frame.
/// </summary>
///
/// <remarks>
/// This is a class instead of a struct so it will always be passed around as a reference,
/// </remarks>
public class FrameData {
    public CommandPool commandPool;
    public CommandBuffer commandBuffer;

    public Semaphore swapchainSemaphore, renderSemaphore;
    public Fence renderFence;
}

/// <summary>
/// A class containing resources for each swapchain image.
/// </summary>
///
/// <remarks>
/// This is a class instead of a struct so it will always be passed around as a reference,
/// </remarks>
public class SwapchainData {
    public Image image;
    public ImageView imageView;
}
