using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using dat_sharp_engine.Util;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace dat_sharp_engine.Rendering.Vulkan;

/// <summary>
/// A class containing methods for simplifying some annoying vulkan calls
/// </summary>
public static class VkHelper {

    /* --------------------------------------- */
    /* Validation Layers                       */
    /* --------------------------------------- */

    /// <summary>
    /// Get a set containing the available validation layers on the system
    /// </summary>
    /// <param name="vk">An instance of the Vk API to use for querying the driver for the properties</param>
    /// <returns>A set </returns>
    public static unsafe ISet<string> GetAvailableValidationLayers(Vk vk) {
        return GetVulkanList(
            (ref uint count, LayerProperties* list) =>
                vk.EnumerateInstanceLayerProperties(ref count, list),
            array =>
                array.Select(prop => {
                    // ReSharper disable once ConvertToLambdaExpression
                    return Marshal.PtrToStringUTF8((IntPtr) prop.LayerName);
                }).ToHashSet()
        )!;
    }

    /* --------------------------------------- */
    /* Surface                                 */
    /* --------------------------------------- */

    /// <summary>
    /// Get the available surface formats for the given surface and device
    /// </summary>
    /// <param name="khrSurface">An instance of the KHRSurface API to use for querying the driver</param>
    /// <param name="physicalDevice">The Physical device to query</param>
    /// <param name="surface">The surface being queried for</param>
    /// <returns>The available surface formats</returns>
    public static unsafe List<SurfaceFormatKHR> GetDeviceSurfaceFormats(KhrSurface khrSurface,
        PhysicalDevice physicalDevice,
        SurfaceKHR surface) {
        return GetVulkanList(
            (ref uint count, SurfaceFormatKHR* list) =>
                khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref count, list)
        );
    }

    /// <summary>
    /// Get The available present modes for the given device and surface
    /// </summary>
    /// <param name="khrSurface">An instance of the KHRSurface API to use for querying the driver</param>
    /// <param name="physicalDevice">The Physical device to query</param>
    /// <param name="surface">The surface being queried for</param>
    /// <returns>The available present modes</returns>
    public static unsafe List<PresentModeKHR> GetDeviceSurfacePresentModes(KhrSurface khrSurface,
        PhysicalDevice physicalDevice,
        SurfaceKHR surface) {
        return GetVulkanList((ref uint count, PresentModeKHR* list) =>
            khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref count, list)
        );
    }

    /* --------------------------------------- */
    /* Swapchain                               */
    /* --------------------------------------- */

    /// <summary>
    /// Get the swapchain images for the given swapchain
    /// </summary>
    /// <param name="khrSwapchain">An instance of the KHRSwapchain APU to use for querying the driver</param>
    /// <param name="device">The device to query</param>
    /// <param name="swapchain">The swapchain that owns the images</param>
    /// <returns>A list of swapchain images</returns>
    public static unsafe List<Image> GetSwapchainImages(KhrSwapchain khrSwapchain,
        Device device,
        SwapchainKHR swapchain) {
        return GetVulkanList((ref uint count, Image* list) =>
            khrSwapchain.GetSwapchainImages(device, swapchain, ref count, list)
        );
    }

    /* --------------------------------------- */
    /* Queues                                  */
    /* --------------------------------------- */

    /// <summary>
    /// Get a list of the Queue Family Properties
    /// </summary>
    /// <param name="vk">An instance of the Vk API to use for querying the driver for the properties</param>
    /// <param name="physicalDevice">The physical device to get the Queue Family Properties from</param>
    /// <returns>A list of the Queue Family Properties for the given device</returns>
    public static unsafe List<QueueFamilyProperties> GetQueueFamilyProperties(Vk vk, PhysicalDevice physicalDevice) {
        return GetVulkanList(method: (ref uint count, QueueFamilyProperties* list) => {
                vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref count, list);
                return Result.Success;
            }
        );
    }

    /// <summary>
    /// Get a string description of the given queue
    /// </summary>
    /// <param name="queueIndex">The index of the queue</param>
    /// <param name="properties">The properties of the queue</param>
    /// <returns>A string description of the queue</returns>
    public static string GetQueueDescription(int queueIndex, in QueueFamilyProperties properties) {
        var message =
            new StringBuilder().Append($"Index: {queueIndex}").Append($" | Count: {properties.QueueCount}");

        if (properties.QueueFlags.HasFlag(QueueFlags.GraphicsBit)) message.Append(" | Graphics");
        if (properties.QueueFlags.HasFlag(QueueFlags.ComputeBit)) message.Append(" | Compute");
        if (properties.QueueFlags.HasFlag(QueueFlags.TransferBit)) message.Append(" | Transfer");
        if (properties.QueueFlags.HasFlag(QueueFlags.ProtectedBit)) message.Append(" | Protected");
        if (properties.QueueFlags.HasFlag(QueueFlags.SparseBindingBit)) message.Append(" | Sparse Binding");
        return message.ToString();
    }

    /// <summary>
    /// Log the definitions of each queue on the given device
    /// </summary>
    /// <param name="vk">The instance of the Vk API</param>
    /// <param name="physicalDevice">The device to get the queues of</param>
    public static void PrintQueues(Vk vk, PhysicalDevice physicalDevice) {
        var queueFamilies = GetQueueFamilyProperties(vk, physicalDevice);

        Logger.EngineLogger.Info("Queues: ");
        for (var i = 0; i < queueFamilies.Count; i++) {
            Logger.EngineLogger.Info(GetQueueDescription(i, queueFamilies[i]));
        }
    }

    /* --------------------------------------- */
    /* Image                                   */
    /* --------------------------------------- */

    /// <summary>
    /// Add an image transition to the given command buffer
    /// </summary>
    ///
    /// <param name="vk">The instance of the Vk API</param>
    /// <param name="commandBuffer">The command buffer to add the image transition to</param>
    /// <param name="image">The image to transition</param>
    /// <param name="currentLayout">The current layout of the image</param>
    /// <param name="newLayout">The layout to transition the image to</param>
    /// <param name="srcStage">The stage to wait on for the transition</param>
    /// <param name="destStage">The stage to execute the transition on</param>
    /// <param name="srcAccessMask"></param>
    /// <param name="destAccessMask"></param>
    public static unsafe void TransitionImage(Vk vk,
        CommandBuffer commandBuffer,
        Image image,
        ImageLayout currentLayout,
        ImageLayout newLayout,
        PipelineStageFlags2 srcStage = PipelineStageFlags2.AllCommandsBit,
        PipelineStageFlags2 destStage = PipelineStageFlags2.AllCommandsBit,
        AccessFlags2 srcAccessMask = AccessFlags2.MemoryWriteBit,
        AccessFlags2 destAccessMask = AccessFlags2.MemoryWriteBit | AccessFlags2.MemoryReadBit) {
        var memoryBarrier2 = new ImageMemoryBarrier2 {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = srcStage,
            SrcAccessMask = srcAccessMask,
            DstStageMask = destStage,
            DstAccessMask = destAccessMask,

            OldLayout = currentLayout,
            NewLayout = newLayout,

            SubresourceRange = VkShortcuts.CreateImageSubresourceRange(newLayout == ImageLayout.DepthAttachmentOptimal
                ? ImageAspectFlags.DepthBit
                : ImageAspectFlags.ColorBit),

            Image = image
        };

        var dependencyInfo = new DependencyInfo {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &memoryBarrier2
        };

        vk.CmdPipelineBarrier2(commandBuffer, dependencyInfo);
    }

    public static unsafe void TransitionImages(Vk vk,
        CommandBuffer commandBuffer,
        ref ImageMemoryBarrier2[] imageMemoryBarriers) {

        fixed (ImageMemoryBarrier2* memoryBarriers = imageMemoryBarriers) {
            var dependencyInfo = new DependencyInfo {
                SType = StructureType.DependencyInfo,
                ImageMemoryBarrierCount = (uint) imageMemoryBarriers.Length,
                PImageMemoryBarriers = memoryBarriers
            };

            vk.CmdPipelineBarrier2(commandBuffer, dependencyInfo);
        }
    }

    /* --------------------------------------- */
    /* Util                                    */
    /* --------------------------------------- */

    /// <summary>
    /// A delegate representing a vulkan enumerate method
    /// </summary>
    /// <typeparam name="T">The Value being enumerated by the vulkan method</typeparam>
    private unsafe delegate Result VulkanListFunction<T>(ref uint count, T* pList) where T : unmanaged;

    /// <summary>
    /// Get the contents of a vulkan enumerate method
    /// </summary>
    /// <param name="method">A wrapper for the Vulkan method that enumerates</param>
    /// <param name="resultTransformer">A method that transforms the resulting list into a preferred format</param>
    /// <typeparam name="TParam">The type of the Vulkan object being enumerated</typeparam>
    /// <typeparam name="TReturn">The return type</typeparam>
    /// <returns>The result of the Vulkan enumerate method in the transformed format</returns>
    private static unsafe TReturn GetVulkanList<TParam, TReturn>(VulkanListFunction<TParam> method, Func<TParam[], TReturn> resultTransformer) where TReturn : IEnumerable where TParam : unmanaged {
        uint count = 0;
        method.Invoke(ref count, null);
        var list = new TParam[count];
        fixed (TParam* pList = list) {
            method.Invoke(ref count, pList);
        }

        return resultTransformer.Invoke(list);
    }

    /// <summary>
    /// Get the contents of a vulkan enumerate method as a List
    /// </summary>
    /// <param name="method">A wrapper for the Vulkan method that enumerates</param>
    /// <typeparam name="T">The type of the Vulkan object being enumerated</typeparam>
    /// <returns>The result of the Vulkan enumerate method in a list</returns>
    private static List<T> GetVulkanList<T>(VulkanListFunction<T> method) where T : unmanaged {
        return GetVulkanList(method, arg => new List<T>(arg));
    }
}
