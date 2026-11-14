using Brutal.VulkanApi;

namespace DatEngine.Gpu.Vulkan;
/// <summary>
/// A collection of parameters related to a single window
/// </summary>
public class VulkanWindow(uint windowHandle) {
    public uint windowHandle = windowHandle;

    // Surface
    public VkSurfaceKHR surface;

    // Swapchain
    public VkSwapchainKHR swapchain;
    public VkFormat swapchainFormat;
    public VkExtent2D swapchainExtent;

    public SwapchainImages[] swapchainImages = null!;

    // Render target

    // Shader graph

    public void Cleanup(Instance instance, Device device) {
        // Destroy our swapchain images
        foreach (var swapchainImage in swapchainImages) {
            device.DestroyImageView(swapchainImage.imageView, null);
            // The swapchain owns the images, so we don't destroy them here
            device.DestroySemaphore(swapchainImage.renderSemaphore, null);
        }

        // Destroying the swapchain gets the images as well
        device.DestroySwapchainKHR(swapchain, null);

        instance.DestroySurfaceKHR(surface, null);
    }
}

public class SwapchainImages(VkImage image, VkImageView imageView, VkSemaphore renderSemaphore) {
    /// <summary>
    /// An image in the swapchain
    /// </summary>
    public VkImage image = image;
    /// <summary>
    /// The imageview representing the <see cref="image"/>
    /// </summary>
    public VkImageView imageView = imageView;

    /// <summary>
    /// A semaphore tracking once rendering has finished and the swapchain image is ready to present
    /// </summary>
    public VkSemaphore renderSemaphore = renderSemaphore;
}