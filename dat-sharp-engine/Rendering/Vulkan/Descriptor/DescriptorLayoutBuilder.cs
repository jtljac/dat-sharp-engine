using dat_sharp_engine.Rendering.Util;
using Silk.NET.Vulkan;

namespace dat_sharp_engine.Rendering.Vulkan.Descriptor;

/// <summary>
/// A class to build Descriptor Layouts
/// </summary>
public class DescriptorLayoutBuilder {
    /// <summary>
    /// The list of bindings being added to the descriptor layout
    /// </summary>
    private readonly List<DescriptorSetLayoutBinding> _bindings = [];

    /// <summary>
    ///
    /// </summary>
    /// <param name="binding">The binding number for this binding</param>
    /// <param name="type">The type for this binding</param>
    /// <returns>The DescriptorLayoutBuilder</returns>
    public DescriptorLayoutBuilder AddBinding(uint binding, DescriptorType type) {
        _bindings.Add(new DescriptorSetLayoutBinding {
            Binding = binding,
            DescriptorCount = 1,
            DescriptorType = type
        });

        return this;
    }

    /// <summary>
    /// Clear the bindings from this Builder
    /// </summary>
    public void Clear() {
        _bindings.Clear();
    }

    /// <summary>
    /// Build the bindings into a DescriptorSetLayout
    /// </summary>
    /// <param name="vk">An instance of the Vk API to use for querying the driver for the properties</param>
    /// <param name="device">The device the bindings are for</param>
    /// <param name="shaderStages">The stages the bindings will be bound to</param>
    /// <returns>A built DescriptorSetLayout</returns>
    /// <exception cref="DatRendererException">If Vulkan fails to build the DescriptorSetLayout</exception>
    public unsafe DescriptorSetLayout Build(Vk vk, Device device, ShaderStageFlags shaderStages) {
        for (var i = 0; i < _bindings.Count; i++) {
            var binding = _bindings[i];
            binding.StageFlags |= shaderStages;
            _bindings[i] = binding;
        }

        fixed (DescriptorSetLayoutBinding* bindings = _bindings.ToArray()) {
            var descriptorSetLayoutInfo = new DescriptorSetLayoutCreateInfo {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint) _bindings.Count,
                PBindings = bindings,
                Flags = 0
            };

            if (vk.CreateDescriptorSetLayout(device, descriptorSetLayoutInfo, null, out var setLayout) !=
                Result.Success) {
                throw new DatRendererException("Failed to build descriptor set layout");
            }

            return setLayout;
        }
    }
}