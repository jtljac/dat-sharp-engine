using dat_sharp_engine.Rendering.Util;
using Silk.NET.Vulkan;

namespace dat_sharp_engine.Rendering.Vulkan.Descriptor;

/// <summary>
/// A class for abstracting the functionality of a descriptor pool
/// <param name="vk">An instance of the Vk API to use for querying the driver for the properties</param>
/// <param name="device">The device that owns the descriptor pools</param>
/// </summary>
public class DescriptorAllocator(Vk vk, Device device) {
    /// <summary>
    /// A record describing the type of a descriptor set and the ratio of the max sets of the pool that the set takes up
    /// </summary>
    /// <param name="Type">The type of an available descriptor set in the pool</param>
    /// <param name="Ratio">The ratio of the max sets of the pool that the set takes up</param>
    public record PoolSizeRatio(DescriptorType Type, float Ratio);

    /// <summary>
    /// The pool this descriptor allocator allocates to
    /// </summary>
    private DescriptorPool _pool;

    /// <summary>
    /// Initialise the pool
    /// </summary>
    /// <param name="maxSets">The maximum amount of sets this pool can have</param>
    /// <param name="poolRatios">An array of PoolSizeRatios that define the types and the ratio of sets they take up</param>
    public unsafe void InitPool(uint maxSets, PoolSizeRatio[] poolRatios) {
        var poolSizes = new DescriptorPoolSize[poolRatios.Length];

        for (var i = 0; i < poolRatios.Length; i++) {
            var (descriptorType, ratio) = poolRatios[i];

            poolSizes[i] = new DescriptorPoolSize {
                Type = descriptorType,
                DescriptorCount = (uint) (maxSets * ratio)
            };
        }

        fixed (DescriptorPoolSize* pPoolSizes = poolSizes) {
            var poolInfo = new DescriptorPoolCreateInfo {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = 0,
                MaxSets = maxSets,
                PoolSizeCount = (uint) poolSizes.Length,
                PPoolSizes = pPoolSizes
            };

            vk.CreateDescriptorPool(device, &poolInfo, null, out _pool);
        }
    }

    /// <summary>
    /// Clear all the descriptor sets from the pool
    /// </summary>
    public void ClearDescriptors() {
        vk.ResetDescriptorPool(device, _pool, 0);
    }

    /// <summary>
    /// Destroy the pool owned by this allocator
    /// </summary>
    public unsafe void DestroyPool() {
        vk.DestroyDescriptorPool(device, _pool, null);
    }

    /// <summary>
    /// Allocate a descriptor set in the pool owned by this allocator
    /// </summary>
    /// <param name="layout">The layout of the descriptor set</param>
    /// <returns>An allocated descriptor set</returns>
    /// <exception cref="DatRendererException">Thrown when vulkan fails to allocate the set in the pool</exception>
    public unsafe DescriptorSet Allocate(DescriptorSetLayout layout) {
        var allocatorInfo = new DescriptorSetAllocateInfo {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _pool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout
        };

        if (vk.AllocateDescriptorSets(device, allocatorInfo, out var set) != Result.Success) {
            throw new DatRendererException("Failed to allocate descriptor set");
        }

        return set;
    }
}