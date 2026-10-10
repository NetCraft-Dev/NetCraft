using System.Threading;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Blaze3d.Pipeline;

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
