using System.Collections.Immutable;
using System.Runtime.InteropServices;
using dat_sharp_engine.Rendering.Util;
using dat_sharp_engine.Rendering.Vulkan.Descriptor;
using dat_sharp_engine.Util;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using VMASharp;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace dat_sharp_engine.Rendering.Vulkan;

/// <summary>
/// A renderer implementation using the Vulkan API
/// </summary>
public class VulkanRenderer : DatRenderer {
    private readonly Vk _vk = Vk.GetApi();
    private readonly Sdl _sdl;

    // Vulkan Instance
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;

    // Memory
    private VulkanMemoryAllocator? _allocator;

    // Queues
    private uint _graphicsQueueIndex; // Queue index for graphics stuff
    private Queue _graphicsQueue; // Queue for graphics stuff
    private uint _transferQueueIndex; // Queue index for transferring assets to the gpu
    private Queue _transferQueue; // Queue for transferring assets to the gpu

    // Surface
    private KhrSurface? _khrSurface;
    private SurfaceKHR _surface;

    // Swapchain
    private KhrSwapchain? _khrSwapchain;
    private SwapchainKHR _swapchain;
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;
    private SwapchainData[] _swapchainData;
    private FrameData[] _frameData;
    private uint _currentFrame = 0;

    // Frame Resources
    private AllocatedImage _drawImage;
    private Extent2D _drawExtent;
    
    // Descriptors
    private DescriptorAllocator _globalDescriptorAllocator;

    private DescriptorSet _drawImageDescriptor;
    private DescriptorSetLayout _drawImageDescriptorLayout;

    // Pipelines
    private Pipeline _gradientPipeline;
    private PipelineLayout _gradientPipelineLayout;

    // Debug
    private ExtDebugUtils? _extDebugUtils;
    private DebugUtilsMessengerEXT _debugUtilsMessenger;

    public VulkanRenderer(DatSharpEngine datSharpEngine) : base(datSharpEngine) {
        _sdl = _datSharpEngine.sdl;
    }

    public override WindowFlags GetWindowFlags() {
        return WindowFlags.Vulkan;
    }

    /* --------------------------------------- */
    /* Initialisation                          */
    /* --------------------------------------- */

    public override void Initialise() {
        Logger.EngineLogger.Info("Initialising Vulkan");

        InitialiseVulkanInstance();
        InitialiseVulkanPhysicalDevice();
        SelectQueues();
        InitialiseVulkanDevice();
        InitialiseVma();
        InitialiseSurface();
        InitialiseSwapchain();
        InitialiseSwapchainImages();
        InitialiseFrameData();
        InitialiseFrameImages();
        InitialiseDescriptors();
        InitialisePipelines();
    }

    /// <summary>
    /// Initialise the vulkan instance and debug validation layers
    /// </summary>
    /// <exception cref="DatRendererInitialisationException">Thrown when the instance fails to initialise</exception>
    private unsafe void InitialiseVulkanInstance() {
        Logger.EngineLogger.Debug("Initialising Vulkan instance");

        // if (_sdl.VulkanLoadLibrary() < 0) {
        //     throw new DatRendererInitialisationException("Failed to load vulkan library");
        // }

        var extensions = GetInstanceExtensions();

        ISet<string> layers;
        if (_datSharpEngine.engineSettings.debug) {
            layers = GetValidationLayers();

            Logger.EngineLogger.Debug("Selected instance extensions: {content}", extensions);
        } else layers = ImmutableHashSet<string>.Empty;


        var engineName = SilkMarshal.StringToPtr("DatSharpEngine");
        var applicationName = SilkMarshal.StringToPtr(_datSharpEngine.appSettings.name);

        ApplicationInfo appInfo = new() {
            SType = StructureType.ApplicationInfo,
            ApiVersion = Vk.Version13,
            EngineVersion = Vk.MakeVersion(EngineConstants.ENGINE_VERSION.Major,
                EngineConstants.ENGINE_VERSION.Minor,
                EngineConstants.ENGINE_VERSION.Patch
            ),
            ApplicationVersion = Vk.MakeVersion(_datSharpEngine.appSettings.version.Major,
                _datSharpEngine.appSettings.version.Minor,
                _datSharpEngine.appSettings.version.Patch
            ),
            PEngineName = (byte*) engineName,
            PApplicationName = (byte*) applicationName
        };


        // Vulkan requires these as C Arrays
        var enabledLayers = SilkMarshal.StringArrayToPtr(layers.ToArray());
        var enabledExtensions = SilkMarshal.StringArrayToPtr(extensions.ToArray());

        InstanceCreateInfo instanceInfo = new() {
            SType = StructureType.InstanceCreateInfo,
            Flags = InstanceCreateFlags.None,
            EnabledLayerCount = (uint) layers.Count,
            PpEnabledLayerNames = (byte**) enabledLayers,
            EnabledExtensionCount = (uint) extensions.Count,
            PpEnabledExtensionNames = (byte**) enabledExtensions,
            PApplicationInfo = &appInfo,
        };

        // We gotta dance this around so it may be included in the instance info and createDebug
        DebugUtilsMessengerCreateInfoEXT debugInfo = new() {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt |
                              DebugUtilsMessageSeverityFlagsEXT.WarningBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.ValidationBitExt |
                          DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                          DebugUtilsMessageTypeFlagsEXT.DeviceAddressBindingBitExt,
            PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT) VulkanDebugCallback
        };

