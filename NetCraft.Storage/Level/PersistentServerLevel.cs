using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using System.Collections.Concurrent;

namespace NetCraft.Storage;

//PersistentServerLevel 持久化服务端关卡对应原版 ServerLevel 接入 RegionFileStorage
//继承 SimpleServerLevel 复用 in-memory 字典做缓存实现 LevelHeightAccessor 供 Parse 用
//GetChunk 未命中时委托 ServerChunkCache 异步调度避免主循环同步等待
//SaveChunk 把 ChunkAccess 序列化为 CompoundTag 写入 RegionFileStorage
//阶段 11.47 接入 Tick 与 Entity 集合对齐原版 ServerLevel.tick 调度骨架
//阶段 11.48 接入 ServerChunkCache 替代同步等待 LoadChunkAsync
public sealed class PersistentServerLevel : SimpleServerLevel, BlockGetter
{
    private readonly SimpleRegionStorage _regionStorage;
    private readonly PalettedContainerFactory _factory;
    private readonly ServerChunkCache _chunkSource;
    //EntityManager 实体生命周期管理 可见与待命两态 tick 期间增删走队列
    private readonly EntityManager _entityManager;
    //_entityStorage 实体独立落盘存储 由 Game 层注入 未注入时实体只活在内存
    private EntityStorage? _entityStorage;
    //_pendingEntityLoads 后台读回的实体 主线程 tick 消费
    //实体读盘算 IO 重活 留在主线程会让区块加载回调卡住整个 tick
    private readonly ConcurrentQueue<ChunkEntities<NetCraft.Registry.Entity>> _pendingEntityLoads = new();
    private long _levelTick;

    public int MinSectionY { get; }
    public int SectionsCount { get; }
    public int MaxSectionY => MinSectionY + SectionsCount - 1;

    //建筑高度按区段范围折算 基类的默认值只作兜底 这里必须给出真实范围
    public override int MinBuildHeight => MinSectionY * 16;
    public override int MaxBuildHeight => (MaxSectionY + 1) * 16;

    public SimpleRegionStorage RegionStorage => _regionStorage;
    public PalettedContainerFactory Factory => _factory;

    //ChunkSource 区块源 ServerChunkCache 供外部诊断与玩家位置更新
    public ServerChunkCache ChunkSource => _chunkSource;

    //SetChunkForced 强制加载开关对应原版 ServerLevel.setChunkForced
    public bool SetChunkForced(int x, int z, bool add)
        => _chunkSource.UpdateChunkForced(new ChunkPos(x, z), add);

    //GetForceLoadedChunks 当前强制加载区块对应原版 ServerLevel.getForceLoadedChunks
    public IReadOnlyCollection<long> GetForceLoadedChunks() => _chunkSource.GetForceLoadedChunks();

    //Entities 关卡内可见实体列表只读视图供外部诊断
    public IEnumerable<NetCraft.Registry.Entity> Entities => _entityManager.Visible;

    //EntityLookup 可见实体分区索引查询入口供业务层按 AABB 范围取实体
    public EntityLookup EntityLookup => _entityManager.VisibleLookup;

    //EntityManager 实体管理器 供外部按 Uuid 查实体与做实体落盘
    public EntityManager EntityManager => _entityManager;

    //EntityStorage 实体落盘存储 未注入时为 null
    public EntityStorage? EntityStorage => _entityStorage;

    //EntityDeathCallback 实体死亡回调 由 Game 层注入 用于广播死亡表现并移除实体
    public Action<NetCraft.Registry.Entity>? EntityDeathCallback { get; set; }

    //CollisionShapeProvider 实体形状碰撞查询 由 Game 层注入 未注入时实体不做碰撞只推进位置
    //碰撞形状要问方块行为 那是 Game 层的东西 Storage 只能拿委托
    public Func<NetCraft.Registry.Entity, AABB, IReadOnlyList<VoxelShape>>? CollisionShapeProvider { get; set; }

    //LevelTick 累计关卡 tick 数用于诊断与刷盘调度
    public long LevelTick => _levelTick;

