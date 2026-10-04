using System.Numerics;
using System.Runtime.InteropServices;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Game.Client.Render.Entity;

//EntityRenderDispatcher 实体渲染调度器对标原版 EntityRenderDispatcher
//管理 EntityRenderer 注册表按 EntityType 分派
//持 EntityVertexBuilder 收集所有可见实体顶点 Upload 到 GPU buffer 后 DrawIndexed
//PoC 骨架版所有实体共用一个 vertex/index buffer 同一 pipeline 后续按 RenderLayer 分批
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
    //_layerRanges 每层顶点/索引范围 Prepare 三趟按 pipeline 分批记录 Draw 按层 SetPipeline+偏移绘制
    private readonly (int VertexStart, int VertexCount, int IndexStart, int IndexCount)[] _layerRanges = new (int, int, int, int)[3];
    //_layerPipelines 实体 pipeline 按层索引 Solid=0 Cutout=1 Translucent=2 与 RenderLayer 枚举顺序一致
    private static readonly RenderPipeline[] s_layerPipelines =
    {
        EntityRenderPipelines.ENTITY_SOLID,
        EntityRenderPipelines.ENTITY_CUTOUT,
        EntityRenderPipelines.ENTITY_TRANSLUCENT
    };

    //EntityVertexCount 最近一帧实体顶点数供调试
    public int EntityVertexCount => _vertexCount;
    //EntityIndexCount 最近一帧实体索引数
    public int EntityIndexCount => _indexCount;
    //VisibleEntityCount 最近一帧可见实体数
    public int VisibleEntityCount => _visible.Count;
    //HasContent 是否有可渲染的实体顶点
    public bool HasContent => _indexCount > 0;
    //LastDrawCallCount 最近一帧 Draw 实际提交的批次（每层 1 次 最多 3）
    public int LastDrawCallCount { get; private set; }

    public EntityRenderDispatcher(GpuBufferPool bufferPool) => _bufferPool = bufferPool;

    //Register 注册实体渲染器按 entityTypeName 索引
    public void Register(string entityTypeName, EntityRenderer renderer)
        => _renderers[entityTypeName] = renderer;

    //ClearEntities 清空可见实体列表每帧 Prepare 前调
    public void ClearEntities() => _visible.Clear();

    //AddEntity 添加可见实体由 LevelRenderer 遍历实体列表时调
    //entityTypeName 匹配 Register 的 key 未注册的实体跳过
    public void AddEntity(string entityTypeName, EntityRenderState state)
    {
        if (_renderers.TryGetValue(entityTypeName, out var renderer))
            _visible.Add((state, renderer));
    }

    //Prepare 生成所有可见实体顶点到 builder 按 pipeline 分批连续顶点段
    //调用方负责相机变换 poseStack 从 Identity 开始每实体 push 世界变换
    //cameraPosition 用于实体相对位置计算
    //三趟遍历 Solid→Cutout→Translucent 每趟记录该层顶点/索引范围供 Draw 按层绘制
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
                //实体世界位置相对相机使 shader ViewProj 只含相机旋转+投影
                //与 terrain 一致 terrain 的 section offset bake 进顶点 position
                poseStack.Translate(
                    state.Position.X - cameraPosition.X,
                    state.Position.Y - cameraPosition.Y,
                    state.Position.Z - cameraPosition.Z);
                //实体 Y 轴旋转朝向
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

    //Upload 把 builder 顶点索引上传到 GPU buffer 从 pool 借 buffer
    //无内容时跳过不借 buffer 避免空 DrawCall
    public void Upload(GpuDevice device)
    {
        //归还上一帧的 buffer
        if (_vertexBuffer is not null) { _bufferPool.ReturnBuffer(_vertexBuffer); _vertexBuffer = null; }
        if (_indexBuffer is not null) { _bufferPool.ReturnBuffer(_indexBuffer); _indexBuffer = null; }
        if (_indexCount == 0) return;
        //上传顶点 11 float/顶点 = 44 字节
        var vertexSpan = CollectionsMarshal.AsSpan(_builder.Vertices);
        var vertexBytes = MemoryMarshal.AsBytes(vertexSpan).ToArray();
        _vertexBuffer = _bufferPool.GetBuffer(vertexBytes.Length, GpuBufferUsage.VertexBuffer);
        _vertexBuffer.Upload<byte>(vertexBytes);
        //上传索引 int/索引 = 4 字节
        var indexSpan = CollectionsMarshal.AsSpan(_builder.Indices);
        var indexBytes = MemoryMarshal.AsBytes(indexSpan).ToArray();
        _indexBuffer = _bufferPool.GetBuffer(indexBytes.Length, GpuBufferUsage.IndexBuffer);
        _indexBuffer.Upload<byte>(indexBytes);
    }

    //Draw 按 pipeline 分批录制实体渲染命令 单 buffer 每层偏移 DrawIndexed
    //pipelineResolver 解析 RenderPipeline→CompiledRenderPipeline descBinder 绑定 descriptor set
    //Solid→Cutout→Translucent 顺序每层 SetPipeline+DrawIndexed 无内容的层跳过
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
