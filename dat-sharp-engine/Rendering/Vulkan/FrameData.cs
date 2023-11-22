using Silk.NET.Vulkan;

namespace dat_sharp_engine.Rendering.Vulkan; 

public struct FrameData {
    public CommandPool _commandPool;
    public CommandBuffer _commandBuffer;

    public Image _swapchainImage;
    public ImageView _swapchainImageViews;
}