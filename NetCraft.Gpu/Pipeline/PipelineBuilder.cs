using System.Collections.ObjectModel;

namespace NetCraft.Gpu.Pipeline;

//PipelineBuilder pipeline 构造器对标原版 RenderPipeline.Builder
//支持 fluent WithXxx 链式声明 + From(snippets) 片段组合
//BuildSnippet 生成可复用的 Snippet Build 生成完整 RenderPipeline 含 sortKey
public sealed class PipelineBuilder
{
    private const int MaxColorTargets = ColorTargetState.MaxColorTargets;
    private const int MaxVertexBindings = 16;
    private const int MaxVertexAttributes = 16;

    private string? _location;
    private string? _vertexShader;
    private string? _fragmentShader;
    private ShaderDefines.Builder? _definesBuilder;
    private List<BindGroupLayout>? _bindGroupLayouts;
    private DepthStencilState? _depthStencilState;
    private PolygonMode? _polygonMode;
    private bool? _cull;
    private readonly ColorTargetState?[] _colorTargetStates = new ColorTargetState?[MaxColorTargets];
    private int _activeColorTargetStateCount;
    private readonly VertexFormat?[] _vertexFormatPerBuffer = new VertexFormat?[MaxVertexBindings];
    private PrimitiveTopology? _primitiveTopology;

    public static PipelineBuilder From(params Snippet[] snippets)
    {
        var builder = new PipelineBuilder();
        foreach (var s in snippets)
            builder.WithSnippet(s);
        return builder;
    }

    public PipelineBuilder WithLocation(string location) { _location = location; return this; }

    public PipelineBuilder WithVertexShader(string vertexShader) { _vertexShader = vertexShader; return this; }

    public PipelineBuilder WithFragmentShader(string fragmentShader) { _fragmentShader = fragmentShader; return this; }

    public PipelineBuilder WithShaderDefine(string key)
    {
        _definesBuilder ??= ShaderDefines.NewBuilder();
        _definesBuilder.Define(key);
        return this;
    }

    public PipelineBuilder WithShaderDefine(string key, int value)
    {
        _definesBuilder ??= ShaderDefines.NewBuilder();
        _definesBuilder.Define(key, value);
        return this;
    }

    public PipelineBuilder WithShaderDefine(string key, float value)
    {
        _definesBuilder ??= ShaderDefines.NewBuilder();
        _definesBuilder.Define(key, value);
        return this;
    }

    public PipelineBuilder WithBindGroupLayout(BindGroupLayout layout)
    {
        _bindGroupLayouts ??= new List<BindGroupLayout>();
        _bindGroupLayouts.Add(layout);
        return this;
    }

    public PipelineBuilder WithPolygonMode(PolygonMode mode) { _polygonMode = mode; return this; }

    public PipelineBuilder WithCull(bool cull) { _cull = cull; return this; }

    public PipelineBuilder WithColorTargetState(ColorTargetState state) => WithColorTargetState(0, state);

    public PipelineBuilder WithColorTargetState(int index, ColorTargetState state)
    {
        _colorTargetStates[index] = state;
        _activeColorTargetStateCount = Math.Max(_activeColorTargetStateCount, index + 1);
        return this;
    }

    public PipelineBuilder WithUnusedColorTargetState(int index)
    {
        _colorTargetStates[index] = null;
        _activeColorTargetStateCount = Math.Max(_activeColorTargetStateCount, index + 1);
        return this;
    }

    public PipelineBuilder WithDepthStencilState(DepthStencilState state)
    {
        _depthStencilState = state;
        return this;
    }

    public PipelineBuilder WithDepthStencilState(DepthStencilState? state)
    {
        _depthStencilState = state;
        return this;
    }

    public PipelineBuilder WithVertexBinding(int bindingIndex, VertexFormat format)
    {
        _vertexFormatPerBuffer[bindingIndex] = format;
        return this;
    }

    public PipelineBuilder WithPrimitiveTopology(PrimitiveTopology topology)
    {
        _primitiveTopology = topology;
        return this;
    }

