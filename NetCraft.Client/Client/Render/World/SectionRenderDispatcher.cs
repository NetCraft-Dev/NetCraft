using System.Collections.Concurrent;
using System.Threading;
using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render.Culling;
using NetCraft.Gpu;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Render.World;

//SectionRenderDispatcher 异步调度器对标原版 SectionRenderDispatcher
//后台线程编译 chunk mesh + mesh 缓存 + 脏标记 Render 线程只做 Upload+Draw
//_sections 持所有 RenderSection _compileQueue 后台线程消费 _uploadQueue Render 线程消费
//_mesh 用 Interlocked.Exchange 原子发布编译线程写 Render 线程读
//_syncRoot 粗粒度锁只保护 Render 线程 Upload+Draw 复合操作编译线程不持锁
//GpuBufferPool 跨帧复用 buffer 重编译/unload 时 ReturnBuffer 不调 EndFrame
public sealed class SectionRenderDispatcher : IDisposable
{
    private readonly ClientLevel _level;
    private readonly ChunkMeshBuilder _meshBuilder;
    private readonly GpuBufferPool _bufferPool;
    private readonly ConcurrentDictionary<long, RenderSection> _sections = new();
    //_compileQueue 后台线程 BlockingCollection.GetConsumingEnumerable 阻塞等
    private readonly BlockingCollection<RenderSection> _compileQueue = new(new ConcurrentQueue<RenderSection>());
    //_uploadQueue 编译线程 Enqueue Render 线程 Lock 内 TryDequeue
    private readonly ConcurrentQueue<RenderSection> _uploadQueue = new();
    private readonly Thread[] _workers;
    private readonly object _syncRoot = new();
    private readonly ViewArea _viewArea = new();
    private const int VertexStride = 40;
    private volatile bool _running;
    private bool _disposed;

    public int SectionCount => _sections.Count;
    public int PendingUploadCount => _uploadQueue.Count;
    public int VisibleSectionCount => _viewArea.VisibleCount;
    //BufferPoolInUseCount 当前借出的 GPU buffer 数供测试验证无泄漏 应 == UploadedSectionCount * 2
    public int BufferPoolInUseCount => _bufferPool.InUseCount;
    //UploadedSectionCount 已上传 section 数遍历 _sections 计数 Uploaded 状态供测试验证 buffer 数
    public int UploadedSectionCount
    {
        get
        {
            var count = 0;
            foreach (var section in _sections.Values)
                if (section.State == RenderSectionState.Uploaded) count++;
            return count;
        }
    }

    //workerCount 默认 max(1, ProcessorCount-1) CPU 密集型用专用线程不走 ThreadPool
    public SectionRenderDispatcher(ClientLevel level, ChunkMeshBuilder meshBuilder, GpuBufferPool bufferPool, int? workerCount = null)
    {
        _level = level;
        _meshBuilder = meshBuilder;
        _bufferPool = bufferPool;
        var count = workerCount ?? Math.Max(1, Environment.ProcessorCount - 1);
        _workers = new Thread[count];
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        for (var i = 0; i < _workers.Length; i++)
        {
            _workers[i] = new Thread(WorkerLoop) { IsBackground = true, Name = $"SectionRenderDispatcher-Worker-{i}" };
            _workers[i].Start();
        }
    }

