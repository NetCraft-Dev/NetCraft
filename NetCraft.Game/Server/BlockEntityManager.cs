using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//BlockEntityManager 方块实体集合对应原版 LevelChunk 内的 blockEntities 容器
//按 BlockPos 索引提供增删查与每帧 tick 原版容器挂在区块上 这里按关卡统一持有简化
//区块反序列化在线程池线程上跑(ServerChunkCache.LoadAsync) 会与本容器的读写并发
//原版这一步在主线程做 本作读盘下到了后台 用一把锁把容器兜住
public sealed class BlockEntityManager
{
    private readonly Dictionary<long, BlockEntity> _entities = new();

    //加入序号 原版方块实体是按加入顺序逐个 tick 的 blockEntityTickers 就是个顺序表
    //字典自己的遍历顺序随增删漂移 同一拍里多个方块实体的收尾次序也跟着漂
    //活塞收回时底座与前方两格是同一拍收尾的 底座先收尾才判定得到"信号已经消失"
    //次序一反过来 收尾刚落回红石块就被活塞读到 立刻又伸出 表现成停不下来的自激循环
    private readonly Dictionary<long, long> _orders = new();
    private long _nextOrder;

    //_entities 是普通字典 并发写会损坏内部数组并抛出越界的假像 进出都要持锁
    private readonly object _lock = new();

    public int Count
    {
        get
        {
            lock (_lock) return _entities.Count;
        }
    }

    //Entities 取全部方块实体的快照 直接返回 Values 会让调用方遍历时撞上并发写
    public IEnumerable<BlockEntity> Entities
    {
        get
        {
            lock (_lock) return _entities.Values.ToArray();
        }
    }

    //Add 登记方块实体并注入关卡 同位置重复登记覆盖
    public T Add<T>(ServerLevel level, T entity) where T : BlockEntity
    {
        entity.Level = level;
        var key = entity.Pos.AsLong();
        lock (_lock)
        {
            _entities[key] = entity;
            _orders[key] = _nextOrder++;
        }
        return entity;
    }

    public BlockEntity? Get(BlockPos pos)
    {
        lock (_lock) return _entities.TryGetValue(pos.AsLong(), out var entity) ? entity : null;
    }

    public bool Remove(BlockPos pos)
    {
        var key = pos.AsLong();
        lock (_lock)
        {
            _orders.Remove(key);
            return _entities.Remove(key);
        }
    }

    //InChunk 取指定区块内的方块实体 落盘采集与区块包下发按区块取
    //先取快照再筛 锁不能跨 yield 留在迭代器里
    public IEnumerable<BlockEntity> InChunk(ChunkPos pos)
    {
        List<BlockEntity> snapshot;
        lock (_lock) snapshot = _entities.Values.ToList();
        foreach (var entity in snapshot)
            if (InChunk(entity.Pos, pos)) yield return entity;
    }

    //RemoveInChunk 移除指定区块内的全部方块实体 返回移除个数
    //区块卸载时调用 对应原版区块卸载带走其方块实体
    public int RemoveInChunk(ChunkPos pos)
    {
        lock (_lock)
        {
            List<long>? keys = null;
            foreach (var (key, entity) in _entities)
            {
                if (!InChunk(entity.Pos, pos)) continue;
                (keys ??= new List<long>()).Add(key);
            }
            if (keys is null) return 0;
            foreach (var key in keys)
            {
                _entities.Remove(key);
                _orders.Remove(key);
            }
            return keys.Count;
        }
    }

    private static bool InChunk(BlockPos blockPos, ChunkPos chunkPos)
        => (blockPos.X >> 4) == chunkPos.X && (blockPos.Z >> 4) == chunkPos.Z;

    //Tick 推进全部方块实体 先取快照避免 tick 过程中增删改动集合
    //实体自身的 tick 在锁外跑 它内部还会回头改本容器 锁只要护住取快照那一下
    public void Tick()
    {
        List<BlockEntity> snapshot;
        lock (_lock)
            //按加入顺序排 与原版 blockEntityTickers 的迭代顺序一致
            snapshot = _entities
                .OrderBy(pair => _orders.TryGetValue(pair.Key, out var order) ? order : long.MaxValue)
                .Select(pair => pair.Value)
                .ToList();
        foreach (var entity in snapshot) entity.Tick();
    }
}
