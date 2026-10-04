using NetCraft.Primitives;
using NetCraft.Util;
using NetCraftEntity = NetCraft.Registry.Entity;

namespace NetCraft.Storage;

//EntityManager 实体生命周期管理 对应原版 net.minecraft.world.level.entity.PersistentEntitySectionManager
//实体按所在 chunk 归属 分两态:
//可见(参与 tick 与网络追踪) / 待命(区块卸载后退出 tick 只留在索引里等下次加载)
//加入即视为可见 对应原版 addNewEntity 无条件进 visibleEntityStorage 再由区块事件纠正
//tick 期间增删走延迟队列 避免遍历过程中改动集合 对应原版 EntityTickList 的 active/pending
//实体跨 chunk 移动后刷新区块归属与空间索引 否则落盘会写错文件 查询会查不到
public sealed class EntityManager
{
    //可见实体空间索引 供 AABB 查询与追踪遍历
    private readonly EntityLookup _visible = new();
    //chunkPacked → 该 chunk 下全部实体(含待命)
    private readonly Dictionary<long, List<NetCraftEntity>> _byChunk = new();
    //entity → 当前所属 chunk 反查便于移动后重分桶
    private readonly Dictionary<NetCraftEntity, long> _entityChunk = new(ReferenceEqualityComparer.Instance);
    //entity → 当前所属 section 反查 空间索引按 section 分桶需同步刷新
    private readonly Dictionary<NetCraftEntity, long> _entitySection = new(ReferenceEqualityComparer.Instance);
    //knownUuids 已纳入管理的实体 按 Uuid 去重对应原版 knownUuids
    private readonly Dictionary<Guid, NetCraftEntity> _knownUuids = new();
    //byEntityId 网络 id 到实体的索引 攻击/交互包只带 entityId 必须按它反查目标
    private readonly Dictionary<int, NetCraftEntity> _byEntityId = new();
    //ticking 本轮参与 tick 的实体
    private readonly List<NetCraftEntity> _ticking = new();
    private readonly List<NetCraftEntity> _pendingAdd = new();
    private readonly List<NetCraftEntity> _pendingRemove = new();
    private bool _processing;

    //Count 已纳入管理的实体总数(含待命)
    public int Count => _knownUuids.Count;

    //Visible 可见实体集合 供网络追踪遍历
    public IEnumerable<NetCraftEntity> Visible => _visible.GetAll();

    //VisibleLookup 可见实体空间索引 供 AABB 范围查询
    public EntityLookup VisibleLookup => _visible;

