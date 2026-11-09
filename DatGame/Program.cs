// See https://aka.ms/new-console-template for more information

using DatEngine;
using DatEngine.Gpu.Vulkan;

DatBrutalEngine datBrutalEngine = new(new VulkanGpu());

datBrutalEngine
    .Init()
    .StartLoop();