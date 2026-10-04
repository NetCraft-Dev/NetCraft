using System.Threading;
using NetCraft.Gpu;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Render.World;

//RenderSectionState section 渲染状态机
//Empty 未编译 Queued 已入编译队列 Compiling 后台编译中 Compiled 编译完待上传 Uploaded 已上传可 Draw
//Dirty 已上传但需重编译旧 buffer 仍有效 Draw 跳过等重编译完成
public enum RenderSectionState : byte
{
    Empty,
    Queued,
    Compiling,
    Compiled,
    Uploaded,
    Dirty
}

//RenderSection 单 section 渲染数据持有者+状态机
//_mesh 用 Interlocked.Exchange 原子发布编译线程写 Render 线程读引用赋值本身原子 Exchange 提供发布屏障
//VertexBuffer/IndexBuffer 只在 Render 线程 Lock 内读写不需同步原语
//BoundingBox 按 Pos<<4 构造供 Frustum 剔除
public sealed class RenderSection
{
    public SectionPos Pos { get; }
    public AABB BoundingBox { get; }

    private volatile RenderSectionState _state;
    public RenderSectionState State => _state;

    private SectionMesh? _mesh;
    public SectionMesh? Mesh => _mesh;

    public GpuBuffer? VertexBuffer { get; private set; }
    public GpuBuffer? IndexBuffer { get; private set; }

    //Slices 每 layer 上传后的偏移信息供 dispatcher.GetSectionSlice 构造 SectionSlice
    //null 表示该 layer 无顶点 Draw 时跳过
    public UploadedSlice?[] Slices { get; } = new UploadedSlice?[3];

    public bool HasUploadedBuffers => VertexBuffer is not null && IndexBuffer is not null;

    //IsRemoved chunk 卸载标记 UnloadSection 置 true 编译/上传线程检查跳过防竞态
    private volatile bool _removed;
    public bool IsRemoved => _removed;

    public RenderSection(SectionPos pos)
    {
        Pos = pos;
        var minX = pos.X << 4;
        var minY = pos.Y << 4;
        var minZ = pos.Z << 4;
        BoundingBox = new AABB(minX, minY, minZ, minX + 16, minY + 16, minZ + 16);
    }

    //MarkRemoved 标记已卸载 编译线程 PublishMesh 前检查避免已移除 section 继续入上传队列
    public void MarkRemoved() => _removed = true;

    //TryMarkDirty 标脏入队 CAS 状态防重复
    //Empty/Compiled → Queued 无旧 buffer 直接入队
    //Uploaded → Dirty 旧 buffer 仍有效 Draw 跳过等重编译完成 UploadTerrainBuffers 检测旧 buffer 先 ReturnBuffer
    //Queued/Compiling/Dirty 返回 false 已入队或编译中跳过
    public bool TryMarkDirty()
    {
        while (true)
        {
            var prev = _state;
            if (prev == RenderSectionState.Queued
                || prev == RenderSectionState.Compiling
                || prev == RenderSectionState.Dirty)
                return false;
            var target = prev == RenderSectionState.Uploaded
                ? RenderSectionState.Dirty
                : RenderSectionState.Queued;
            if (Interlocked.CompareExchange(ref _state, target, prev) == prev)
                return true;
        }
    }

    //TryBeginCompile worker Take 后调 Queued/Dirty → Compiling
    public bool TryBeginCompile()
    {
        while (true)
        {
            var prev = _state;
            if (prev != RenderSectionState.Queued && prev != RenderSectionState.Dirty)
                return false;
            if (Interlocked.CompareExchange(ref _state, RenderSectionState.Compiling, prev) == prev)
                return true;
        }
    }

    //PublishMesh 编译线程调原子替换 mesh + 转 Compiled 供 Render 线程 Upload
    public void PublishMesh(SectionMesh mesh)
    {
        Interlocked.Exchange(ref _mesh, mesh);
        Interlocked.Exchange(ref _state, RenderSectionState.Compiled);
    }

    //SetUploadedBuffers Render 线程 Lock 内调上传完更新 buffer 引用 + 转 Uploaded
    public void SetUploadedBuffers(GpuBuffer vb, GpuBuffer ib)
    {
        VertexBuffer = vb;
        IndexBuffer = ib;
        _state = RenderSectionState.Uploaded;
    }

    //ReleaseBuffers Render 线程 Lock 内调重编译前或 unload 时归还旧 buffer 到 pool
    public void ReleaseBuffers(GpuBufferPool pool)
    {
        if (VertexBuffer is not null) pool.ReturnBuffer(VertexBuffer);
        if (IndexBuffer is not null) pool.ReturnBuffer(IndexBuffer);
        VertexBuffer = null;
        IndexBuffer = null;
        Array.Fill(Slices, null);
    }
}

//UploadedSlice 单 layer 上传后的偏移信息存 RenderSection.Slices
//BaseVertex 该 layer 顶点在 vertex buffer 的起始顶点索引 FirstIndex 索引起始 IndexCount 索引数
public readonly record struct UploadedSlice(int BaseVertex, int FirstIndex, int IndexCount);
