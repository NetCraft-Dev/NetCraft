using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//TicketStorage 区块票容器对应原版 net.minecraft.world.level.TicketStorage
//活跃票驱动加载与模拟 停用票只在内存里等着落盘 两类票都会写进 chunk_tickets.dat
//读盘先把票放进停用区 等 ActivateAllDeactivatedTickets 才转活跃 免的世界还没准备好旧票就把区块拉起来
//关服做反向操作 让世界尽快平静下来 票本身仍在内存里会被一起写盘 下次开服再激活
public sealed class TicketStorage : SavedData
{
    //TypeId 存档标识对应原版 minecraft:chunk_tickets 落盘到 data/minecraft/chunk_tickets.dat
    private const string TypeId = "minecraft:chunk_tickets";

    //TicketsTag 票列表字段名对应原版 "tickets"
    private const string TicketsTag = "tickets";

    //ChunkPosTag 票所属区块打包坐标字段对应原版 Pair 里的 chunk_pos
    private const string ChunkPosTag = "chunk_pos";
    //TypeTag 票类型注册名字段对应原版 "type"
    private const string TypeTag = "type";
    //LevelTag 票等级字段对应原版 "level"
    private const string LevelTag = "level";
    //TicksLeftTag 剩余 tick 字段对应原版 "ticks_left"
    private const string TicksLeftTag = "ticks_left";

    //_tickets 活跃票表 键是 ChunkPos 打包值 值是同一区块上的票列表
    private readonly Dictionary<long, List<Ticket>> _tickets = new();
    //_deactivatedTickets 停用票表 读盘与关服时票会待在这里
    private readonly Dictionary<long, List<Ticket>> _deactivatedTickets = new();
    //_chunksWithForcedTickets 强制加载区块集合 供 /forceload query 查询
    private readonly HashSet<long> _chunksWithForcedTickets = new();
    //_loadingListener 加载票等级变化回调 由 LoadingChunkTracker 注册
    private Action<long, int, bool>? _loadingListener;
    //_simulationListener 模拟票等级变化回调 由 SimulationChunkTracker 注册
    private Action<long, int, bool>? _simulationListener;

    //Type SavedData 工厂读档时把票全放进停用区
    public static readonly SavedDataType<TicketStorage> Type = new TicketStorageType(TypeId);

    //TypeFor 按维度取票表的数据类型 票表必须每维度一份
    //三个维度共用一份时每次接入新维度都会覆盖等级回调 最后只剩一个维度收得到票变化
    //主世界沿用原标识 其余维度加后缀 免得几个维度写进同一个文件
    public static SavedDataType<TicketStorage> TypeFor(Identifier dimension)
        => new TicketStorageType(dimension.Path == "overworld" ? TypeId : $"{TypeId}_{dimension.Path}");

    public override string Id => TypeId;

    public TicketStorage() { }

    //构造从存档标签还原 对应原版 fromPacked
    public TicketStorage(CompoundTag tag) => Load(tag);

    private sealed class TicketStorageType(string id) : SavedDataType<TicketStorage>
    {
        public string Id => id;

        public TicketStorage Create(CompoundTag tag, RegistryAccess registryAccess) => new(tag);
    }

    //AddTicket 加一张票 同类型同等级已存在时只续期不重复添加 对应原版 addTicket
    //返回 false 表示已有同票只做了续期
    public bool AddTicket(Ticket ticket, ChunkPos pos) => AddTicket(pos.Pack(), ticket);

    public bool AddTicket(long packed, Ticket ticket)
    {
        var list = GetOrCreateList(_tickets, packed);
        foreach (var existing in list)
        {
            if (!existing.IsSameTypeAndLevel(ticket)) continue;
            existing.ResetTicksLeft();
            SetDirty();
            return false;
        }
        var oldSimulationLevel = GetTicketLevelIn(list, true);
        var oldLoadingLevel = GetTicketLevelIn(list, false);
        list.Add(ticket);
        //只有等级变小才通知 等级变大由撤票那侧负责
        if (ticket.Type.DoesSimulate && ticket.Level < oldSimulationLevel)
            _simulationListener?.Invoke(packed, ticket.Level, true);
        if (ticket.Type.DoesLoad && ticket.Level < oldLoadingLevel)
            _loadingListener?.Invoke(packed, ticket.Level, true);
        if (ReferenceEquals(ticket.Type, TicketType.Forced)) _chunksWithForcedTickets.Add(packed);
        SetDirty();
        return true;
    }

    //RemoveTicket 移除同类型同等级的票对应原版 removeTicket
    public bool RemoveTicket(TicketType type, int level, ChunkPos pos)
        => RemoveTicket(new Ticket(type, level), pos);

