using NetCraft.Game.Client.Level;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Game.Client.Render.World;

//MovingBlockRenderer 客户端移动方块的渲染通道 对应原版客户端对 PistonMovingBlockEntity 的渲染
//区块网格是静态烘焙的 活塞每刻推进半格这种位移烘不进去 这里单开一条每帧重建的通道
//每帧把被推方块的模型按当前进度平移到世界坐标再烘一份顶点 上传后按层绘制
//顶点 bake 的是世界坐标与区块网格同一套 shader 的 ViewProj 只含相机
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

    //MovingBlockCount 最近一帧参与渲染的移动方块数供调试
    public int MovingBlockCount { get; private set; }

    //LastDrawCallCount 最近一帧实际提交的批次数 每层一次最多 3
    public int LastDrawCallCount { get; private set; }

    //Prepare 按当前刻把每格移动方块烘到世界坐标
    //进度由收到状态包后走过的刻数推 每刻半格两刻走完 与原版 PistonMovingBlockEntity.tick 一致
    public void Prepare()
    {
        MovingBlockCount = 0;
        _meshData.Clear();
        var pose = new PoseStack();
        foreach (var moving in _level.MovingBlocks)
        {
            var progress = moving.Progress;
            //与原版 getXOff/getYOff/getZOff 同一换算: 伸出时从 -1 走到 0 收回时从 0 走到 1
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

    //Upload 把本帧烘好的顶点索引按层上传 上一层没内容的层不借 buffer 免得空 DrawCall
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

    //Draw 按 Solid→Cutout→Translucent 顺序提交 无内容的层跳过
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

    //ReturnLayer 归还该层的 GPU buffer 并清零索引计数
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
