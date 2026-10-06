using NetCraft.Game.Client.Level;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Game.Client.Render.World;

//MovingBlockRenderer client rendering pass for moving blocks, maps to the vanilla client's rendering of PistonMovingBlockEntity
//The chunk mesh is baked statically; a displacement like a piston advancing half a block each tick cannot be baked in, so this opens a dedicated per-frame rebuilt pass
//Each frame it translates the pushed block's model to world coordinates by current progress and bakes a vertex set, uploaded then drawn per layer
//Vertices bake world coordinates; same shader set as the chunk mesh, whose ViewProj only contains the camera
public sealed class MovingBlockRenderer : IDisposable
{
    private const int LayerCount = 3;

    private readonly ClientLevel _level;
    private readonly ChunkMeshBuilder _builder;
    private readonly GpuBufferPool _bufferPool;
    private readonly ChunkMeshData _meshData = new();
    private readonly GpuBuffer?[] _vertexBuffers = new GpuBuffer?[LayerCount];
    private readonly GpuBuffer?[] _indexBuffers = new GpuBuffer?[LayerCount];
    private readonly int[] _indexCounts = new int[LayerCount];
    private bool _disposed;

    public MovingBlockRenderer(ClientLevel level, ChunkMeshBuilder builder, GpuBufferPool bufferPool)
    {
        _level = level;
        _builder = builder;
        _bufferPool = bufferPool;
    }

    //MovingBlockCount last frame's number of rendered moving blocks, for debugging
    public int MovingBlockCount { get; private set; }

    //LastDrawCallCount batches actually submitted last frame, one per layer, at most 3
    public int LastDrawCallCount { get; private set; }

    //Prepare bakes each moving block to world coordinates for the current tick
    //Progress is derived from ticks elapsed since receiving the state packet: half a block per tick, done in two ticks, consistent with vanilla PistonMovingBlockEntity.tick
    public void Prepare()
    {
        MovingBlockCount = 0;
        _meshData.Clear();
        var pose = new PoseStack();
        foreach (var moving in _level.MovingBlocks)
        {
            var progress = moving.Progress;
            //Same conversion as vanilla getXOff/getYOff/getZOff: extending goes from -1 to 0, retracting from 0 to 1
            var extended = moving.Extending ? progress - 1f : 1f - progress;
            pose.PushPose();
            pose.Scale(1f / 16f, 1f / 16f, 1f / 16f);
            pose.Translate(
                moving.Pos.X + moving.Direction.StepX * extended,
                moving.Pos.Y + moving.Direction.StepY * extended,
                moving.Pos.Z + moving.Direction.StepZ * extended);
            _builder.BuildBlock(moving.RenderState, moving.Pos, pose, _meshData);
            pose.PopPose();
            MovingBlockCount++;
        }
    }

    //Upload uploads this frame's baked vertices/indices per layer; empty layers do not borrow buffers to avoid an empty DrawCall
    public void Upload(GpuDevice device)
    {
        var mesh = _meshData.TotalVertexCount == 0 ? null : SectionMesh.FromChunkMeshData(_meshData);
        for (var i = 0; i < LayerCount; i++)
        {
            ReturnLayer(i);
            if (mesh is null) continue;
            var layer = (RenderLayer)i;
            var vertices = mesh.GetVertices(layer);
            var indices = mesh.GetIndices(layer);
            _indexCounts[i] = indices.Length;
            if (indices.Length == 0) continue;
            var vertexBuffer = _bufferPool.GetBuffer(vertices.Length, GpuBufferUsage.VertexBuffer);
            vertexBuffer.Upload(vertices.ToArray());
            var indexBuffer = _bufferPool.GetBuffer(indices.Length * sizeof(int), GpuBufferUsage.IndexBuffer);
            indexBuffer.Upload(indices.ToArray());
            _vertexBuffers[i] = vertexBuffer;
            _indexBuffers[i] = indexBuffer;
        }
    }

    //Draw submits in Solid→Cutout→Translucent order, skipping empty layers
    public void Draw(IRenderPass pass,
        Func<RenderPipeline, CompiledRenderPipeline> pipelineResolver,
        Action<IRenderPass> descBinder)
    {
        LastDrawCallCount = 0;
        for (var i = 0; i < LayerCount; i++)
        {
            if (_indexCounts[i] == 0) continue;
            var layer = (RenderLayer)i;
            if (_vertexBuffers[i] is not { } vertexBuffer || _indexBuffers[i] is not { } indexBuffer) continue;
            var pipeline = layer switch
            {
                RenderLayer.Solid => WorldRenderPipelines.SOLID_TERRAIN,
                RenderLayer.Cutout => WorldRenderPipelines.CUTOUT_TERRAIN,
                _ => WorldRenderPipelines.TRANSLUCENT_TERRAIN
            };
            pass.SetPipeline(pipelineResolver(pipeline));
            descBinder(pass);
            pass.DisableScissor();
            pass.SetVertexBuffer(0, vertexBuffer);
            pass.SetIndexBuffer(indexBuffer, GpuIndexType.UInt32);
            pass.DrawIndexed(_indexCounts[i], 1, 0, 0, 0);
            LastDrawCallCount++;
        }
    }

    //ReturnLayer returns the layer's GPU buffers and zeroes the index count
    private void ReturnLayer(int layer)
    {
        _indexCounts[layer] = 0;
        if (_vertexBuffers[layer] is { } vertexBuffer)
        {
            _bufferPool.ReturnBuffer(vertexBuffer);
            _vertexBuffers[layer] = null;
        }
        if (_indexBuffers[layer] is { } indexBuffer)
        {
            _bufferPool.ReturnBuffer(indexBuffer);
            _indexBuffers[layer] = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        for (var i = 0; i < LayerCount; i++) ReturnLayer(i);
        _disposed = true;
    }
}
