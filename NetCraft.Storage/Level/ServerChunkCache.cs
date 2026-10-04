using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Light;

namespace NetCraft.Storage;

//ServerChunkCache 服务端区块缓存对应原版 net.minecraft.server.level.ServerChunkCache
//持有 ChunkMap 与加载回调异步调度区块加载避免主循环同步阻塞
//阶段 11.48 引入替代 PersistentServerLevel.GetChunk 同步等待
//阶段 11.52 加 generator 回调存档未命中时走 ChunkStatus 生成链生成新 chunk
//loader 回调由 PersistentServerLevel 传入 LoadChunkAsync 避免循环依赖
public sealed class ServerChunkCache : ChunkSource
{
    private readonly ChunkMap _chunkMap;
    private readonly Func<ChunkPos, Task<ChunkAccess?>> _loader;
    //_ticketStorage 区块票存档 由服务端注入 超时清理与强制加载都走它
    private TicketStorage? _ticketStorage;
    //_loadingTracker 加载等级传播 源是参与加载的票
    private LoadingChunkTracker? _loadingTracker;
    //_simulationTracker 模拟等级传播 源是参与模拟的票
    private SimulationChunkTracker? _simulationTracker;
    //_playerCenters 每个玩家上次的视野中心与视距 跨块或改视距时才重算票
    private readonly Dictionary<object, (int X, int Z, int ViewDistance, int SimulationLevel)> _playerCenters = new();
    //_loadingTicketRefs 视距内区块的玩家引用计数 归零才真正撤票
    private readonly Dictionary<long, int> _loadingTicketRefs = new();
    //_simulationTicketRefs 玩家所在区块的引用计数 归零才真正撤票
    private readonly Dictionary<long, int> _simulationTicketRefs = new();
    //generator 存档未命中时调用的生成器由 Game 层传入走 ChunkStatusProcessor 流水线
    private readonly Func<ChunkPos, ChunkAccess?>? _generator;
    //区块生成与光照都在后台线程推进 已加载缓存主线程与生成线程都会碰 必须用并发字典
    private readonly ConcurrentDictionary<long, ChunkAccess> _loaded = new();
    //_generateGate 生成并发闸门 生成为 CPU 密集同步过程
    //并发度由 OptimizationFlags.ChunkGenerationParallel 决定 关闭时退化为 1 便于对照串行基线
    //留一个核给主线程 对齐原版用可用核数减一 不这么做生成线程会把主线程的 CPU 抢光
    private readonly SemaphoreSlim _generateGate = new(
        OptimizationFlags.ChunkGenerationParallel ? Math.Max(1, Environment.ProcessorCount - 1) : 1);
    //_lightEngine 区块就绪后按内容建立光照数据 惰性创建
    private readonly object _lightLock = new();
    //_lightGate 光照引擎内部是非线程安全字典 原版在专用线程串行跑 这里用信号量串行化
    //用信号量而非 lock: 主线程这一侧等锁时能把线程还给线程池 不白占一个线程
    //生成线程那一侧一律用零超时尝试 抢不到就直接让路 绝不排队跟主线程抢
    private readonly SemaphoreSlim _lightGate = new(1, 1);
    private ServerLightChunkGetter? _lightChunkGetter;
    private LevelLightEngine? _lightEngine;
    //光照变化待下发区段 按区块收集受影响的光照区段索引 传播跑完统一下发对应原版 ChunkMap.onLightUpdate
    private readonly Dictionary<ChunkPos, HashSet<int>> _pendingSkySections = new();
    private readonly Dictionary<ChunkPos, HashSet<int>> _pendingBlockSections = new();
    //UnloadBudgetPerTick 每 tick 最多卸载几个区块 对应原版 processUnloads 的 hasMoreTime 预算
    //一次卸载要取整块快照 不限量的话玩家跑远时会在一拍里卸掉一整列把主线程拖住
    private const int UnloadBudgetPerTick = 16;

    //LightBatchBudget 每批处理的队列项数量 太小批次过密丢吞吐 太大主线程等待变长
    private const int LightBatchBudget = 8192;

    //LightDrainBatchLimit 生成线程一轮最多连推几批光照
    //原版这里是一次 runUpdate 跑完就结束 由 tryScheduleUpdate 的 scheduled 标志再触发下一轮
    //写成"直到全局队列空才停"会让每个生成线程无限自旋: 既反复抢锁把主线程饿死 又反复排回线程池把它占满
    private const int LightDrainBatchLimit = 8;

