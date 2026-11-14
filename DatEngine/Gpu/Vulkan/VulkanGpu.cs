using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.SdlApi;
using Brutal.Strings;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using DatEngine.Engine;
using DatEngine.Util;

namespace DatEngine.Gpu.Vulkan;

public class VulkanGpu : BaseGpu {
    private static readonly string[] VulkanInstanceExtensions = [
        "VK_KHR_get_surface_capabilities2"
    ];

    // Vulkan Instance Objects
    /// <summary>
    /// The instance of Vulkan used for interacting with Vulkan
    /// </summary>
    protected Instance instance = null!;
    /// <summary>
    /// Handle for the debug messenger
    /// </summary>
    protected VkDebugUtilsMessengerEXT debugMessenger;
    /// <summary>
    /// The physical GPU that this Vulkan instance will use
    /// </summary>
    protected PhysicalDevice physicalDevice = null!;
    /// <summary>
    /// The logical representation of the device for accessing and creating resources
    /// </summary>
    protected Device device = null!;

    /// <summary>
    /// The handle for creating and managing Memory on the GPU
    /// </summary>
    protected VmaAllocator allocator = null!;

    // Queues
    /// <summary>
    /// The queue to use for graphics commands
    /// </summary>
    /// <remarks>
    /// If the <see cref="transferQueue"/> or <see cref="computeQueue"/> do not have a dedicated queue, they will return
    /// this graphics queue
    /// </remarks>
    protected VulkanQueue graphicsQueue;

    /// <value>
    /// True when there isn't an independent transfer queue, so transfer operations have to go through
    /// the <see cref="graphicsQueue"/>
    /// </value>
    protected bool unifiedTransferQueue;

    /// <summary>
    /// The queue to use for transfers between the CPU and GPU memory
    /// </summary>
    protected VulkanQueue? transferQueue {
        get => unifiedTransferQueue ? graphicsQueue : field;
        set;
    }

    /// <value>
    /// True when there isn't an independent compute queue, so compute operations have to go through
    /// the <see cref="graphicsQueue"/>
    /// </value>
    protected bool unifiedComputeQueue;

    /// <summary>
    /// The queue to use for async compute operations
    /// </summary>
    protected VulkanQueue? computeQueue {
        get => unifiedComputeQueue ? graphicsQueue : field;
        set;
    }

    // Windows
    /// <summary>
    /// The windows that Vulkan is set up to render to
    /// </summary>
    protected Dictionary<uint, VulkanWindow> windows = [];

    /// <summary>
    /// The main window
    /// </summary>
    protected VulkanWindow? primaryWindow => windows.Count > 0 ? windows[0] : null;

    /* --------------------------------------- */
    /* Instance Initialisation                 */
    /* --------------------------------------- */

    /**
     * <summary>
     * Initialise the Vulkan GPU Handler, and all it's required components
     * </summary>
     */
    public override void Init() {
        Logger.EngineLogger.Trace("Initializing Vulkan");

        instance = InitialiseInstance();
#if DEBUG
        debugMessenger = InitialiseDebugMessenger();
#endif
        physicalDevice = InitialisePhysicalDevice();
        InitialiseLogicalDevice();
        allocator = InitialiseVma();
    }

