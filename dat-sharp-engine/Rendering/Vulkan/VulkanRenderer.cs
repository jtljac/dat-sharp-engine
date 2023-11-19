using System.Runtime.InteropServices;
using System.Text;
using dat_sharp_engine.Util;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Version = System.Version;

namespace dat_sharp_engine.Rendering.Vulkan; 

public class VulkanRenderer : DatRenderer {
    private readonly Vk _vk = Vk.GetApi();
    private readonly Sdl _sdl;

    // Vulkan Instance
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    
    // Memory
    // private VmaAllocator _allocator = VmaAllocator.Null;
    
    // Queues
    
    // Debug
    private ExtDebugUtils _extDebugUtils;
    private DebugUtilsMessengerEXT _debugUtilsMessenger;

    public VulkanRenderer(DatSharpEngine datSharpEngine) : base(datSharpEngine) {
        _sdl = _datSharpEngine._sdl;
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
    /// <exception cref="Exception"></exception>
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

    private unsafe void InitialiseVulkanDevice() {
        Logger.EngineLogger.Debug("Initialising Vulkan Logical Device");
        
        
    }

    public override void Draw(float deltaTime, float gameTime) {
        // Console.WriteLine(gameTime);
    }

    public override unsafe void Cleanup() {
        _extDebugUtils.Dispose();
        _vk.DestroyInstance(_instance, null);
        _vk.Dispose();
    }
    
    
    
    /* --------------------------------------- */
    /* Debug                                   */
    /* --------------------------------------- */

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
        
        Logger.EngineLogger.Debug("Available validation layers extensions: {content}", availableLayers);

        return requestedLayers.TrueForAll((layer) => availableLayers.Contains(layer));
    }
}