    //LightUpdateSink 光照变化下发回调 由 Game 层注入 参数为区块坐标与天光/方块光受影响的区段索引
    //Storage 层拿不到玩家列表 发包含在 Game 层 与原版 ChunkMap 持有 ServerLevel 的分层差异相对应
    public Action<ChunkPos, IReadOnlyList<int>, IReadOnlyList<int>>? LightUpdateSink { get; set; }

    //ChunkMap 玩家视距管理器
    public ChunkMap ChunkMap => _chunkMap;

    //TicketStorage 区块票存档 未注入时为 null
    public TicketStorage? TicketStorage => _ticketStorage;

    //AttachTicketStorage 接上区块票存档并建立两个等级跟踪器
    //对应原版 ServerChunkCache 构造时 computeIfAbsent(TicketStorage.TYPE) 后交给 ChunkMap
    //跟踪器只把票变化入队 真正推进等级由 tick 里的收敛调用完成
    public void AttachTicketStorage(TicketStorage storage)
    {
        _ticketStorage = storage;
        _loadingTracker = new LoadingChunkTracker(_chunkMap.Distance, storage);
        _simulationTracker = new SimulationChunkTracker(storage);
    }

    //SimulationDistance 模拟距离 玩家周围多少格内实体可 tick 对应原版 simulationDistance
    public int SimulationDistance { get; set; } = 10;

    //RunTicketTrackers 立刻把两套票等级收敛到当前票表
    //正常由 Tick 每刻调一次 出票之后马上要读判定时才需要显式调
    public void RunTicketTrackers()
    {
        _simulationTracker?.RunAllUpdates();
        _loadingTracker?.RunDistanceUpdates(int.MaxValue);
    }

    //InEntityTickingRange 该区块是否在实体可 tick 范围 对应原版 inEntityTickingRange
    //模拟距离之外只加载不推进 这一层就是"弱加载"的实质
    //未接入票表时退回全部可 tick 与接入之前的行为保持一致
    public bool InEntityTickingRange(long packedPos)
        => _simulationTracker is not { } tracker || ChunkLevel.IsEntityTicking(tracker.GetLevelAt(packedPos));

    //InBlockTickingRange 该区块是否在方块可 tick 范围 对应原版 inBlockTickingRange
    //调度刻与方块实体的推进按它过滤 比实体范围宽一档
    public bool InBlockTickingRange(long packedPos)
        => _simulationTracker is not { } tracker || ChunkLevel.IsBlockTicking(tracker.GetLevelAt(packedPos));

    //UpdateChunkForced 强制加载开关对应原版 ServerChunkCache.updateChunkForced
    public bool UpdateChunkForced(ChunkPos pos, bool add)
        => _ticketStorage?.UpdateChunkForced(pos, add) ?? false;

    //GetForceLoadedChunks 当前强制加载区块对应原版 ServerChunkCache.getForceLoadedChunks
    public IReadOnlyCollection<long> GetForceLoadedChunks()
        => _ticketStorage?.GetForceLoadedChunks() ?? Array.Empty<long>();

    //HoldersCount 当前 holder 数量供诊断
    public int HoldersCount => _chunkMap.HoldersCount;

    //LoadedCount 已加载缓存数量供诊断
    public int LoadedCount => _loaded.Count;

    //Holders 持有器视图 供 GUI 区块图逐块取票等级与加载进度 取不创建
    public ICollection<ChunkHolder> Holders => _chunkMap.Holders;

    //ViewDistance 视距半径 区块图用它标出加载范围的边界
    public int ViewDistance => _chunkMap.ViewDistance;

    //LoadedChunks 已加载区块视图供落盘与随机刻遍历
    //并发字典的 Values 本身是弱一致视图 这两个调用点每 tick 都要取一次 不再复制成列表
    public ICollection<ChunkAccess> LoadedChunks => _loaded.Values;

    //ChunkLoaded 区块首次进入内存的回调 关卡据此把该 chunk 的实体载入实体管理器
    public Action<ChunkPos>? ChunkLoaded { get; set; }

    //ChunkUnloaded 区块离开内存的回调 关卡据此清掉随区块存在的方块实体
    public Action<ChunkPos>? ChunkUnloaded { get; set; }

    //ChunkSaveSink 区块离开内存前的落盘回调 由关卡注入 未注入时卸载不落盘
    //实现必须同步取好快照 序列化与写盘自己异步 卸载这边不等结果
    public Action<ChunkAccess>? ChunkSaveSink { get; set; }

    //MinSectionY/SectionsCount 世界高度范围 默认主世界 -64..320
    public int MinSectionY { get; }

    public int SectionsCount { get; }

