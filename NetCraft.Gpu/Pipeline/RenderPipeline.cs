using System.Threading;

namespace NetCraft.Gpu.Pipeline;

//RenderPipeline 声明式渲染管线对标原版 RenderPipeline record
//不可变值对象携带 location/shaders/defines/bindGroupLayouts/depth/colorTargets/topology 等声明字段
//由 PipelineBuilder.Build 产生 sortKey 单调递增驱动渲染排序合批
//经 IGpuDevice.PrecompilePipeline 编译为 CompiledRenderPipeline 持有 VkPipeline 句柄
public sealed class RenderPipeline
{
    private static int s_nextSortKey;

    public string Location { get; }
    public string VertexShader { get; }
    public string FragmentShader { get; }
    public ShaderDefines ShaderDefines { get; }
    public IReadOnlyList<BindGroupLayout> BindGroupLayouts { get; }
    public DepthStencilState? DepthStencilState { get; }
    public PolygonMode PolygonMode { get; }
    public bool Cull { get; }
    public IReadOnlyList<ColorTargetState> ColorTargetStates { get; }
    public IReadOnlyList<VertexFormat?> VertexFormatPerBuffer { get; }
    public PrimitiveTopology PrimitiveTopology { get; }
    public int SortKey { get; }

    internal RenderPipeline(
        string location,
        string vertexShader,
        string fragmentShader,
        ShaderDefines shaderDefines,
        IReadOnlyList<BindGroupLayout> bindGroupLayouts,
        IReadOnlyList<ColorTargetState> colorTargetStates,
        DepthStencilState? depthStencilState,
        PolygonMode polygonMode,
        bool cull,
        IReadOnlyList<VertexFormat?> vertexFormatPerBuffer,
        PrimitiveTopology primitiveTopology)
    {
        Location = location;
        VertexShader = vertexShader;
        FragmentShader = fragmentShader;
        ShaderDefines = shaderDefines;
        BindGroupLayouts = bindGroupLayouts;
        ColorTargetStates = colorTargetStates;
        DepthStencilState = depthStencilState;
        PolygonMode = polygonMode;
        Cull = cull;
        VertexFormatPerBuffer = vertexFormatPerBuffer;
        PrimitiveTopology = primitiveTopology;
        SortKey = Interlocked.Increment(ref s_nextSortKey) - 1;
    }

    //WantsDepthTexture 是否需要深度纹理附件 depthStencilState != null
    public bool WantsDepthTexture() => DepthStencilState != null;

    public ColorTargetState GetColorTargetState() => ColorTargetStates[0];

    public VertexFormat? GetVertexFormatBinding(int bindingIndex) => VertexFormatPerBuffer[bindingIndex];

    public override string ToString() => Location;
}
