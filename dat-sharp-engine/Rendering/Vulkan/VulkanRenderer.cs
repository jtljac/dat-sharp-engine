using System.Runtime.InteropServices;
using System.Text;
using dat_sharp_engine.Util;
using Silk.NET.Core.Contexts;
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
    private uint _graphicsQueueIndex;    // Queue index for graphics stuff
    private Queue _graphicsQueue;        // Queue for graphics stuff
    private uint _transferQueueIndex;    // Queue index for transferring assets to the gpu
    private Queue _transferQueue;        // Queue for transferring assets to the gpu

    private SurfaceKHR _surface;
    
    // Debug
    private ExtDebugUtils _extDebugUtils;
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

        var layers = new List<string> {
            "VK_LAYER_KHRONOS_validation",
            "VK_LAYER_RENDERDOC_Capture",
            "VK_LAYER_LUNARG_monitor",
            "VK_LAYER_MANGOAPP_overlay"
        };
        
        Logger.EngineLogger.Debug("Selected instance extensions: {content}", extensions);

        if (!CheckValidationLayerSupport(layers)) {
            throw new Exception("Requested validation layers were missing");
        }

        var engineName = SilkMarshal.StringToPtr("DatSharpEngine");
        var applicationName = SilkMarshal.StringToPtr(_datSharpEngine.appSettings.name);

        ApplicationInfo appInfo = new() {
            SType = StructureType.ApplicationInfo,
            ApiVersion = Vk.Version13,
            EngineVersion = Vk.MakeVersion(EngineConstants.ENGINE_VERSION.Major,
                EngineConstants.ENGINE_VERSION.Minor,
                EngineConstants.ENGINE_VERSION.Patch),
            ApplicationVersion = Vk.MakeVersion(_datSharpEngine.appSettings.version.Major,
                _datSharpEngine.appSettings.version.Minor,
                _datSharpEngine.appSettings.version.Patch),
            PEngineName = (byte*)engineName,
            PApplicationName = (byte*)applicationName
        };

        DebugUtilsMessengerCreateInfoEXT debugInfo = new() {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt |
                              DebugUtilsMessageSeverityFlagsEXT.WarningBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.ValidationBitExt |
                          DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                          DebugUtilsMessageTypeFlagsEXT.DeviceAddressBindingBitExt,
            PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)VulkanDebugCallback
        };

        // Vulkan requires these as C Arrays
        var enabledLayers = SilkMarshal.StringArrayToPtr(layers.ToArray());
        var enabledExtensions = SilkMarshal.StringArrayToPtr(extensions.ToArray());

        InstanceCreateInfo instanceInfo = new() {
            SType = StructureType.InstanceCreateInfo,
            Flags = InstanceCreateFlags.None,
            EnabledLayerCount = (uint)layers.Count,
            PpEnabledLayerNames = (byte**)enabledLayers,
            EnabledExtensionCount = (uint)extensions.Count,
            PpEnabledExtensionNames = (byte**)enabledExtensions,
            PApplicationInfo = &appInfo,
            PNext = &debugInfo
        };

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
        if(!_vk.TryGetInstanceExtension(_instance, out _extDebugUtils))
            throw new Exception($"Could not get instance extension {ExtDebugUtils.ExtensionName}");
        _extDebugUtils.CreateDebugUtilsMessenger(_instance, debugInfo, null, out _debugUtilsMessenger);
    }

    /// <summary>
    /// Get the required instance extensions
    /// </summary>
    /// <returns>A list of instance extensions to use</returns>
    /// <exception cref="Exception">
    /// Thrown when the engine fails to get the required instance extensions from SDL
    /// </exception>
    private unsafe List<string> GetInstanceExtensions() {
        var extensions = new List<string> {
            ExtDebugUtils.ExtensionName
        };
        
        uint pCount = 0;
        _sdl.VulkanGetInstanceExtensions(_datSharpEngine.window, ref pCount, (byte**)null);

        var names = new string[pCount];

        if (_sdl.VulkanGetInstanceExtensions(_datSharpEngine.window, ref pCount, names) != SdlBool.True) {
            throw new Exception("Failed to get required instance extensions");
        }
        
        extensions.AddRange(names);
        return extensions;
    }

    /// <summary>
    /// Select a physical device for vulkan to use
    /// </summary>
    private unsafe void InitialiseVulkanPhysicalDevice() {
        Logger.EngineLogger.Debug("Selecting GPU for Vulkan");
        
        // Select a gpu
        // TODO: Better selection method
        var devices = _vk.GetPhysicalDevices(_instance);
        foreach (var gpu in devices)
        {
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
    private unsafe void SelectQueues() {
        uint queueFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, ref queueFamilyCount, null);
        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFam = queueFamilies) {
            _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, ref queueFamilyCount, queueFam);
        }

        // Find Graphics queue
        for (var i = 0u; i < queueFamilies.Length; i++) {
            var properties = queueFamilies[i];

            if (!properties.QueueFlags.HasFlag(QueueFlags.GraphicsBit)) continue;
            
            _graphicsQueueIndex = i;
            break;
        }
        
        Logger.EngineLogger.Debug("Selected Graphics Queue: {}", 
            GetQueueDescription((int) _graphicsQueueIndex, queueFamilies[_graphicsQueueIndex])
        );
        
        // Find transfer queue
        var tempTrans = -1;
        for (var i = 0; i < queueFamilies.Length; i++) {
            var properties = queueFamilies[i];

            if (!properties.QueueFlags.HasFlag(QueueFlags.TransferBit)
                || properties.QueueFlags.HasFlag(QueueFlags.GraphicsBit)
                || properties.QueueFlags.HasFlag(QueueFlags.ComputeBit)) continue;
            
            tempTrans = i;
            break;
        }

        // If we can't find a dedicated transfer queue, just use the graphics queue
        _transferQueueIndex = tempTrans != -1 ? (uint)tempTrans : _graphicsQueueIndex;
        
        Logger.EngineLogger.Debug("Selected Transfer Queue: {}", 
            GetQueueDescription((int) _transferQueueIndex, queueFamilies[_transferQueueIndex])
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
            new DeviceQueueCreateInfo() {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueCount = 1,
                QueueFamilyIndex = _graphicsQueueIndex,
                PQueuePriorities = &priority
            }
        };

        // Make sure we only request 1 queue if the transfer and graphics queues are the same
        if (_transferQueueIndex != _graphicsQueueIndex) {
            queueInfos.Add(new DeviceQueueCreateInfo() {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueCount = 1,
                QueueFamilyIndex = _transferQueueIndex,
                PQueuePriorities = &priority
            });
        }

        // Toggle on features
        PhysicalDeviceFeatures features = new PhysicalDeviceFeatures() {
            FillModeNonSolid = Vk.True
        };
        
        // Device features that require setup via PNext
        // (This would have been in it's own function but memory management needs to be handled in silly ways for garbage
        // collection reasons)
        var drawParametersFeatures = new PhysicalDeviceShaderDrawParametersFeatures() {
            SType = StructureType.PhysicalDeviceShaderDrawParametersFeatures,
            ShaderDrawParameters = true
        };
        var vulkan13Features = new PhysicalDeviceVulkan13Features() {
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
                EnabledExtensionCount = (uint)deviceExtensions.Count,
                PpEnabledExtensionNames = (byte**)enabledExtensions,
                QueueCreateInfoCount = (uint)queueInfos.Count,
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
        VkNonDispatchableHandle handle;
        _sdl.VulkanCreateSurface(_datSharpEngine.window, _instance.ToHandle(), &handle);
        _surface = handle.ToSurface();
    }

    private void InitialiseSwapchain() {
    }

    public override void Draw(float deltaTime, float gameTime) {
        // Console.WriteLine(gameTime);
    }

    public override unsafe void Cleanup() {
        _allocator.Dispose();
        _vk.DestroyDevice(_device, null);
        _extDebugUtils.Dispose();
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
        var message = Marshal.PtrToStringUTF8((nint)pCallbackData->PMessage);
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
    private unsafe bool CheckValidationLayerSupport(List<string> requestedLayers) {
        List<String> availableLayers;
        {
            uint layerCount = 0;
            _vk.EnumerateInstanceLayerProperties(ref layerCount, null);
            var properties = new LayerProperties[layerCount];
            fixed (LayerProperties* @props = properties) {
                _vk.EnumerateInstanceLayerProperties(ref layerCount, @props);
            }

            availableLayers = properties.Select((prop) => Marshal.PtrToStringUTF8((IntPtr)prop.LayerName)).ToList()!;
        }
        
        Logger.EngineLogger.Trace("Available validation layers extensions: {content}", availableLayers);

        return requestedLayers.TrueForAll((layer) => availableLayers.Contains(layer));
    }
    
    /// <summary>
    /// Print the definitions of each queue on the given device
    /// </summary>
    /// <param name="physicalDevice">The device to get the queues of</param>
    private unsafe void PrintQueues(PhysicalDevice physicalDevice) {
        uint queueFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilyCount, null);
        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFam = queueFamilies) {
            _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilyCount, queueFam);
        }
        Logger.EngineLogger.Info("Queues: ");
        for (var i = 0; i < queueFamilies.Length; i++) {
            Logger.EngineLogger.Info(GetQueueDescription(i, queueFamilies[i]));
        }
    }

    /// <summary>
    /// Get a string description of the given queue
    /// </summary>
    /// <param name="queueIndex">The index of the queue</param>
    /// <param name="properties">The properties of the queue</param>
    /// <returns>A string description of the queue</returns>
    private static string GetQueueDescription(int queueIndex, QueueFamilyProperties properties) {
        StringBuilder message =
            new StringBuilder().Append($"Index: {queueIndex}").Append($" | Count: {properties.QueueCount}");

        if (properties.QueueFlags.HasFlag(QueueFlags.GraphicsBit)) message.Append(" | Graphics");
        if (properties.QueueFlags.HasFlag(QueueFlags.ComputeBit)) message.Append(" | Compute");
        if (properties.QueueFlags.HasFlag(QueueFlags.TransferBit)) message.Append(" | Transfer");
        if (properties.QueueFlags.HasFlag(QueueFlags.ProtectedBit)) message.Append(" | Protected");
        if (properties.QueueFlags.HasFlag(QueueFlags.SparseBindingBit)) message.Append(" | Sparse Binding");
        return message.ToString();
    }
}