    public PersistentServerLevel(
        SimpleRegionStorage regionStorage,
        int minSectionY = -4,
        int sectionsCount = 24,
        PalettedContainerFactory? factory = null,
        Identifier? dimension = null,
        int dataVersion = 0,
        RegistryAccess? registryAccess = null,
        int viewDistance = 8,
        Func<ChunkPos, ChunkAccess?>? generator = null,
        int simulationDistance = 10)
        : base(dimension, dataVersion, registryAccess)
    {
        _regionStorage = regionStorage;
        _factory = factory ?? PalettedContainerFactory.Default;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
        //loader 委托 LoadChunkAsync 由 ServerChunkCache 异步调度避免循环依赖
        //async lambda 让 Task<LevelChunk?> 隐式转 Task<ChunkAccess?> 因 Task 不支持协变
        //generator 由 Game 层传入走 ChunkStatusProcessor 生成链存档未命中时生成新 chunk
        _chunkSource = new ServerChunkCache(async pos => await LoadChunkAsync(pos), viewDistance, generator);
        //模拟距离决定真正参与 tick 的范围 视距比它大时中间那圈就是只加载不 tick 的弱加载
        //与视距同样取原版的 clamp 区间
        _chunkSource.SimulationDistance = Math.Clamp(simulationDistance, 3, 32);
        //实体加入即参与 tick 区块卸载时转待命 区块加载完成回调把该 chunk 的待命实体转回可 tick
        _entityManager = new EntityManager();
        _chunkSource.ChunkLoaded = OnChunkLoaded;
        _chunkSource.ChunkUnloaded = OnChunkUnloaded;
        _chunkSource.ChunkSaveSink = SaveChunkOnUnload;
    }

    //OnChunkLoaded 区块加载完成后的接线 读实体并登记调度刻容器
    private void OnChunkLoaded(ChunkPos pos)
    {
        LoadChunkEntities(pos);
        EnsureChunkTicksRegistered(pos);
    }

    //OnChunkUnloaded 区块卸载后的接线 方块实体随区块离开内存
    //实体侧只停 tick 不删索引 原版语义是等区块重新加载再恢复
    //排刻容器与 in-memory 副本也要一起摘掉 否则该区块再加载会重复登记并拿到已卸载的旧对象
    private void OnChunkUnloaded(ChunkPos pos)
    {
        BlockEntityBridge?.Unload(pos);
        UnregisterChunkTicks(pos);
        RemoveChunk(pos);
    }

    //UnloadChunk 显式卸载区块 调用方负责先把改动落盘 对应原版 ChunkMap 的区块卸载
    public bool UnloadChunk(ChunkPos pos) => _chunkSource.UnloadChunk(pos);

    //AttachEntityStorage 注入实体落盘存储 由 Game 层在构造服务端后挂载
    public void AttachEntityStorage(EntityStorage storage) => _entityStorage = storage;