    public bool RemoveTicket(Ticket ticket, ChunkPos pos) => RemoveTicket(pos.Pack(), ticket);

    public bool RemoveTicket(long packed, Ticket ticket)
    {
        if (!_tickets.TryGetValue(packed, out var list)) return false;
        var found = false;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].IsSameTypeAndLevel(ticket)) continue;
            list.RemoveAt(i);
            found = true;
        }
        if (!found) return false;
        if (list.Count == 0) _tickets.Remove(packed);
        //撤票后按剩下的票重报等级 这一侧不带 onlyDecreased
        if (ticket.Type.DoesSimulate) _simulationListener?.Invoke(packed, GetTicketLevelIn(list, true), false);
        if (ticket.Type.DoesLoad) _loadingListener?.Invoke(packed, GetTicketLevelIn(list, false), false);
        if (ReferenceEquals(ticket.Type, TicketType.Forced)) UpdateForcedChunks();
        SetDirty();
        return true;
    }

    //AddTicketWithRadius 按半径出票 等级 = 33 - radius 对应原版 addTicketWithRadius
    public void AddTicketWithRadius(TicketType type, ChunkPos pos, int radius)
        => AddTicket(new Ticket(type, ChunkLevel.FullChunkLevel - radius), pos);

    //RemoveTicketWithRadius 按半径撤票对应原版 removeTicketWithRadius
    public bool RemoveTicketWithRadius(TicketType type, ChunkPos pos, int radius)
        => RemoveTicket(type, ChunkLevel.FullChunkLevel - radius, pos);

    //GetTicketLevelAt 该区块当前票等级 无票返回 MaxLevel+1 表示不加载 对应原版 getTicketLevelAt
    //simulation 为真只算参与模拟的票 为假只算参与加载的票
    public int GetTicketLevelAt(long packedPos, bool simulation)
        => GetTicketLevelIn(_tickets.GetValueOrDefault(packedPos), simulation);

    //GetTicketLevelIn 票列表里的最低等级 只看适用该用途的票 空表返回 MaxLevel+1
    private static int GetTicketLevelIn(List<Ticket>? list, bool simulation)
    {
        if (list is null) return ChunkLevel.MaxLevel + 1;
        var level = ChunkLevel.MaxLevel + 1;
        foreach (var ticket in list)
        {
            var applies = simulation ? ticket.Type.DoesSimulate : ticket.Type.DoesLoad;
            if (applies && ticket.Level < level) level = ticket.Level;
        }
        return level;
    }

    //GetTickets 该区块上的活跃票 没有返回 null 对应原版 getTickets
    public IReadOnlyList<Ticket>? GetTickets(long packedPos) => _tickets.GetValueOrDefault(packedPos);

    //UpdateChunkForced 强制加载开关对应原版 updateChunkForced
    //等级取实体可 tick 档 与原版 ChunkMap.FORCED_TICKET_LEVEL 一致
    public bool UpdateChunkForced(ChunkPos pos, bool add)
        => add
            ? AddTicket(new Ticket(TicketType.Forced, ChunkLevel.EntityTickingLevel), pos)
            : RemoveTicket(TicketType.Forced, ChunkLevel.EntityTickingLevel, pos);

    //GetForceLoadedChunks 当前强制加载的区块打包坐标 对应原版 getForceLoadedChunks
    public IReadOnlyCollection<long> GetForceLoadedChunks() => _chunksWithForcedTickets;

    //SetLoadingChunkUpdatedListener 注册加载票变化回调 对应原版 setLoadingChunkUpdatedListener
    public void SetLoadingChunkUpdatedListener(Action<long, int, bool> listener) => _loadingListener = listener;

    //SetSimulationChunkUpdatedListener 注册模拟票变化回调 对应原版 setSimulationChunkUpdatedListener
    public void SetSimulationChunkUpdatedListener(Action<long, int, bool> listener) => _simulationListener = listener;

    //UpdateForcedChunks 重算强制加载集合 对应原版 updateForcedChunks
    private void UpdateForcedChunks()
    {
        _chunksWithForcedTickets.Clear();
        foreach (var (packed, tickets) in _tickets)
            foreach (var ticket in tickets)
            {
                if (!ReferenceEquals(ticket.Type, TicketType.Forced)) continue;
                _chunksWithForcedTickets.Add(packed);
                break;
            }
    }

    //ShouldKeepDimensionActive 是否有票要求维度保持活跃 对应原版 shouldKeepDimensionActive
    public bool ShouldKeepDimensionActive()
    {
        foreach (var list in _tickets.Values)
            foreach (var ticket in list)
                if (ticket.Type.ShouldKeepDimensionActive) return true;
        return false;
    }

    //PurgeStaleTickets 超时票清理每 tick 一次 对应原版 purgeStaleTickets
    //isReadyForSaving 回调判断区块能否安全丢票 返回 false 的本 tick 先留着
    //带 CanExpireIfUnloaded 的票不受回调限制 区块没就绪也能直接清
    public void PurgeStaleTickets(Func<long, bool>? isReadyForSaving = null)
    {
        List<(long Packed, Ticket Ticket)>? expired = null;
        foreach (var (packed, list) in _tickets)
        {
            foreach (var ticket in list)
            {
                if (!ticket.Type.HasTimeout) continue;
                if (!ticket.Type.CanExpireIfUnloaded && isReadyForSaving is not null
                    && !isReadyForSaving(packed)) continue;
                ticket.DecreaseTicksLeft();
                if (ticket.IsTimedOut) (expired ??= new()).Add((packed, ticket));
            }
        }
        if (expired is null) return;
        foreach (var (packed, ticket) in expired)
            RemoveTicket(ticket.Type, ticket.Level, ChunkPos.Unpack(packed));
    }

    //ActivateAllDeactivatedTickets 停用票全转活跃对应原版 activateAllDeactivatedTickets
    //启动链在初始区块准备完之后调用 此刻世界已就绪旧票才允许驱动加载
    public void ActivateAllDeactivatedTickets()
    {
        if (_deactivatedTickets.Count == 0) return;
        foreach (var (packed, list) in _deactivatedTickets)
        {
            var pos = ChunkPos.Unpack(packed);
            foreach (var ticket in list) AddTicket(ticket, pos);
        }
        _deactivatedTickets.Clear();
    }

    //DeactivateTicketsOnClosing 关服把活跃票搬进停用区 对应原版 deactivateTicketsOnClosing
    //停用后不再驱动加载 但票还在内存里会被写盘 下次开服再激活
    //UNKNOWN 是临时票不搬
    public void DeactivateTicketsOnClosing()
    {
        foreach (var (packed, list) in _tickets)
            foreach (var ticket in list)
            {
                if (ReferenceEquals(ticket.Type, TicketType.Unknown)) continue;
                GetOrCreateList(_deactivatedTickets, packed).Add(ticket);
            }
        _tickets.Clear();
        _chunksWithForcedTickets.Clear();
    }

    //Save 只写 persist 票停用票也要写 对应原版 packTickets 的过滤
    public override CompoundTag Save(CompoundTag tag)
    {
        var list = new ListTag();
        PackInto(_tickets, list);
        PackInto(_deactivatedTickets, list);
        tag.Put(TicketsTag, list);
        return tag;
    }

    //Load 读盘票统一先进停用区 等 ActivateAllDeactivatedTickets 才生效 对应原版 fromPacked
    private void Load(CompoundTag tag)
    {
        var list = tag.GetListOrEmpty(TicketsTag);
        foreach (var element in list)
        {
            if (element is not CompoundTag entry) continue;
            var typeName = entry.GetStringValue(TypeTag);
            var type = Identifier.TryParse(typeName) is { } identifier
                ? TicketType.ByName(identifier)
                : null;
            if (type is null)
            {
                Log.Warning($"Unknown ticket type {typeName}");
                continue;
            }
            GetOrCreateList(_deactivatedTickets, entry.GetLongOr(ChunkPosTag, 0L))
                .Add(new Ticket(type, entry.GetIntOr(LevelTag, 0), entry.GetLongOr(TicksLeftTag, 0L)));
        }
    }

    //PackInto 把票表里带 persist 的票逐张写进列表
    private static void PackInto(Dictionary<long, List<Ticket>> map, ListTag list)
    {
        foreach (var (packed, tickets) in map)
            foreach (var ticket in tickets)
            {
                if (!ticket.Type.Persist) continue;
                var entry = new CompoundTag();
                entry.PutLong(ChunkPosTag, packed);
                entry.PutString(TypeTag, ticket.Type.Name.ToString());
                entry.PutInt(LevelTag, ticket.Level);
                entry.PutLong(TicksLeftTag, ticket.TicksLeft);
                list.Add(entry);
            }
    }

    //GetOrCreateList 取或新建某区块的票列表
    private static List<Ticket> GetOrCreateList(Dictionary<long, List<Ticket>> map, long packed)
    {
        if (map.TryGetValue(packed, out var list)) return list;
        list = new List<Ticket>();
        map[packed] = list;
        return list;
    }
}
