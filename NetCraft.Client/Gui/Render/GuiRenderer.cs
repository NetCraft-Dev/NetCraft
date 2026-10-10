using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
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

namespace NetCraft.Client.Gui.Render;

//GuiRenderer render phase main entry, maps to vanilla GuiRenderer
//The submission phase groups and batches by pipeline+texture+scissor in Prepare
//The render phase submits to the GPU in Draw, merging same-group elements into 1 DrawCall
//Stage 4.4a implements the Prepare batching logic, unit-testable; 4.4b implements the Upload/Draw submission
public sealed class GuiRenderer : IDisposable
{
    private readonly StagedVertexBuffer _vertexBuffer;
    private readonly List<Mesh> _meshes = new();
    //_pipRenderers PIP renderer registry dispatched by RenderStateClass, maps to vanilla pictureInPictureRenderers
    //Filled externally by RegisterPipRenderer; Prepare walks pipStates and calls the matching renderer.Prepare
    private readonly Dictionary<Type, IPictureInPictureRenderer> _pipRenderers = new();
    private bool _disposed;
    //_firstMeshIndexAfterBlur the BeforeBlur segment's mesh count; the AfterBlur segment starts here
    //-1 means no blur split, Prepare not called or the snapshot has no BlurBeforeThisStratum
    private int _firstMeshIndexAfterBlur = -1;
    //DrawCallCount drawIndexed/draw count submitted by DrawRange in the last frame, for perf acceptance
    //MeshCount batched mesh count VertexCount batched total vertex count
    public int DrawCallCount { get; private set; }
    public int MeshCount => _meshes.Count;
    public int VertexCount { get; private set; }

    public GuiRenderer()
    {
        _vertexBuffer = new StagedVertexBuffer();
    }

    //Meshes the batched mesh list for Draw to iterate and submit
    public IReadOnlyList<Mesh> Meshes => _meshes;

    //FirstMeshIndexAfterBlur the BeforeBlur/AfterBlur split point, read after Prepare
    //-1 no blur split 0 means BeforeBlur is empty >0 means BeforeBlur has meshes and AfterBlur starts here
    public int FirstMeshIndexAfterBlur => _firstMeshIndexAfterBlur;

    //RegisterPipRenderer registers a PIP renderer dispatched by RenderStateClass, maps to vanilla pictureInPictureRenderers.put
    //Prepare walks pipStates, looks up by state type and calls renderer.Prepare for offscreen rendering+blit
    public void RegisterPipRenderer<T>(PictureInPictureRenderer<T> renderer) where T : PictureInPictureRenderState
    {
        _pipRenderers[renderer.RenderStateClass] = renderer;
    }

    //PreparePip calls PIP renderer.Prepare on the Render thread for offscreen rendering+blit, adding the BlitRenderState to the snapshot
    //Must be called before Prepare so the BlitRenderState enters the snapshot for batching
    //P15 moves the call to OnRecordCommandBuffer; vkQueueSubmit must be serial on the Render thread to avoid queue contention
    public void PreparePip(GuiRenderState snapshot, int guiScale)
    {
        if (guiScale <= 0 || _pipRenderers.Count == 0) return;
        snapshot.ForEachPictureInPicture(pip =>
        {
            if (_pipRenderers.TryGetValue(pip.GetType(), out var renderer))
                renderer.Prepare(pip, snapshot, guiScale);
        });
    }

    //Prepare the render phase groups and batches by pipeline+texture+scissor
    //Same-group elements are written into one Draw and submitted in a single drawIndexed
    //Repeated calls are safe: EndFrame resets the previous frame then re-batches
    //snapshot is passed by the caller, built and deep-copied on Tick; the Render thread only reads it
    //For a blur split it batches BeforeBlur elements then AfterBlur elements, with FirstMeshIndexAfterBlur marking the split
    //Without BlurBeforeThisStratum, BeforeBlur iterates everything and AfterBlur is empty, equivalent to no split
    //PIP prepare is already done earlier by OnRecordCommandBuffer calling PreparePip, so Prepare only batches and no longer calls PIP Submit
    public void Prepare(GuiRenderState snapshot, int guiScale = 0)
    {
        _meshes.Clear();
        _firstMeshIndexAfterBlur = -1;
        _vertexBuffer.EndFrame();

        Action<GuiElementRenderState> append = element =>
        {
            var mesh = FindMesh(element.Pipeline, element.TextureSetup, element.ScissorArea);
            if (mesh == null)
            {
                var format = element.Pipeline.VertexFormatPerBuffer.Count > 0
                    ? element.Pipeline.VertexFormatPerBuffer[0]
                    : null;
                if (format == null)
                    throw new InvalidOperationException($"pipeline {element.Pipeline.Location} lacks a VertexFormat, cannot AppendDraw");
                var draw = _vertexBuffer.AppendDraw(format, element.Pipeline.PrimitiveTopology);
                var builder = _vertexBuffer.GetVertexBuilder(draw);
                mesh = new Mesh(element.Pipeline, element.TextureSetup, element.ScissorArea, draw, builder);
                _meshes.Add(mesh);
            }
            element.BuildVertices(mesh.VertexBuilder);
        };
        snapshot.ForEachElement(append, TraverseRange.BeforeBlur);
        if (snapshot.HasBlurSplit)
            _firstMeshIndexAfterBlur = _meshes.Count;
        snapshot.ForEachElement(append, TraverseRange.AfterBlur);

        foreach (var mesh in _meshes)
            _vertexBuffer.EndDraw(mesh.Draw);
        DrawCallCount = 0;
        VertexCount = 0;
        foreach (var mesh in _meshes)
            VertexCount += mesh.Draw.VertexCount;
    }