    /**
     * <summary>
     * Set up the Vulkan driver and retrieve the instance
     * </summary>
     * <returns>A new Vulkan Instance</returns>
     */
    private unsafe Instance InitialiseInstance() {
        Logger.EngineLogger.Trace("Initialising Vulkan instance");

        // Collect Extensions
        var instanceExtensions = new HashSet<string>();
        var validationLayers = new HashSet<string>();

        VulkanHelpers.AddPlatformRequiredExtensions(instanceExtensions);

        foreach (var instanceExtension in Sdl.Vulkan.GetInstanceExtensions()) {
            instanceExtensions.Add(instanceExtension);
        }

        foreach (var instanceExtension in VulkanInstanceExtensions) {
            instanceExtensions.Add(instanceExtension);
        }

#if DEBUG
        // In debug mode, we want to load the validation layers
        VulkanHelpers.AddDebugExtensions(instanceExtensions);
        VulkanHelpers.AddValidationLayers(validationLayers);

        Logger.EngineLogger.Debug("Loaded Validation Layers:");
        foreach (var layer in validationLayers) {
            Logger.EngineLogger.Debug("\t{}", layer);
        }
#endif

        Logger.EngineLogger.Debug("Loaded Instance Extensions:");
        foreach (var instanceExtension in instanceExtensions) {
            Logger.EngineLogger.Debug("\t{}", instanceExtension);
        }
        fixed (byte* pAppName = new String8("Test App"))
        fixed (byte* pEngineName = new String8("Dat Brutal Engine")) {
            return Brutal.VulkanApi.Vulkan.CreateInstance(new Instance.CreateInfo {
                    AppInfo = new VkApplicationInfo {
                        ApplicationName = pAppName,
                        EngineName = pEngineName,
                        ApplicationVersion = Brutal.VulkanApi.Vulkan.MakeApiVersion(0, 1, 0, 0),
                        EngineVersion = Brutal.VulkanApi.Vulkan.MakeApiVersion(0, 1, 0, 0),
                        ApiVersion = (int) VulkanHelpers.Api.VERSION_1_3,
                    },
                    Flags = VulkanHelpers.GetPlatformRequiredInstanceCreateFlags(),
                    EnabledExtensions = instanceExtensions,
                    EnabledLayers = validationLayers
                }
            );
        }
    }

    /// <summary>
    /// Register the debug message handler
    /// </summary>
    /// <returns>A new Debug messenger Instance</returns>
    private VkDebugUtilsMessengerEXT InitialiseDebugMessenger() {
        Logger.EngineLogger.Trace("Initialising Vulkan Debug Messenger");

        unsafe {
            return instance.CreateDebugUtilsMessengerEXT(new VkDebugUtilsMessengerCreateInfoEXT {
                    MessageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.ErrorBitEXT |
                                      VkDebugUtilsMessageSeverityFlagsEXT.WarningBitEXT,
                    MessageType = VkDebugUtilsMessageTypeFlagsEXT.ValidationBitEXT |
                                  VkDebugUtilsMessageTypeFlagsEXT.PerformanceBitEXT |
                                  VkDebugUtilsMessageTypeFlagsEXT.DeviceAddressBindingBitEXT,
                    PfnUserCallback = new PfnVkDebugUtilsMessengerCallbackEXT(&VulkanDebugCallback),
                },
                null
            );
        }
    }

    /// <summary>
    /// Select a physical GPU from the system to use for Vulkan
    /// </summary>
    /// <returns>A physical device handle</returns>
    private PhysicalDevice InitialisePhysicalDevice() {
        Logger.EngineLogger.Trace("Initialising Vulkan Physical Device");

        // TODO: Selection Process
        return instance.PhysicalDevices[0];
    }

