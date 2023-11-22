using System.Collections.Immutable;
using System.Runtime.InteropServices;
using dat_sharp_engine.Util;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using VMASharp;

namespace dat_sharp_engine.Rendering.Vulkan;

public class VulkanRenderer : DatRenderer {
    private readonly Vk _vk = Vk.GetApi();
    private readonly Sdl _sdl;

    // Vulkan Instance
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;

    // Memory
    private VulkanMemoryAllocator _allocator;

    // Queues
    private uint _graphicsQueueIndex; // Queue index for graphics stuff
    private Queue _graphicsQueue; // Queue for graphics stuff
    private uint _transferQueueIndex; // Queue index for transferring assets to the gpu
    private Queue _transferQueue; // Queue for transferring assets to the gpu

    private KhrSurface _khrSurface;
    private SurfaceKHR _surface;

    private KhrSwapchain _khrSwapchain;
    private uint framesInFlight = 0;
    private FrameData[] _frameData;

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
        InitialiseFrameData();
    }

    /// <summary>
    /// Initialise the vulkan instance and debug validation layers
    /// </summary>
    /// <exception cref="Exception">Thrown when the instance fails to initialise</exception>
    private unsafe void InitialiseVulkanInstance() {
        Logger.EngineLogger.Debug("Initialising Vulkan instance");

        // if (_sdl.VulkanLoadLibrary() < 0) {
        //     throw new Exception("Failed to load vulkan library");
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
            throw new Exception("Failed to initialise vulkan");
        }

        // Setup debug stuff
        if (!_datSharpEngine.engineSettings.debug) return;

        if (!_vk.TryGetInstanceExtension(_instance, out _extDebugUtils))
            throw new Exception($"Could not get instance extension {ExtDebugUtils.ExtensionName}");

        _extDebugUtils!.CreateDebugUtilsMessenger(_instance, debugInfo, null, out _debugUtilsMessenger);
    }

    /// <summary>
    /// Get the required instance extensions
    /// </summary>
    /// <returns>A list of instance extensions to use</returns>
    /// <exception cref="Exception">
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
            throw new Exception("Failed to get required instance extensions");
        }

        extensions.UnionWith(names);
        return extensions;
    }

    private ISet<string> GetValidationLayers() {
        var layers = new HashSet<string> {
            "VK_LAYER_KHRONOS_validation",
            "VK_LAYER_RENDERDOC_Capture",
            "VK_LAYER_LUNARG_monitor",
            "VK_LAYER_MANGOAPP_overlay"
        };

        if (!CheckValidationLayerSupport(layers)) {
            throw new Exception("Requested validation layers were missing");
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
    /// <exception cref="Exception">Thrown when creating the physical device fails</exception>
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
        var vulkan13Features = new PhysicalDeviceVulkan13Features {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            DynamicRendering = true,
            Synchronization2 = true,
            PNext = &drawParametersFeatures
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
                throw new Exception("Failed to create device");
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
            VulkanAPIVersion = Vk.Version13
        };

        _allocator = new VulkanMemoryAllocator(vmaCreateInfo);
    }

    private unsafe void InitialiseSurface() {
        Logger.EngineLogger.Debug("Initialising Vulkan Surface");

        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface)) {
            throw new Exception($"Could not get Instance extension {KhrSurface.ExtensionName}");
        }

        VkNonDispatchableHandle handle;
        _sdl.VulkanCreateSurface(_datSharpEngine.window, _instance.ToHandle(), &handle);
        _surface = handle.ToSurface();
    }

    private void InitialiseFrameData() {
        InitialiseSwapchain();
    }

    private void InitialiseSwapchain() {
        Logger.EngineLogger.Debug("Initialising Swapchain");

        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain)) {
            throw new Exception($"Could not get device extension {KhrSwapchain.ExtensionName}");
        }

        if (_khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice,
                _surface,
                out var surfaceCapabilities
            ) != Result.Success) {
            throw new Exception("Failed to get surface capabilities");
        }

        framesInFlight = Math.Clamp(_datSharpEngine.engineSettings.bufferedFrames,
            surfaceCapabilities.MinImageCount,
            surfaceCapabilities.MaxImageCount
        );

        if (_khrSwapchain.GetDeviceGroupPresentCapabilities(_device, out var presentCapabilities) != Result.Success) {
            throw new Exception("Failed to get present capabilities");
        }
    }

    public override void Draw(float deltaTime, float gameTime) {
        // Console.WriteLine(gameTime);
    }

    public override unsafe void Cleanup() {
        _khrSwapchain.Dispose();

        _khrSurface.DestroySurface(_instance, _surface, null);
        _khrSurface.Dispose();

        _allocator.Dispose();

        _vk.DestroyDevice(_device, null);

        // May be null if not debug
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

        Logger.EngineLogger.Trace("Available validation layers extensions: {content}", availableLayers);

        return requestedLayers.All(layer => availableLayers.Contains(layer));
    }
}
