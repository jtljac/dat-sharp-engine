using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace dat_sharp_engine.Rendering.Vulkan;

/// <summary>
/// A class for quickly building some annoying Vulkan info structures
/// </summary>
public static class VkShortcuts {

    /* --------------------------------------- */
    /* Command Structures                      */
    /* --------------------------------------- */

    /// <summary>
    /// Create a CommandBufferAllocateInfo
    /// </summary>
    ///
    /// <seealso href="https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkCommandBufferAllocateInfo.html">
    /// Vulkan Docs
    /// </seealso>
    ///
    /// <param name="commandPool">The command pool this buffer is allocated for</param>
    /// <param name="count">The number of buffers to allocate</param>
    /// <returns>A populated CommandBufferAllocatedInfo</returns>
    public static CommandBufferAllocateInfo CreateCommandBufferAllocateInfo(CommandPool commandPool, uint count) {
        return new CommandBufferAllocateInfo {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = commandPool,
            CommandBufferCount = count,
            Level = CommandBufferLevel.Primary
        };
    }

    /// <summary>
    /// Create a CommandBufferBeginInfo
    /// </summary>
    ///
    /// <seealso href="https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkCommandBufferBeginInfo.html">
    /// Vulkan Docs
    /// </seealso>
    ///
    /// <param name="flags">The flags for the command buffer</param>
    /// <returns>A populated CommandBufferBeginInfo</returns>
    public static CommandBufferBeginInfo CreateCommandBufferBeginInfo(
        CommandBufferUsageFlags flags = CommandBufferUsageFlags.None) {
        return new CommandBufferBeginInfo {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = flags
        };
    }

    /// <summary>
    /// Create a CommandBufferSubmitInfo
    /// </summary>
    ///
    /// <seealso href="https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkCommandBufferSubmitInfo.html">
    /// Vulkan Docs
    /// </seealso>
    ///
    /// <param name="commandBuffer">The command buffer being submitted</param>
    /// <returns>A populated CommandBufferSubmitInfo</returns>
    public static CommandBufferSubmitInfo CreateCommandBufferSubmitInfo(CommandBuffer commandBuffer) {
        return new CommandBufferSubmitInfo {
            SType = StructureType.CommandBufferSubmitInfo,
            CommandBuffer = commandBuffer,
            DeviceMask = 0
        };
    }

    /* --------------------------------------- */
    /* Synchronisation Structures              */
    /* --------------------------------------- */

    /// <summary>
    /// Create a FenceCreateInfo
    /// </summary>
    ///
    /// <seealso href="https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkFenceCreateInfo.html">
    /// Vulkan Docs
    /// </seealso>
    ///
    /// <param name="flags">The flags for the fence</param>
    /// <returns>A populated FenceCreateInfo</returns>
    public static FenceCreateInfo CreateFenceCreateInfo(FenceCreateFlags flags = FenceCreateFlags.None) {
        return new FenceCreateInfo {
            SType = StructureType.FenceCreateInfo,
            Flags = flags
        };
    }

    /// <summary>
    /// Create a SemaphoreCreateInfo
    /// </summary>
    ///
    /// <seealso href="https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkSemaphoreCreateInfo.html">
    /// Vulkan Docs
    /// </seealso>
    ///
    /// <param name="flags">The flags for the semaphore</param>
    /// <returns>A populated SemaphoreCreateInfo</returns>
    public static SemaphoreCreateInfo CreateSemaphoreCreateInfo(SemaphoreCreateFlags flags = SemaphoreCreateFlags.None) {
        return new SemaphoreCreateInfo {
            SType = StructureType.SemaphoreCreateInfo,
            Flags = flags
        };
    }

    /// <summary>
    /// Create a SemaphoreSubmitInfo
    /// </summary>
    ///
    /// <seealso href="https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkSemaphoreSubmitInfo.html">
    /// Vulkan Docs
    /// </seealso>
    ///
    /// <param name="stageMask">The stage mask to limit the synchronisation scope</param>
    /// <param name="semaphore">The semaphore to submit</param>
    /// <returns>A populated SemaphoreSubmitInfo</returns>
    public static SemaphoreSubmitInfo CreateSemaphoreSubmitInfo(PipelineStageFlags2 stageMask, Semaphore semaphore) {
        return new SemaphoreSubmitInfo {
            SType = StructureType.SemaphoreSubmitInfo,
            Semaphore = semaphore,
            StageMask = stageMask,
            DeviceIndex = 0,
            Value = 1
        };
    }

    /* --------------------------------------- */
    /* Queue                                   */
    /* --------------------------------------- */

