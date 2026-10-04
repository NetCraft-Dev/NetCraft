using System.Collections.Concurrent;
using System.Collections.Generic;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//ChunkMap 区块调度与视距管理对应原版 net.minecraft.server.level.ChunkMap
//持有 holder 表与待卸载集合 内嵌 DistanceManager 把票等级落到 holder 上
//阶段 11.48 简化实现仅追踪玩家中心 chunk 与视距计算
//3.4 起 holder 归属从 ServerChunkCache 迁到此处 与原版一致
public sealed class ChunkMap
{
    private readonly int _viewDistance;
    //_chunks 已建立的持有器 生成线程与主线程都会碰 沿用并发字典
    private readonly ConcurrentDictionary<long, ChunkHolder> _chunks = new();
    //_toDrop 等级掉出加载范围的持有器 对应原版 toDrop
    private readonly HashSet<long> _toDrop = new();
    //_pendingUnloads 已判定卸载但还没处理的持有器 对应原版 pendingUnloads
    private readonly Dictionary<long, ChunkHolder> _pendingUnloads = new();

    //ViewDistance 玩家视距半径单位 chunk
    public int ViewDistance => _viewDistance;

    //Distance 距离管理器 3.3 的 BFS 传播由它把票等级落到持有器上
    public DistanceManager Distance { get; }

    public ChunkMap(int viewDistance)
    {
        //视距下限 3 上限 32 对齐原版 server-view-distance
        _viewDistance = Math.Clamp(viewDistance, 3, 32);
        Distance = new ChunkMapDistanceManager(this);
    }

    //HoldersCount 当前持有器数量供诊断
    public int HoldersCount => _chunks.Count;

    //Holders 持有器视图供 tick 遍历与诊断
    public ICollection<ChunkHolder> Holders => _chunks.Values;

    //GetHolder 取持有器不创建对应原版 getUpdatingChunkIfPresent
    public ChunkHolder? GetHolder(long packedPos) => _chunks.GetValueOrDefault(packedPos);

    //GetOrCreateHolder 取或建立持有器对应原版 getOrCreateHolder
    public ChunkHolder GetOrCreateHolder(ChunkPos pos)
        => _chunks.GetOrAdd(pos.Pack(), _ => new ChunkHolder(pos));

    //TryRemoveHolder 摘掉持有器返回是否存在过
    public bool TryRemoveHolder(long packedPos) => _chunks.TryRemove(packedPos, out _);

    //ClearHolders 清空持有器
    public void ClearHolders() => _chunks.Clear();

    //UpdateChunkScheduling 按新等级调整持有器对应原版 updateChunkScheduling
    //新旧等级都在加载范围外就原样返回 等级掉出范围记进待卸载 回到范围则复活或新建
    public ChunkHolder? UpdateChunkScheduling(long packedPos, int level, ChunkHolder? holder, int oldLevel)
    {
        if (!ChunkLevel.IsLoaded(oldLevel) && !ChunkLevel.IsLoaded(level)) return holder;
        if (holder is null)
        {
            if (!ChunkLevel.IsLoaded(level)) return null;
            //先看待卸载区有没有同一块的旧持有器 有就复活它省掉重新加载
            if (!_pendingUnloads.Remove(packedPos, out holder))
                holder = new ChunkHolder(ChunkPos.Unpack(packedPos));
            _chunks[packedPos] = holder;
        }
        holder.UpdateTicketLevel(level);
        //待卸载集合跟着等级走 新建的持有器同样要登记 否则等级回升后旧键会一直留在集合里
        //留着的后果是 LoadingChunkTracker.GetLevel 对该区块一直报未加载 等级收敛会反复误判
        if (ChunkLevel.IsLoaded(level)) _toDrop.Remove(packedPos);
        else _toDrop.Add(packedPos);
        return holder;
    }

    //IsChunkToRemove 该区块是否在待卸载集合对应原版 isChunkToRemove
    public bool IsChunkToRemove(long packedPos) => _toDrop.Contains(packedPos);

    //ChunkMapDistanceManager 把距离管理器三条规则转发给本表对应原版 ChunkMap 的内嵌子类
    private sealed class ChunkMapDistanceManager : DistanceManager
    {
        private readonly ChunkMap _map;

        public ChunkMapDistanceManager(ChunkMap map) => _map = map;

        public override bool IsChunkToRemove(long packedPos) => _map.IsChunkToRemove(packedPos);

        public override ChunkHolder? GetChunk(long packedPos) => _map.GetHolder(packedPos);

        public override ChunkHolder? UpdateChunkScheduling(long packedPos, int newLevel, ChunkHolder? holder, int oldLevel)
            => _map.UpdateChunkScheduling(packedPos, newLevel, holder, oldLevel);
    }
}
