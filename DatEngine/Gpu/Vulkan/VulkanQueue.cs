using Brutal.VulkanApi;

namespace DatEngine.Gpu.Vulkan;

/// <summary>
/// A struct grouping a <see cref="VkQueue"/> and it's queue family index
/// </summary>
public record struct VulkanQueue(int queueIndex, Queue queue);