    //LightEngine 光照引擎惰性创建 既是光照计算入口也是下发时的数据来源
    public LevelLightEngine LightEngine
    {
        get
        {
            if (_lightEngine is not null) return _lightEngine;
            lock (_lightLock)
            {
                //传只读查询 光照传播取邻居不能触发新加载否则沿邻居链递归到栈溢出
                _lightChunkGetter ??= new ServerLightChunkGetter(GetLoadedChunk, MinSectionY, SectionsCount)
                {
                    LightUpdateCallback = OnLightSectionUpdated,
                };
                _lightEngine ??= new LevelLightEngine(_lightChunkGetter, hasBlockLight: true, hasSkyLight: true);
                return _lightEngine;
            }
        }
    }

    //WithLightLock 在光照锁内执行读取 供下发光照数据的调用方与生成线程串行
    //光照引擎的区段表是普通字典 主线程读与生成线程写撞上会直接损坏状态
    //调用方应把区块序列化这类重活放在锁外 锁内只做光照数据拷贝
    public T WithLightLock<T>(Func<LevelLightEngine, T> action)
    {
        _lightGate.Wait();
        try { return action(LightEngine); }
        finally { _lightGate.Release(); }
    }

    public ServerChunkCache(Func<ChunkPos, Task<ChunkAccess?>> loader, int viewDistance = 8,
        Func<ChunkPos, ChunkAccess?>? generator = null, int minSectionY = -4, int sectionsCount = 24)
    {
        _loader = loader;
        _chunkMap = new ChunkMap(viewDistance);
        _generator = generator;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
    }

    //GetChunk 按 chunkX/chunkZ 获取完整区块对应原版 getChunk
    //主循环安全未加载返回 null 不阻塞
    public override ChunkAccess? GetChunk(int x, int z)
        => GetChunk(x, z, ChunkStatus.FULL, false);

    //GetChunk 按 chunkX/chunkZ 与 ChunkStatus 获取区块对应原版 getChunk
    //require 为 true 时未加载同步等待加载完成后返回加载失败抛异常仅测试或必须同步场景用
    //require 为 false 时未加载返回 null 不阻塞主循环
    public override ChunkAccess? GetChunk(int x, int z, ChunkStatus status, bool require)
    {
        var key = ChunkPos.Pack(x, z);
        if (_loaded.TryGetValue(key, out var cached))
            return cached;
        if (_chunkMap.GetHolder(key) is { IsDone: true } holder)
        {
            var result = holder.Future.Result;
            if (result.IsSuccess)
            {
                _loaded[key] = result.Chunk!;
                return result.Chunk;
            }
            if (require) throw result.Error!;
            return null;
        }
        if (require)
            return GetChunkFuture(x, z, status).GetAwaiter().GetResult().OrElse(null);
        //非阻塞路径同样要提交异步加载 否则区块永远停在未加载状态
        //调用方下次 tick 再来取 生成在后台推进不阻塞主循环
        _ = GetChunkFuture(x, z, status);
        return null;
    }

    //IsChunkFailed 区块是否已确认加载失败区别于尚未就绪
    //发送端据此丢弃永久失败项避免队列永不排空
    public bool IsChunkFailed(int x, int z)
        => _chunkMap.GetHolder(ChunkPos.Pack(x, z)) is { IsDone: true } h
            && !h.Future.Result.IsSuccess;

    //GetLoadedChunk 只取已加载区块不触发加载对应原版 getChunkForLighting 的只读语义
    //holder 已完成但尚未并入缓存的一并返回 未就绪返回 null 由光照引擎按完全不透明处理
    public ChunkAccess? GetLoadedChunk(int x, int z)
    {
        var key = ChunkPos.Pack(x, z);
        if (_loaded.TryGetValue(key, out var cached)) return cached;
        if (_chunkMap.GetHolder(key) is { IsDone: true } holder)
        {
            var result = holder.Future.Result;
            if (result.IsSuccess) return result.Chunk;
        }
        return null;
    }

    //HasChunk 判断区块是否已加载对应原版 hasChunk
    public override bool HasChunk(int x, int z)
    {
        Log.Debug($"HasChunk entry x={x} z={z}");
        var key = ChunkPos.Pack(x, z);
        var result = _loaded.ContainsKey(key)
            || (_chunkMap.GetHolder(key) is { IsDone: true } h && h.Future.Result.IsSuccess);
        Log.Debug($"HasChunk exit result={result}");
        return result;
    }

    //GetChunkFuture 异步获取区块 future 对应原版 getChunkFuture
    //已加载缓存命中立即返回未加载提交 LoadAsync 任务
    public Task<ChunkResult> GetChunkFuture(int x, int z, ChunkStatus status)
    {
        var key = ChunkPos.Pack(x, z);
        if (_loaded.TryGetValue(key, out var cached))
            return Task.FromResult(ChunkResult.Success(cached));
        var holder = GetOrCreateHolder(x, z);
        if (holder.IsDone)
            return holder.Future;
        if (holder.MarkScheduled()) _ = LoadAsync(holder);
        return holder.Future;
    }

