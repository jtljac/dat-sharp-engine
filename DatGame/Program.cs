// See https://aka.ms/new-console-template for more information

using DatEngine;
using DatEngine.Gpu.Vulkan;

DatSharpEngine datSharpEngine = new(new VulkanGpu());

datSharpEngine
    .Init()
    .StartLoop();