using System.Runtime.InteropServices;
using dat_sharp_engine.Rendering.Util;
using Silk.NET.Vulkan;

namespace dat_sharp_engine.Rendering.Vulkan;

public class PipelineBuilder {
    private readonly Vk _vk;
    private readonly Device _device;

    private List<PipelineShaderStageCreateInfo> _shaderStages = [];
    private List<Format> _colourAttachmentFormats = [];

    private PipelineInputAssemblyStateCreateInfo _inputAssembly;
    private PipelineRasterizationStateCreateInfo _rasterizer;
    private PipelineColorBlendAttachmentState _colorBlendAttachmentState;
    private PipelineMultisampleStateCreateInfo _multisampleState;
    private PipelineLayout _pipelineLayout;
    private PipelineDepthStencilStateCreateInfo _depthStencil;
    private PipelineRenderingCreateInfo _renderInfo;

    public PipelineBuilder(Vk vk, Device device) {
        _vk = vk;
        _device = device;

        Clear();
    }

    ~PipelineBuilder() {
        CleanupShaderStages();
    }

    private unsafe void CleanupShaderStages() {
        foreach (var pipelineShaderStageCreateInfo in _shaderStages) {
            Marshal.FreeHGlobal((IntPtr) pipelineShaderStageCreateInfo.PName);
        }
    }

    private unsafe void Clear() {
        CleanupShaderStages();
        _shaderStages.Clear();

        _inputAssembly = new PipelineInputAssemblyStateCreateInfo(StructureType.PipelineInputAssemblyStateCreateInfo);
        _rasterizer = new PipelineRasterizationStateCreateInfo(StructureType.PipelineRasterizationStateCreateInfo);
        _colorBlendAttachmentState = new PipelineColorBlendAttachmentState();
        _multisampleState = new PipelineMultisampleStateCreateInfo(StructureType.PipelineMultisampleStateCreateInfo);
        _pipelineLayout = new PipelineLayout();
        _depthStencil = new PipelineDepthStencilStateCreateInfo(StructureType.PipelineDepthStencilStateCreateInfo);
        _renderInfo = new PipelineRenderingCreateInfo(StructureType.PipelineRenderingCreateInfoKhr);
    }

    public unsafe PipelineBuilder AddShaderStage(ShaderStageFlags stageFlags, ShaderModule shaderModule, string entryName = "main") {
        var pEntryPoint = Marshal.StringToHGlobalAnsi(entryName);

        _shaderStages.Add(new PipelineShaderStageCreateInfo {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = stageFlags,
            Module = shaderModule,
            PName = (byte*) pEntryPoint,
        });

        return this;
    }

    /// <summary>
    ///
    /// <para/>
    /// Note: The memory behind PName will be cleared for you when the shader stages are next cleared, or this pipeline
    /// builder is garbage collected
    /// </summary>
    /// <param name="shaderStageCreateInfo"></param>
    /// <returns></returns>
    public PipelineBuilder AddShaderStage(PipelineShaderStageCreateInfo shaderStageCreateInfo) {
        _shaderStages.Add(shaderStageCreateInfo);

        return this;
    }

    public PipelineBuilder AddDefaultGraphicsShaderStages(ShaderModule vertexShader, ShaderModule fragmentShader,
        string vertexEntry = "main", string fragmentEntry = "main") {
        CleanupShaderStages();
        _shaderStages.Clear();

        AddShaderStage(ShaderStageFlags.VertexBit, vertexShader, vertexEntry);
        AddShaderStage(ShaderStageFlags.FragmentBit, fragmentShader, fragmentEntry);

        return this;
    }

    public PipelineBuilder ClearShaderStages() {
        CleanupShaderStages();
        _shaderStages.Clear();

        return this;
    }

    public PipelineBuilder SetInputTopology(PrimitiveTopology topology) {
        _inputAssembly.Topology = topology;
        _inputAssembly.PrimitiveRestartEnable = false;

        return this;
    }

    public PipelineBuilder SetInputAssembly(PipelineInputAssemblyStateCreateInfo inputAssemblyState) {
        _inputAssembly = inputAssemblyState;

        return this;
    }

    public PipelineBuilder SetPipelineLayout(PipelineLayout pipelineLayout) {
        _pipelineLayout = pipelineLayout;

        return this;
    }

    public PipelineBuilder SetPolygonMode(PolygonMode mode) {
        _rasterizer.PolygonMode = mode;
        _rasterizer.LineWidth = 1;
        return this;
    }

    public PipelineBuilder SetCullMode(CullModeFlags cullMode, FrontFace frontFace) {
        _rasterizer.CullMode = cullMode;
        _rasterizer.FrontFace = frontFace;

        return this;
    }