    //Upload stitches vertices into the vertex buffer and indices into the index buffer, then uploads to the GPU
    //Must be called after Prepare and before Draw; the buffer is reused across frames, host-visible and rewritten each frame
    public void Upload(GpuDevice device) => _vertexBuffer.Upload(device);

    //Draw render phase submits everything, equivalent to DrawRange(0, _meshes.Count)
    //With a blur split the caller uses DrawRange to submit BeforeBlur/AfterBlur separately with blur in between
    public void Draw(RenderPass pass, Action<RenderPass> bindGlobals, Func<TextureSetup, (GpuTextureView? View, GpuSampler? Sampler)> textureResolver)
        => DrawRange(pass, bindGlobals, textureResolver, 0, _meshes.Count);

    //DrawRange submits the meshes in _meshes[start..end) for segmented blur execution
    //The pipeline is bound declaratively, then bindGlobals writes the global uniforms for that pipeline layout
    //Each mesh's color texture is bound through the last BindGroupLayout's sampler name
    //Must be called after Upload with the render pass open; QUADS uses DrawIndexed with baseVertex encoded in the indices, others use non-indexed Draw
    public void DrawRange(RenderPass pass, Action<RenderPass> bindGlobals, Func<TextureSetup, (GpuTextureView? View, GpuSampler? Sampler)> textureResolver, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            var mesh = _meshes[i];
            pass.SetPipeline(mesh.Pipeline);
            bindGlobals(pass);
            DrawCallCount++;

            //The color texture binds through the sampler name declared by the pipeline's last BindGroupLayout
            var bindGroup = mesh.Pipeline.BindGroupLayouts[^1];
            if (bindGroup.Samplers.Count > 0)
            {
                var (view, sampler) = textureResolver(mesh.TextureSetup);
                if (view != null && sampler != null)
                    pass.BindTexture(bindGroup.Samplers[0], view, sampler);
            }

            //scissor switched dynamically; an empty rectangle disables clipping for full-screen rendering
            if (mesh.ScissorArea.IsEmpty)
                pass.DisableScissor();
            else
                pass.EnableScissor(mesh.ScissorArea.X, mesh.ScissorArea.Y, mesh.ScissorArea.Width, mesh.ScissorArea.Height);

            var info = _vertexBuffer.GetExecuteInfo(mesh.Draw);
            pass.SetVertexBuffer(0, info.VertexBuffer.Slice());
            if (info.IndexBuffer != null && info.IndexCount > 0)
            {
                //QUADS indices already encode baseVertex; DrawIndexed passes vertexOffset 0 and relies on the firstIndex offset
                pass.SetIndexBuffer(info.IndexBuffer, NetCraft.Client.Blaze3d.IndexType.Int);
                pass.DrawIndexed(info.IndexCount, 1, info.FirstIndex, 0, 0);
            }
            else
            {
                //Non-QUADS uses non-indexed drawing with firstVertex = BaseVertex offset to this Draw's vertex segment
                pass.Draw(mesh.Draw.VertexCount, 1, mesh.Draw.BaseVertex, 0);
            }
        }
    }

    //GetExecuteInfo returns the mesh's Draw execution info, passed to IRenderPass in the submission phase
    public ExecuteInfo GetExecuteInfo(Mesh mesh) => _vertexBuffer.GetExecuteInfo(mesh.Draw);

    //EndFrame resets the staging area while keeping GPU buffers for cross-frame reuse
    public void EndFrame() => _vertexBuffer.EndFrame();

    //FindMesh linearly searches for the mesh matching pipeline+texture+scissor
    //The mesh count is usually small (a few to a few dozen), so a linear search suffices
    private Mesh? FindMesh(RenderPipeline pipeline, TextureSetup texture, ScreenRectangle scissor)
    {
        foreach (var mesh in _meshes)
            if (mesh.Matches(pipeline, texture, scissor))
                return mesh;
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _vertexBuffer.Dispose();
        _disposed = true;
    }
}

//Mesh render state group for one batch; elements with the same pipeline+texture+scissor are merged
//Draw records vertex/index metadata; ExecuteDraw submits with pipeline/texture/scissor
public sealed class Mesh
{
    public RenderPipeline Pipeline { get; }
    public TextureSetup TextureSetup { get; }
    public ScreenRectangle ScissorArea { get; }
    public Draw Draw { get; }
    public IVertexConsumer VertexBuilder { get; }

    internal Mesh(RenderPipeline pipeline, TextureSetup textureSetup, ScreenRectangle scissorArea, Draw draw, IVertexConsumer vertexBuilder)
    {
        Pipeline = pipeline;
        TextureSetup = textureSetup;
        ScissorArea = scissorArea;
        Draw = draw;
        VertexBuilder = vertexBuilder;
    }

    //Matches whether batching is possible: same pipeline (reference)+texture (texture reference)+scissor (value)
    public bool Matches(RenderPipeline pipeline, TextureSetup texture, ScreenRectangle scissor)
        => ReferenceEquals(Pipeline, pipeline)
            && Equals(TextureSetup, texture)
            && ScissorArea == scissor;
}
