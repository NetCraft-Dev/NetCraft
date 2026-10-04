using System.Numerics;
using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render.Culling;
using NetCraft.Game.Client.Render.Entity;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Render.World;

//LevelRenderer 世界渲染主入口对标原版 LevelRenderer
//W8 改造：删 StagedVertexBuffer 同步构建 持 SectionRenderDispatcher 异步编译 Render 线程只做 Upload+Draw
//Prepare 构造 frustum 调 dispatcher.SetCameraPosition 触发 ViewArea diff 新可见 section 入编译队列
//Upload 调 dispatcher.UploadTerrainBuffers Lock 内上传编译完成的 mesh
//Draw 按 Solid→Cutout→Translucent 顺序遍历 EnumerateVisibleSections 取 GetSectionSlice 提交
//每 section 独立 DrawCall 100 section=300 DrawCall uber buffer 合批留优化
//顶点格式 POSITION_COLOR_UV_LIGHT_NORMAL stride 40 字节 section offset 在 ChunkMeshBuilder 已 bake shader Model=Identity
//订阅 level.SectionDirty 转发 dispatcher.MarkDirty 扩散自身+6邻居
public sealed class LevelRenderer : IDisposable, IWorldRenderer
{
    //POSITION_COLOR_UV_LIGHT_NORMAL 顶点 stride 40 字节 Position(12)+Color(4)+UV0(8)+Light(4)+Normal(12)
    private const int VertexStride = 40;

    private readonly ClientLevel _level;
    private readonly Camera _camera;
    private readonly SectionRenderDispatcher _dispatcher;
    private readonly Action<SectionPos> _sectionDirtyHandler;
    private readonly Action<SectionPos> _chunkUnloadedHandler;
    //可见 section 缓冲跨帧复用避免每帧分配 List
    private readonly List<RenderSection> _visibleSectionBuffer = new();

    //本帧 frustum Prepare 构造 Draw 复用
    private Frustum? _frustum;
    //本帧总顶点数 Draw 内累加供 F3 显示
    private int _totalVertexCount;
    //EntityDispatcher 实体渲染调度器 null 时不渲染实体由 Game 层注入
    public EntityRenderDispatcher? EntityDispatcher { get; set; }

    //MovingBlocks 移动方块渲染通道 null 时不渲染推进中的活塞 由 Game 层注入
    public MovingBlockRenderer? MovingBlocks { get; set; }

    //性能指标供 GameScreen F3 显示
    public int SectionCount => _dispatcher.SectionCount;
    public int VisibleSectionCount => _dispatcher.VisibleSectionCount;
    public int TotalVertexCount => _totalVertexCount;
    public int DrawCallCount { get; private set; }

    //ViewProj 当前帧 view*proj 矩阵 VulkanGuiApp 读此属性上传 set 0 UBO
    public Matrix4x4 ViewProj => _camera.GetViewProjMatrix();

    public LevelRenderer(ClientLevel level, Camera camera, SectionRenderDispatcher dispatcher)
    {
        _level = level;
        _camera = camera;
        _dispatcher = dispatcher;
        _sectionDirtyHandler = pos => _dispatcher.MarkDirty(pos);
        _level.SectionDirty += _sectionDirtyHandler;
        //chunk 卸载时清理 RenderSection 释放 GPU buffer 防泄漏
        //UnloadSection 持 Lock 防与 Render 线程 Upload/Draw 竞争 buffer 引用
        _chunkUnloadedHandler = pos =>
        {
            _dispatcher.Lock();
            try { _dispatcher.UnloadSection(pos); }
            finally { _dispatcher.Unlock(); }
        };
        _level.ChunkUnloaded += _chunkUnloadedHandler;
    }

    //Prepare 构造 frustum 调 dispatcher.SetCameraPosition 触发 ViewArea diff 新可见 section 入编译队列
    //W7 同步遍历 Build 已删除 mesh 构建移到 dispatcher 后台线程
    public void Prepare()
    {
        _frustum = new Frustum(_camera.GetViewProjMatrix());
        _dispatcher.SetCameraPosition(_camera, _frustum);
        PrepareEntities();
        //推进中的活塞每帧重烘一遍 位移随刻数变静态网格承载不了
        MovingBlocks?.Prepare();
    }

    //PrepareEntities 把客户端实体转成渲染状态交给实体调度器
    //未注册渲染器的类型由调度器自行跳过 这里不按类型过滤
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
                //存活刻数按客户端世界刻推算 世界冻结时刻数不涨 动画随之停住
                AgeInTicks = _level.TickCount - entity.SpawnedAtTick,
                BobOffset = entity.BobOffset,
                //渲染器需要的业务数据经这里透传 Gpu 层不认识实体类型
                CustomData = entity.DroppedItem,
            });
        }
        entities.Prepare(_camera.Position);
    }

    //Upload 调 dispatcher.UploadTerrainBuffers Lock 内上传编译完成的 mesh 到 GpuBufferPool 借出的 buffer
    //device 参数保留兼容 IWorldRenderer 签名 dispatcher 内部用 GpuBufferPool 绑定的 device
    public void Upload(GpuDevice device)
    {
        _dispatcher.Lock();
        try { _dispatcher.UploadTerrainBuffers(); }
        finally { _dispatcher.Unlock(); }
        EntityDispatcher?.Upload(device);
        MovingBlocks?.Upload(device);
    }

    //Draw 按 Solid→Cutout→Translucent 顺序遍历可见 section 取 GetSectionSlice 提交
    //pipeline 按 layer 切换每 layer 一次 SetPipeline+descBinder section 内每 layer 一次 DrawCall
    //Lock 内遍历保证 Upload 与 Draw 间 buffer 引用不被回收
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
                    _ => throw new InvalidOperationException($"未知 RenderLayer {layer}")
                };
                pass.SetPipeline(pipelineResolver(pipeline));
                descBinder(pass);
                //terrain pipeline 启用 VK_DYNAMIC_STATE_SCISSOR draw 前必须 CmdSetScissor 否则驱动未定义行为
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
        //移动方块用的是 terrain 那套 pipeline 排在区块之后同一遍绘制
        if (MovingBlocks is not null)
        {
            MovingBlocks.Draw(pass, pipelineResolver, descBinder);
            DrawCallCount += MovingBlocks.LastDrawCallCount;
        }
        //实体渲染在 terrain 之后同一 world pass 内 depth test 保证遮挡正确
        //按 model.Pipeline 分 Solid/Cutout/Translucent 批次每层独立 SetPipeline
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
        //实体调度器随世界渲染一起释放 它持有从 buffer pool 借出的顶点索引缓冲
        EntityDispatcher?.Dispose();
        EntityDispatcher = null;
        MovingBlocks?.Dispose();
        MovingBlocks = null;
    }

    private static readonly RenderLayer[] s_layers = { RenderLayer.Solid, RenderLayer.Cutout, RenderLayer.Translucent };
}
