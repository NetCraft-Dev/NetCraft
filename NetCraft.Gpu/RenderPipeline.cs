using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//CompiledRenderPipeline 编译后的渲染管线对标原版 CompiledRenderPipeline 接口
//由 IGpuDevice.PrecompilePipeline 编译声明式 RenderPipeline 产生
//VulkanRenderPipeline 是其 Vulkan 后端实现持有 VkPipeline 句柄
public abstract class CompiledRenderPipeline : IDisposable
{
    public RenderPipelineDescription Description { get; }

    protected CompiledRenderPipeline(RenderPipelineDescription description)
    {
        Description = description;
    }

    public virtual void Dispose() { }
}

//GpuVertexFormat 顶点属性格式
public enum GpuVertexFormat
{
    Float,
    Vec2Float,
    Vec3Float,
    Vec4Float,
    Byte4Norm
}

//GpuVertexAttribute 顶点属性描述
public sealed class GpuVertexAttribute
{
    public int Location { get; set; }
    public GpuVertexFormat Format { get; set; }
    public int Offset { get; set; }
}

//GpuVertexBinding 顶点绑定描述
public sealed class GpuVertexBinding
{
    public int Binding { get; set; }
    public int Stride { get; set; }
    public List<GpuVertexAttribute> Attributes { get; set; } = new();
}

//GpuPrimitiveTopology 图元拓扑
public enum GpuPrimitiveTopology
{
    TriangleList,
    TriangleStrip,
    LineList,
    PointList
}

//RenderPipelineDescription 渲染管线描述
//子类根据此描述创建底层管线
public sealed class RenderPipelineDescription
{
    //VertexShaderSource vertex shader SPIR-V 字节码
    //null 表示用 PoC 内置三角形 shader
    public byte[]? VertexShaderSpirv { get; set; }
    //FragmentShaderSource fragment shader SPIR-V 字节码
    public byte[]? FragmentShaderSpirv { get; set; }
    //VertexBindings 顶点布局绑定空列表表示无顶点输入走 gl_VertexIndex
    public List<GpuVertexBinding> VertexBindings { get; set; } = new();
    //DescriptorLayouts 描述符集布局列表用于 uniform buffer/sampler 绑定
    //空列表表示管线不需要外部资源绑定
    public List<GpuDescriptorLayout> DescriptorLayouts { get; set; } = new();
    //DescriptorLayoutDescriptions 描述符集布局描述列表用于声明式 RenderPipeline 转 description 后由 device 编译
    //非空时 PrecompilePipeline 实现负责编译为 GpuDescriptorLayout 后填入 DescriptorLayouts
    public List<GpuDescriptorLayoutDescription> DescriptorLayoutDescriptions { get; set; } = new();
    //Topology 图元拓扑
    public GpuPrimitiveTopology Topology { get; set; } = GpuPrimitiveTopology.TriangleList;
    //DepthTestEnabled 深度测试
    public bool DepthTestEnabled { get; set; }
    //DepthCompareOp 深度比较函数 默认 Less VulkanRenderPipeline 从此字段读取不再硬编码
    public CompareOp DepthCompareOp { get; set; } = CompareOp.Less;
    //BlendEnabled alpha 混合
    public bool BlendEnabled { get; set; }
    //DynamicScissorEnabled 启用 VK_DYNAMIC_STATE_SCISSOR 运行时 vkCmdSetScissor 设置裁剪
    //GUI pipeline 设 true 三角形/立方体 pipeline 保持 false 默认
    public bool DynamicScissorEnabled { get; set; }
    //TargetFormat 目标颜色附件格式 null 表示用 swapchain 格式
    public GpuImageFormat? TargetFormat { get; set; }
    //TargetExtent 目标 extent 0 表示用 swapchain extent
    public int TargetWidth { get; set; }
    public int TargetHeight { get; set; }
    //ClearColor 清屏色 RGBA
    public (float R, float G, float B, float A) ClearColor { get; set; } = (0f, 0f, 0f, 1f);

