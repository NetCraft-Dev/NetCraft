using System.Numerics;
using NetCraft.Client.Level;
using NetCraft.Client.Render.Culling;
using NetCraft.Client.Render.Entity;
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
using NetCraft.Primitives;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
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

namespace NetCraft.Client.Render.World;

//LevelRenderer world rendering main entry, maps to vanilla LevelRenderer
//W8 rework: removed synchronous StagedVertexBuffer building, holds a SectionRenderDispatcher for async compiling; the Render thread only does Upload+Draw
//Prepare builds the frustum and calls dispatcher.SetCameraPosition to trigger a ViewArea diff, enqueueing newly visible sections for compile
//Upload calls dispatcher.UploadTerrainBuffers to upload compiled meshes inside the Lock
//Draw iterates EnumerateVisibleSections in Solid→Cutout→Translucent order and submits via GetSectionSlice
//One DrawCall per section: 100 sections = 300 DrawCalls; uber buffer batching left as an optimization
//Vertex format POSITION_COLOR_UV_LIGHT_NORMAL stride 40 bytes; section offset is baked in ChunkMeshBuilder, shader Model=Identity
//Subscribes to level.SectionDirty and forwards to dispatcher.MarkDirty, spreading to itself + 6 neighbors
public sealed class LevelRenderer : IDisposable, IWorldRenderer
{
    //POSITION_COLOR_UV_LIGHT_NORMAL vertex stride 40 bytes Position(12)+Color(4)+UV0(8)+Light(4)+Normal(12)
    private const int VertexStride = 40;

    private readonly ClientLevel _level;
    private readonly Camera _camera;
    private readonly SectionRenderDispatcher _dispatcher;
    private readonly Action<SectionPos> _sectionDirtyHandler;
    private readonly Action<SectionPos> _chunkUnloadedHandler;
    //Visible section buffer reused across frames to avoid allocating a List every frame
    private readonly List<RenderSection> _visibleSectionBuffer = new();

    //This frame's frustum; built in Prepare and reused by Draw
    private Frustum? _frustum;
    //This frame's total vertex count; accumulated in Draw for F3 display
    private int _totalVertexCount;
    //EntityDispatcher entity render dispatcher; null means no entity rendering, injected by the Game layer
    public EntityRenderDispatcher? EntityDispatcher { get; set; }

    //MovingBlocks moving block render pass; null means no rendering of advancing pistons, injected by the Game layer
    public MovingBlockRenderer? MovingBlocks { get; set; }

    //Performance metrics for GameScreen's F3 display
    public int SectionCount => _dispatcher.SectionCount;
    public int VisibleSectionCount => _dispatcher.VisibleSectionCount;
    public int TotalVertexCount => _totalVertexCount;
    public int DrawCallCount { get; private set; }

    //ViewProj this frame's view*proj matrix; VulkanGuiApp reads this property to upload the set 0 UBO
    public Matrix4x4 ViewProj => _camera.GetViewProjMatrix();

    public LevelRenderer(ClientLevel level, Camera camera, SectionRenderDispatcher dispatcher)
    {
        _level = level;
        _camera = camera;
        _dispatcher = dispatcher;
        _sectionDirtyHandler = pos => _dispatcher.MarkDirty(pos);
        _level.SectionDirty += _sectionDirtyHandler;
        //On chunk unload, clean up RenderSection and release GPU buffers to prevent leaks
        //UnloadSection holds the Lock to prevent racing with the Render thread's Upload/Draw over buffer references
        _chunkUnloadedHandler = pos =>
        {
            _dispatcher.Lock();
            try { _dispatcher.UnloadSection(pos); }
            finally { _dispatcher.Unlock(); }
        };
        _level.ChunkUnloaded += _chunkUnloadedHandler;
    }

    //Prepare builds the frustum and calls dispatcher.SetCameraPosition to trigger a ViewArea diff, enqueueing newly visible sections for compile
    //W7 synchronous traversal Build removed; mesh building moved to the dispatcher's background thread
    public void Prepare()
    {
        _frustum = new Frustum(_camera.GetViewProjMatrix());
        _dispatcher.SetCameraPosition(_camera, _frustum);
        PrepareEntities();
        //Advancing pistons are re-baked every frame; the displacement changes with ticks and a static mesh cannot carry it
        MovingBlocks?.Prepare();
    }

