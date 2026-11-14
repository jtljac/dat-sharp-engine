using Brutal.Numerics;
using Brutal.VulkanApi;

namespace DatEngine.Gpu.Vulkan;

/// <summary>
/// A helper class for various annoying Vulkan methods
/// </summary>
public sealed class VkHelper {

    /// <summary>
    /// Get the Queue Family Properties array from the <paramref name="physicalDevice"/>
    /// </summary>
    /// <param name="physicalDevice">The physical device to get the Queue Family properties from</param>
    /// <returns>An array of Vulkan Queue Family Properties</returns>
    public static VkQueueFamilyProperties2[] GetQueueFamilyProperties(PhysicalDevice physicalDevice) {
        return GetVulkanArray<VkQueueFamilyProperties2>(
            physicalDevice.GetQueueFamilyProperties2_Write,
            physicalDevice.GetQueueFamilyProperties2_Count
        );
    }

    public static VkSurfaceCapabilities2KHR GetSurfaceCapabilities(PhysicalDevice physicalDevice, VkSurfaceKHR surface) {
        return physicalDevice.GetSurfaceCapabilities2KHR(new VkPhysicalDeviceSurfaceInfo2KHR {
            Surface = surface
        });
    }

    /// <summary>
    /// Get the available Surface Formats for the given <paramref name="surface"/>
    /// </summary>
    /// <param name="physicalDevice">The physical device that owns the surface</param>
    /// <param name="surface">The surface to get the formats of</param>
    /// <returns>An array of Vulkan Surface Formats</returns>
    public static VkSurfaceFormat2KHR[] GetSurfaceFormats(PhysicalDevice physicalDevice, VkSurfaceKHR surface) {
        var surfaceInfo = new VkPhysicalDeviceSurfaceInfo2KHR {
            Surface = surface
        };

        return GetVulkanArray<VkSurfaceFormat2KHR>(
            array => physicalDevice.GetSurfaceFormats2KHR_Write(surfaceInfo, array),
            () => physicalDevice.GetSurfaceFormats2KHR_Count(surfaceInfo)
        );
    }

    /// <summary>
    /// Get the available present modes for the given <paramref name="surface"/>
    /// </summary>
    /// <param name="physicalDevice">The physical device that owns the surface</param>
    /// <param name="surface">The surface to get the formats of</param>
    /// <returns>An array of Vulkan Surface Present Modes</returns>
    public static VkPresentModeKHR[] GetSurfacePresentModes(PhysicalDevice physicalDevice, VkSurfaceKHR surface) {
        return GetVulkanArray<VkPresentModeKHR>(
            array => physicalDevice.GetSurfacePresentModesKHR_Write(surface, array),
            () => physicalDevice.GetSurfacePresentModesKHR_Count(surface)
        );
    }

    public static VkImage[] GetSwapchainImages(Device device, VkSwapchainKHR swapchain) {
        return GetVulkanArray<VkImage>(
            array => device.GetSwapchainImagesKHR_Write(swapchain, array),
            () => device.GetSwapchainImagesKHR_Count(swapchain)
        );
    }


    /// <summary>
    /// A utility method to handle the weird Count Write dance we have to do with the getting arrays from the Vulkan
    /// Driver.
    /// <para>
    /// On many occasions, you can just pass the method reference, sometimes you will need to pass a lamba that handles
    /// the parameters though
    /// </para>
    /// </summary>
    /// <param name="write">A method reference that populates the array</param>
    /// <param name="count">A method reference that gets the number of elements in the array</param>
    /// <typeparam name="T">The type of the elements in the array</typeparam>
    /// <returns>An array of the given Vulkan Type</returns>
    private static T[] GetVulkanArray<T>(Action<ReadOnlySpan<T>> write, Func<int> count) where T : new() {
        var array = new T[count.Invoke()];
        // If we're passing a create info, this gets very broken without initialising each element
        Array.Fill(array, new T());
        write.Invoke(array.AsSpan());
        return array;
    }

}