    //FromDeclaration 从声明式 RenderPipeline 转换为 RenderPipelineDescription
    //阶段 8 通过 ShaderManager 加载嵌入 GLSL 编译为 SPIR-V 注入 ShaderDefines
    public static RenderPipelineDescription FromDeclaration(RenderPipeline declaration, ShaderManager shaderManager)
    {
        var desc = new RenderPipelineDescription
        {
            VertexShaderSpirv = shaderManager.LoadVertexShader(declaration.VertexShader, declaration.ShaderDefines),
            FragmentShaderSpirv = shaderManager.LoadFragmentShader(declaration.FragmentShader, declaration.ShaderDefines),
            Topology = ToLegacyTopology(declaration.PrimitiveTopology),
            DepthTestEnabled = declaration.DepthStencilState != null,
            DepthCompareOp = declaration.DepthStencilState?.DepthTest ?? CompareOp.Less,
            BlendEnabled = declaration.ColorTargetStates.Count > 0 && declaration.ColorTargetStates[0].BlendFunction != null,
            DynamicScissorEnabled = true,
            TargetFormat = ToLegacyFormat(declaration.ColorTargetStates[0].Format)
        };

        foreach (var b in declaration.BindGroupLayouts)
        {
            var layoutDesc = new GpuDescriptorLayoutDescription();
            foreach (var s in b.Samplers)
                layoutDesc.Bindings.Add(new GpuDescriptorBinding
                {
                    Binding = layoutDesc.Bindings.Count,
                    DescriptorType = GpuDescriptorType.CombinedImageSampler,
                    StageFlags = GpuShaderStageFlags.AllGraphics
                });
            foreach (var u in b.Uniforms)
                layoutDesc.Bindings.Add(new GpuDescriptorBinding
                {
                    Binding = layoutDesc.Bindings.Count,
                    DescriptorType = GpuDescriptorType.UniformBuffer,
                    StageFlags = GpuShaderStageFlags.AllGraphics
                });
            desc.DescriptorLayoutDescriptions.Add(layoutDesc);
        }

        for (int i = 0; i < declaration.VertexFormatPerBuffer.Count; i++)
        {
            var vf = declaration.VertexFormatPerBuffer[i];
            if (vf == null) continue;
            var binding = new GpuVertexBinding { Binding = i, Stride = vf.Stride };
            int location = 0;
            foreach (var e in vf.Elements)
            {
                binding.Attributes.Add(new GpuVertexAttribute
                {
                    Location = location++,
                    Format = ToLegacyFormat(e.Format),
                    Offset = e.Offset
                });
            }
            desc.VertexBindings.Add(binding);
        }

        return desc;
    }

    private static GpuPrimitiveTopology ToLegacyTopology(PrimitiveTopology t) => t switch
    {
        PrimitiveTopology.TriangleList => GpuPrimitiveTopology.TriangleList,
        PrimitiveTopology.TriangleStrip => GpuPrimitiveTopology.TriangleStrip,
        PrimitiveTopology.Lines => GpuPrimitiveTopology.LineList,
        PrimitiveTopology.Points => GpuPrimitiveTopology.PointList,
        _ => GpuPrimitiveTopology.TriangleList
    };

    private static GpuImageFormat ToLegacyFormat(GpuFormat f) => f switch
    {
        GpuFormat.R8G8B8A8Unorm => GpuImageFormat.R8G8B8A8Unorm,
        GpuFormat.B8G8R8A8Unorm => GpuImageFormat.B8G8R8A8Unorm,
        GpuFormat.R8Unorm => GpuImageFormat.R8Unorm,
        GpuFormat.D32Sfloat => GpuImageFormat.D32Sfloat,
        _ => GpuImageFormat.R8G8B8A8Unorm
    };

    private static GpuVertexFormat ToLegacyFormat(VertexElementFormat f) => f switch
    {
        VertexElementFormat.Float => GpuVertexFormat.Float,
        VertexElementFormat.Vec2 => GpuVertexFormat.Vec2Float,
        VertexElementFormat.Vec3 => GpuVertexFormat.Vec3Float,
        VertexElementFormat.Vec4 => GpuVertexFormat.Vec4Float,
        VertexElementFormat.UByte4Norm => GpuVertexFormat.Byte4Norm,
        _ => GpuVertexFormat.Float
    };
}
