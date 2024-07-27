#if !DEBUG
using System.Collections.Immutable;
#endif

using System.Numerics;
using System.Runtime.InteropServices;
using dat_sharp_engine.AssetManagement;
using dat_sharp_engine.Collection;
using dat_sharp_engine.Mesh;
using dat_sharp_engine.Rendering.Util;
using dat_sharp_engine.Rendering.Vulkan.Descriptor;
using dat_sharp_engine.Rendering.Vulkan.GpuStructures;
using dat_sharp_engine.Util;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using VMASharp;
using Buffer = System.Buffer;
using Queue = Silk.NET.Vulkan.Queue;
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
    private bool _unifiedTransferQueue;
    private uint _transferQueueIndex; // Queue index for transferring assets to the gpu
    private Queue _transferQueue; // Queue for transferring assets to the gpu
    public Queue TransferQueue {
        get => _unifiedTransferQueue ? _graphicsQueue : _transferQueue;
        set => _transferQueue = value;
    }

    public Queue ComputeQueue {
        get => _unifiedComputeQueue ? _graphicsQueue : _computeQueue;
        set => _computeQueue = value;
    }

    private bool _unifiedComputeQueue;
    private uint _computeQueueIndex; // Queue index for asynchronous compute
    private Queue _computeQueue; // Queue for performing asynchronous compute

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
    private PipelineLayout _trianglePipelineLayout;
    private Pipeline _trianglePipeline;

    // Immediate submit
    private Fence _immFence;
    private CommandPool _immGraphicsCommandPool;
    private CommandBuffer _immGraphicsCommandBuffer;
    private CommandPool _immTransferCommandPool;
    private CommandBuffer _immTransferCommandBuffer;

    // Gpu Memory tracking
    private readonly ConcurrentIdTrackedResource<AllocatedBuffer> _bufferList = new();
    private readonly ConcurrentIdTrackedResource<AllocatedMesh?> _meshList = new();

    // Debug
    private ExtDebugUtils? _extDebugUtils;
    private DebugUtilsMessengerEXT _debugUtilsMessenger;
    private Mesh3d _tempMesh3d;

    public VulkanRenderer() {
        _sdl = DatSharpEngine.instance.sdl;
    }

    public override uint GetWindowFlags() {
        return (uint) WindowFlags.Vulkan;

    }

    /* --------------------------------------- */
    /* Initialisation                          */
    /* --------------------------------------- */

    public override void Initialise() {
        base.Initialise();
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
        InitialiseExtraCommands();
        InitialiseExtraSyncStructures();
        InitialiseFrameImages();
        InitialiseDescriptors();
        InitialisePipelines();
        InitialiseMesh();
    }

    /// <summary>
    /// Initialise the vulkan instance and debug validation layers
    /// </summary>
    /// <exception cref="DatRendererException">Thrown when the instance fails to initialise</exception>
    private unsafe void InitialiseVulkanInstance() {
        Logger.EngineLogger.Debug("Initialising Vulkan instance");

        // if (_sdl.VulkanLoadLibrary() < 0) {
        //     throw new DatRendererInitialisationException("Failed to load vulkan library");
        // }

        var extensions = GetInstanceExtensions();

#if DEBUG
        var layers = GetValidationLayers();

        Logger.EngineLogger.Debug("Selected instance extensions: {content}", extensions);
#else
        ISet<string>layers = ImmutableHashSet<string>.Empty;
#endif


        var engineName = SilkMarshal.StringToPtr("DatSharpEngine");
        var applicationName = SilkMarshal.StringToPtr(DatSharpEngine.instance.appSettings.name);

        ApplicationInfo appInfo = new() {
            ApiVersion = Vk.Version13,
            EngineVersion = Vk.MakeVersion(EngineConstants.EngineVersion.Major,
                EngineConstants.EngineVersion.Minor,
                EngineConstants.EngineVersion.Patch
            ),
            ApplicationVersion = Vk.MakeVersion(DatSharpEngine.instance.appSettings.version.Major,
                DatSharpEngine.instance.appSettings.version.Minor,
                DatSharpEngine.instance.appSettings.version.Patch
            ),
            PEngineName = (byte*) engineName,
            PApplicationName = (byte*) applicationName
        };


        // Vulkan requires these as C Arrays
        var enabledLayers = SilkMarshal.StringArrayToPtr(layers.ToArray());
        var enabledExtensions = SilkMarshal.StringArrayToPtr(extensions.ToArray());

        InstanceCreateInfo instanceInfo = new() {
            Flags = InstanceCreateFlags.None,
            EnabledLayerCount = (uint) layers.Count,
            PpEnabledLayerNames = (byte**) enabledLayers,
            EnabledExtensionCount = (uint) extensions.Count,
            PpEnabledExtensionNames = (byte**) enabledExtensions,
            PApplicationInfo = &appInfo,
        };


#if DEBUG
        // We gotta dance this around so it may be included in the instance info and createDebug
        DebugUtilsMessengerCreateInfoEXT debugInfo = new() {
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt |
                              DebugUtilsMessageSeverityFlagsEXT.WarningBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.ValidationBitExt |
                          DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                          DebugUtilsMessageTypeFlagsEXT.DeviceAddressBindingBitExt,
            PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT) VulkanDebugCallback
        };

        instanceInfo.PNext = &debugInfo;