    public PipelineBuilder SetRasterizationState(PipelineRasterizationStateCreateInfo rasterizationState) {
        _rasterizer = rasterizationState;

        return this;
    }

    public unsafe PipelineBuilder DisableMultisampling() {
        _multisampleState.SampleShadingEnable = false;
        _multisampleState.RasterizationSamples = SampleCountFlags.Count1Bit;
        _multisampleState.MinSampleShading = 1;
        _multisampleState.PSampleMask = null;

        _multisampleState.AlphaToCoverageEnable = false;
        _multisampleState.AlphaToOneEnable = false;

        return this;
    }

    public PipelineBuilder DisableBlending() {
        _colorBlendAttachmentState.ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit |
                                                    ColorComponentFlags.BBit | ColorComponentFlags.ABit;
        _colorBlendAttachmentState.BlendEnable = false;

        return this;
    }

    public PipelineBuilder AddColourAttachment(Format format) {
        _colourAttachmentFormats.Add(format);

        return this;
    }

    public PipelineBuilder ClearColourAttachments() {
        _colourAttachmentFormats.Clear();

        return this;
    }

    public PipelineBuilder SetDepthFormat(Format format) {
        _renderInfo.DepthAttachmentFormat = format;

        return this;
    }

    public PipelineBuilder SetStencilFormat(Format format) {
        _renderInfo.StencilAttachmentFormat = format;

        return this;
    }

    public PipelineBuilder DisableDepthTest() {
        _depthStencil.DepthTestEnable = false;
        _depthStencil.DepthWriteEnable = false;
        _depthStencil.DepthCompareOp = CompareOp.Never;
        _depthStencil.DepthBoundsTestEnable = false;
        _depthStencil.StencilTestEnable = false;
        _depthStencil.Front = new StencilOpState();
        _depthStencil.Back = new StencilOpState();
        _depthStencil.MinDepthBounds = 0;
        _depthStencil.MaxDepthBounds= 1;

        return this;
    }

    private static PipelineViewportStateCreateInfo BuildViewportState() {
        return new PipelineViewportStateCreateInfo {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            ScissorCount = 1
        };
    }

    private static unsafe PipelineColorBlendStateCreateInfo BuildColorBlendState(
        PipelineColorBlendAttachmentState* blendAttachmentState) {
        return new PipelineColorBlendStateCreateInfo {
            SType = StructureType.PipelineColorBlendStateCreateInfo,

            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = blendAttachmentState
        };
    }

    public unsafe Pipeline Build() {
        DynamicState[] dynamicStates = [DynamicState.Viewport, DynamicState.Scissor];

        fixed (PipelineShaderStageCreateInfo* stages = CollectionsMarshal.AsSpan(_shaderStages))
        fixed (Format* colorAttachmentFormats = CollectionsMarshal.AsSpan(_colourAttachmentFormats))
        fixed (PipelineInputAssemblyStateCreateInfo* inputAssembly = &_inputAssembly)
        fixed (PipelineRasterizationStateCreateInfo* rasterizationState = &_rasterizer)
        fixed (PipelineColorBlendAttachmentState* blendAttachment = &_colorBlendAttachmentState)
        fixed (PipelineMultisampleStateCreateInfo* multisampleState = &_multisampleState)
        fixed (PipelineDepthStencilStateCreateInfo* depthStencil= &_depthStencil)
        fixed (PipelineRenderingCreateInfo* renderInfo = &_renderInfo)
        fixed (DynamicState* dynamicState = dynamicStates) {
            var viewportState = BuildViewportState();
            var colorBlendState = BuildColorBlendState(blendAttachment);
            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
                { SType = StructureType.PipelineVertexInputStateCreateInfo };

            var dynamicInfo = new PipelineDynamicStateCreateInfo {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint) dynamicStates.Length,
                PDynamicStates = dynamicState
            };

            // These need to be set here whilst it's fixed
            _renderInfo.ColorAttachmentCount = (uint) _colourAttachmentFormats.Count;
            _renderInfo.PColorAttachmentFormats = colorAttachmentFormats;

            var pipelineCreateInfo = new GraphicsPipelineCreateInfo {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = renderInfo,

                StageCount = (uint) _shaderStages.Count,
                PStages = stages,

                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = rasterizationState,
                PMultisampleState = multisampleState,
                PColorBlendState = &colorBlendState,
                PDepthStencilState = depthStencil,
                Layout = _pipelineLayout,
                PDynamicState = &dynamicInfo
            };

            if (_vk.CreateGraphicsPipelines(_device, default, 1, pipelineCreateInfo, null, out var pipeline) !=
                Result.Success) {
                throw new Exception();
            }

            return pipeline;
        }
    }
}