    //LoadChunkEntities 读该 chunk 的实体并入管理 对应原版 PersistentEntitySectionManager 的区块加载分支
    //由 ServerChunkCache 区块加载完成回调触发 该回调在主线程 tick 里跑
    //读盘放后台 结果入队等下一个 tick 消费: EntityManager 的索引是普通字典只能主线程改
    public void LoadChunkEntities(ChunkPos pos)
    {
        var storage = _entityStorage;
        if (storage is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var loaded = await storage.LoadEntities(pos).ConfigureAwait(false);
                _pendingEntityLoads.Enqueue(loaded);
            }
            catch (Exception e)
            {
                //读失败不能静默 否则该区块实体永久缺失
                Log.Warning($"Entity read failed {pos}: {e.Message}");
            }
        });
    }

    //DrainPendingEntityLoads 主线程消费后台读回的实体 对应原版 PersistentEntitySectionManager.processPendingLoads
    private void DrainPendingEntityLoads()
    {
        while (_pendingEntityLoads.TryDequeue(out var loaded))
        {
            foreach (var entity in loaded.GetEntities())
            {
                entity.Level = this;
                BindCollisionShapes(entity);
                _entityManager.AddEntity(entity);
            }
            if (!loaded.IsEmpty())
                Log.Info($"Entities loaded for chunk {loaded.Pos}: {loaded.GetEntities().Count}");
        }
    }

    //SaveAllEntitiesAsync 把各 chunk 的实体写盘 对应原版区块卸载与关服时的实体落盘
    //先按 chunk 组装 ChunkEntities 再统一刷盘 关闭时 force=true 确保写完
    public async Task SaveAllEntitiesAsync()
    {
        if (_entityStorage is null) return;
        var total = 0;
        foreach (var chunkPos in _entityManager.LoadedChunks)
        {
            var entities = new List<NetCraft.Registry.Entity>(_entityManager.GetEntitiesInChunk(chunkPos));
            _entityStorage.StoreEntities(new ChunkEntities<NetCraft.Registry.Entity>(chunkPos, entities));
            total += entities.Count;
        }
        await _entityStorage.Flush(true).ConfigureAwait(false);
        Log.Info($"Saved {total} entities across {_entityManager.LoadedChunks.Count()} chunks");
    }

    //LoadChunkAsync 从 RegionFileStorage 异步加载并反序列化对应原版 chunk load 路径
    //未命中返回 null 反序列化失败抛 ChunkReadException
    //ServerChunkCache 通过 loader 回调调用此方法
    public async Task<LevelChunk?> LoadChunkAsync(ChunkPos pos)
    {
        Log.Debug($"LoadChunkAsync entry pos={pos}");
        var optional = await _regionStorage.Read(pos).ConfigureAwait(false);
        if (!optional.IsPresent)
        {
            //Log.Debug($"LoadChunkAsync 出口 result=null 存档未命中");
            return null;
        }
        var tag = optional.Get();
        var data = SerializableChunkData.Parse(this, _factory, tag);
        if (data is null)
        {
            //Log.Debug($"LoadChunkAsync 出口 result=null 解析失败");
            return null;
        }
        var result = data.Read(this, SimplePoiManager.Empty, null, pos);
        //Log.Debug($"LoadChunkAsync 出口 result={(result is null ? "null" : result.Pos.ToString())}");
        return result;
    }

    //GetChunk 优先走 in-memory 缓存未命中委托 ServerChunkCache 异步调度
    //require=false 不阻塞主循环 未就绪返回 null 由调用方下次 tick 再取
    public override ChunkAccess? GetChunk(ChunkPos pos)
    {
        var cached = base.GetChunk(pos);
        if (cached is not null) return cached;
        return _chunkSource.GetChunk(pos.X, pos.Z, ChunkStatus.FULL, false);
    }

    //GetLoadedChunk 只取已在内存的区块 不触发加载
    //每 tick 遍历实体包围盒那类查询走它 位置挨着未加载区块时不至于凭空拉起一个没票的区块
    public override ChunkAccess? GetLoadedChunk(ChunkPos pos) => _chunkSource.GetLoadedChunk(pos.X, pos.Z);

    //GetChunkSync 同步获取区块 require=true 触发加载仅测试或必须同步场景用
    public ChunkAccess? GetChunkSync(ChunkPos pos, bool require = true)
        => _chunkSource.GetChunk(pos.X, pos.Z, ChunkStatus.FULL, require);

    //IsChunkFailed 区块是否已确认加载失败供发送端丢弃永久失败项
    public bool IsChunkFailed(ChunkPos pos) => _chunkSource.IsChunkFailed(pos.X, pos.Z);

    //WriteBlockState 只落状态与高度图 光照邻居等联动由 ServerLevel.SetBlock 按顺序触发
    protected override BlockState? WriteBlockState(BlockPos pos, BlockState state)
    {
        var chunk = GetChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        var section = chunk?.GetSection(pos.Y >> 4);
        var previous = section?.SetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15, state);
        if (previous is not null && chunk is not null)
            chunk.UpdateHeightmaps(pos.X, pos.Y, pos.Z, state);
        return previous;
    }

    //UpdateLight 方块状态变化后重算该位置光照 与 ProcessLight 共用一把锁保证引擎串行
    public override void UpdateLight(BlockPos pos) => _chunkSource.UpdateLight(pos);

    //GetLightValue 读指定光照层在该位置的值 对应原版 Level.getBrightness
    public override int GetLightValue(NetCraft.Registry.LightLayer layer, BlockPos pos)
        => _chunkSource.LightEngine.GetLayerListener(layer).GetLightValue(pos);

    //UpdateLightBatch 批量方块变化后的光照重算 全部标记完只跑一轮传播
    public void UpdateLightBatch(IReadOnlyList<BlockPos> positions) => _chunkSource.UpdateLightBatch(positions);

    //TickLight 每刻末尾统一推进光照并下发 由主循环在方块事件之后调一次
    public void TickLight() => _chunkSource.TickLight();

    //IsPositionTicking 该区块是否在方块可 tick 范围 对应原版 shouldTickBlocksAt
    //调度刻与方块实体的推进都按它过滤: 模拟距离之外的区块只加载着不推进
    //原版这条链是 shouldTickBlocksAt -> inBlockTickingRange -> 模拟等级 <= 32
    protected override bool IsPositionTicking(long chunkKey) => _chunkSource.InBlockTickingRange(chunkKey);

    //GetBlockState 按世界坐标读方块 未加载按空气 对应原版 BlockGetter.getBlockState
    //依附面判定要拿真实世界: 移动活塞这类形状由方块实体提供 空世界视图下会被判成没有形状
    public BlockState GetBlockState(int x, int y, int z)
    {
        var chunk = GetChunk(new ChunkPos(x >> 4, z >> 4));
        return chunk is null ? default : chunk.GetBlockState(x, y, z);
    }

    //LightUpdateSink 光照变化下发回调 由 Game 层注入 收区块坐标与两层受影响的区段索引
    public Action<ChunkPos, IReadOnlyList<int>, IReadOnlyList<int>>? LightUpdateSink
    {
        get => _chunkSource.LightUpdateSink;
        set => _chunkSource.LightUpdateSink = value;
    }

    //SaveChunkAsync 把 ChunkAccess 序列化写入 RegionFileStorage
    //使用 PersistentServerLevel.Factory 保证 codec 与注册的 Block/Biome 一致
    public async Task SaveChunkAsync(ChunkAccess chunk)
    {
        Log.Debug($"SaveChunkAsync entry chunk={chunk.Pos}");
        var data = SerializableChunkData.CopyOf(this, chunk, _factory);
        var tag = data.Write();
        //ConfigureAwait(false) 是必须的 调用方可能是有同步上下文的 UI 线程 例如 GUI 命令框敲 save-all
        //不脱离上下文的话续体要排回 UI 线程 而 UI 线程正阻塞等这个 Task 完成 会直接互等卡死
        await _regionStorage.Write(chunk.Pos, tag).ConfigureAwait(false);
        AddChunk(chunk);
        //Log.Debug($"SaveChunkAsync 出口");
    }

    //SaveChunkOnUnload 区块离开内存前的落盘 主线程只取快照 序列化与写盘交给后台 IO 线程
    //与 SaveChunkAsync 的区别是不把区块收回 in-memory 副本 卸载就是要让它离开内存
    //快照必须在主线程先取 方块实体也在这一步收进 NBT 卸载回调随后就把它们清掉了
    public void SaveChunkOnUnload(ChunkAccess chunk)
    {
        var data = SerializableChunkData.CopyOf(this, chunk, _factory);
        //supplier 形式在主线程入队 同一区块后续的读会命中这笔待写 不会读到落盘前的旧数据
        _ = _regionStorage.Write(data.ChunkPos, data.Write);
    }

    //Synchronize 刷盘对应原版 chunk save 阶段
    public Task SynchronizeAsync(bool flush)
        => _regionStorage.Synchronize(flush);

    //SaveAllChunksAsync 把区块源里已加载的区块全部落盘对应原版 saveAllChunks
    //区块生成后只活在 ServerChunkCache 内存里，不主动保存磁盘永远是空文件
    public async Task SaveAllChunksAsync()
    {
        var snapshots = SnapshotAllChunks();
        await WriteSnapshotsAsync(snapshots).ConfigureAwait(false);
    }

    //SnapshotAllChunks 主线程收集全部已加载区块的快照供后台落盘
    //CopyOf 在主线程执行避免后台拷贝 section 与主线程写区块的数据竞争
    public List<SerializableChunkData> SnapshotAllChunks()
    {
        var snapshots = new List<SerializableChunkData>();
        foreach (var chunk in _chunkSource.LoadedChunks)
        {
            snapshots.Add(SerializableChunkData.CopyOf(this, chunk, _factory));
            AddChunk(chunk);
        }
        return snapshots;
    }

    //WriteSnapshotsAsync 后台序列化 NBT 并写 region 不阻塞主线程
    //NBT 序列化与磁盘 IO 是刷盘耗时大头 对应原版 savingExecutor 的 worker 侧
    public async Task WriteSnapshotsAsync(IEnumerable<SerializableChunkData> snapshots)
    {
        var count = 0;
        foreach (var data in snapshots)
        {
            var tag = data.Write();
            await _regionStorage.Write(data.ChunkPos, tag).ConfigureAwait(false);
            count++;
        }
        if (count > 0)
            Log.Info($"Saved {count} chunks");
    }

    //GetNextEntityId 分配下一个可用实体 id 对应原版 ServerLevel.getNextEntityId
    //占用检查交给实体管理器 已加载(含卸载后待命)的实体都算占用
    public override int GetNextEntityId()
        => NetCraft.Registry.Entity.NextEntityId(_entityManager.HasEntityWithId);

    //AddEntity 加入实体到关卡对应原版 Level.addFreshEntity
    //顺带注入方块碰撞查询与关卡引用 Uuid 重复时拒绝加入
    //所在区块未加载时实体先处于待命态 等区块加载再参与 tick 与追踪
    public bool AddEntity(NetCraft.Registry.Entity entity)
    {
        AttachEntity(entity);
        return _entityManager.AddEntity(entity);
    }

    //AttachEntity 回填关卡引用与碰撞查询并挂接死亡回调
    //死亡回调只在这里挂 保证任何来源(读盘/summon)产生的实体死亡都能被服务端感知
    private void AttachEntity(NetCraft.Registry.Entity entity)
    {
        entity.Level = this;
        BindCollisionShapes(entity);
        if (EntityDeathCallback is null) return;
        //同一实体重复加入时先摘再挂 避免回调执行多次
        entity.Died -= EntityDeathCallback;
        entity.Died += EntityDeathCallback;
    }

    //BindCollisionShapes 把形状碰撞查询绑到实体上 未注入委托时保持为 null
    //委托按实体查 碰撞形状要按实体算 台阶朝向与脚手架的下落放宽都取决于实体本身
    private void BindCollisionShapes(NetCraft.Registry.Entity entity)
    {
        if (CollisionShapeProvider is not { } provider) return;
        entity.CollisionShapes = box => provider(entity, box);
    }

    //RemoveEntity 移除关卡实体返回是否成功
    public bool RemoveEntity(NetCraft.Registry.Entity entity)
        => _entityManager.RemoveEntity(entity);

    //Tick 关卡每帧调度对应原版 ServerLevel.tick
    //1. tick ChunkSource 推进区块调度完成区块移入缓存并触发实体加载回调
    //2. tick 实体管理器 推进可见实体并处理跨区块移动
    //3. 递增 levelTick 供刷盘调度
    //runsNormally 为假时世界推进停住 区块调度与实体管理照跑 对应原版冻结只过滤实体不停止遍历
    public void Tick(bool runsNormally = true)
    {
        //Log.Debug($"Tick 入口 levelTick={_levelTick}");
        _chunkSource.Tick();
        //区块调度刚触发了实体读盘 已完成的在这里落地 本拍新加入的实体还能赶上实体 tick
        DrainPendingEntityLoads();
        //经 GetChunk 直接取回而没走加载回调的区块在这里补登记调度刻容器
        foreach (var chunk in _chunkSource.LoadedChunks) EnsureChunkTicksRegistered(chunk);
        //实体推进按模拟距离过滤 模拟距离之外的区块实体只留在索引里待命
        _entityManager.Tick(!runsNormally, _chunkSource.InEntityTickingRange);
        if (runsNormally) _levelTick++;
        //Log.Debug($"Tick 出口 levelTick={_levelTick}");
    }

    //EntityBoxes 实体进入判定要在实体管理器里的实体之外再算上注入的玩家包围盒
    protected override IEnumerable<AABB> EntityBoxes()
    {
        foreach (var entity in _entityManager.Visible) yield return entity.BoundingBox;
        if (ExtraEntityBoxes is null) yield break;
        foreach (var box in ExtraEntityBoxes()) yield return box;
    }

    //CountLevelEntitiesInBox 按空间索引取候选再按包围盒相交过滤 玩家由基类另算
    protected override int CountLevelEntitiesInBox(AABB box)
    {
        var count = 0;
        foreach (var entity in _entityManager.VisibleLookup.GetInRange(box.Min, box.Max))
            if (box.Intersects(entity.BoundingBox)) count++;
        return count;
    }

    //LevelEntitiesInBox 与 CountLevelEntitiesInBox 同一套索引 只是把实体本身交出去
    protected override IEnumerable<NetCraft.Registry.Entity> LevelEntitiesInBox(AABB box)
    {
        foreach (var entity in _entityManager.VisibleLookup.GetInRange(box.Min, box.Max))
            if (box.Intersects(entity.BoundingBox)) yield return entity;
    }
}
