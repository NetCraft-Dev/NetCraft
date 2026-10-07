using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace NetCraft.Gpu.Vulkan;

//ClearColorValue float RGBA clear color
public readonly record struct ClearColorValueRGBA(float R, float G, float B, float A);

//VulkanRenderPipeline Vulkan backend render pipeline
//4.3 rework removes the traditional RenderPass and uses dynamic rendering; pipeline creation uses the PipelineRenderingCreateInfoKHR PNext chain
//The attachment ImageView is passed by VulkanRenderPass (the Vulkan IRenderPass implementation) at CmdBeginRenderingKHR
public sealed unsafe class VulkanRenderPipeline : CompiledRenderPipeline
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly Format _colorFormat;
    private readonly Silk.NET.Vulkan.Pipeline _pipeline;
    private readonly PipelineLayout _pipelineLayout;
    private readonly Extent2D _extent;
    private readonly ShaderModule _vertModule;
    private readonly ShaderModule _fragModule;
    private bool _disposed;

    public Silk.NET.Vulkan.Pipeline Pipeline => _pipeline;
    public PipelineLayout PipelineLayout => _pipelineLayout;
    public Extent2D Extent => _extent;
    //ColorFormat color attachment format for VulkanRenderPass to build RenderingAttachmentInfoKHR
    public Format ColorFormat => _colorFormat;
    public ClearColorValueRGBA ClearColor { get; set; }

    internal VulkanRenderPipeline(
        Vk vk,
        Device device,
        Format swapchainFormat,
        Extent2D swapchainExtent,
        RenderPipelineDescription description) : base(description)
    {
        _vk = vk;
        _device = device;
        _extent = swapchainExtent;
        _colorFormat = swapchainFormat;
        ClearColor = new(description.ClearColor.R, description.ClearColor.G, description.ClearColor.B, description.ClearColor.A);
        var vertCode = description.VertexShaderSpirv ?? SpirvShaders.VertexShader;
        var fragCode = description.FragmentShaderSpirv ?? SpirvShaders.FragmentShader;
        _vertModule = CreateShaderModule(vertCode);
        _fragModule = CreateShaderModule(fragCode);
        _pipelineLayout = CreatePipelineLayout(description);
        _pipeline = CreateGraphicsPipeline(description);
    }

    //FromDescription creates the pipeline from the description; the target format and extent default to B8G8R8A8Unorm/800x600
    public static VulkanRenderPipeline FromDescription(VulkanGpuDevice device, RenderPipelineDescription description)
    {
        var fmt = description.TargetFormat ?? GpuImageFormat.B8G8R8A8Unorm;
        var extent = new Extent2D
        {
            Width = (uint)(description.TargetWidth > 0 ? description.TargetWidth : 800),
            Height = (uint)(description.TargetHeight > 0 ? description.TargetHeight : 600)
        };
        return new VulkanRenderPipeline(device.Api, device.Device, ToVkFormat(fmt), extent, description);
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        var createInfo = new ShaderModuleCreateInfo
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length
        };
        fixed (byte* codePtr = code)
        {
            createInfo.PCode = (uint*)codePtr;
            ShaderModule module;
            if (_vk.CreateShaderModule(_device, &createInfo, null, &module) != Result.Success)
            {
                throw new InvalidOperationException("ShaderModule creation failed");
            }
            return module;
        }
    }

    //CreatePipelineLayout takes the VkDescriptorSetLayout array from description.DescriptorLayouts to create a PipelineLayout
    //With an empty list SetLayoutCount=0 and an empty layout is still created for pure gl_VertexIndex shaders
    private PipelineLayout CreatePipelineLayout(RenderPipelineDescription description)
    {
        var layouts = description.DescriptorLayouts;
        var layoutHandles = new DescriptorSetLayout[layouts.Count];
        for (int i = 0; i < layouts.Count; i++)
        {
            if (layouts[i] is not VulkanDescriptorLayout vkLayout)
                throw new ArgumentException("DescriptorLayout must be a VulkanDescriptorLayout");
            layoutHandles[i] = vkLayout.Handle;
        }
        fixed (DescriptorSetLayout* p = layoutHandles)
        {
            var layoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)layouts.Count,
                PSetLayouts = p,
                PushConstantRangeCount = 0
            };
            PipelineLayout layout;
            if (_vk.CreatePipelineLayout(_device, &layoutInfo, null, &layout) != Result.Success)
            {
                throw new InvalidOperationException("PipelineLayout creation failed");
            }
            return layout;
        }
    }

    private Silk.NET.Vulkan.Pipeline CreateGraphicsPipeline(RenderPipelineDescription description)
    {
        var vertStage = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _vertModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };
        var fragStage = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _fragModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };
        var shaderStages = stackalloc PipelineShaderStageCreateInfo[2];
        shaderStages[0] = vertStage;
        shaderStages[1] = fragStage;

        //The vertex input layout is built from description.VertexBindings
        //Silk.NET's VertexInputBindingDescription has managed fields so stackalloc T* is impossible; array + fixed is used
        var bindings = description.VertexBindings;
        var bindingDescsArray = new VertexInputBindingDescription[bindings.Count];
        var attrDescsList = new List<VertexInputAttributeDescription>();
        for (int i = 0; i < bindings.Count; i++)
        {
            bindingDescsArray[i] = new VertexInputBindingDescription
            {
                Binding = (uint)bindings[i].Binding,
                Stride = (uint)bindings[i].Stride,
                InputRate = VertexInputRate.Vertex
            };
            foreach (var attr in bindings[i].Attributes)
            {
                attrDescsList.Add(new VertexInputAttributeDescription
                {
                    Location = (uint)attr.Location,
                    Binding = (uint)bindings[i].Binding,
                    Format = ToVkFormat(attr.Format),
                    Offset = (uint)attr.Offset
                });
            }
        }
        var attrDescsArray = attrDescsList.ToArray();

        var inputAssembly = new PipelineInputAssemblyStateCreateInfo
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = ToVkTopology(description.Topology),
            PrimitiveRestartEnable = Vk.False
        };
        var viewport = new Viewport
        {
            X = 0.0f,
            Y = 0.0f,
            Width = _extent.Width,
            Height = _extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        var scissor = new Rect2D { Offset = default, Extent = _extent };
        var viewportState = new PipelineViewportStateCreateInfo
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            //With DynamicScissorEnabled the scissor is provided dynamically by vkCmdSetScissor and PScissors is null
            PScissors = description.DynamicScissorEnabled ? null : &scissor
        };
        var rasterizer = new PipelineRasterizationStateCreateInfo
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = Vk.False,
            RasterizerDiscardEnable = Vk.False,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1.0f,
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = Vk.False
        };
        var multisampling = new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = Vk.False,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };
        var colorBlendAttachment = new PipelineColorBlendAttachmentState
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            BlendEnable = description.BlendEnabled ? Vk.True : Vk.False
        };
        if (description.BlendEnabled)
        {
            colorBlendAttachment.SrcColorBlendFactor = BlendFactor.SrcAlpha;
            colorBlendAttachment.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
            colorBlendAttachment.ColorBlendOp = BlendOp.Add;
            colorBlendAttachment.SrcAlphaBlendFactor = BlendFactor.One;
            colorBlendAttachment.DstAlphaBlendFactor = BlendFactor.Zero;
            colorBlendAttachment.AlphaBlendOp = BlendOp.Add;
        }
        var colorBlending = new PipelineColorBlendStateCreateInfo
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = Vk.False,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment
        };
        colorBlending.BlendConstants[0] = 0.0f;
        colorBlending.BlendConstants[1] = 0.0f;
        colorBlending.BlendConstants[2] = 0.0f;
        colorBlending.BlendConstants[3] = 0.0f;

        //With depthTest=true depth test and write are enabled; the compare function is read from the description instead of hardcoded
        //DepthStencilState.DEFAULT uses GreaterOrEqual with reversed-Z clearDepth=0, but standard Vulkan [0,1] uses Less+clearDepth=1
        //The current Camera projection uses standard Vulkan depth Less, matching terrain pipelines; entity pipelines match with GreaterOrEqual
        var depthStencil = new PipelineDepthStencilStateCreateInfo
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = description.DepthTestEnabled ? Vk.True : Vk.False,
            DepthWriteEnable = description.DepthTestEnabled ? Vk.True : Vk.False,
            DepthCompareOp = ToVkCompareOp(description.DepthCompareOp),
            DepthBoundsTestEnable = Vk.False,
            MinDepthBounds = 0f,
            MaxDepthBounds = 1f,
            StencilTestEnable = Vk.False
        };

        Silk.NET.Vulkan.Pipeline pipeline;
        fixed (VertexInputBindingDescription* bindingDescs = bindingDescsArray)
        fixed (VertexInputAttributeDescription* attrDescs = attrDescsArray)
        {
            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = (uint)bindings.Count,
                PVertexBindingDescriptions = bindingDescs,
                VertexAttributeDescriptionCount = (uint)attrDescsList.Count,
                PVertexAttributeDescriptions = attrDescs
            };
            //PipelineRenderingCreateInfoKHR 4.3 rework for dynamic rendering pipeline creation
            //With RenderPass=null the driver creates the pipeline from this struct's attachment format, compatible with CmdBeginRenderingKHR
            var colorFormat = _colorFormat;
            var renderingInfo = new PipelineRenderingCreateInfoKHR
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 1,
                PColorAttachmentFormats = &colorFormat,
                DepthAttachmentFormat = description.DepthTestEnabled ? Format.D32Sfloat : Format.Undefined,
                StencilAttachmentFormat = Format.Undefined
            };
            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &renderingInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                Layout = _pipelineLayout,
                //RenderPass=null takes the dynamic rendering path with the PNext chain providing PipelineRenderingCreateInfoKHR
                RenderPass = default,
                Subpass = 0,
                BasePipelineHandle = default
            };
            //DynamicScissorEnabled enables VK_DYNAMIC_STATE_SCISSOR, setting the scissor at runtime with vkCmdSetScissor
            //dynamicStates/dynamicStateInfo are stackalloc'd at the top of the fixed block with a scope covering the CreateGraphicsPipelines call
            var dynamicStates = stackalloc DynamicState[1];
            dynamicStates[0] = DynamicState.Scissor;
            var dynamicStateInfo = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 1,
                PDynamicStates = dynamicStates
            };
            if (description.DynamicScissorEnabled)
            {
                pipelineInfo.PDynamicState = &dynamicStateInfo;
            }
            if (_vk.CreateGraphicsPipelines(_device, default, 1, &pipelineInfo, null, &pipeline) != Result.Success)
            {
                throw new InvalidOperationException("GraphicsPipeline creation failed");
            }
        }
        SilkMarshal.Free((nint)vertStage.PName);
        SilkMarshal.Free((nint)fragStage.PName);
        return pipeline;
    }

    public static Format ToVkFormat(GpuImageFormat fmt) => fmt switch
    {
        GpuImageFormat.R8G8B8A8Unorm => Format.R8G8B8A8Unorm,
        GpuImageFormat.B8G8R8A8Unorm => Format.B8G8R8A8Unorm,
        GpuImageFormat.R8G8B8Unorm => Format.R8G8B8Unorm,
        GpuImageFormat.R8Unorm => Format.R8Unorm,
        GpuImageFormat.D32Sfloat => Format.D32Sfloat,
        _ => throw new ArgumentOutOfRangeException(nameof(fmt))
    };

    private static Format ToVkFormat(GpuVertexFormat fmt) => fmt switch
    {
        GpuVertexFormat.Float => Format.R32Sfloat,
        GpuVertexFormat.Vec2Float => Format.R32G32Sfloat,
        GpuVertexFormat.Vec3Float => Format.R32G32B32Sfloat,
        GpuVertexFormat.Vec4Float => Format.R32G32B32A32Sfloat,
        GpuVertexFormat.Byte4Norm => Format.R8G8B8A8Unorm,
        _ => throw new ArgumentOutOfRangeException(nameof(fmt))
    };

    private static PrimitiveTopology ToVkTopology(GpuPrimitiveTopology topo) => topo switch
    {
        GpuPrimitiveTopology.TriangleList => PrimitiveTopology.TriangleList,
        GpuPrimitiveTopology.TriangleStrip => PrimitiveTopology.TriangleStrip,
        GpuPrimitiveTopology.LineList => PrimitiveTopology.LineList,
        GpuPrimitiveTopology.PointList => PrimitiveTopology.PointList,
        _ => throw new ArgumentOutOfRangeException(nameof(topo))
    };

    //ToVkCompareOp maps NetCraft.Gpu.Pipeline.CompareOp to Silk.NET.Vulkan.CompareOp
    private static Silk.NET.Vulkan.CompareOp ToVkCompareOp(NetCraft.Gpu.Pipeline.CompareOp op) => op switch
    {
        NetCraft.Gpu.Pipeline.CompareOp.Never => Silk.NET.Vulkan.CompareOp.Never,
        NetCraft.Gpu.Pipeline.CompareOp.Less => Silk.NET.Vulkan.CompareOp.Less,
        NetCraft.Gpu.Pipeline.CompareOp.Equal => Silk.NET.Vulkan.CompareOp.Equal,
        NetCraft.Gpu.Pipeline.CompareOp.LessOrEqual => Silk.NET.Vulkan.CompareOp.LessOrEqual,
        NetCraft.Gpu.Pipeline.CompareOp.Greater => Silk.NET.Vulkan.CompareOp.Greater,
        NetCraft.Gpu.Pipeline.CompareOp.NotEqual => Silk.NET.Vulkan.CompareOp.NotEqual,
        NetCraft.Gpu.Pipeline.CompareOp.GreaterOrEqual => Silk.NET.Vulkan.CompareOp.GreaterOrEqual,
        NetCraft.Gpu.Pipeline.CompareOp.Always => Silk.NET.Vulkan.CompareOp.Always,
        _ => Silk.NET.Vulkan.CompareOp.Less
    };

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyPipeline(_device, _pipeline, null);
        _vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
        //4.3 rework removes the traditional RenderPass and no longer DestroyRenderPass
        _vk.DestroyShaderModule(_device, _fragModule, null);
        _vk.DestroyShaderModule(_device, _vertModule, null);
        _disposed = true;
    }
}