    /// <summary>
    /// Select the queues and set up the logical device
    /// </summary>
    private unsafe void InitialiseLogicalDevice() {
        Logger.EngineLogger.Trace("Initialising Vulkan Logical Device");

        // Find all the queue families
        var queueFamilies = VkHelper.GetQueueFamilyProperties(physicalDevice);
        var usedQueues = new int[queueFamilies.Length];

        // We're just gonna assume this supports present
        var graphicsQueueIndex = GetBestQueue(queueFamilies, VkQueueFlags.GraphicsBit, usedQueues);
        usedQueues[graphicsQueueIndex]++;

        var computeQueueIndex = GetBestQueue(queueFamilies, VkQueueFlags.ComputeBit, usedQueues);
        if (computeQueueIndex != -1) usedQueues[computeQueueIndex]++;
        else unifiedComputeQueue = true;

        var transferQueueIndex = GetBestQueue(queueFamilies, VkQueueFlags.TransferBit, usedQueues);

        // Since transfer queue ability is guaranteed if the Graphics or compute bit is set, it's optional to set the
        // transfer flag in these cases, so just in case we'll try grabbing a compute queue, then a graphics queue
        if (transferQueueIndex == -1) {
            foreach (var queueFlag in (VkQueueFlags[]) [VkQueueFlags.ComputeBit, VkQueueFlags.GraphicsBit]) {
                transferQueueIndex = GetBestQueue(queueFamilies, queueFlag, usedQueues);

                if (transferQueueIndex != -1) break;
            }
        }

        if (transferQueueIndex != -1) usedQueues[transferQueueIndex]++;
        else unifiedTransferQueue = true;

        // Extensions
        var deviceExtensions = new HashSet<string>();
        VulkanHelpers.AddSwapchainExtensions(deviceExtensions);
        VulkanHelpers.AddMandatoryIfSupportedDeviceExtensions(deviceExtensions, physicalDevice);

        // Load features
        var shaderDrawParametersFeatures = new VkPhysicalDeviceShaderDrawParametersFeatures {
            ShaderDrawParameters = true
        };

        var features12 = new VkPhysicalDeviceVulkan12Features {
            BufferDeviceAddress = true,
            DescriptorIndexing = true,
            Next = &shaderDrawParametersFeatures
        };

        var features13 = new VkPhysicalDeviceVulkan13Features {
            DynamicRendering = true,
            Synchronization2 = true,
            Next = &features12
        };

        // We only need 3 since the max number of queues will be 3
        var queuePriorities = stackalloc float[]{1.0f, 1.0f, 1.0f};

        var queueCreateInfos = usedQueues
            .Select((queueCount, index) => (queueCount, index))
            .Where(queue => queue.queueCount > 0)
            .Select(queue => new VkDeviceQueueCreateInfo {
                    QueueFamilyIndex = queue.index,
                    QueueCount = queue.queueCount,
                    QueuePriorities = queuePriorities
                }
            )
            .ToArray();

        device = physicalDevice.CreateDevice(new DeviceEx.CreateInfo {
                Extensions = deviceExtensions,
                QueueCreateInfos = queueCreateInfos,
                Next = &features13
            }
        );

        // Extract Queues
        if (!unifiedTransferQueue) {
            transferQueue = new VulkanQueue(
                transferQueueIndex,
                device.GetQueue(transferQueueIndex, --usedQueues[transferQueueIndex])
            );
        }

        if (!unifiedComputeQueue) {
            computeQueue = new VulkanQueue(
                computeQueueIndex,
                device.GetQueue(computeQueueIndex, --usedQueues[computeQueueIndex])
            );
        }

        graphicsQueue = new VulkanQueue(
            graphicsQueueIndex,
            device.GetQueue(graphicsQueueIndex, --usedQueues[graphicsQueueIndex])
        );
    }

    /// <summary>
    /// Set up the Memory allocator for managing GPU Memory
    /// </summary>
    /// <returns>An allocator for the <see cref="device"/></returns>
    private VmaAllocator InitialiseVma() {
        Logger.EngineLogger.Trace("Initializing VMA");

        return device.CreateVmaAllocator(new VmaAllocatorCreateInfo {
            PhysicalDevice = physicalDevice,
            Device = device,
            Instance = instance,
            VulkanApiVersion = (uint) VulkanHelpers.Api.VERSION_1_3
        });
    }

    /* --------------------------------------- */
    /* Window Initialisation                   */
    /* --------------------------------------- */

    /// <summary>
    /// Set up the surface for rendering to
    /// </summary>
    /// <param name="sdlWindow">The SDL handle that creates the surface</param>
    /// <param name="vulkanWindow">The Vulkan window to store the surface in</param>
    /// <exception cref="DatGpuException">Failed to create the surface for rendering to</exception>
    private void InitialiseWindowSurface(SdlWindow sdlWindow, VulkanWindow vulkanWindow) {
        Logger.EngineLogger.Trace($"Initialising window<{vulkanWindow.windowHandle}> surface");

        // Get surface from SDL
        try {
            vulkanWindow.surface = new VkSurfaceKHR(Sdl.Vulkan.CreateSurface(sdlWindow, instance.VkHandle));
        } catch (SdlException exception) {
            throw new DatGpuException($"Failed to create surface for window {vulkanWindow.windowHandle}", exception);
        }
    }