        if (_datSharpEngine.engineSettings.debug) {
            instanceInfo.PNext = &debugInfo;
        }

        var result = _vk.CreateInstance(instanceInfo, null, out _instance);

        // Don't forget to free temporary c memory
        SilkMarshal.Free(enabledLayers);
        SilkMarshal.Free(enabledExtensions);
        SilkMarshal.Free(engineName);
        SilkMarshal.Free(applicationName);

        if (result != Result.Success) {
            throw new DatRendererInitialisationException("Failed to initialise vulkan");
        }

        // Setup debug stuff
        if (!_datSharpEngine.engineSettings.debug) return;

        if (!_vk.TryGetInstanceExtension(_instance, out _extDebugUtils))
            throw new DatRendererInitialisationException($"Could not get instance extension {ExtDebugUtils.ExtensionName}");

        _extDebugUtils!.CreateDebugUtilsMessenger(_instance, debugInfo, null, out _debugUtilsMessenger);
    }

    /// <summary>
    /// Get the required instance extensions
    /// </summary>
    /// <returns>A list of instance extensions to use</returns>
    /// <exception cref="DatRendererInitialisationException">
    /// Thrown when the engine fails to get the required instance extensions from SDL
    /// </exception>
    private unsafe ISet<string> GetInstanceExtensions() {
        var extensions = new HashSet<string> {
            ExtDebugUtils.ExtensionName,
            KhrSurface.ExtensionName
        };

        uint pCount = 0;
        _sdl.VulkanGetInstanceExtensions(_datSharpEngine.window, ref pCount, (byte**) null);

        var names = new string[pCount];

        if (_sdl.VulkanGetInstanceExtensions(_datSharpEngine.window, ref pCount, names) != SdlBool.True) {
            throw new DatRendererInitialisationException("Failed to get required instance extensions");
        }

        extensions.UnionWith(names);
        return extensions;
    }

    private ISet<string> GetValidationLayers() {
        var layers = new HashSet<string> {
            "VK_LAYER_KHRONOS_validation",
            "VK_LAYER_LUNARG_monitor",
            "VK_LAYER_MANGOAPP_overlay"
        };

        if (!CheckValidationLayerSupport(layers)) {
            throw new DatRendererInitialisationException("Requested validation layers were missing");
        }

        return layers;
    }

    /// <summary>
    /// Select a physical device for vulkan to use
    /// </summary>
    private unsafe void InitialiseVulkanPhysicalDevice() {
        Logger.EngineLogger.Debug("Selecting GPU for Vulkan");

        // Select a gpu
        // TODO: Better selection method
        var devices = _vk.GetPhysicalDevices(_instance);
        foreach (var gpu in devices) {
            var properties = _vk.GetPhysicalDeviceProperties(gpu);
            if (_datSharpEngine.engineSettings.gpu.HasValue &&
                properties.DeviceID == _datSharpEngine.engineSettings.gpu) {
                _physicalDevice = gpu;
                break;
            }

            if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu) _physicalDevice = gpu;
        }

        if (_physicalDevice.Handle == 0) _physicalDevice = devices.First();

        var deviceProps = _vk.GetPhysicalDeviceProperties(_physicalDevice);
        Logger.EngineLogger.Info("Selected GPU: {}", SilkMarshal.PtrToString((nint) deviceProps.DeviceName));

        // TODO: Save selection
    }

    /// <summary>
    /// Select the queues for the engine to use
    /// </summary>
    private void SelectQueues() {
        var queueFamilies = VkHelper.GetQueueFamilyProperties(_vk, _physicalDevice);

        // Find Graphics queue
        for (var i = 0; i < queueFamilies.Count; i++) {
            var properties = queueFamilies[i];

            if (!properties.QueueFlags.HasFlag(QueueFlags.GraphicsBit)) continue;

            _graphicsQueueIndex = (uint) i;
            break;
        }

        Logger.EngineLogger.Debug("Selected Graphics Queue: {}",
            VkHelper.GetQueueDescription((int) _graphicsQueueIndex, queueFamilies[(int) _graphicsQueueIndex])
        );

        // Find transfer queue
        var tempTrans = -1;
        for (var i = 0; i < queueFamilies.Count; i++) {
            var properties = queueFamilies[i];

            if (!properties.QueueFlags.HasFlag(QueueFlags.TransferBit)
                || properties.QueueFlags.HasFlag(QueueFlags.GraphicsBit)
                || properties.QueueFlags.HasFlag(QueueFlags.ComputeBit)) continue;

            tempTrans = i;
            break;
        }

        // If we can't find a dedicated transfer queue, just use the graphics queue
        _transferQueueIndex = tempTrans != -1 ? (uint) tempTrans : _graphicsQueueIndex;

        Logger.EngineLogger.Debug("Selected Transfer Queue: {}",
            VkHelper.GetQueueDescription((int) _transferQueueIndex, queueFamilies[(int) _transferQueueIndex])
        );

        // // Find Compute queue
        // var tempComp = -1;
        // for (var i = 0; i < queueFamilies.Length; i++) {
        //     var properties = queueFamilies[i];
        //
        //     if (!properties.QueueFlags.HasFlag(QueueFlags.ComputeBit)
        //         || properties.QueueFlags.HasFlag(QueueFlags.GraphicsBit)
        //         || properties.QueueFlags.HasFlag(QueueFlags.TransferBit)) continue;
        //
        //     tempComp = i;
        //     break;
        // }
        //
        // // If we can't find a dedicated compute queue, just use the graphics queue
        // _computeQueueIndex = tempComp != -1 ? (uint)tempComp : graphicsQueueIndex;
        //
        // Logger.EngineLogger.Debug("Selected Compute Queue: {}",
        //     GetQueueDescription((int) _computeQueueIndex, queueFamilies[_computeQueueIndex])
        // );
    }

    /// <summary>
    /// Initialise the logical device
    /// </summary>
    /// <exception cref="DatRendererInitialisationException">Thrown when creating the physical device fails</exception>
    private unsafe void InitialiseVulkanDevice() {
        Logger.EngineLogger.Debug("Initialising Vulkan Logical Device");

        var deviceExtensions = new List<string> {
            KhrSwapchain.ExtensionName
        };

        var enabledExtensions = SilkMarshal.StringArrayToPtr(deviceExtensions.ToArray());

        var priority = 1.0f;
        List<DeviceQueueCreateInfo> queueInfos = new() {
            new DeviceQueueCreateInfo {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueCount = 1,
                QueueFamilyIndex = _graphicsQueueIndex,
                PQueuePriorities = &priority
            }
        };

        // Make sure we only request 1 queue if the transfer and graphics queues are the same
        if (_transferQueueIndex != _graphicsQueueIndex) {
            queueInfos.Add(new DeviceQueueCreateInfo {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueCount = 1,
                    QueueFamilyIndex = _transferQueueIndex,
                    PQueuePriorities = &priority
                }
            );
        }

        // Toggle on features
        var features = new PhysicalDeviceFeatures {
            FillModeNonSolid = Vk.True
        };

        // Device features that require setup via PNext
        // (This would have been in it's own function but memory management needs to be handled in silly ways for garbage
        // collection reasons)
        var drawParametersFeatures = new PhysicalDeviceShaderDrawParametersFeatures {
            SType = StructureType.PhysicalDeviceShaderDrawParametersFeatures,
            ShaderDrawParameters = true
        };

        var vulkan12Features = new PhysicalDeviceVulkan12Features {
            SType = StructureType.PhysicalDeviceVulkan12Features,
            BufferDeviceAddress = true,
            DescriptorIndexing = true,
            PNext = &drawParametersFeatures
        };
        var vulkan13Features = new PhysicalDeviceVulkan13Features {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            DynamicRendering = true,
            Synchronization2 = true,
            PNext = &vulkan12Features
        };

        fixed (DeviceQueueCreateInfo* queues = queueInfos.ToArray()) {
            DeviceCreateInfo deviceInfo = new() {
                SType = StructureType.DeviceCreateInfo,
                EnabledLayerCount = 0,
                PpEnabledLayerNames = null,
                EnabledExtensionCount = (uint) deviceExtensions.Count,
                PpEnabledExtensionNames = (byte**) enabledExtensions,
                QueueCreateInfoCount = (uint) queueInfos.Count,
                PQueueCreateInfos = queues,
                PEnabledFeatures = &features,
                PNext = &vulkan13Features
            };

            if (_vk.CreateDevice(_physicalDevice, deviceInfo, null, out _device) != Result.Success) {
                throw new DatRendererInitialisationException("Failed to create device");
            }

            _vk.GetDeviceQueue(_device, _graphicsQueueIndex, 0, out _graphicsQueue);
            _vk.GetDeviceQueue(_device, _transferQueueIndex, 0, out _transferQueue);
        }
    }

    /// <summary>
    /// Initialise the memory allocator
    /// </summary>
    private void InitialiseVma() {
        Logger.EngineLogger.Debug("Initialising Vulkan Memory Allocator");

        VulkanMemoryAllocatorCreateInfo vmaCreateInfo = new() {
            VulkanAPIObject = _vk,
            Instance = _instance,
            PhysicalDevice = _physicalDevice,
            LogicalDevice = _device,
            VulkanAPIVersion = Vk.Version13,
            Flags = AllocatorCreateFlags.BufferDeviceAddress
        };

        _allocator = new VulkanMemoryAllocator(vmaCreateInfo);
    }

    /// <summary>
    /// Initialise the surface API and Surface
    /// </summary>
    /// <exception cref="DatRendererInitialisationException">Thrown when the KHRSurface API fails to be acquired</exception>
    private unsafe void InitialiseSurface() {
        Logger.EngineLogger.Debug("Initialising Vulkan Surface");

        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface)) {
            throw new DatRendererInitialisationException($"Could not get Instance extension {KhrSurface.ExtensionName}");
        }

        VkNonDispatchableHandle handle;
        _sdl.VulkanCreateSurface(_datSharpEngine.window, _instance.ToHandle(), &handle);
        _surface = handle.ToSurface();
    }

    /// <summary>
    /// Initialise the swapchain api and swapchain
    /// </summary>
    /// <exception cref="DatRendererInitialisationException">
    /// Thrown when the Swapchain API is unavailable, or the swapchain fails to be created
    /// </exception>
    private unsafe void InitialiseSwapchain() {
        Logger.EngineLogger.Debug("Initialising Swapchain");

        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain)) {
            throw new DatRendererInitialisationException($"Could not get device extension {KhrSwapchain.ExtensionName}");
        }

        if (_khrSurface!.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice,
                _surface,
                out var surfaceCapabilities
            ) != Result.Success) {
            throw new DatRendererInitialisationException("Failed to get surface capabilities");
        }

        var imageCount = Math.Clamp(_datSharpEngine.engineSettings.bufferedFrames,
            surfaceCapabilities.MinImageCount,
            surfaceCapabilities.MaxImageCount == 0 ? uint.MaxValue : surfaceCapabilities.MaxImageCount
        );

        var swapchainFormat = GetPreferredSwapchainFormat();
        var presentMode = GetPreferredPresentMode();
        var extent = new Extent2D(
            Math.Clamp((uint) _datSharpEngine.engineSettings.width,
                surfaceCapabilities.MinImageExtent.Width,
                surfaceCapabilities.MaxImageExtent.Width
            ),
            Math.Clamp((uint) _datSharpEngine.engineSettings.height,
                surfaceCapabilities.MinImageExtent.Height,
                surfaceCapabilities.MaxImageExtent.Height
            )
        );

        SwapchainCreateInfoKHR swapchainInfo = new() {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            Clipped = true,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PreTransform = surfaceCapabilities.CurrentTransform,
            MinImageCount = imageCount,
            ImageFormat = swapchainFormat.Format,
            ImageColorSpace = swapchainFormat.ColorSpace,
            ImageExtent = extent,
            PresentMode = presentMode,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit,
            ImageSharingMode = SharingMode.Exclusive,
            QueueFamilyIndexCount = 0,
            PQueueFamilyIndices = null
        };

        if (_khrSwapchain!.CreateSwapchain(_device, swapchainInfo, null, out _swapchain) != Result.Success) {
            throw new DatRendererInitialisationException("Failed to create swapchain");
        }

        _swapchainFormat = swapchainFormat.Format;
        _swapchainExtent = extent;
    }

    private unsafe void InitialiseSwapchainImages() {
        Logger.EngineLogger.Debug("Initialising Swapchain Images");

        var swapchainImages = VkHelper.GetSwapchainImages(_khrSwapchain!, _device, _swapchain);
        _swapchainData = new SwapchainData[swapchainImages.Count];

        for (var i = 0; i < swapchainImages.Count; i++) {
            var swapchainData = _swapchainData[i] = new SwapchainData();
            var swapchainImage = swapchainImages[i];

            swapchainData.image = swapchainImage;

            ImageViewCreateInfo imageViewInfo = new() {
                SType = StructureType.ImageViewCreateInfo,
                Image = swapchainImage,
                ViewType = ImageViewType.Type2D,
                Format = _swapchainFormat,
                Components = {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange = {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (_vk.CreateImageView(_device, imageViewInfo, null, out swapchainData.imageView) != Result.Success) {
                throw new DatRendererInitialisationException($"Failed to create swapchain imageView");
            }
        }
    }

    /// <summary>
    /// Setup the data used each frame
    /// </summary>
    private void InitialiseFrameData() {
        Logger.EngineLogger.Debug("Initialising Framedata");

        _frameData = new FrameData[_datSharpEngine.engineSettings.bufferedFrames].Select(_ => new FrameData()).ToArray();

        foreach (var frameData in _frameData) {
            InitialiseFrameCommands(frameData);
            InitialiseFrameSyncStructures(frameData);
        }
    }

    /// <summary>
    /// Setup the command structure for the given frame
    /// </summary>
    /// <param name="frameData">The frame being setup</param>
    /// <exception cref="DatRendererInitialisationException">
    /// Thrown when Vulkan fails to create any of the command structures
    /// </exception>
    private unsafe void InitialiseFrameCommands(FrameData frameData) {
        CommandPoolCreateInfo commandPoolInfo = new() {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _graphicsQueueIndex
        };

        if (_vk.CreateCommandPool(_device, commandPoolInfo, null, out frameData.commandPool) != Result.Success) {
            throw new DatRendererInitialisationException("Failed to create swapchain command pool");
        }

        var bufferAllocateInfo = VkShortcuts.CreateCommandBufferAllocateInfo(frameData.commandPool, 1);

        if (_vk.AllocateCommandBuffers(_device, bufferAllocateInfo, out frameData.commandBuffer) != Result.Success) {
            throw new DatRendererInitialisationException("Failed to create swapchain command buffer");
        }
    }

    /// <summary>
    /// Setup the synchronisation structures for the given frame
    /// </summary>
    /// <param name="frameData">The frame being setup</param>
    /// <exception cref="DatRendererInitialisationException">
    /// Thrown when vulkan fails to create any of the synchronisation structures
    /// </exception>
    private unsafe void InitialiseFrameSyncStructures(FrameData frameData) {
        var fenceInfo = VkShortcuts.CreateFenceCreateInfo(FenceCreateFlags.SignaledBit);
        var semaphoreInfo = VkShortcuts.CreateSemaphoreCreateInfo();

        if (_vk.CreateFence(_device, fenceInfo, null, out frameData.renderFence) != Result.Success) {
            throw new DatRendererInitialisationException("Failed to create frame render fence");
        }

        if (_vk.CreateSemaphore(_device, semaphoreInfo, null, out frameData.swapchainSemaphore) != Result.Success) {
            throw new DatRendererInitialisationException("Failed to create frame swapchain semaphore");
        }

        if (_vk.CreateSemaphore(_device, semaphoreInfo, null, out frameData.renderSemaphore) != Result.Success) {
            throw new DatRendererInitialisationException("Failed to create frame render semaphore");
        }
    }

    private unsafe void InitialiseFrameImages() {
        Logger.EngineLogger.Debug("Initialising Frame Images");
        var drawImageExtent = new Extent3D(_datSharpEngine.engineSettings.width, _datSharpEngine.engineSettings.height, 1);

        const Format format = Format.R16G16B16A16Sfloat;

        const ImageUsageFlags usageFlags = ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit |
                                           ImageUsageFlags.StorageBit | ImageUsageFlags.ColorAttachmentBit;

        var imageInfo = VkShortcuts.CreateImageCreateInfo(format, usageFlags, drawImageExtent);

        var allocationInfo = new AllocationCreateInfo {
            Usage = MemoryUsage.GPU_Only,
            RequiredFlags = MemoryPropertyFlags.DeviceLocalBit
        };

        var image = _allocator!.CreateImage(imageInfo, allocationInfo, out var allocation);

        var imageViewInfo =
            VkShortcuts.CreateImageViewCreateInfo(format, image, ImageAspectFlags.ColorBit);

        if (_vk.CreateImageView(_device, imageViewInfo, null, out var imageView) != Result.Success) {
            throw new DatRendererInitialisationException("Failed to create render image view");
        }

        _drawImage = new AllocatedImage(image, imageView, allocation, drawImageExtent, format);
        _drawExtent = new Extent2D(drawImageExtent.Width, drawImageExtent.Height);
    }

    private unsafe void InitialiseDescriptors() {
        Logger.EngineLogger.Debug("Initialising Descriptors");
        DescriptorAllocator.PoolSizeRatio[] sizes = [
            new DescriptorAllocator.PoolSizeRatio(DescriptorType.StorageImage, 1)
        ];
        
        _globalDescriptorAllocator = new DescriptorAllocator(_vk, _device);
        _globalDescriptorAllocator.InitPool(10, sizes);

        _drawImageDescriptorLayout = new DescriptorLayoutBuilder()
            .AddBinding(0, DescriptorType.StorageImage)
            .Build(_vk, _device, ShaderStageFlags.ComputeBit);

        _drawImageDescriptor = _globalDescriptorAllocator.Allocate(_drawImageDescriptorLayout);

        var imageInfo = new DescriptorImageInfo {
            ImageLayout = ImageLayout.General,
            ImageView = _drawImage.imageView
        };

        var drawImageWrite = new WriteDescriptorSet {
            SType = StructureType.WriteDescriptorSet,

            DstBinding = 0,
            DstSet = _drawImageDescriptor,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageImage,
            PImageInfo = &imageInfo
        };

        _vk.UpdateDescriptorSets(_device, 1, &drawImageWrite, 0, null);
    }

    void InitialisePipelines() {
        Logger.EngineLogger.Debug("Initialising Pipelines");
        InitialiseBackgroundPipelines();
    }

    private unsafe void InitialiseBackgroundPipelines() {
        fixed (DescriptorSetLayout* setLayout = &_drawImageDescriptorLayout) {
            PipelineLayoutCreateInfo computeLayout = new PipelineLayoutCreateInfo {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = setLayout
            };

            if (_vk.CreatePipelineLayout(_device, computeLayout, null, out _gradientPipelineLayout) != Result.Success) {
                throw new DatRendererInitialisationException("Failed to initialise gradient Pipeline Layout");
            }
        }

        var computeDrawShader = VkHelper.LoadShaderModel(_vk,
            _device,
            "/home/jacob/Projects/Dotnet/dat-sharp-engine/dat-sharp-engine/Assets/Shaders/comp.spv"
        );

        const string entryPoint = "main";
        var pEntryPoint = (byte*)Marshal.StringToHGlobalAnsi(entryPoint);

        var stageInfo = new PipelineShaderStageCreateInfo {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = computeDrawShader,
            PName = pEntryPoint
        };

        var computePipelineInfo = new ComputePipelineCreateInfo {
            SType = StructureType.ComputePipelineCreateInfo,

            Layout = _gradientPipelineLayout,
            Stage = stageInfo
        };

        if (_vk.CreateComputePipelines(_device, default, 1, computePipelineInfo, null, out _gradientPipeline) != Result.Success) {
            Marshal.Release((IntPtr) pEntryPoint);
            throw new DatRendererInitialisationException("Failed to initialise gradient Pipeline Layout");
        }

        _vk.DestroyShaderModule(_device, computeDrawShader, null);
    }

    /// <summary>
    /// Get the preferred surface format for the swapchain
    /// </summary>
    /// <returns>The preferred surface format for the swapchain</returns>
    private SurfaceFormatKHR GetPreferredSwapchainFormat() {
        var supportedFormats = VkHelper.GetDeviceSurfaceFormats(_khrSurface!, _physicalDevice, _surface);
        return supportedFormats
            .Where(format => format is { Format: Format.B8G8R8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr })
            .FirstOrDefault(supportedFormats[0]);
    }

    /// <summary>
    /// Get the preferred present mode for the swapchain
    /// <para/>
    /// This uses the <see cref="EngineSettings.vsync"/> engine setting
    /// </summary>
    /// <returns>The preferred present mode</returns>
    private PresentModeKHR GetPreferredPresentMode() {
        var formats = VkHelper.GetDeviceSurfacePresentModes(_khrSurface!, _physicalDevice, _surface);

        PresentModeKHR[] options = _datSharpEngine.engineSettings.vsync
            ? [PresentModeKHR.FifoRelaxedKhr, PresentModeKHR.FifoKhr]
            : [PresentModeKHR.ImmediateKhr, PresentModeKHR.FifoKhr];

        return options.First(option => formats.Contains(option));
    }

    public override unsafe void Draw(float deltaTime, float gameTime) {
        Logger.EngineLogger.Debug(deltaTime);
        var currentFrameData = GetCurrentFrameData();

        _vk.WaitForFences(_device, 1, currentFrameData.renderFence, true, 1000000000);
        _vk.ResetFences(_device, 1, currentFrameData.renderFence);

        uint imageIndex = 0;
        // TODO: Handle out of date
        _khrSwapchain!.AcquireNextImage(_device,
            _swapchain,
            1000000000,
            currentFrameData.swapchainSemaphore,
            new Fence(null),
            ref imageIndex
        );

        var swapchainData = _swapchainData[imageIndex];

        _vk.ResetCommandBuffer(currentFrameData.commandBuffer, CommandBufferResetFlags.None);
        var commandBeginInfo = VkShortcuts.CreateCommandBufferBeginInfo(CommandBufferUsageFlags.OneTimeSubmitBit);
        _vk.BeginCommandBuffer(currentFrameData.commandBuffer, commandBeginInfo);

        // Clear colour
        VkHelper.TransitionImage(_vk,
            currentFrameData.commandBuffer,
            _drawImage.image,
            ImageLayout.Undefined,
            ImageLayout.General,
            PipelineStageFlags2.TopOfPipeBit,
            PipelineStageFlags2.ComputeShaderBit,
            0,
            AccessFlags2.ShaderWriteBit
        );

        DrawBackground(currentFrameData);

        // Transition draw and swapchain images for transfer
        VkHelper.TransitionImage(_vk,
            currentFrameData.commandBuffer,
            _drawImage.image,
            ImageLayout.General,
            ImageLayout.TransferSrcOptimal,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            AccessFlags2.MemoryWriteBit,
            AccessFlags2.MemoryReadBit);

        VkHelper.TransitionImage(_vk,
            currentFrameData.commandBuffer,
            swapchainData.image,
            ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            AccessFlags2.None,
            AccessFlags2.MemoryWriteBit);

        VkHelper.CopyImageToImage(_vk,
            currentFrameData.commandBuffer,
            _drawImage.image,
            swapchainData.image,
            _drawExtent,
            _swapchainExtent
        );

        VkHelper.TransitionImage(_vk,
            currentFrameData.commandBuffer,
            swapchainData.image,
            ImageLayout.TransferDstOptimal,
            ImageLayout.PresentSrcKhr,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            AccessFlags2.MemoryWriteBit,
            AccessFlags2.MemoryWriteBit);

        _vk.EndCommandBuffer(currentFrameData.commandBuffer);

        // Submit Queue
        var cmdInfo = VkShortcuts.CreateCommandBufferSubmitInfo(currentFrameData.commandBuffer);
        var waitInfo = VkShortcuts.CreateSemaphoreSubmitInfo(PipelineStageFlags2.ColorAttachmentOutputBit,
            currentFrameData.swapchainSemaphore
        );
        var signalInfo = VkShortcuts.CreateSemaphoreSubmitInfo(PipelineStageFlags2.AllGraphicsBit,
            currentFrameData.renderSemaphore
        );

        _vk.QueueSubmit2(_graphicsQueue,
            1,
            VkShortcuts.CreateSubmitInfo(&cmdInfo, &signalInfo, &waitInfo),
            currentFrameData.renderFence
        );

        fixed (SwapchainKHR* swapchain = &_swapchain)
        fixed (Semaphore* waitSemaphore = &currentFrameData.renderSemaphore) {
            // Present
            var presentInfo = new PresentInfoKHR {
                SType = StructureType.PresentInfoKhr,
                SwapchainCount = 1,
                PSwapchains = swapchain,

                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphore,

                PImageIndices = &imageIndex
            };

            _khrSwapchain!.QueuePresent(_graphicsQueue, presentInfo);
        }

        ++_currentFrame;
    }

    private unsafe void DrawBackground(FrameData currentFrameData) {
        _vk.CmdBindPipeline(currentFrameData.commandBuffer, PipelineBindPoint.Compute, _gradientPipeline);

        var drawImageDescriptor = _drawImageDescriptor;
        _vk.CmdBindDescriptorSets(currentFrameData.commandBuffer,
            PipelineBindPoint.Compute,
            _gradientPipelineLayout,
            0,
            1,
            &drawImageDescriptor,
            null
        );

        _vk.CmdDispatch(currentFrameData.commandBuffer,
            (uint) Math.Ceiling(_drawExtent.Width / 16.0),
            (uint) Math.Ceiling(_drawExtent.Height / 16.0),
            1
        );
    }

    public override unsafe void Cleanup() {
        _vk.DeviceWaitIdle(_device);

        _vk.DestroyPipeline(_device, _gradientPipeline, null);
        _vk.DestroyPipelineLayout(_device, _gradientPipelineLayout, null);

        _vk.DestroyImageView(_device, _drawImage.imageView, null);
        _vk.DestroyImage(_device, _drawImage.image, null);
        _allocator!.FreeMemory(_drawImage.allocation);

        foreach (var frameData in _frameData) {
            _vk.DestroySemaphore(_device, frameData.renderSemaphore, null);
            _vk.DestroySemaphore(_device, frameData.swapchainSemaphore, null);
            _vk.DestroyFence(_device, frameData.renderFence, null);
            _vk.DestroyCommandPool(_device, frameData.commandPool, null);
        }

        foreach (var swapchainData in _swapchainData) {
            _vk.DestroyImageView(_device, swapchainData.imageView, null);
        }
        _khrSwapchain?.Dispose();

        _khrSurface?.DestroySurface(_instance, _surface, null);
        _khrSurface?.Dispose();

        _allocator?.Dispose();

        _vk.DestroyDevice(_device, null);

        _extDebugUtils?.DestroyDebugUtilsMessenger(_instance, _debugUtilsMessenger, null);
        _extDebugUtils?.Dispose();

        _vk.DestroyInstance(_instance, null);
        _vk.Dispose();
    }


    /* --------------------------------------- */
    /* Debug                                   */
    /* --------------------------------------- */

    /// <summary>
    /// The Callback used for validation layer messages
    /// </summary>
    /// <param name="severityFlags">The flags for the severity of the message</param>
    /// <param name="messageTypeFlags">The flags for the type of the message</param>
    /// <param name="pCallbackData">The message data</param>
    /// <param name="pUserData">An attached pointer for user data</param>
    /// <returns>Always false</returns>
    private static unsafe uint VulkanDebugCallback(DebugUtilsMessageSeverityFlagsEXT severityFlags,
        DebugUtilsMessageTypeFlagsEXT messageTypeFlags,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData) {
        var message = Marshal.PtrToStringUTF8((nint) pCallbackData->PMessage);
        Logger.EngineLogger.Warn("Vulkan | {} | {}", severityFlags, message);

        return Vk.False;
    }

    /* --------------------------------------- */
    /* Util                                    */
    /* --------------------------------------- */

    /// <summary>
    /// Check the device supports the requested validation layers
    /// </summary>
    /// <param name="requestedLayers">A list of the layers the engine wants to use</param>
    /// <returns>True if the device has all of the requested layers available</returns>
    private bool CheckValidationLayerSupport(IEnumerable<string> requestedLayers) {
        var availableLayers = VkHelper.GetAvailableValidationLayers(_vk);

        Logger.EngineLogger.Debug("Available validation layers extensions: {content}", availableLayers);

        return requestedLayers.All(layer => availableLayers.Contains(layer));
    }

    /// <summary>
    /// Get the frame data for the current frame<br/>
    /// Note this uses <see cref="_currentFrame"/>
    /// </summary>
    /// <returns>The frame data for the current frame</returns>
    private FrameData GetCurrentFrameData() {
        return _frameData[_currentFrame % _datSharpEngine.engineSettings.bufferedFrames];
    }
}
