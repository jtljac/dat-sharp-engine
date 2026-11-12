using Brutal.Numerics;
using Brutal.VulkanApi;

namespace DatEngine.Gpu.Vulkan;

/// <summary>
/// A helper class for various annoying Vulkan methods
/// </summary>
public sealed class VkHelper {
    public static VkQueueFamilyProperties[] GetQueueFamilyProperties(PhysicalDevice physicalDevice) {
        return GetVulkanArray<VkQueueFamilyProperties>(
            physicalDevice.GetQueueFamilyProperties_Write,
            physicalDevice.GetQueueFamilyProperties_Count
        );
    }

    public static VkSurfaceFormatKHR[] GetSurfaceFormats(PhysicalDevice physicalDevice, VkSurfaceKHR surface) {
        return GetVulkanArray<VkSurfaceFormatKHR>(
            array => physicalDevice.GetSurfaceFormatsKHR_Write(surface, array),
            () => physicalDevice.GetSurfaceFormatsKHR_Count(surface)
        );
    }

    public static VkPresentModeKHR[] GetSurfacePresentModes(PhysicalDevice physicalDevice, VkSurfaceKHR surface) {
        return GetVulkanArray<VkPresentModeKHR>(
            array => physicalDevice.GetSurfacePresentModesKHR_Write(surface, array),
            () => physicalDevice.GetSurfacePresentModesKHR_Count(surface)
        );

    }

    private static T[] GetVulkanArray<T>(Action<ReadOnlySpan<T>> write, Func<int> count) {
        var array = new T[count.Invoke()];
        write.Invoke(array.AsSpan());
        return array;
    }

}