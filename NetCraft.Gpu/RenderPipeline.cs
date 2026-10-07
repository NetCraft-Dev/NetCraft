using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//CompiledRenderPipeline compiled render pipeline, maps to the vanilla CompiledRenderPipeline interface
//Produced by IGpuDevice.PrecompilePipeline compiling a declarative RenderPipeline
//VulkanRenderPipeline is its Vulkan backend implementation holding a VkPipeline handle
public abstract class CompiledRenderPipeline : IDisposable
{
    public RenderPipelineDescription Description { get; }

    protected CompiledRenderPipeline(RenderPipelineDescription description)
    {
        Description = description;
    }

    public virtual void Dispose() { }
}

//GpuVertexFormat vertex attribute format
public enum GpuVertexFormat
{
    Float,
    Vec2Float,
    Vec3Float,
    Vec4Float,
    Byte4Norm
}

//GpuVertexAttribute vertex attribute description
public sealed class GpuVertexAttribute
{
    public int Location { get; set; }
    public GpuVertexFormat Format { get; set; }
    public int Offset { get; set; }
}

//GpuVertexBinding vertex binding description
public sealed class GpuVertexBinding
{
    public int Binding { get; set; }
    public int Stride { get; set; }
    public List<GpuVertexAttribute> Attributes { get; set; } = new();
}

//GpuPrimitiveTopology primitive topology
public enum GpuPrimitiveTopology
{
    TriangleList,
    TriangleStrip,
    LineList,
    PointList
}

//RenderPipelineDescription render pipeline description
//Subclasses create the underlying pipeline from this description
public sealed class RenderPipelineDescription
{
    //VertexShaderSource vertex shader SPIR-V bytecode
    //null means use the PoC's built-in triangle shader
    public byte[]? VertexShaderSpirv { get; set; }
    //FragmentShaderSource fragment shader SPIR-V bytecode
    public byte[]? FragmentShaderSpirv { get; set; }
    //VertexBindings vertex layout bindings; an empty list means no vertex input and uses gl_VertexIndex
    public List<GpuVertexBinding> VertexBindings { get; set; } = new();
    //DescriptorLayouts descriptor set layout list for uniform buffer/sampler binding
    //An empty list means the pipeline needs no external resource bindings
    public List<GpuDescriptorLayout> DescriptorLayouts { get; set; } = new();
    //DescriptorLayoutDescriptions descriptor set layout description list, used after converting a declarative RenderPipeline to a description for the device to compile
    //When non-empty, the PrecompilePipeline implementation compiles them into GpuDescriptorLayouts and fills in DescriptorLayouts
    public List<GpuDescriptorLayoutDescription> DescriptorLayoutDescriptions { get; set; } = new();
    //Topology primitive topology
    public GpuPrimitiveTopology Topology { get; set; } = GpuPrimitiveTopology.TriangleList;
    //DepthTestEnabled depth test
    public bool DepthTestEnabled { get; set; }
    //DepthCompareOp depth compare function, default Less; VulkanRenderPipeline reads this field instead of hardcoding
    public CompareOp DepthCompareOp { get; set; } = CompareOp.Less;
    //BlendEnabled alpha blending
    public bool BlendEnabled { get; set; }
    //DynamicScissorEnabled enables VK_DYNAMIC_STATE_SCISSOR, setting the scissor at runtime with vkCmdSetScissor
    //GUI pipelines set true; triangle/cube pipelines keep the false default
    public bool DynamicScissorEnabled { get; set; }
    //TargetFormat target color attachment format; null means use the swapchain format
    public GpuImageFormat? TargetFormat { get; set; }
    //TargetExtent target extent; 0 means use the swapchain extent
    public int TargetWidth { get; set; }
    public int TargetHeight { get; set; }
    //ClearColor clear color RGBA
    public (float R, float G, float B, float A) ClearColor { get; set; } = (0f, 0f, 0f, 1f);

    //FromDeclaration converts a declarative RenderPipeline into a RenderPipelineDescription
    //Stage 8 loads embedded GLSL via ShaderManager, compiles to SPIR-V and injects ShaderDefines
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
