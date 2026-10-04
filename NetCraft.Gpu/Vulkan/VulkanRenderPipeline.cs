using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace NetCraft.Gpu.Vulkan;

//ClearColorValue 浮点 RGBA 清屏色
public readonly record struct ClearColorValueRGBA(float R, float G, float B, float A);

//VulkanRenderPipeline Vulkan 后端渲染管线
//4.3 改造移除传统 RenderPass 改走 dynamic rendering pipeline 创建用 PipelineRenderingCreateInfoKHR PNext 链
//附件 ImageView 由 VulkanRenderPass(Vulkan IRenderPass 实现) 在 CmdBeginRenderingKHR 时传入
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
    //ColorFormat 颜色附件格式供 VulkanRenderPass 构造 RenderingAttachmentInfoKHR 用
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

    //FromDescription 从 description 创建管线目标格式和 extent 从 description 取默认 B8G8R8A8Unorm/800x600
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
                throw new InvalidOperationException("ShaderModule 创建失败");
            }
            return module;
        }
    }

    //CreatePipelineLayout 从 description.DescriptorLayouts 取 VkDescriptorSetLayout 数组创建 PipelineLayout
    //空列表时 SetLayoutCount=0 仍创建空 layout 供纯 gl_VertexIndex shader 用
    private PipelineLayout CreatePipelineLayout(RenderPipelineDescription description)
    {
        var layouts = description.DescriptorLayouts;
        var layoutHandles = new DescriptorSetLayout[layouts.Count];
        for (int i = 0; i < layouts.Count; i++)
        {
            if (layouts[i] is not VulkanDescriptorLayout vkLayout)
                throw new ArgumentException("DescriptorLayout 必须是 VulkanDescriptorLayout");
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
                throw new InvalidOperationException("PipelineLayout 创建失败");
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

        //顶点输入布局从 description.VertexBindings 构建
        //Silk.NET 的 VertexInputBindingDescription 含 managed 字段无法 stackalloc T* 用 array + fixed
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
            //DynamicScissorEnabled 时 scissor 由 vkCmdSetScissor 动态提供 PScissors 置 null
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

        //depthTest=true 时启用深度测试和写入深度比较函数从 description 读不再硬编码
        //DepthStencilState.DEFAULT 用 GreaterOrEqual 配合 reversed-Z clearDepth=0 但 Vulkan 标准 [0,1] Less+clearDepth=1
        //当前 Camera 投影用标准 Vulkan 深度 Less 匹配 terrain pipeline 用 GreaterOrEqual 匹配实体 pipeline
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
            //PipelineRenderingCreateInfoKHR 4.3 改造走 dynamic rendering pipeline 创建
            //RenderPass=null 时驱动按此结构附件格式创建 pipeline 兼容 CmdBeginRenderingKHR
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
                //RenderPass=null 走 dynamic rendering 路径 PNext 链提供 PipelineRenderingCreateInfoKHR
                RenderPass = default,
                Subpass = 0,
                BasePipelineHandle = default
            };
            //DynamicScissorEnabled 启用 VK_DYNAMIC_STATE_SCISSOR 运行时 vkCmdSetScissor 设置裁剪
            //dynamicStates/dynamicStateInfo 在 fixed 块顶层 stackalloc 作用域覆盖 CreateGraphicsPipelines 调用
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
                throw new InvalidOperationException("GraphicsPipeline 创建失败");
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

    //ToVkCompareOp 把 NetCraft.Gpu.Pipeline.CompareOp 映射到 Silk.NET.Vulkan.CompareOp
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
        //4.3 改造移除传统 RenderPass 不再 DestroyRenderPass
        _vk.DestroyShaderModule(_device, _fragModule, null);
        _vk.DestroyShaderModule(_device, _vertModule, null);
        _disposed = true;
    }
}