    //WithSnippet 合并 snippet 后者非空字段覆盖前者 shaderDefines 合并 bindGroupLayouts 追加
    public PipelineBuilder WithSnippet(Snippet snippet)
    {
        if (snippet.VertexShader != null) _vertexShader = snippet.VertexShader;
        if (snippet.FragmentShader != null) _fragmentShader = snippet.FragmentShader;
        if (snippet.ShaderDefines != null)
        {
            _definesBuilder ??= ShaderDefines.NewBuilder();
            foreach (var kv in snippet.ShaderDefines.Values)
                _definesBuilder.Define(kv.Key, kv.Value);
            foreach (var flag in snippet.ShaderDefines.Flags)
                _definesBuilder.Define(flag);
        }
        if (snippet.BindGroupLayouts != null)
        {
            _bindGroupLayouts ??= new List<BindGroupLayout>();
            _bindGroupLayouts.AddRange(snippet.BindGroupLayouts);
        }
        if (snippet.DepthStencilState != null) _depthStencilState = snippet.DepthStencilState;
        if (snippet.Cull != null) _cull = snippet.Cull;
        if (snippet.PolygonMode != null) _polygonMode = snippet.PolygonMode;
        if (snippet.PrimitiveTopology != null) _primitiveTopology = snippet.PrimitiveTopology;
        for (int i = 0; i < snippet.ColorTargetStates.Count; i++)
        {
            var s = snippet.ColorTargetStates[i];
            if (_colorTargetStates[i] == null && s != null)
                _colorTargetStates[i] = s;
        }
        _activeColorTargetStateCount = Math.Max(_activeColorTargetStateCount, snippet.ColorTargetStates.Count);
        for (int i = 0; i < snippet.VertexFormatPerBuffer.Count; i++)
        {
            var vf = snippet.VertexFormatPerBuffer[i];
            if (vf != null)
                _vertexFormatPerBuffer[i] = vf;
        }
        return this;
    }

    public Snippet BuildSnippet()
    {
        var colorTargets = new ColorTargetState?[MaxColorTargets];
        for (int i = 0; i < _activeColorTargetStateCount; i++)
            colorTargets[i] = _colorTargetStates[i];
        var vertexFormats = new VertexFormat?[MaxVertexBindings];
        for (int i = 0; i < MaxVertexBindings; i++)
            vertexFormats[i] = _vertexFormatPerBuffer[i];
        return new Snippet(
            _vertexShader,
            _fragmentShader,
            _definesBuilder?.Build(),
            _bindGroupLayouts != null ? new ReadOnlyCollection<BindGroupLayout>(_bindGroupLayouts) : null,
            new ReadOnlyCollection<ColorTargetState?>(colorTargets),
            _depthStencilState,
            _polygonMode,
            _cull,
            new ReadOnlyCollection<VertexFormat?>(vertexFormats),
            _primitiveTopology);
    }

    public RenderPipeline Build()
    {
        if (_location == null) throw new InvalidOperationException("Missing location");
        if (_vertexShader == null) throw new InvalidOperationException("Missing vertex shader");
        if (_fragmentShader == null) throw new InvalidOperationException("Missing fragment shader");
        if (_primitiveTopology == null) throw new InvalidOperationException("Missing primitive topology");

        IReadOnlyList<ColorTargetState> colorTargets;
        if (_activeColorTargetStateCount == 0)
        {
            colorTargets = new[] { ColorTargetState.DEFAULT };
        }
        else
        {
            var arr = new ColorTargetState[_activeColorTargetStateCount];
            BlendFunction? lastBlend = null;
            for (int i = 0; i < _activeColorTargetStateCount; i++)
            {
                arr[i] = _colorTargetStates[i] ?? default;
                if (_colorTargetStates[i] is { } cs && cs.BlendFunction is { } blend)
                {
                    if (lastBlend is { } lb && !lb.Equals(blend))
                        throw new InvalidOperationException("Blend functions must currently be the same for all color targets");
                    lastBlend = blend;
                }
            }
            colorTargets = arr;
        }

        int attribCount = 0;
        foreach (var vf in _vertexFormatPerBuffer)
            if (vf != null) attribCount += vf.Elements.Count;
        if (attribCount > MaxVertexAttributes)
            throw new InvalidOperationException("Binding more than 16 vertex attributes is not supported");

        var vertexFormats = new VertexFormat?[MaxVertexBindings];
        for (int i = 0; i < MaxVertexBindings; i++)
            vertexFormats[i] = _vertexFormatPerBuffer[i];

        return new RenderPipeline(
            _location,
            _vertexShader,
            _fragmentShader,
            _definesBuilder?.Build() ?? ShaderDefines.NewBuilder().Build(),
            _bindGroupLayouts != null ? new ReadOnlyCollection<BindGroupLayout>(_bindGroupLayouts) : Array.Empty<BindGroupLayout>(),
            colorTargets,
            _depthStencilState,
            _polygonMode ?? PolygonMode.Fill,
            _cull ?? true,
            new ReadOnlyCollection<VertexFormat?>(vertexFormats),
            _primitiveTopology!.Value);
    }
}
