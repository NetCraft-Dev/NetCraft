using System.Threading;

namespace NetCraft.Gpu.Pipeline;

//RenderPipeline declarative render pipeline, maps to vanilla RenderPipeline record
//Immutable value object carrying declaration fields location/shaders/defines/bindGroupLayouts/depth/colorTargets/topology
//Produced by PipelineBuilder.Build; sortKey increases monotonically to drive render sorting and batching
//Compiled via IGpuDevice.PrecompilePipeline into a CompiledRenderPipeline holding a VkPipeline handle
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

    //WantsDepthTexture whether a depth texture attachment is needed, depthStencilState != null
    public bool WantsDepthTexture() => DepthStencilState != null;

    public ColorTargetState GetColorTargetState() => ColorTargetStates[0];

    public VertexFormat? GetVertexFormatBinding(int bindingIndex) => VertexFormatPerBuffer[bindingIndex];

    public override string ToString() => Location;
}