#endif

        var result = _vk.CreateInstance(instanceInfo, null, out _instance);

        // Don't forget to free temporary c memory
        SilkMarshal.Free(enabledLayers);
        SilkMarshal.Free(enabledExtensions);
        SilkMarshal.Free(engineName);
        SilkMarshal.Free(applicationName);

        if (result != Result.Success) {
            throw new DatRendererException($"Failed to initialise vulkan: {result.ToString()}");
        }

        // Setup debug stuff
#if DEBUG
        if (!_vk.TryGetInstanceExtension(_instance, out _extDebugUtils))
            throw new DatRendererException($"Could not get instance extension {ExtDebugUtils.ExtensionName}");

        _extDebugUtils!.CreateDebugUtilsMessenger(_instance, debugInfo, null, out _debugUtilsMessenger);
#endif
    }

    /// <summary>
    /// Get the required instance extensions
    /// </summary>
    /// <returns>A list of instance extensions to use</returns>
    /// <exception cref="DatRendererException">
    /// Thrown when the engine fails to get the required instance extensions from SDL
    /// </exception>
    private unsafe ISet<string> GetInstanceExtensions() {
        var extensions = new HashSet<string> {
            ExtDebugUtils.ExtensionName,
            KhrSurface.ExtensionName
        };

        uint pCount = 0;
        _sdl.VulkanGetInstanceExtensions(DatSharpEngine.instance.window, ref pCount, (byte**) null);

        var names = new string[pCount];

        if (_sdl.VulkanGetInstanceExtensions(DatSharpEngine.instance.window, ref pCount, names) != SdlBool.True) {
            throw new DatRendererException("Failed to get required instance extensions");
        }

        extensions.UnionWith(names);
        return extensions;
    }

    private HashSet<string> GetValidationLayers() {
        var layers = new HashSet<string> {
            "VK_LAYER_KHRONOS_validation",
            "VK_LAYER_LUNARG_monitor",
            // "VK_LAYER_MANGOAPP_overlay"
        };

        if (!CheckValidationLayerSupport(layers)) {
            throw new DatRendererException("Requested validation layers were missing");
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
            PhysicalDeviceProperties2
                .Chain(out var deviceProperties2)
                .AddNext(out PhysicalDeviceIDProperties deviceIdProperties);

            _vk.GetPhysicalDeviceProperties2(gpu, &deviceProperties2);

            var properties = deviceProperties2.Properties;
            var deviceId = deviceIdProperties.DeviceUuid[0] << 3
                | deviceIdProperties.DeviceUuid[1] << 2
                | deviceIdProperties.DeviceUuid[2] << 1
                | deviceIdProperties.DeviceUuid[3];

            if (EngineCVars.GpuUuidCvar.value == deviceId) {
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
        _graphicsQueueIndex = (uint) GetBestQueue(queueFamilies, QueueFlags.GraphicsBit, Array.Empty<uint>());

        // Find transfer queue
        var tempTrans = GetBestQueue(queueFamilies, QueueFlags.TransferBit, [_graphicsQueueIndex]);

        // If we can't find a dedicated transfer queue, just use the graphics queue
        if (tempTrans != -1)
            _transferQueueIndex = (uint) tempTrans;
        else {
            _unifiedTransferQueue = true;
            _transferQueueIndex = _graphicsQueueIndex;
        }

        // Find Compute queue
        var tempComp = GetBestQueue(queueFamilies, QueueFlags.ComputeBit, tempTrans == -1 ? [_graphicsQueueIndex] : [_graphicsQueueIndex, _transferQueueIndex]);

        // If we can't find a dedicated compute queue, just use the graphics queue
        if (tempComp != -1)
            _computeQueueIndex = (uint) tempComp;
        else {
            _unifiedComputeQueue = true;
            _computeQueueIndex = _graphicsQueueIndex;
        }

        Logger.EngineLogger.Debug("Selected Graphics Queue: {}",
            VkHelper.GetQueueDescription((int) _graphicsQueueIndex, queueFamilies[(int) _graphicsQueueIndex])
        );

        Logger.EngineLogger.Debug("Selected Transfer Queue: {}",
            VkHelper.GetQueueDescription((int) _transferQueueIndex, queueFamilies[(int) _transferQueueIndex])
        );

        Logger.EngineLogger.Debug("Selected Compute Queue: {}",
            VkHelper.GetQueueDescription((int) _computeQueueIndex, queueFamilies[(int) _computeQueueIndex])
        );
    }

    /// <summary>
    /// Initialise the logical device
    /// </summary>
    /// <exception cref="DatRendererException">Thrown when creating the physical device fails</exception>
    private unsafe void InitialiseVulkanDevice() {
        Logger.EngineLogger.Debug("Initialising Vulkan Logical Device");

        var deviceExtensions = new List<string> {
            KhrSwapchain.ExtensionName
        };

        var enabledExtensions = SilkMarshal.StringArrayToPtr(deviceExtensions.ToArray());

        /*
         * Stupid bollocks to dynamically setup queues
         * Basically we need to count the number of usages in each queue and allocate that many of each queue, so we
         * count with a dictionary.
         * We don't currently deal with priorities, and we need a pinned array to use as a pointer, so we just create
         * one filled with 1s that is the biggest it'll ever be and reuse it.
         */
        Dictionary<uint, uint> queueDict = [];
        ++CollectionsMarshal.GetValueRefOrAddDefault(queueDict, _graphicsQueueIndex, out _);
        if (!_unifiedTransferQueue) ++CollectionsMarshal.GetValueRefOrAddDefault(queueDict, _transferQueueIndex, out _);
        if (!_unifiedComputeQueue) ++CollectionsMarshal.GetValueRefOrAddDefault(queueDict, _computeQueueIndex, out _);

        var priorities = Enumerable.Repeat(1.0f, (int) queueDict.Values.Max()).ToArray();

        fixed (float* pQueuePriorities = priorities) {
            var queueInfos = new DeviceQueueCreateInfo[queueDict.Count];
            var i = 0;
            foreach (var queueDictKey in queueDict.Keys) {
                queueInfos[i++] = new DeviceQueueCreateInfo {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueCount = queueDict[queueDictKey],
                    QueueFamilyIndex = queueDictKey,
                    PQueuePriorities = pQueuePriorities
                };
            }

            // Toggle on features
            var features = new PhysicalDeviceFeatures {
                FillModeNonSolid = Vk.True
            };

            // Device features that require setup via PNext
            // (This would have been in its own function but memory management needs to be handled in silly ways for garbage
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

            fixed (DeviceQueueCreateInfo* queues = queueInfos) {
                var deviceInfo = new DeviceCreateInfo {
                    EnabledLayerCount = 0,
                    PpEnabledLayerNames = null,
                    EnabledExtensionCount = (uint) deviceExtensions.Count,
                    PpEnabledExtensionNames = (byte**) enabledExtensions,
                    QueueCreateInfoCount = (uint) queueInfos.Length,
                    PQueueCreateInfos = queues,
                    PEnabledFeatures = &features,
                };

                deviceInfo
                    .SetNext(ref vulkan13Features)
                    .SetNext(ref vulkan12Features)
                    .SetNext(ref drawParametersFeatures);

                if (_vk.CreateDevice(_physicalDevice, deviceInfo, null, out _device) != Result.Success) {
                    throw new DatRendererException("Failed to create device");
                }

                // We always want the graphics queue to get the 0 index queue, so we gotta do this weird unwind when
                // to account for queues
                if (!_unifiedComputeQueue)
                    _vk.GetDeviceQueue(_device, _computeQueueIndex, --CollectionsMarshal.GetValueRefOrNullRef(queueDict, _computeQueueIndex), out _computeQueue);
                if (!_unifiedTransferQueue)
                    _vk.GetDeviceQueue(_device, _transferQueueIndex, --CollectionsMarshal.GetValueRefOrNullRef(queueDict, _transferQueueIndex), out _transferQueue);

                _vk.GetDeviceQueue(_device, _graphicsQueueIndex, --CollectionsMarshal.GetValueRefOrNullRef(queueDict, _graphicsQueueIndex), out _graphicsQueue);
            }
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
    /// <exception cref="DatRendererException">Thrown when the KHRSurface API fails to be acquired</exception>
    private unsafe void InitialiseSurface() {
        Logger.EngineLogger.Debug("Initialising Vulkan Surface");

        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface)) {
            throw new DatRendererException($"Could not get Instance extension {KhrSurface.ExtensionName}");
        }

        VkNonDispatchableHandle handle;
        _sdl.VulkanCreateSurface(DatSharpEngine.instance.window, _instance.ToHandle(), &handle);
        _surface = handle.ToSurface();
    }

    /// <summary>
    /// Initialise the swapchain api and swapchain
    /// </summary>
    /// <exception cref="DatRendererException">
    /// Thrown when the Swapchain API is unavailable, or the swapchain fails to be created
    /// </exception>
    private unsafe void InitialiseSwapchain() {
        Logger.EngineLogger.Debug("Initialising Swapchain");

        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain)) {
            throw new DatRendererException($"Could not get device extension {KhrSwapchain.ExtensionName}");
        }

        if (_khrSurface!.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice,
                _surface,
                out var surfaceCapabilities
            ) != Result.Success) {
            throw new DatRendererException("Failed to get surface capabilities");
        }

        var imageCount = Math.Clamp(EngineCVars.BufferedFramesCvar.value,
            surfaceCapabilities.MinImageCount,
            surfaceCapabilities.MaxImageCount == 0 ? uint.MaxValue : surfaceCapabilities.MaxImageCount
        );

        var swapchainFormat = GetPreferredSwapchainFormat();
        var presentMode = GetPreferredPresentMode();
        var extent = new Extent2D(
            Math.Clamp((uint) EngineCVars.WindowWidthCvar.value,
                surfaceCapabilities.MinImageExtent.Width,
                surfaceCapabilities.MaxImageExtent.Width
            ),
            Math.Clamp((uint) EngineCVars.WindowHeightCvar.value,
                surfaceCapabilities.MinImageExtent.Height,
                surfaceCapabilities.MaxImageExtent.Height
            )
        );

        SwapchainCreateInfoKHR swapchainInfo = new() {
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
            throw new DatRendererException("Failed to create swapchain");
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
                throw new DatRendererException($"Failed to create swapchain imageView");
            }
        }
    }

    /// <summary>
    /// Setup the data used each frame
    /// </summary>
    private void InitialiseFrameData() {
        Logger.EngineLogger.Debug("Initialising Framedata");

        _frameData = new FrameData[EngineCVars.BufferedFramesCvar.value].Select(_ => new FrameData()).ToArray();

        foreach (var frameData in _frameData) {
            InitialiseFrameCommands(frameData);
            InitialiseFrameSyncStructures(frameData);
        }
    }

    /// <summary>
    /// Setup the command structure for the given frame
    /// </summary>
    /// <param name="frameData">The frame being setup</param>
    /// <exception cref="DatRendererException">
    /// Thrown when Vulkan fails to create any of the command structures
    /// </exception>
    private unsafe void InitialiseFrameCommands(FrameData frameData) {
        CommandPoolCreateInfo commandPoolInfo = new() {
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _graphicsQueueIndex
        };

        if (_vk.CreateCommandPool(_device, commandPoolInfo, null, out frameData.commandPool) != Result.Success) {
            throw new DatRendererException("Failed to create swapchain command pool");
        }

        var bufferAllocateInfo = VkShortcuts.CreateCommandBufferAllocateInfo(frameData.commandPool, 1);

        if (_vk.AllocateCommandBuffers(_device, bufferAllocateInfo, out frameData.commandBuffer) != Result.Success) {
            throw new DatRendererException("Failed to create swapchain command buffer");
        }
    }

    /// <summary>
    /// Setup the synchronisation structures for the given frame
    /// </summary>
    /// <param name="frameData">The frame being setup</param>
    /// <exception cref="DatRendererException">
    /// Thrown when vulkan fails to create any of the synchronisation structures
    /// </exception>
    private unsafe void InitialiseFrameSyncStructures(FrameData frameData) {
        var fenceInfo = VkShortcuts.CreateFenceCreateInfo(FenceCreateFlags.SignaledBit);
        var semaphoreInfo = VkShortcuts.CreateSemaphoreCreateInfo();

        if (_vk.CreateFence(_device, fenceInfo, null, out frameData.renderFence) != Result.Success) {
            throw new DatRendererException("Failed to create frame render fence");
        }

        if (_vk.CreateSemaphore(_device, semaphoreInfo, null, out frameData.swapchainSemaphore) != Result.Success) {
            throw new DatRendererException("Failed to create frame swapchain semaphore");
        }

        if (_vk.CreateSemaphore(_device, semaphoreInfo, null, out frameData.renderSemaphore) != Result.Success) {
            throw new DatRendererException("Failed to create frame render semaphore");
        }
    }

    private unsafe void InitialiseFrameImages() {
        Logger.EngineLogger.Debug("Initialising Frame Images");
        var drawImageExtent = new Extent3D((uint?) EngineCVars.WindowWidthCvar.value, (uint?) EngineCVars.WindowHeightCvar.value, 1);

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
            throw new DatRendererException("Failed to create render image view");
        }

        _drawImage = new AllocatedImage(image, imageView, allocation, drawImageExtent, format);
        _drawExtent = new Extent2D(drawImageExtent.Width, drawImageExtent.Height);
    }

    private unsafe void InitialiseExtraCommands() {
        CommandPoolCreateInfo commandPoolInfo = new() {
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _graphicsQueueIndex
        };

        // Graphics Queue
        if (_vk.CreateCommandPool(_device, commandPoolInfo, null, out _immGraphicsCommandPool) != Result.Success) {
            throw new DatRendererException("Failed to create immediate graphics command pool");
        }

        var bufferAllocateInfo = VkShortcuts.CreateCommandBufferAllocateInfo(_immGraphicsCommandPool, 1);

        if (_vk.AllocateCommandBuffers(_device, bufferAllocateInfo, out _immGraphicsCommandBuffer) != Result.Success) {
            throw new DatRendererException("Failed to create immediate graphics command buffer");
        }

        // Transfer Queue
        commandPoolInfo.QueueFamilyIndex = _transferQueueIndex;

        if (_vk.CreateCommandPool(_device, commandPoolInfo, null, out _immTransferCommandPool) != Result.Success) {
            throw new DatRendererException("Failed to create immediate transfer command pool");
        }

        bufferAllocateInfo.CommandPool = _immTransferCommandPool;

        if (_vk.AllocateCommandBuffers(_device, bufferAllocateInfo, out _immTransferCommandBuffer) != Result.Success) {
            throw new DatRendererException("Failed to create immediate transfer buffer");
        }
    }

    private unsafe void InitialiseExtraSyncStructures() {
        var fenceInfo = VkShortcuts.CreateFenceCreateInfo(FenceCreateFlags.SignaledBit);

        if (_vk.CreateFence(_device, fenceInfo, null, out _immFence) != Result.Success) {
            throw new DatRendererException("Failed to create frame render fence");
        }
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
            DstBinding = 0,
            DstSet = _drawImageDescriptor,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageImage,
            PImageInfo = &imageInfo
        };

        _vk.UpdateDescriptorSets(_device, 1, &drawImageWrite, 0, null);
    }

    private void InitialisePipelines() {
        Logger.EngineLogger.Debug("Initialising Pipelines");
        InitialiseBackgroundPipelines();
        InitialiseMeshPipeline();
    }
    private unsafe void InitialiseBackgroundPipelines() {
        fixed (DescriptorSetLayout* setLayout = &_drawImageDescriptorLayout) {
            var computeLayout = new PipelineLayoutCreateInfo {
                SetLayoutCount = 1,
                PSetLayouts = setLayout,
            };

            var pushConstant = new PushConstantRange {
                Offset = 0,
                Size = (uint) sizeof(ComputePushConstants),
                StageFlags = ShaderStageFlags.ComputeBit
            };

            computeLayout.PushConstantRangeCount = 1;
            computeLayout.PPushConstantRanges = &pushConstant;

            if (_vk.CreatePipelineLayout(_device, computeLayout, null, out _gradientPipelineLayout) != Result.Success) {
                throw new DatRendererException("Failed to initialise gradient Pipeline Layout");
            }
        }

        var computeDrawShader = VkHelper.LoadShaderModel(_vk,
            _device,
            "/home/jacob/Projects/Dotnet/dat-sharp-engine/dat-sharp-engine/Assets/Shaders/comp.spv"
        );

        const string entryPoint = "main";
        var pEntryPoint = Marshal.StringToHGlobalAnsi(entryPoint);

        var stageInfo = new PipelineShaderStageCreateInfo {
            Stage = ShaderStageFlags.ComputeBit,
            Module = computeDrawShader,
            PName = (byte*) pEntryPoint
        };

        var computePipelineInfo = new ComputePipelineCreateInfo {
            Layout = _gradientPipelineLayout,
            Stage = stageInfo
        };

        if (_vk.CreateComputePipelines(_device, default, 1, computePipelineInfo, null, out _gradientPipeline) != Result.Success) {
            throw new DatRendererException("Failed to initialise gradient Pipeline Layout");
        }

        Marshal.FreeHGlobal(pEntryPoint);

        _vk.DestroyShaderModule(_device, computeDrawShader, null);
    }

    private unsafe void InitialiseTrianglePipeline() {
        var triangleFrag = VkHelper.LoadShaderModel(_vk,
            _device,
            "/home/jacob/Projects/Dotnet/dat-sharp-engine/dat-sharp-engine/Assets/Shaders/colouredTriangle.frag.spv"
        );
        var triangleVert = VkHelper.LoadShaderModel(_vk,
            _device,
            "/home/jacob/Projects/Dotnet/dat-sharp-engine/dat-sharp-engine/Assets/Shaders/colouredTriangle.vert.spv"
        );

        var pipelineLayout = new PipelineLayoutCreateInfo {
            SetLayoutCount = 0,
            PSetLayouts = null,
        };

        if (_vk.CreatePipelineLayout(_device, pipelineLayout, null, out _trianglePipelineLayout) != Result.Success) {
            throw new DatRendererException("Failed to initialise gradient Pipeline Layout");
        }

        _trianglePipeline = new PipelineBuilder(_vk, _device)
            .SetPipelineLayout(_trianglePipelineLayout)
            .AddDefaultGraphicsShaderStages(triangleVert, triangleFrag)
            .SetInputTopology(PrimitiveTopology.TriangleList)
            .SetPolygonMode(PolygonMode.Fill)
            .SetCullMode(CullModeFlags.None, FrontFace.Clockwise)
            .DisableMultisampling()
            .DisableBlending()
            .DisableDepthTest()
            .AddColourAttachment(_drawImage.format)
            .SetDepthFormat(Format.Undefined)
            .Build();

        _vk.DestroyShaderModule(_device, triangleFrag, null);
        _vk.DestroyShaderModule(_device, triangleVert, null);
    }

    private unsafe void InitialiseMeshPipeline() {
        var triangleFrag = VkHelper.LoadShaderModel(_vk,
            _device,
            "/home/jacob/Projects/Dotnet/dat-sharp-engine/dat-sharp-engine/Assets/Shaders/colouredTriangle.frag.spv"
        );
        var triangleVert = VkHelper.LoadShaderModel(_vk,
            _device,
            "/home/jacob/Projects/Dotnet/dat-sharp-engine/dat-sharp-engine/Assets/Shaders/triangleMesh.vert.spv"
        );

        var bufferRange = new PushConstantRange {
            Offset = 0,
            Size = (uint) sizeof(DrawPushConstants),
            StageFlags = ShaderStageFlags.VertexBit,
        };

        var pipelineLayout = new PipelineLayoutCreateInfo {
            SetLayoutCount = 0,
            PSetLayouts = null,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &bufferRange
        };

        if (_vk.CreatePipelineLayout(_device, pipelineLayout, null, out _trianglePipelineLayout) != Result.Success) {
            throw new DatRendererException("Failed to initialise gradient Pipeline Layout");
        }

        _trianglePipeline = new PipelineBuilder(_vk, _device)
            .SetPipelineLayout(_trianglePipelineLayout)
            .AddDefaultGraphicsShaderStages(triangleVert, triangleFrag)
            .SetInputTopology(PrimitiveTopology.TriangleList)
            .SetPolygonMode(PolygonMode.Fill)
            .SetCullMode(CullModeFlags.None, FrontFace.Clockwise)
            .DisableMultisampling()
            .DisableBlending()
            .DisableDepthTest()
            .AddColourAttachment(_drawImage.format)
            .SetDepthFormat(Format.Undefined)
            .Build();

        _vk.DestroyShaderModule(_device, triangleFrag, null);
        _vk.DestroyShaderModule(_device, triangleVert, null);
    }

    private void InitialiseMesh() {
        _tempMesh3d = AssetManager.instance.GetAsset<Mesh3d>("Primitives/Plane.datmesh");
        _tempMesh3d.AcquireGpuAsset();
    }

    /* --------------------------------------- */
    /* Queue                                   */
    /* --------------------------------------- */

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

        // Transition draw for graphics pipeline
        VkHelper.TransitionImage(_vk,
            currentFrameData.commandBuffer,
            _drawImage.image,
            ImageLayout.General,
            ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags2.ComputeShaderBit,
            PipelineStageFlags2.FragmentShaderBit,
            AccessFlags2.MemoryWriteBit,
            AccessFlags2.MemoryReadBit);

        DrawGeometry(_drawImage, currentFrameData);

        // Transition draw and swapchain images for transfer
        VkHelper.TransitionImage(_vk,
            currentFrameData.commandBuffer,
            _drawImage.image,
            ImageLayout.ColorAttachmentOptimal,
            ImageLayout.TransferSrcOptimal,
            PipelineStageFlags2.FragmentShaderBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            AccessFlags2.MemoryWriteBit,
            AccessFlags2.MemoryReadBit);

        VkHelper.TransitionImage(_vk,
            currentFrameData.commandBuffer,
            swapchainData.image,
            ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal,
            PipelineStageFlags2.FragmentShaderBit,
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

        ComputePushConstants pushConstants = new(new Vector4D<float>(1, 0, 0, 1), new Vector4D<float>(0, 0, 1, 1));

        _vk.CmdPushConstants(currentFrameData.commandBuffer, _gradientPipelineLayout, ShaderStageFlags.ComputeBit, 0, (uint) sizeof(ComputePushConstants), ref pushConstants);

        _vk.CmdDispatch(currentFrameData.commandBuffer,
            (uint) Math.Ceiling(_drawExtent.Width / 16.0),
            (uint) Math.Ceiling(_drawExtent.Height / 16.0),
            1
        );
    }

    private unsafe void DrawGeometry(AllocatedImage drawImage, FrameData currentFrameData) {
        var colorAttachment = VkShortcuts.CreateRenderingAttachmentInfo(drawImage.imageView, null);

        var renderingInfo = VkShortcuts.CreateRenderingInfo(_drawExtent, &colorAttachment, null);
        _vk.CmdBeginRendering(currentFrameData.commandBuffer, renderingInfo);

        var viewport = new Viewport {
            X = 0,
            Y = 0,
            Width = _drawExtent.Width,
            Height = _drawExtent.Height,
            MinDepth = 0,
            MaxDepth = 1
        };
        _vk.CmdSetViewport(currentFrameData.commandBuffer, 0, 1, viewport);

        var scissor = new Rect2D {
            Offset = new Offset2D {
                X = 0,
                Y = 0
            },
            Extent = _drawExtent
        };
        _vk.CmdSetScissor(currentFrameData.commandBuffer, 0, 1, scissor);

        _vk.CmdBindPipeline(currentFrameData.commandBuffer, PipelineBindPoint.Graphics, _trianglePipeline);

        if (_tempMesh3d.isGpuLoaded) {
            _meshList.Get(_tempMesh3d.gpuIndex, out var gpuMesh);

            var pushConstants = new DrawPushConstants(Matrix4X4<float>.Identity, gpuMesh!.vertexBufferAddress);
            _vk.CmdPushConstants(currentFrameData.commandBuffer, _trianglePipelineLayout, ShaderStageFlags.VertexBit, 0, (uint) sizeof(DrawPushConstants), ref pushConstants);
            _vk.CmdBindIndexBuffer(currentFrameData.commandBuffer, gpuMesh.indexBuffer.buffer, 0, IndexType.Uint32);

            _vk.CmdDrawIndexed(currentFrameData.commandBuffer, 6, 1, 0, 0, 0);
        }


        _vk.CmdEndRendering(currentFrameData.commandBuffer);
    }


    public delegate void ImmediateSubmission(CommandBuffer buffer);

    public unsafe void ImmediateSubmit(ImmediateSubmission method) {
        _vk.ResetFences(_device, 1, _immFence);
        _vk.ResetCommandBuffer(_immGraphicsCommandBuffer, 0);

        var commandBufferInfo =
            VkShortcuts.CreateCommandBufferBeginInfo(CommandBufferUsageFlags.OneTimeSubmitBit);

        _vk.BeginCommandBuffer(_immGraphicsCommandBuffer, commandBufferInfo);
        method(_immGraphicsCommandBuffer);
        _vk.EndCommandBuffer(_immGraphicsCommandBuffer);

        var submitInfo = VkShortcuts.CreateCommandBufferSubmitInfo(_immGraphicsCommandBuffer);
        var submit = VkShortcuts.CreateSubmitInfo(&submitInfo, null, null);

        _vk.QueueSubmit2(_graphicsQueue, 1, submit, _immFence);
        _vk.WaitForFences(_device, 1, _immFence, true, ulong.MaxValue);
    }
    
    /* --------------------------------------- */
    /* Asset Handling                          */
    /* --------------------------------------- */

    public override unsafe ulong UploadMesh(Mesh3d mesh) {
        var vertexSize = (ulong) mesh.vertices!.Length;
        var indexSize = (ulong) (mesh.indices!.Length * sizeof(uint));

        var vertexBuffer = CreateBuffer(vertexSize,
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit |
            BufferUsageFlags.ShaderDeviceAddressBit,
            MemoryUsage.GPU_Only
        );

        var vertexAddress = _vk.GetBufferDeviceAddress(_device,
            new BufferDeviceAddressInfo { Buffer = vertexBuffer.buffer }
        );

        var indexBuffer = CreateBuffer(indexSize,
            BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
            MemoryUsage.GPU_Only
        );

        // TODO: Move to dedicated transfer queue

        var stagingBuffer =
            CreateBuffer(vertexSize + indexSize, BufferUsageFlags.TransferSrcBit, MemoryUsage.CPU_Only);

        var bufferPtr = stagingBuffer.allocation.Map();

        fixed(byte* vertex = mesh.vertices) {
            Buffer.MemoryCopy(vertex, bufferPtr.ToPointer(), (ulong) stagingBuffer.allocation.Size, vertexSize);
        }

        fixed (uint* index = mesh.indices) {
            Buffer.MemoryCopy(index, (byte*) bufferPtr.ToPointer() + (int) vertexSize , indexSize, indexSize);
        }

        ImmediateSubmit((cmd) => {
            var vertexCopy = new BufferCopy {
                DstOffset = 0,
                SrcOffset = 0,
                Size = vertexSize
            };

            _vk.CmdCopyBuffer(cmd, stagingBuffer.buffer, vertexBuffer.buffer, 1, vertexCopy);

            var indexCopy = new BufferCopy {
                DstOffset = 0,
                SrcOffset = vertexSize,
                Size = indexSize
            };

            _vk.CmdCopyBuffer(cmd, stagingBuffer.buffer, indexBuffer.buffer, 1, indexCopy);
        });

        stagingBuffer.allocation.Unmap();

        DestroyBuffer(stagingBuffer);

        var newMesh = new AllocatedMesh(indexBuffer, vertexBuffer, vertexAddress);

        return _meshList.Insert(newMesh);
    }

    public override void DestroyMesh(ulong meshId) {
        if (!_meshList.Get(meshId, out var allocatedMesh)) return;

        DestroyBuffer(allocatedMesh.indexBuffer);
        DestroyBuffer(allocatedMesh.vertexBuffer);
    }

    /* --------------------------------------- */
    /* Allocation                              */
    /* --------------------------------------- */

    /// <summary>
    /// Create a new GPU Buffer
    /// </summary>
    /// <param name="allocSize">The size of the buffer</param>
    /// <param name="usage">The usage flags for the buffer</param>
    /// <param name="memoryUsage">The usage flags for the memory</param>
    /// <param name="flags">The flags for the memory allocation</param>
    /// <returns>An allocated buffer</returns>
    public AllocatedBuffer CreateBuffer(ulong allocSize, BufferUsageFlags usage, MemoryUsage memoryUsage, AllocationCreateFlags flags = AllocationCreateFlags.Mapped) {
        var bufferInfo = new BufferCreateInfo {
            Size = allocSize,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        var allocInfo = new AllocationCreateInfo {
            Usage = memoryUsage,
            Flags = flags
        };

        var buffer = _allocator!.CreateBuffer(bufferInfo, allocInfo, out var allocation);

        return new AllocatedBuffer(buffer, allocation);
    }

    /// <summary>
    /// Destroy an allocated buffer
    /// </summary>
    /// <param name="buffer">The allocated buffer to destroy</param>
    public unsafe void DestroyBuffer(AllocatedBuffer buffer) {
        _vk.DestroyBuffer(_device, buffer.buffer, null);
        _allocator!.FreeMemory(buffer.allocation);
    }

    /// <summary>
    /// Destroy an allocated image
    /// </summary>
    /// <param name="image">The allocated image to destroy</param>
    public unsafe void DestroyImage(AllocatedImage image) {
        _vk.DestroyImage(_device, image.image, null);
        _allocator!.FreeMemory(image.allocation);
    }

    /* --------------------------------------- */
    /* Cleanup                                 */
    /* --------------------------------------- */

    public override unsafe void Cleanup() {
        _vk.DeviceWaitIdle(_device);

        _vk.DestroyPipeline(_device, _trianglePipeline, null);
        _vk.DestroyPipelineLayout(_device, _trianglePipelineLayout, null);

        _vk.DestroyPipeline(_device, _gradientPipeline, null);
        _vk.DestroyPipelineLayout(_device, _gradientPipelineLayout, null);

        _vk.DestroyImageView(_device, _drawImage.imageView, null);

        DestroyImage(_drawImage);

        _vk.DestroyFence(_device, _immFence, null);

        _vk.DestroyCommandPool(_device, _immGraphicsCommandPool, null);

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

        PresentModeKHR[] options = EngineCVars.VsyncCvar.value
            ? [PresentModeKHR.FifoRelaxedKhr, PresentModeKHR.FifoKhr]
            : [PresentModeKHR.ImmediateKhr, PresentModeKHR.FifoKhr];

        return options.First(option => formats.Contains(option));
    }

    /// <summary>
    /// Get the best queue family to use for the given flags
    /// </summary>
    /// <param name="queues">The list of queue family properties to pick from</param>
    /// <param name="requiredFlags">The flags required for the queue being selected</param>
    /// <param name="usedQueues">The queues that have already been used</param>
    /// <returns>The best queue family for the flags, or -1 if there isn't one</returns>
    private static int GetBestQueue(IEnumerable<QueueFamilyProperties> queues, QueueFlags requiredFlags, uint[] usedQueues) {
        // Must have all required flags
        // Preferably as few other flags as possible
        return queues
            .Select((properties, index) => (properties, index))
            .Where(properties => {
                    return (properties.properties.QueueFlags & requiredFlags) != 0
                           && usedQueues.Count(element => element == properties.index) < properties.properties.QueueCount;
                }
            )
            .OrderBy(properties => BitOperations.PopCount((uint) properties.properties.QueueFlags))
            .Select((properties) => properties.index)
            .FirstOrDefault(-1);
    }

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
        return _frameData[_currentFrame % EngineCVars.BufferedFramesCvar.value];
    }
}
