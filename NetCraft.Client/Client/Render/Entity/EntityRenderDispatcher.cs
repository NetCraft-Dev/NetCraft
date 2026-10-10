using System.Numerics;
using System.Runtime.InteropServices;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
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

namespace NetCraft.Client.Render.Entity;

//EntityRenderDispatcher entity render dispatcher, maps to vanilla EntityRenderDispatcher
//Manages the EntityRenderer registry and dispatches by EntityType
//Holds an EntityVertexBuilder collecting all visible entity vertices; uploads to a GPU buffer then DrawIndexed
//The PoC skeleton uses one vertex/index buffer and one pipeline for all entities; later versions batch by RenderLayer
public sealed class EntityRenderDispatcher : IDisposable
{
    private readonly Dictionary<string, EntityRenderer> _renderers = new();
    private readonly List<(EntityRenderState State, EntityRenderer Renderer)> _visible = new();
    private readonly EntityVertexBuilder _builder = new();
    private readonly GpuBufferPool _bufferPool;
    private GpuBuffer? _vertexBuffer;
    private GpuBuffer? _indexBuffer;
    private int _vertexCount;
    private int _indexCount;
    private bool _disposed;
    //_layerRanges per-layer vertex/index ranges; Prepare does three passes batching by pipeline and records them, Draw sets pipeline + offset per layer
    private readonly (int VertexStart, int VertexCount, int IndexStart, int IndexCount)[] _layerRanges = new (int, int, int, int)[3];
    //_layerPipelines entity pipelines indexed by layer Solid=0 Cutout=1 Translucent=2, matching the RenderLayer enum order
    private static readonly RenderPipeline[] s_layerPipelines =
    {
        EntityRenderPipelines.ENTITY_SOLID,
        EntityRenderPipelines.ENTITY_CUTOUT,
        EntityRenderPipelines.ENTITY_TRANSLUCENT
    };

    //EntityVertexCount last frame's entity vertex count, for debugging
    public int EntityVertexCount => _vertexCount;
    //EntityIndexCount last frame's entity index count
    public int EntityIndexCount => _indexCount;
    //VisibleEntityCount last frame's visible entity count
    public int VisibleEntityCount => _visible.Count;
    //HasContent whether there are renderable entity vertices
    public bool HasContent => _indexCount > 0;
    //LastDrawCallCount batches actually submitted by Draw last frame (one per layer, at most 3)
    public int LastDrawCallCount { get; private set; }

    public EntityRenderDispatcher(GpuBufferPool bufferPool) => _bufferPool = bufferPool;

    //Register registers an entity renderer indexed by entityTypeName
    public void Register(string entityTypeName, EntityRenderer renderer)
        => _renderers[entityTypeName] = renderer;

    //ClearEntities clears the visible entity list, called before Prepare each frame
    public void ClearEntities() => _visible.Clear();

    //AddEntity adds a visible entity, called by LevelRenderer while iterating the entity list
    //entityTypeName matches Register's key; unregistered entities are skipped
    public void AddEntity(string entityTypeName, EntityRenderState state)
    {
        if (_renderers.TryGetValue(entityTypeName, out var renderer))
            _visible.Add((state, renderer));
    }

    //Prepare generates all visible entity vertices into builder, batching contiguous vertex segments by pipeline
    //The caller handles the camera transform; poseStack starts from Identity and pushes a world transform per entity
    //cameraPosition used to compute entity relative position
    //Three passes Solid→Cutout→Translucent, each recording that layer's vertex/index range for Draw to render per layer
    public void Prepare(Vector3 cameraPosition)
    {
        _builder.Clear();
        _vertexCount = 0;
        _indexCount = 0;
        for (var layerIndex = 0; layerIndex < s_layerPipelines.Length; layerIndex++)
        {
            var pipeline = s_layerPipelines[layerIndex];
            var vertexStart = _builder.VertexCount;
            var indexStart = _builder.Indices.Count;
            foreach (var (state, renderer) in _visible)
            {
                if (renderer.Pipeline != pipeline) continue;
                var poseStack = new PoseStack();
                //Entity world positions are relative to the camera so the shader ViewProj only contains camera rotation + projection
                //Consistent with terrain: terrain's section offset is baked into vertex position
                poseStack.Translate(
                    state.Position.X - cameraPosition.X,
                    state.Position.Y - cameraPosition.Y,
                    state.Position.Z - cameraPosition.Z);
                //Entity Y-axis rotation orientation
                if (state.YRot != 0f)
                    poseStack.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, state.YRot));
                renderer.Render(poseStack, _builder, state);
            }
            _layerRanges[layerIndex] = (vertexStart, _builder.VertexCount - vertexStart,
                indexStart, _builder.Indices.Count - indexStart);
        }
        _vertexCount = _builder.VertexCount;
        _indexCount = _builder.Indices.Count;
    }

    //Upload uploads builder vertices/indices to GPU buffers, borrowing from the pool
    //Skip without borrowing when empty, to avoid an empty DrawCall
    public void Upload(GpuDevice device)
    {
        //Return the previous frame's buffers
        if (_vertexBuffer is not null) { _bufferPool.ReturnBuffer(_vertexBuffer); _vertexBuffer = null; }
        if (_indexBuffer is not null) { _bufferPool.ReturnBuffer(_indexBuffer); _indexBuffer = null; }
        if (_indexCount == 0) return;
        //Upload vertices: 11 floats/vertex = 44 bytes
        var vertexSpan = CollectionsMarshal.AsSpan(_builder.Vertices);
        var vertexBytes = MemoryMarshal.AsBytes(vertexSpan).ToArray();
        _vertexBuffer = _bufferPool.GetBuffer(vertexBytes.Length, GpuBuffer.UsageVertex | GpuBuffer.UsageCopyDst);
        _vertexBuffer.Upload<byte>(vertexBytes);
        //Upload indices: int/index = 4 bytes
        var indexSpan = CollectionsMarshal.AsSpan(_builder.Indices);
        var indexBytes = MemoryMarshal.AsBytes(indexSpan).ToArray();
        _indexBuffer = _bufferPool.GetBuffer(indexBytes.Length, GpuBuffer.UsageIndex | GpuBuffer.UsageCopyDst);
        _indexBuffer.Upload<byte>(indexBytes);
    }

    //Draw records entity render commands batched by pipeline; a single buffer with per-layer offset DrawIndexed
    //pipelineResolver resolves RenderPipeline→CompiledRenderPipeline; descBinder binds the descriptor set
    //Solid→Cutout→Translucent order; per layer SetPipeline+DrawIndexed, skipping empty layers
    public void Draw(IRenderPass pass,
        Func<RenderPipeline, CompiledRenderPipeline> pipelineResolver,
        Action<IRenderPass> descBinder)
    {
        if (_vertexBuffer is null || _indexBuffer is null || _indexCount == 0) return;
        pass.SetVertexBuffer(0, _vertexBuffer);
        pass.SetIndexBuffer(_indexBuffer, GpuIndexType.UInt32);
        LastDrawCallCount = 0;
        for (var layerIndex = 0; layerIndex < s_layerPipelines.Length; layerIndex++)
        {
            var (vertexStart, _, indexStart, indexCount) = _layerRanges[layerIndex];
            if (indexCount == 0) continue;
            pass.SetPipeline(pipelineResolver(s_layerPipelines[layerIndex]));
            descBinder(pass);
            pass.DisableScissor();
            pass.DrawIndexed(indexCount, 1, indexStart, vertexStart, 0);
            LastDrawCallCount++;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _vertexBuffer = null;
        _indexBuffer = null;
        _disposed = true;
    }
}