    /// <summary>
    /// Set up the swapchain
    /// </summary>
    /// <param name="vulkanWindow">The window to set up the swapchain for</param>
    private void InitialiseWindowSwapchain(VulkanWindow vulkanWindow) {
        Logger.EngineLogger.Trace($"Initialising window<{vulkanWindow.windowHandle}> swapchain");

        var capabilities = VkHelper.GetSurfaceCapabilities(physicalDevice, vulkanWindow.surface);
        var format = GetBestSwapchainFormat(vulkanWindow);

        // Wayland returns 0xFFFFFFFF (-1) to indicate that the surface extent will be determined by the window size
        vulkanWindow.swapchainExtent = capabilities.SurfaceCapabilities.CurrentExtent.Width == -1
            ? new VkExtent2D(WindowManager.primaryWindowSize.value.X, WindowManager.primaryWindowSize.value.Y)
            : capabilities.SurfaceCapabilities.CurrentExtent;
        vulkanWindow.swapchainFormat = format.Format;

        var bufferedImages = Math.Clamp(bufferedFrames,
            capabilities.SurfaceCapabilities.MinImageCount,
            capabilities.SurfaceCapabilities.MaxImageCount == 0
                ? int.MaxValue
                : capabilities.SurfaceCapabilities.MaxImageCount
        );

        vulkanWindow.swapchain = device.CreateSwapchainKHR(new VkSwapchainCreateInfoKHR {
            Surface = vulkanWindow.surface,
            MinImageCount =  bufferedImages,
            ImageFormat = vulkanWindow.swapchainFormat,
            ImageColorSpace = format.ColorSpace,
            ImageExtent = vulkanWindow.swapchainExtent,
            ImageArrayLayers = 1,
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransferDstBit,
            PresentMode = GetIdealPresentMode(vulkanWindow),
            PreTransform = VkSurfaceTransformFlagsKHR.IdentityBitKHR,
            CompositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueBitKHR
        }, null);

        // With the swapchain created, we still need to get our images
        vulkanWindow.swapchainImages = [..
            VkHelper.GetSwapchainImages(device, vulkanWindow.swapchain).Select(image => new SwapchainImages(
                image,
                device.CreateImageView(new VkImageViewCreateInfo {
                        Image = image,
                        ViewType = VkImageViewType._2D,
                        Format = vulkanWindow.swapchainFormat,
                        SubresourceRange = {
                            AspectMask = VkImageAspectFlags.ColorBit,
                            BaseMipLevel = 0,
                            LevelCount = 1,
                            LayerCount = 1
                        }
                    },
                    null
                ),
                device.CreateSemaphore(new VkSemaphoreCreateInfo(), null)
            )
        )];
    }

    /* --------------------------------------- */
    /* Init Helpers                            */
    /* --------------------------------------- */

    /// <summary>
    /// Get the best queue available for the given required queue flags.
    ///
    /// <para>
    /// This tries to select the queue that has all the required flags, and the least number of flags, I.e. the most
    /// specific queue.
    /// </para>
    /// </summary>
    /// <param name="queueFamilies">A list of <see cref="VkQueueFamilyProperties"/> available on the <see cref="physicalDevice"/></param>
    /// <param name="requiredFlags">The flags that are minimum required for the sought after queue</param>
    /// <param name="usedQueues">An array mapping queue family index to the number of queues from that family already used</param>
    /// <returns>The queue family index of the found queue</returns>
    protected static int GetBestQueue(VkQueueFamilyProperties2[] queueFamilies, VkQueueFlags requiredFlags, int[] usedQueues) {
        return queueFamilies
            .Select((properties, index) => (properties, index))
            .Where((properties) =>
                (properties.properties.QueueFamilyProperties.QueueFlags & requiredFlags) != 0
                    && usedQueues[properties.index] < properties.properties.QueueFamilyProperties.QueueCount
            )
            .OrderBy(properties => BitOperations.PopCount((uint) properties.properties.QueueFamilyProperties.QueueFlags))
            .Select(properties => properties.index)
            .FirstOrDefault(-1);
    }

