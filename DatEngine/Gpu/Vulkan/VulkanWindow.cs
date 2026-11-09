using Brutal.VulkanApi;

namespace DatEngine.Gpu.Vulkan;
/// <summary>
/// A collection of parameters related to a single window
/// </summary>
public class VulkanWindow(VkSurfaceKHR surface) {
    // Surface
    public readonly VkSurfaceKHR surface = surface;

    // Swapchain
    public VkSwapchainKHR swapchain;
    public VkFormat swapchainFormat;
    public VkExtent2D swapchainExtent;

    // Render target

    // Shader graph
}