    /// <summary>
    /// Create a SubmitInfo2
    /// </summary>
    /// <param name="cmdSubmitInfo">A pointer to a CommandBufferSubmitInfo</param>
    /// <param name="signalSemaphoreInfo">A pointer to a SignalSemaphoreInfo</param>
    /// <param name="waitSemaphoreInfo">A pointer to a WaitSemaphoreInfo</param>
    /// <returns>A populated SubmitInfo2</returns>
    public static unsafe SubmitInfo2 CreateSubmitInfo(CommandBufferSubmitInfo* cmdSubmitInfo,
        SemaphoreSubmitInfo* signalSemaphoreInfo,
        SemaphoreSubmitInfo* waitSemaphoreInfo) {
        return new SubmitInfo2 {
            SType = StructureType.SubmitInfo2,

            WaitSemaphoreInfoCount = waitSemaphoreInfo == null ? 0u : 1u,
            PWaitSemaphoreInfos = waitSemaphoreInfo,

            SignalSemaphoreInfoCount = signalSemaphoreInfo == null ? 0u : 1u,
            PSignalSemaphoreInfos = signalSemaphoreInfo,

            CommandBufferInfoCount = 1,
            PCommandBufferInfos = cmdSubmitInfo
        };
    }
    
    /* --------------------------------------- */
    /* Rendering                               */
    /* --------------------------------------- */
    /// <summary>
    /// Create a RenderingAttachmentInfo
    /// </summary>
    /// <param name="imageView">The image view being rendered to</param>
    /// <param name="clearValue">The clearvalue, or null if the imageview isn't cleared</param>
    /// <param name="imageLayout">The layout of the image</param>
    /// <returns>The populated RenderingAttachmentInfo</returns>
    public static RenderingAttachmentInfo CreateRenderingAttachmentInfo(ImageView imageView,
        ClearValue? clearValue, ImageLayout imageLayout = ImageLayout.ColorAttachmentOptimal) {
        var renderingAttachmentInfo = new RenderingAttachmentInfo {
            SType = StructureType.RenderingAttachmentInfo,

            ImageView = imageView,
            ImageLayout = imageLayout,

            LoadOp = clearValue.HasValue ? AttachmentLoadOp.Clear : AttachmentLoadOp.Load,
            StoreOp = AttachmentStoreOp.Store,
        };

        if (clearValue.HasValue) {
            renderingAttachmentInfo.ClearValue = clearValue.Value;
        }
        return renderingAttachmentInfo;
    }


    public static unsafe RenderingInfo CreateRenderingInfo(Extent2D drawExtent, RenderingAttachmentInfo* colorAttachment, RenderingAttachmentInfo* depth) {
        return new RenderingInfo {
            SType = StructureType.RenderingInfo,

            RenderArea = new Rect2D(null, drawExtent),
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = colorAttachment,
            PDepthAttachment = depth
        };
    }

    /* --------------------------------------- */
    /* Image                                   */
    /* --------------------------------------- */

    /// <summary>
    /// Create an ImageCreateInfo
    /// </summary>
    /// <param name="format">The format of the image</param>
    /// <param name="usageFlags">The usage flags for the image</param>
    /// <param name="extent">The 3d extent of the image</param>
    /// <returns>A populated ImageCreateInfo</returns>
    public static ImageCreateInfo CreateImageCreateInfo(Format format, ImageUsageFlags usageFlags, Extent3D extent) {
        return new ImageCreateInfo {
            SType = StructureType.ImageCreateInfo,
            
            ImageType = ImageType.Type2D,
            
            Format = format,
            Extent = extent,

            MipLevels = 1,
            ArrayLayers = 1,

            Samples = SampleCountFlags.Count1Bit,

            Tiling = ImageTiling.Optimal,
            Usage = usageFlags
        };
    }

    /// <summary>
    /// Create an ImageViewCreateInfo
    /// </summary>
    /// <param name="format">The format of the image</param>
    /// <param name="image">The image the imageview is for</param>
    /// <param name="aspectFlags">The aspect flags of the image</param>
    /// <returns></returns>
    public static ImageViewCreateInfo CreateImageViewCreateInfo(Format format, Image image, ImageAspectFlags aspectFlags) {
        return new ImageViewCreateInfo {
            SType = StructureType.ImageViewCreateInfo,

            ViewType = ImageViewType.Type2D,
            Image = image,
            Format = format,

            SubresourceRange = new ImageSubresourceRange {
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
                AspectMask = aspectFlags
            }
        };
    }
    
    /* --------------------------------------- */
    /* Misc                                    */
    /* --------------------------------------- */

    /// <summary>
    /// Create a simple ImageSubresourceRange covering all Mip Levels
    /// </summary>
    ///
    /// <seealso href="https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkImageSubresourceRange.html">
    /// Vulkan Docs
    /// </seealso>
    ///
    /// <param name="aspectMask">The aspect mask for the ImageSubresourceRange</param>
    /// <returns>A populated ImageSubresourceRange</returns>
    public static ImageSubresourceRange CreateImageSubresourceRange(ImageAspectFlags aspectMask) {
        return new ImageSubresourceRange {
            AspectMask = aspectMask,
            BaseMipLevel = 0,
            LevelCount = Vk.RemainingMipLevels,
            BaseArrayLayer = 0,
            LayerCount = Vk.RemainingArrayLayers
        };
    }
}