    /// <summary>
    /// Get the best available swapchain format for the given <paramref name="vulkanWindow"/>'s surface
    /// </summary>
    /// <param name="vulkanWindow">The Window to get the best format for</param>
    /// <returns>The best available swapchain format</returns>
    protected VkSurfaceFormatKHR GetBestSwapchainFormat(VulkanWindow vulkanWindow) {
        var formats = VkHelper.GetSurfaceFormats(physicalDevice, vulkanWindow.surface);

        foreach (var format in formats) {
            if (format is {
                    // This one is basically guaranteed
                    SurfaceFormat: { Format: VkFormat.B8G8R8SRGB, ColorSpace: VkColorSpaceKHR.SRGBNonlinearKHR }
                }) {
                return format.SurfaceFormat;
            }
        }

        // Sometimes it's not basically guaranteed, fallback to whatever is first in the array
        // TODO: Maybe we can pick slightly better
        return formats[0].SurfaceFormat;
    }

    /// <summary>
    /// Get the best available present mode for the given <paramref name="vulkanWindow"/>'s surface
    /// </summary>
    /// <param name="vulkanWindow">The Window to get the best present mode for</param>
    /// <returns>The best available present mode</returns>
    protected VkPresentModeKHR GetIdealPresentMode(VulkanWindow vulkanWindow) {
        var availablePresentModes = VkHelper.GetSurfacePresentModes(physicalDevice, vulkanWindow.surface);

        // Prefer Relaxed, Neither are guaranteed though
        // Note we want to avoid mailbox as we don't need to utterly saturate the GPU
        foreach (var presentMode in availablePresentModes) {
            if (enableVsync
                    ? presentMode == VkPresentModeKHR.FifoRelaxedKHR
                    : presentMode == VkPresentModeKHR.ImmediateKHR) {
                return presentMode;
            }
        }

        // We're guaranteed to have Fifo if we've gotten this far
        return VkPresentModeKHR.FifoKHR;
    }

    /* --------------------------------------- */
    /* Debug Handling                          */
    /* --------------------------------------- */

    /// <summary>
    /// The Callback used for validation layer messages
    /// </summary>
    /// <param name="severityFlags">The flags for the severity of the message</param>
    /// <param name="messageTypeFlags">The flags for the type of the message</param>
    /// <param name="pCallbackData">The message data</param>
    /// <param name="pUserData">An attached pointer for user data</param>
    /// <returns>Always false</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe Bool32 VulkanDebugCallback(VkDebugUtilsMessageSeverityFlagsEXT severityFlags,
        VkDebugUtilsMessageTypeFlagsEXT messageTypeFlags,
        VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData) {
        var message = Marshal.PtrToStringUTF8((nint) pCallbackData->Message);
        Logger.EngineLogger.Warn("Vulkan | {Flags} | {Message}", severityFlags, message);

        return false;
    }


    /* --------------------------------------- */
    /* Clean-up                                */
    /* --------------------------------------- */

    public override void Cleanup() {
        // Finish up before we start ripping out the memory
        device.WaitIdle();

        // Allocations

        // Windows
        foreach (var vulkanWindow in windows.Values) {
            vulkanWindow.Cleanup(instance, device);
        }
        windows.Clear();

        device.DestroyVmaAllocator(allocator);
        device.Dispose();

        instance.DestroyDebugUtilsMessengerEXT(debugMessenger, null);
        instance.Dispose();
    }

    /* --------------------------------------- */
    /* Window Handling                         */
    /* --------------------------------------- */

    /// <inheritdoc/>
    public override void RegisterWindow(uint windowId, SdlWindow sdlWindow) {
        Logger.EngineLogger.Trace($"Initialising window<{windowId}> in Vulkan");
        VulkanWindow vulkanWindow = new(windowId);
        windows.Add(windowId, vulkanWindow);

        InitialiseWindowSurface(sdlWindow, vulkanWindow);

        InitialiseWindowSwapchain(vulkanWindow);


    }
}