    //AddEntity 纳入实体 对应原版 addNewEntity
    //Uuid 重复直接拒绝 加入即开始 tick 与追踪 区块卸载后由 OnChunkUnloaded 转为待命
    //没分配过 id 的实体在这里补分配 对应原版构造里的 level.getNextEntityId 分配后立刻登记才算占住
    public bool AddEntity(NetCraftEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!_knownUuids.TryAdd(entity.Uuid, entity)) return false;
        if (entity.EntityId == 0) entity.SetId(NetCraftEntity.NextEntityId(HasEntityWithId));
        _byEntityId[entity.EntityId] = entity;
        AddToChunkIndex(entity, ChunkOf(entity.Pos));
        StartTicking(entity);
        return true;
    }

    //RemoveEntity 移除实体 对应原版 removeEntity
    //tick 期间只入队 tick 结束再真正摘除
    public bool RemoveEntity(NetCraftEntity entity)
    {
        if (entity is null || !_knownUuids.ContainsKey(entity.Uuid)) return false;
        if (_processing)
        {
            _pendingRemove.Add(entity);
            return true;
        }
        return RemoveNow(entity);
    }

    //OnChunkLoaded 区块加载完成 该 chunk 下待命实体转为可见可 tick
    public void OnChunkLoaded(ChunkPos pos)
    {
        if (!_byChunk.TryGetValue(pos.Pack(), out var list)) return;
        for (var i = 0; i < list.Count; i++)
            StartTicking(list[i]);
    }

    //OnChunkUnloaded 区块卸载 该 chunk 下实体退出 tick 与追踪 仍留在索引里等下次加载
    public void OnChunkUnloaded(ChunkPos pos)
    {
        if (!_byChunk.TryGetValue(pos.Pack(), out var list)) return;
        for (var i = 0; i < list.Count; i++)
            StopTicking(list[i]);
    }

    //Tick 推进全部可见实体 对应原版 EntityTickList.forEach
    //顺序: tick 实体 → 收集自请求移除的 → 应用本轮增删 → 刷新移动实体的归属与空间索引
    //frozen 为真时实体一律原地不动 对应原版 isEntityFrozen 命中(玩家不在本集合里 由 PlayerList 单独调度)
    //遍历与索引刷新照跑 冻结后新加入的实体才能被追踪同步出去 掉出来看不见才是真的"没反应"
    //entityTicking 为真时该区块的实体才推进 对应原版 ServerLevel.tick 里的 inEntityTickingRange 过滤
    //模拟距离之外的区块实体只留在索引里待命 不传则全部推进
    public void Tick(bool frozen = false, Func<long, bool>? entityTicking = null)
    {
        _processing = true;
        if (!frozen)
            for (var i = 0; i < _ticking.Count; i++)
            {
                var entity = _ticking[i];
                if (entityTicking is not null && !entityTicking(ChunkOf(entity.Pos).Pack())) continue;
                entity.Tick();
            }
        _processing = false;
        //实体自己请求移除的(空栈/超时/拾取)并入延迟队列 遍历中不改动 _ticking
        for (var i = 0; i < _ticking.Count; i++)
        {
            var entity = _ticking[i];
            if (entity.IsRemoved && !_pendingRemove.Contains(entity)) _pendingRemove.Add(entity);
        }
        ApplyPending();
        for (var i = 0; i < _ticking.Count; i++)
            UpdatePlacement(_ticking[i]);
    }

    //GetEntitiesInChunk 取指定 chunk 下全部实体(含待命) 供实体落盘
    public IReadOnlyList<NetCraftEntity> GetEntitiesInChunk(ChunkPos pos)
        => _byChunk.TryGetValue(pos.Pack(), out var list) ? list : Array.Empty<NetCraftEntity>();

    //LoadedChunks 当前挂有实体的 chunk 位置 供实体落盘遍历
    //原先 Select+ToList 每次调用都materialize一份列表 落盘每轮都要用它 改成惰性产出
    public IEnumerable<ChunkPos> LoadedChunks
    {
        get
        {
            foreach (var key in _byChunk.Keys) yield return ChunkPos.Unpack(key);
        }
    }

    //GetByUuid 按 Uuid 查实体 对应原版 getEntity(uuid)
    public NetCraftEntity? GetByUuid(Guid uuid)
        => _knownUuids.TryGetValue(uuid, out var entity) ? entity : null;

    //GetByEntityId 按网络 id 查实体 对应原版 getEntity(int id)
    //攻击与交互包只带 entityId 命中不了就当作无效目标丢弃
    public NetCraftEntity? GetByEntityId(int entityId)
        => _byEntityId.TryGetValue(entityId, out var entity) ? entity : null;

    //HasEntityWithId 该网络 id 是否已被占用 对应原版 ChunkMap.hasEntityWithId
    //实体 id 分配时查重用 已卸载但仍留在索引里的实体也算占用
    public bool HasEntityWithId(int entityId) => _byEntityId.ContainsKey(entityId);

    //Clear 清空全部实体与索引
    public void Clear()
    {
        _visible.Clear();
        _byChunk.Clear();
        _entityChunk.Clear();
        _entitySection.Clear();
        _knownUuids.Clear();
        _byEntityId.Clear();
        _ticking.Clear();
        _pendingAdd.Clear();
        _pendingRemove.Clear();
    }

    //StartTicking 让实体进入可见可 tick 集合 tick 期间入队等本轮结束再生效
    private void StartTicking(NetCraftEntity entity)
    {
        if (_processing)
        {
            _pendingAdd.Add(entity);
            return;
        }
        if (_ticking.Contains(entity)) return;
        _ticking.Add(entity);
        _visible.Add(entity);
        _entitySection[entity] = SectionKeyOf(entity.Pos);
    }

    //StopTicking 把实体退出可见可 tick 集合
    private void StopTicking(NetCraftEntity entity)
    {
        _ticking.Remove(entity);
        _visible.Remove(entity);
        _entitySection.Remove(entity);
        _pendingAdd.Remove(entity);
    }

    //RemoveNow 立即摘除实体与其全部索引
    private bool RemoveNow(NetCraftEntity entity)
    {
        if (!_knownUuids.Remove(entity.Uuid)) return false;
        _byEntityId.Remove(entity.EntityId);
        if (_entityChunk.TryGetValue(entity, out var chunk)) RemoveFromChunkIndex(entity, chunk);
        StopTicking(entity);
        _pendingRemove.Remove(entity);
        return true;
    }

    //ApplyPending 应用 tick 期间累积的增删
    private void ApplyPending()
    {
        if (_pendingRemove.Count > 0)
        {
            //RemoveNow 会顺带清理该队列 先取快照再清空避免遍历中改动集合
            var removals = _pendingRemove.ToArray();
            _pendingRemove.Clear();
            foreach (var entity in removals) RemoveNow(entity);
        }
        for (var i = 0; i < _pendingAdd.Count; i++)
        {
            var entity = _pendingAdd[i];
            //tick 期间被移除的实体不再进入可见集合
            if (_knownUuids.ContainsKey(entity.Uuid)) StartTicking(entity);
        }
        _pendingAdd.Clear();
    }

    //UpdatePlacement 同步实体移动后的区块归属与空间索引
    private void UpdatePlacement(NetCraftEntity entity)
    {
        var chunk = ChunkOf(entity.Pos);
        var chunkKey = chunk.Pack();
        if (_entityChunk.TryGetValue(entity, out var previousChunk) && previousChunk != chunkKey)
        {
            RemoveFromChunkIndex(entity, previousChunk);
            AddToChunkIndex(entity, chunk);
        }
        var section = SectionKeyOf(entity.Pos);
        if (_entitySection.TryGetValue(entity, out var previousSection) && previousSection == section) return;
        _visible.Remove(entity);
        _visible.Add(entity);
        _entitySection[entity] = section;
    }

    private void AddToChunkIndex(NetCraftEntity entity, ChunkPos chunk)
    {
        var key = chunk.Pack();
        if (!_byChunk.TryGetValue(key, out var list))
        {
            list = new List<NetCraftEntity>();
            _byChunk[key] = list;
        }
        if (!list.Contains(entity)) list.Add(entity);
        _entityChunk[entity] = key;
    }

    private void RemoveFromChunkIndex(NetCraftEntity entity, long chunkKey)
    {
        if (_byChunk.TryGetValue(chunkKey, out var list))
        {
            list.Remove(entity);
            if (list.Count == 0) _byChunk.Remove(chunkKey);
        }
        _entityChunk.Remove(entity);
    }

    private static ChunkPos ChunkOf(Vec3 pos)
        => new(Mth.Floor(pos.X) >> 4, Mth.Floor(pos.Z) >> 4);

    private static long SectionKeyOf(Vec3 pos)
        => SectionPos.AsLong(
            SectionPos.BlockToSectionCoord(pos.X),
            SectionPos.BlockToSectionCoord(pos.Y),
            SectionPos.BlockToSectionCoord(pos.Z));
}