    //Stop CompleteAdding 让 worker 退出 GetConsumingEnumerable Join 等线程结束
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _compileQueue.CompleteAdding();
        foreach (var worker in _workers)
            worker?.Join();
    }

    //SetCameraPosition Render 线程调 ViewArea.Update 视锥 diff 新可见 section 调 MarkDirty 编译
    //camera 参数首版未用留未来按 position 范围遍历优化当前遍历所有 loaded chunks
    public void SetCameraPosition(Camera camera, Frustum frustum)
    {
        _viewArea.Update(frustum, _level, pos => MarkDirty(pos));
    }

    //MarkDirty 标脏入队 自身+6 邻居跨 section 边界面剔除需重算
    //未加载的邻居 section 跳过不创建 RenderSection 避免 _sections 膨胀
    public void MarkDirty(SectionPos pos)
    {
        MarkDirtySingle(pos);
        MarkDirtySingle(new SectionPos(pos.X + 1, pos.Y, pos.Z));
        MarkDirtySingle(new SectionPos(pos.X - 1, pos.Y, pos.Z));
        MarkDirtySingle(new SectionPos(pos.X, pos.Y + 1, pos.Z));
        MarkDirtySingle(new SectionPos(pos.X, pos.Y - 1, pos.Z));
        MarkDirtySingle(new SectionPos(pos.X, pos.Y, pos.Z + 1));
        MarkDirtySingle(new SectionPos(pos.X, pos.Y, pos.Z - 1));
    }

    private void MarkDirtySingle(SectionPos pos)
    {
        if (_level.GetSection(pos.X, pos.Y, pos.Z) is null) return;
        var section = _sections.GetOrAdd(pos.AsLong(), _ => new RenderSection(pos));
        if (section.TryMarkDirty())
        {
            try { _compileQueue.Add(section); }
            catch (InvalidOperationException) { } //CompleteAdding 后 Add 抛忽略
        }
    }

    //Lock/Unlock Render 线程持锁期间 Upload+Draw 编译线程不持锁只原子发布 mesh
    public void Lock() => Monitor.Enter(_syncRoot);
    public void Unlock() => Monitor.Exit(_syncRoot);

    //UploadTerrainBuffers Render 线程 Lock 内调取 _uploadQueue 全部借 buffer 上传
    //重编译时旧 buffer 先 ReturnBuffer 再借新每 section 一个 vb+ib 含所有 layer
    //各 layer 顶点拼接索引偏移 baseVertex 记录 UploadedSlice 供 GetSectionSlice
    public void UploadTerrainBuffers()
    {
        while (_uploadQueue.TryDequeue(out var section))
        {
            //已卸载 section 跳过不借 buffer UnloadSection 已归还旧 buffer
            if (section.IsRemoved) continue;
            var mesh = section.Mesh;
            if (mesh is null || mesh.TotalVertexCount == 0)
            {
                //空 mesh 释放旧 buffer 标记 Uploaded 但 Slices 全 null Draw 跳过
                if (section.HasUploadedBuffers) section.ReleaseBuffers(_bufferPool);
                section.SetUploadedBuffers(_bufferPool.GetBuffer(4, GpuBufferUsage.VertexBuffer), _bufferPool.GetBuffer(4, GpuBufferUsage.IndexBuffer));
                continue;
            }
            if (section.HasUploadedBuffers) section.ReleaseBuffers(_bufferPool);
            var vertexBytes = new byte[mesh.TotalVertexCount * VertexStride];
            var indexData = new int[mesh.TotalIndexCount];
            var vertexByteOffset = 0;
            var indexOffset = 0;
            var baseVertex = 0;
            var firstIndex = 0;
            foreach (var layer in s_layers)
            {
                if (!mesh.HasLayer(layer)) continue;
                var vertices = mesh.GetVertices(layer);
                var vertexCount = mesh.GetVertexCount(layer);
                var indices = mesh.GetIndices(layer);
                vertices.CopyTo(new Span<byte>(vertexBytes, vertexByteOffset, vertices.Length));
                for (var i = 0; i < indices.Length; i++)
                    indexData[indexOffset + i] = indices[i] + baseVertex;
                section.Slices[(int)layer] = new UploadedSlice(baseVertex, firstIndex, indices.Length);
                vertexByteOffset += vertices.Length;
                indexOffset += indices.Length;
                baseVertex += vertexCount;
                firstIndex += indices.Length;
            }
            var vb = _bufferPool.GetBuffer(vertexBytes.Length, GpuBufferUsage.VertexBuffer);
            var ib = _bufferPool.GetBuffer(indexData.Length * sizeof(int), GpuBufferUsage.IndexBuffer);
            vb.Upload<byte>(vertexBytes);
            ib.Upload<int>(indexData);
            section.SetUploadedBuffers(vb, ib);
        }
    }

    //GetSectionSlice Render 线程 Lock 内调取 section 的 UploadedSlice + buffer 引用
    //State!=Uploaded 或 layer 无顶点返回 null Draw 跳过
    public SectionSlice? GetSectionSlice(SectionPos pos, RenderLayer layer)
    {
        if (!_sections.TryGetValue(pos.AsLong(), out var section)) return null;
        if (section.State != RenderSectionState.Uploaded) return null;
        var slice = section.Slices[(int)layer];
        if (slice is null) return null;
        return new SectionSlice(section.VertexBuffer!, section.IndexBuffer!, slice.Value.BaseVertex, slice.Value.FirstIndex, slice.Value.IndexCount);
    }

    //EnumerateVisibleSections Render 线程 Lock 内调遍历已上传 section 测 frustum
    //加 _viewArea.IsVisible 过滤避免渲染已卸载 chunk 的 section chunk 卸载后 ViewArea 下帧刷新不再可见
    //ConcurrentDictionary.Values 返回快照遍历安全 yield 在 Lock 内 foreach 立即消费
    public IEnumerable<RenderSection> EnumerateVisibleSections(Frustum frustum)
    {
        foreach (var section in _sections.Values)
        {
            if (section.State != RenderSectionState.Uploaded) continue;
            if (!_viewArea.IsVisible(section.Pos)) continue;
            if (frustum.IsVisible(section.BoundingBox)) yield return section;
        }
    }

    //UnloadSection chunk 卸载时调 Render 线程 Lock 内归还 buffer 移除 section
    //MarkRemoved 防竞态：编译线程 PublishMesh 前检查跳过 上传线程 dequeue 后检查跳过不借 buffer
    public void UnloadSection(SectionPos pos)
    {
        if (_sections.TryRemove(pos.AsLong(), out var section))
        {
            section.MarkRemoved();
            section.ReleaseBuffers(_bufferPool);
        }
    }

    //WorkerLoop 后台线程主循环 GetConsumingEnumerable 阻塞等编译完成后入 _uploadQueue
    //持 ClientLevel 读锁调 GetSection+Snapshot+Build 防 SetBlockState 修改 section 内部 PalettedContainer
    //读锁内 PublishMesh+Enqueue 锁外做 减少锁持有时间 chunk 卸载 rawSection null 跳过留 Compiling
    private void WorkerLoop()
    {
        foreach (var section in _compileQueue.GetConsumingEnumerable())
        {
            if (!_running) break;
            if (!section.TryBeginCompile()) continue;
            SectionMesh? mesh = null;
            var compileSuccess = false;
            try
            {
                _level.EnterReadLock();
                try
                {
                    var rawSection = _level.GetSection(section.Pos.X, section.Pos.Y, section.Pos.Z);
                    if (rawSection is null)
                    {
                        //chunk 已卸载 section 卡 Compiling 等 UnloadSection 清理首版不处理
                        compileSuccess = false;
                    }
                    else if (rawSection.HasOnlyAir())
                    {
                        mesh = new SectionMesh();
                        compileSuccess = true;
                    }
                    else
                    {
                        var regionCache = RenderRegionCache.Snapshot(_level, section.Pos);
                        var originX = section.Pos.X << 4;
                        var originY = section.Pos.Y << 4;
                        var originZ = section.Pos.Z << 4;
                        var meshData = _meshBuilder.Build(rawSection, regionCache, originX, originY, originZ);
                        mesh = SectionMesh.FromChunkMeshData(meshData);
                        compileSuccess = true;
                    }
                }
                finally { _level.ExitReadLock(); }
                //编译期间 section 被卸载跳过发布 UnloadSection 已清理 buffer
                if (compileSuccess && mesh is not null && !section.IsRemoved)
                {
                    section.PublishMesh(mesh);
                    _uploadQueue.Enqueue(section);
                }
            }
            catch
            {
                if (_running && section.TryMarkDirty())
                {
                    try { _compileQueue.Add(section); } catch (InvalidOperationException) { }
                }
            }
        }
    }

    private static readonly RenderLayer[] s_layers = { RenderLayer.Solid, RenderLayer.Cutout, RenderLayer.Translucent };

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _compileQueue.Dispose();
        foreach (var section in _sections.Values)
        {
            if (section.HasUploadedBuffers) section.ReleaseBuffers(_bufferPool);
        }
        _sections.Clear();
        _disposed = true;
    }
}

//SectionSlice dispatcher.GetSectionSlice 返回的渲染切片供 LevelRenderer.Draw 提交
//VertexBuffer/IndexBuffer 共享每 section 一个 buffer BaseVertex/FirstIndex 定位 layer 偏移
public readonly record struct SectionSlice(GpuBuffer VertexBuffer, GpuBuffer IndexBuffer, int BaseVertex, int FirstIndex, int IndexCount);
