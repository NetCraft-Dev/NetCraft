namespace NetCraft.Gpu.Pipeline;

//Snippet pipeline snippet, maps to vanilla RenderPipeline.Snippet record
//All fields are optional for composition; later non-null fields override earlier ones, supporting snippet composition in RenderPipelines
public sealed class Snippet
{
    public string? VertexShader { get; }
    public string? FragmentShader { get; }
    public ShaderDefines? ShaderDefines { get; }
    public IReadOnlyList<BindGroupLayout>? BindGroupLayouts { get; }
    public IReadOnlyList<ColorTargetState?> ColorTargetStates { get; }
    public DepthStencilState? DepthStencilState { get; }
    public PolygonMode? PolygonMode { get; }
    public bool? Cull { get; }
    public IReadOnlyList<VertexFormat?> VertexFormatPerBuffer { get; }
    public PrimitiveTopology? PrimitiveTopology { get; }

    public Snippet(
        string? vertexShader,
        string? fragmentShader,
        ShaderDefines? shaderDefines,
        IReadOnlyList<BindGroupLayout>? bindGroupLayouts,
        IReadOnlyList<ColorTargetState?> colorTargetStates,
        DepthStencilState? depthStencilState,
        PolygonMode? polygonMode,
        bool? cull,
        IReadOnlyList<VertexFormat?> vertexFormatPerBuffer,
        PrimitiveTopology? primitiveTopology)
    {
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
    }
}