    //GetOrCreateHolder 获取或创建 holder 对应原版 ChunkMap.getOrCreateHolder
    public ChunkHolder GetOrCreateHolder(int x, int z)
        => _chunkMap.GetOrCreateHolder(new ChunkPos(x, z));

    //TryGetHolder 查询 holder 不创建对应原版 getHolder
    public ChunkHolder? TryGetHolder(int x, int z)
        => _chunkMap.GetHolder(ChunkPos.Pack(x, z));

    //LoadAsync 异步加载区块完成或失败后回填 holder 对应原版 schedule chunk load
    //loader 返回 null 表示存档无此区块走 generator 走 ChunkStatus 生成链生成新 chunk
    //generator 也为 null 时 holder.Fail 传递 UnloadedChunkException
    private async Task LoadAsync(ChunkHolder holder)
    {
        try
        {
            var chunk = await _loader(holder.Pos).ConfigureAwait(false);
            if (chunk is null && _generator is not null)
            {
                //生成是 CPU 密集的同步过程 用闸门把并发压到核数量级避免线程池被瞬间打满
                await _generateGate.WaitAsync().ConfigureAwait(false);
                try { chunk = _generator(holder.Pos); }
                finally { _generateGate.Release(); }
            }
            if (chunk is null)
            {
                holder.Fail(new UnloadedChunkException($"Chunk {holder.Pos} not found in storage and no generator"));
            }
            else
            {
                ProcessLight(chunk);
                holder.Complete(chunk);
            }
        }
        catch (Exception e)
        {
            //静默吞异常会让读档失败表现为区块永远发不出去 必须留下痕迹
            Log.Warning($"Chunk load failed {holder.Pos}: {e}");
            holder.Fail(new UnloadedChunkException($"Failed to load chunk {holder.Pos}", e));
        }
    }