    //PrepareEntities converts client entities into render states for the entity dispatcher
    //Types without a registered renderer are skipped by the dispatcher itself; no type filtering here
    private void PrepareEntities()
    {
        var entities = EntityDispatcher;
        if (entities is null) return;
        entities.ClearEntities();
        foreach (var entity in _level.Entities)
        {
            var typeName = entity.Type.Id.ToString();
            entities.AddEntity(typeName, new EntityRenderState
            {
                Position = new Vector3((float)entity.Pos.X, (float)entity.Pos.Y, (float)entity.Pos.Z),
                YRot = entity.YRot,
                XRot = entity.XRot,
                Name = typeName,
                //Age in ticks is derived from the client world tick; when the world is frozen the count does not advance, so animation stops
                AgeInTicks = _level.TickCount - entity.SpawnedAtTick,
                BobOffset = entity.BobOffset,
                //Business data the renderer needs is passed through here; the Gpu layer does not know entity types
                CustomData = entity.DroppedItem,
            });
        }
        entities.Prepare(_camera.Position);
    }

    //Upload calls dispatcher.UploadTerrainBuffers to upload compiled meshes inside the Lock into GpuBufferPool-borrowed buffers
    //The device parameter is kept for IWorldRenderer signature compatibility; the dispatcher internally uses the device bound to GpuBufferPool
    public void Upload(GpuDevice device)
    {
        _dispatcher.Lock();
        try { _dispatcher.UploadTerrainBuffers(); }
        finally { _dispatcher.Unlock(); }
        EntityDispatcher?.Upload(device);
        MovingBlocks?.Upload(device);
    }

    //Draw iterates visible sections in Solid→Cutout→Translucent order and submits via GetSectionSlice
    //Pipeline switches per layer: one SetPipeline+descBinder per layer, one DrawCall per layer within a section
    //Traversal inside the Lock ensures buffer references are not reclaimed between Upload and Draw
    public void Draw(IRenderPass pass,
        Func<RenderPipeline, CompiledRenderPipeline> pipelineResolver,
        Action<IRenderPass> descBinder)
    {
        DrawCallCount = 0;
        _totalVertexCount = 0;
        if (_frustum is null) return;
        _dispatcher.Lock();
        try
        {
            _visibleSectionBuffer.Clear();
            foreach (var section in _dispatcher.EnumerateVisibleSections(_frustum))
            {
                _visibleSectionBuffer.Add(section);
                if (section.Mesh is not null) _totalVertexCount += section.Mesh.TotalVertexCount;
            }
            foreach (var layer in s_layers)
            {
                var pipeline = layer switch
                {
                    RenderLayer.Solid => WorldRenderPipelines.SOLID_TERRAIN,
                    RenderLayer.Cutout => WorldRenderPipelines.CUTOUT_TERRAIN,
                    RenderLayer.Translucent => WorldRenderPipelines.TRANSLUCENT_TERRAIN,
                    _ => throw new InvalidOperationException($"Unknown RenderLayer {layer}")
                };
                pass.SetPipeline(pipelineResolver(pipeline));
                descBinder(pass);
                //The terrain pipeline enables VK_DYNAMIC_STATE_SCISSOR, so CmdSetScissor must be called before draw or driver behavior is undefined
                pass.DisableScissor();
                foreach (var section in _visibleSectionBuffer)
                {
                    var slice = _dispatcher.GetSectionSlice(section.Pos, layer);
                    if (slice is null) continue;
                    pass.SetVertexBuffer(0, slice.Value.VertexBuffer, (ulong)slice.Value.BaseVertex * VertexStride);
                    pass.SetIndexBuffer(slice.Value.IndexBuffer, GpuIndexType.UInt32);
                    pass.DrawIndexed(slice.Value.IndexCount, 1, slice.Value.FirstIndex, 0, 0);
                    DrawCallCount++;
                }
            }
        }
        finally { _dispatcher.Unlock(); }
        //Moving blocks use the terrain pipelines and are drawn after chunks in the same pass
        if (MovingBlocks is not null)
        {
            MovingBlocks.Draw(pass, pipelineResolver, descBinder);
            DrawCallCount += MovingBlocks.LastDrawCallCount;
        }
        //Entity rendering comes after terrain in the same world pass; depth test ensures correct occlusion
        //Batched by model.Pipeline into Solid/Cutout/Translucent, each layer with its own SetPipeline
        if (EntityDispatcher is not null && EntityDispatcher.HasContent)
        {
            EntityDispatcher.Draw(pass, pipelineResolver, descBinder);
            DrawCallCount += EntityDispatcher.LastDrawCallCount;
        }
    }

    public void Dispose()
    {
        _level.SectionDirty -= _sectionDirtyHandler;
        _level.ChunkUnloaded -= _chunkUnloadedHandler;
        //The entity dispatcher is disposed along with world rendering; it holds vertex/index buffers borrowed from the buffer pool
        EntityDispatcher?.Dispose();
        EntityDispatcher = null;
        MovingBlocks?.Dispose();
        MovingBlocks = null;
    }

    private static readonly RenderLayer[] s_layers = { RenderLayer.Solid, RenderLayer.Cutout, RenderLayer.Translucent };
}