    //ProcessLight 区块就绪后按内容建立光照数据 对应原版 initializeLight 与 lightChunk 两阶段
    //顺序固定为 标区段空态 -> 启用光照 -> 传播光源 与原版一致 反过来天光预处理会走错分支
    //空区段同样登记 否则顶部空区段没有层数据 天光查询会走"数据之上恒为 15"的快捷分支
    //未加载的邻居由引擎按完全不透明处理 邻居后续加载不会回溯重算属已知简化
    //推完有界批数就收手 剩下的交给主线程的 TickLight 按每刻预算接着推
    private void ProcessLight(ChunkAccess chunk)
    {
        try
        {
            //登记与初始化这一小段不抢锁等待: 少了它这个区块的天光光源高度图就永远进不了引擎
            _lightGate.Wait();
            try
            {
                var engine = LightEngine;
                //区块此时还没并入已加载缓存 先登记到光照取块器 否则引擎取不到它的天光光源高度图
                _lightChunkGetter?.Track(chunk);
                for (var sectionY = chunk.MinSectionY; sectionY <= chunk.MaxSectionY; sectionY++)
                {
                    var section = chunk.GetSection(sectionY);
                    engine.UpdateSectionStatus(new SectionPos(chunk.Pos.X, sectionY, chunk.Pos.Z),
                        section is null || section.HasOnlyAir());
                }
                engine.SetLightEnabled(chunk.Pos, true);
                engine.PropagateLightSources(chunk.Pos);
                ReprocessLoadedNeighbors(chunk.Pos);
            }
            finally
            {
                _lightGate.Release();
            }

            //按有界批数往下推 抢不到锁说明此刻正有人在推 这一轮直接让路
            //排队等锁会饿死主线程: 每个方块变化、每次光照下发都要拿这把锁 生成线程没有理由跟它抢
            for (var batch = 0; batch < LightDrainBatchLimit; batch++)
            {
                if (!_lightGate.Wait(0)) break;
                var finished = false;
                try
                {
                    var engine = LightEngine;
                    engine.RunLightUpdates(LightBatchBudget);
                    finished = !engine.HasLightWork();
                }
                finally
                {
                    _lightGate.Release();
                }
                if (finished) break;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Chunk light failed {chunk.Pos}: {e.Message}");
        }
        //区块包自带的整区块光照已经覆盖这批变化 不单独下发 丢掉免得攒到下次方块变更一起发
        //光照计算中途失败留下的半批变化同样要丢掉 所以放在 catch 之后而非 try 末尾
        _lightGate.Wait();
        try
        {
            _pendingSkySections.Clear();
            _pendingBlockSections.Clear();
        }
        finally
        {
            _lightGate.Release();
        }
    }

    //ReprocessLoadedNeighbors 补算已加载的相邻区块 对应原版 LIGHT 阶段要求邻居区块已就绪的语义
    //本作区块一次性推进到 FULL 没有阶段依赖 先加载的一侧看不到后加载的邻居
    //引擎取不到邻居列的天光光源高度就把边界当无光源处理 边界会偏暗且之后不会回补
    //这里只把邻居的光源重新入队 实际传播交给调用方的分批循环 否则会绕开预算一次跑完
    private void ReprocessLoadedNeighbors(ChunkPos pos)
    {
        var engine = LightEngine;
        foreach (var neighbor in HorizontalNeighbors(pos))
        {
            //未加载的邻居不用补算 它自己加载时能看到本区块
            if (GetLoadedChunk(neighbor.X, neighbor.Z) is null) continue;
            engine.PropagateLightSources(neighbor);
        }
    }

    //HorizontalNeighbors 水平四邻区块坐标 天光只沿水平方向受邻居列高影响
    private static IEnumerable<ChunkPos> HorizontalNeighbors(ChunkPos pos)
    {
        yield return new ChunkPos(pos.X, pos.Z - 1);
        yield return new ChunkPos(pos.X, pos.Z + 1);
        yield return new ChunkPos(pos.X - 1, pos.Z);
        yield return new ChunkPos(pos.X + 1, pos.Z);
    }

    //OnLightSectionUpdated 光照引擎一轮传播后回传受影响区段 累积到待下发集合
    //由 ServerLightChunkGetter 的光照回调转发 调用点在光照传播内部即已持有光照锁
    private void OnLightSectionUpdated(LightLayer layer, SectionPos pos)
    {
        var engine = LightEngine;
        var index = pos.Y - engine.GetMinLightSection();
        if (index < 0 || index >= engine.GetLightSectionCount()) return;
        var target = layer == LightLayer.Sky ? _pendingSkySections : _pendingBlockSections;
        var chunk = pos.AsChunkPos();
        if (!target.TryGetValue(chunk, out var sections))
            target[chunk] = sections = new HashSet<int>();
        sections.Add(index);
    }

    //FlushLightUpdates 把攒下的光照变化按区块交给 Game 层下发 对应原版区块 tick 末的光照广播
    //必须在光照锁外调用 发包要序列化整段光照数据 占着锁会让生成线程一直等
    private void FlushLightUpdates()
    {
        var sink = LightUpdateSink;
        if (sink is null) return;
        List<(ChunkPos Pos, int[] Sky, int[] Block)> batch;
        _lightGate.Wait();
        try
        {
            if (_pendingSkySections.Count == 0 && _pendingBlockSections.Count == 0) return;
            batch = new List<(ChunkPos, int[], int[])>(_pendingSkySections.Count + _pendingBlockSections.Count);
            var chunks = new HashSet<ChunkPos>(_pendingSkySections.Keys);
            chunks.UnionWith(_pendingBlockSections.Keys);
            foreach (var chunk in chunks)
            {
                _pendingSkySections.TryGetValue(chunk, out var sky);
                _pendingBlockSections.TryGetValue(chunk, out var block);
                batch.Add((chunk,
                    sky is null ? Array.Empty<int>() : sky.ToArray(),
                    block is null ? Array.Empty<int>() : block.ToArray()));
            }
            _pendingSkySections.Clear();
            _pendingBlockSections.Clear();
        }
        finally
        {
            _lightGate.Release();
        }
        foreach (var (pos, sky, block) in batch)
            sink(pos, sky, block);
    }

    //UpdateLightBatch 批量方块变化后的光照重算 对应原版同一次批量写入只跑一轮光照传播
    //逐格 UpdateLight 每次都 RunLightUpdates 会把传播队列反复清空 fill 这类批量写入底下就是 O(n) 轮全量传播
    //这里先标记完所有位置的光照脏点 最后只跑一次传播
    public void UpdateLightBatch(IReadOnlyList<BlockPos> positions)
    {
        if (positions.Count == 0) return;
        //必须和其它路径一样走信号量: 原写成 lock(_lightGate) 锁的是信号量对象本身
        //Monitor 与 SemaphoreSlim 是两套互斥机制 两者不互斥 等于没加锁
        _lightGate.Wait();
        try
        {
            var engine = LightEngine;
            foreach (var pos in positions) MarkLightDirty(pos);
            engine.RunLightUpdates();
        }
        catch (Exception e)
        {
            Log.Warning($"Batch light failed: {e.Message}");
        }
        finally
        {
            _lightGate.Release();
        }
        FlushLightUpdates();
    }

    //MarkLightDirty 标记一个位置的光照脏点 对应原版 LevelChunk.setBlockState 里的 updateSectionStatus 与 checkBlock
    //只把节点排进队列不传播 调用方必须已持有光照锁
    private void MarkLightDirty(BlockPos pos)
    {
        var engine = LightEngine;
        //区块顶部原本全空的区段没有光照层 方块落进去时先同步区段空态把层建起来
        var chunk = GetLoadedChunk(pos.X >> 4, pos.Z >> 4);
        var section = chunk?.GetSection(pos.Y >> 4);
        engine.UpdateSectionStatus(new SectionPos(pos.X >> 4, pos.Y >> 4, pos.Z >> 4),
            section is null || section.HasOnlyAir());
        //再刷新该列的天光光源高度图 否则天光引擎读到的仍是变更前的遮挡高度
        _lightChunkGetter?.UpdateSkyLightSources(pos);
        engine.CheckBlock(pos);
    }

    //UpdateLight 方块状态变化后标记该位置的光照脏点
    //与 ProcessLight 共用同一把锁 引擎的区段表非线程安全必须串行
    //这里不跑传播也不下发 传播与下发交给 TickLight 每刻末尾统一做一次 与原版一致
    //原先是每写一格就把传播队列跑空一次 活塞搬运这类一拍几十次 setBlock 会变成几十轮全量传播
    public void UpdateLight(BlockPos pos)
    {
        _lightGate.Wait();
        try
        {
            MarkLightDirty(pos);
        }
        catch (Exception e)
        {
            Log.Warning($"Light mark failed {pos}: {e.Message}");
        }
        finally
        {
            _lightGate.Release();
        }
    }

    //TickLight 每刻末尾统一推进光照队列并把变化下发给客户端
    //对应原版区块 tick 里那一次光照推进 同一拍内大量方块变化只在这里跑一轮传播
    public void TickLight()
    {
        _lightGate.Wait();
        try
        {
            //没有脏点也没有积压就跳过 免得空跑一遍收尾与区段表交换
            //按预算推进: 一拍积压上万条时一次跑完会把主线程定住 剩下的留到下一拍
            if (LightEngine.HasLightWork()) LightEngine.RunLightUpdates(LightBatchBudget);
        }
        catch (Exception e)
        {
            Log.Warning($"Light flush failed: {e.Message}");
        }
        finally
        {
            _lightGate.Release();
        }
        FlushLightUpdates();
    }

    //Tick 推进区块调度对应原版 ServerChunkCache.tick
    //先清超时票 再把票等级收敛成持有器等级 最后回收票不再需要的持有器
    public override void Tick()
    {
        //Log.Debug($"Tick 入口 holders={_chunkMap.HoldersCount} loaded={_loaded.Count}");
        //超时票每 tick 清理一次 对应原版 ServerChunkCache.tick 里的 purgeStaleTickets
        _ticketStorage?.PurgeStaleTickets(ReadyForSaving);
        //票等级收敛成持有器等级 对应原版 DistanceManager.runAllUpdates
        //先模拟后加载与原版顺序一致 收敛结果经 DistanceManager 写进持有器
        _simulationTracker?.RunAllUpdates();
        _loadingTracker?.RunDistanceUpdates(int.MaxValue);
        _chunkMap.Distance.ChunksToUpdateFutures.Clear();
        List<long>? expired = null;
        foreach (var holder in _chunkMap.Holders)
        {
            var key = holder.Pos.Pack();
            if (holder.IsDone)
            {
                var result = holder.Future.Result;
                if (result.IsSuccess && !_loaded.ContainsKey(key))
                {
                    _loaded[key] = result.Chunk!;
                    ChunkLoaded?.Invoke(result.Chunk!.Pos);
                }
            }
            //票不再要求加载到 FULL 的持有器回收 已加载的区块留在 _loaded 里继续可用
            //正在加载中的先留着 现在丢掉它 future 完成后就没地方回填缓存了
            //按票等级判定而不是持有器自身等级: 等级是 BFS 逐级衰减出来的 撤票后要跑好几 tick 才升过阈值
            //本作区块一次性生成到 FULL 没有中间态 期间这些空壳持有器没有任何用途 越早回收越好
            var loading = holder.WasScheduled && !holder.IsDone;
            if (!loading && !IsLoadWanted(key, holder))
            {
                expired ??= new List<long>();
                expired.Add(key);
            }
        }
        if (expired is not null)
        {
            //卸载一个区块要取快照并写盘 一拍里做太多会拖住主线程 超预算的留到下一拍
            var budget = UnloadBudgetPerTick;
            foreach (var key in expired)
            {
                if (budget <= 0) break;
                if (UnloadChunkInternal(key)) budget--;
            }
        }
        //Log.Debug($"Tick 出口");
    }

    //ReadyForSaving 该区块的持有器是否已可安全丢票对应原版 canTicketExpire 的 holder 判定
    //持有器不存在或已加载完成都算可以 未就绪时票先留着免得区块没落盘就丢票
    private bool ReadyForSaving(long packedPos)
        => _chunkMap.GetHolder(packedPos) is not { } holder || holder.IsDone;

    //IsLoadWanted 该区块是否还在加载范围内 对应原版 ChunkLevel.isLoaded 的判定
    //用持有器自己的等级(票经 BFS 传播后的结果) 而不是原始票等级
    //视距外那一圈本来就没票 靠原始票等级判会立刻把它们当成该回收的
    //而它们恰恰是要保住的弱加载带 阈值与 LoadingChunkTracker.SetLevel 保持一致
    private bool IsLoadWanted(long packedPos, ChunkHolder holder)
        => holder.TicketLevel <= ChunkLevel.BlockTickingLevel;

    //ReleaseChunkLight 区块离开内存时释放它在光照引擎里留下的全部数据 对应原版 ThreadedLevelLightEngine.updateChunkStatus
    //光照引擎按区段坐标存数据 它不知道区段属于哪个区块 卸载时不清就再没人来收
    //漏掉这一步区段表与数据层会随加载过的区块一直堆下去 跑图越久内存越高 最后全靠 Gen2 强制回收硬撑
    //先撤队列数据再标区段为空: 标空让 26 邻居计数逐级归零 收尾那轮才会真正丢掉数据层
    private void ReleaseChunkLight(ChunkPos pos)
    {
        //光照引擎没建起来说明这个区块从没算过光照 没有东西要还
        if (_lightEngine is null) return;
        _lightGate.Wait();
        try
        {
            _lightEngine.RetainData(pos, false);
            _lightEngine.SetLightEnabled(pos, false);
            //光照区段比世界区段上下各多一层 队列数据按光照区段范围清
            for (var sectionY = _lightEngine.GetMinLightSection(); sectionY < _lightEngine.GetMaxLightSection(); sectionY++)
            {
                var lightSection = new SectionPos(pos.X, sectionY, pos.Z);
                _lightEngine.QueueSectionData(LightLayer.Block, lightSection, null);
                _lightEngine.QueueSectionData(LightLayer.Sky, lightSection, null);
            }
            for (var sectionY = MinSectionY; sectionY < MinSectionY + SectionsCount; sectionY++)
                _lightEngine.UpdateSectionStatus(new SectionPos(pos.X, sectionY, pos.Z), true);
            //光照视图抓着整块区块 不摘掉卸载的区块会被它一直钉在内存里
            _lightChunkGetter?.DropView(pos.X, pos.Z);
        }
        finally
        {
            _lightGate.Release();
        }
    }

    //UnloadChunk 把区块移出内存并触发卸载回调 返回是否确实卸载了
    //调用方必须确保该区块已落盘 否则内存里的改动会随卸载丢掉
    //这条路径不落盘 票驱动的自动卸载走 UnloadChunkInternal
    public bool UnloadChunk(ChunkPos pos)
    {
        var key = pos.Pack();
        _chunkMap.TryRemoveHolder(key);
        if (!_loaded.TryRemove(key, out _)) return false;
        ReleaseChunkLight(pos);
        ChunkUnloaded?.Invoke(pos);
        return true;
    }

    //UnloadChunkInternal 卸载一个票不再要求的区块 返回是否真的动了持有器
    //顺序固定为 摘持有器 -> 落盘 -> 释放光照 -> 移出内存 -> 通知关卡
    //先摘持有器再落盘: 票在同一拍里回来只会新建持有器 碰不到这批已经准备丢弃的对象
    //落盘排在释放光照之前: 方块实体与区块数据这时都还挂在对象上
    private bool UnloadChunkInternal(long packedPos)
    {
        if (!_chunkMap.TryRemoveHolder(packedPos)) return false;
        //只建了持有器还没加载出区块的直接摘掉 没有数据要落盘
        if (!_loaded.TryRemove(packedPos, out var chunk)) return true;
        ChunkSaveSink?.Invoke(chunk);
        ReleaseChunkLight(chunk.Pos);
        ChunkUnloaded?.Invoke(chunk.Pos);
        return true;
    }

    //UpdatePlayerTickets 玩家跨块或视距变化时更新其加载与模拟票
    //对应原版 ChunkMap.move 里 PlayerTicketTracker 与 addPlayer 的票更新
    //视距内逐区块出 PLAYER_LOADING 票 玩家所在区块出 PLAYER_SIMULATION 票
    //票按引用计数管理 两个玩家视距重叠时先走的人撤票不会影响后走的人
    public void UpdatePlayerTickets(object owner, int chunkX, int chunkZ, int viewDistance)
    {
        if (_ticketStorage is null) return;
        var radius = Math.Clamp(viewDistance, 2, 32);
        if (_playerCenters.TryGetValue(owner, out var previous)
            && previous.X == chunkX && previous.Z == chunkZ && previous.ViewDistance == radius) return;
        if (previous.ViewDistance > 0)
        {
            RemoveLoadingTickets(previous.X, previous.Z, previous.ViewDistance);
            RemoveSimulationTicket(ChunkPos.Pack(previous.X, previous.Z), previous.SimulationLevel);
        }
        //模拟票等级只按模拟距离算 对应原版 getPlayerTicketLevel
        //不拿视距封顶: 视距比模拟距离小时只是加载范围更窄 模拟等级的语义不该跟着变
        var simulationLevel = Math.Max(0, ChunkLevel.EntityTickingLevel - SimulationDistance);
        AddLoadingTickets(chunkX, chunkZ, radius);
        AddSimulationTicket(ChunkPos.Pack(chunkX, chunkZ), simulationLevel);
        _playerCenters[owner] = (chunkX, chunkZ, radius, simulationLevel);
    }

    //RemovePlayerTickets 玩家离开时撤掉其全部票 对应原版 ChunkMap.removePlayer
    public void RemovePlayerTickets(object owner)
    {
        if (_ticketStorage is null) return;
        if (!_playerCenters.Remove(owner, out var previous)) return;
        RemoveLoadingTickets(previous.X, previous.Z, previous.ViewDistance);
        RemoveSimulationTicket(ChunkPos.Pack(previous.X, previous.Z), previous.SimulationLevel);
    }

    //AddLoadingTickets 视距方形内逐区块加一张加载票 已被其它玩家覆盖的区块只加计数
    private void AddLoadingTickets(int centerX, int centerZ, int radius)
    {
        for (var dx = -radius; dx <= radius; dx++)
            for (var dz = -radius; dz <= radius; dz++)
            {
                var key = ChunkPos.Pack(centerX + dx, centerZ + dz);
                _loadingTicketRefs.TryGetValue(key, out var count);
                _loadingTicketRefs[key] = count + 1;
                if (count == 0)
                    _ticketStorage!.AddTicket(key, new Ticket(TicketType.PlayerLoading, ChunkLevel.EntityTickingLevel));
            }
    }

    //RemoveLoadingTickets 撤掉视距方形内的加载票 还有别的玩家覆盖就只减计数
    private void RemoveLoadingTickets(int centerX, int centerZ, int radius)
    {
        for (var dx = -radius; dx <= radius; dx++)
            for (var dz = -radius; dz <= radius; dz++)
            {
                var key = ChunkPos.Pack(centerX + dx, centerZ + dz);
                if (!_loadingTicketRefs.TryGetValue(key, out var count)) continue;
                if (count > 1)
                {
                    _loadingTicketRefs[key] = count - 1;
                    continue;
                }
                _loadingTicketRefs.Remove(key);
                _ticketStorage!.RemoveTicket(key, new Ticket(TicketType.PlayerLoading, ChunkLevel.EntityTickingLevel));
            }
    }

    //AddSimulationTicket 玩家所在区块加一张模拟票
    private void AddSimulationTicket(long packedPos, int level)
    {
        _simulationTicketRefs.TryGetValue(packedPos, out var count);
        _simulationTicketRefs[packedPos] = count + 1;
        if (count == 0) _ticketStorage!.AddTicket(packedPos, new Ticket(TicketType.PlayerSimulation, level));
    }

    //RemoveSimulationTicket 撤掉玩家所在区块的模拟票 等级按出票时记下的值撤
    private void RemoveSimulationTicket(long packedPos, int level)
    {
        if (!_simulationTicketRefs.TryGetValue(packedPos, out var count)) return;
        if (count > 1)
        {
            _simulationTicketRefs[packedPos] = count - 1;
            return;
        }
        _simulationTicketRefs.Remove(packedPos);
        _ticketStorage!.RemoveTicket(packedPos, new Ticket(TicketType.PlayerSimulation, level));
    }

    protected override void Dispose(bool disposing)
    {
        Log.Debug($"Dispose entry disposing={disposing}");
        if (disposing)
        {
            _chunkMap.ClearHolders();
            _loaded.Clear();
        }
        base.Dispose(disposing);
        //Log.Debug($"Dispose 出口");
    }
}
