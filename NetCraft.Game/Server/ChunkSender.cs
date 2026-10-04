using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Storage.Light;

namespace NetCraft.Game.Server;

//ChunkSender 渐进区块发送器对应原版 PlayerChunkSender
//UpdateCenter 玩家跨块时更新视野中心 新入视野的入队离开的遗忘对应原版 updateChunkTracking+applyChunkTrackingView
//Tick 每 tick 发一批 BatchStart+就绪区块+BatchFinished 未就绪的留在待发集合下次 tick 再取
//生成在 ServerChunkCache 后台推进不阻塞主循环 连接断开后永久终止
public sealed class ChunkSender
{
    //ChunksPerTick 每 tick 发送区块数对齐原版 desiredChunksPerTick 量级
    public const int ChunksPerTick = 4;

    private readonly Connection _connection;
    private readonly Func<ChunkPos, ChunkAccess?> _provider;
    private readonly Func<ChunkPos, bool>? _isFailed;
    //_lightSource 光照数据来源为空时下发空光照
    private readonly ServerChunkCache? _lightSource;
    //_blockEntityBridge 方块实体数据来源 为空时区块包不带方块实体
    private readonly IBlockEntityBridge? _blockEntityBridge;
    //待发送坐标按入队顺序排列 每 tick 从头取就绪的凑一批
    //原版 pendingChunks 也是插入序队列 本作先前每 tick 全量扫加排序纯属多余开销
    private readonly List<long> _pending = new();
    private readonly HashSet<long> _pendingSet = new();
    //视野内已入队过的坐标含已发送的 离开视野时据此判定是否需要发 Forget
    private readonly HashSet<long> _tracked = new();
    private int _centerX = int.MinValue;
    private int _centerZ;
    private int _viewDistance = 1;
    //_cursor 下轮扫描起点 未就绪区块原地保留不重复从头扫
    private int _cursor;
    private int _sentCount;
    private int _failedCount;
    private bool _closed;

    public ChunkSender(Connection connection, Func<ChunkPos, ChunkAccess?> provider,
        Func<ChunkPos, bool>? isFailed = null, ServerChunkCache? lightSource = null,
        IBlockEntityBridge? blockEntityBridge = null)
    {
        _connection = connection;
        _provider = provider;
        _isFailed = isFailed;
        _lightSource = lightSource;
        _blockEntityBridge = blockEntityBridge;
    }

    //PendingCount 待发送区块数供诊断
    public int PendingCount => _pending.Count;

    //FailedCount 已丢弃的加载失败区块数供诊断
    public int FailedCount => _failedCount;

    //UpdateCenter 更新视野中心 新入视野的入队离开的遗忘并同步 SetChunkCacheCenter
    //中心与视距都没变时直接返回避免重复发包
    public void UpdateCenter(int centerChunkX, int centerChunkZ, int viewDistance)
    {
        var radius = Math.Clamp(viewDistance, 1, 32);
        if (_centerX != int.MinValue && _centerX == centerChunkX && _centerZ == centerChunkZ && _viewDistance == radius)
            return;
        _centerX = centerChunkX;
        _centerZ = centerChunkZ;
        _viewDistance = radius;
        var radiusSquared = radius * radius;
        //新入视野坐标入队 圆形判定与初始入队口径一致
        //本批内部按距离排好再追加 于是队列整体近似距离序 发送端不必再每 tick 全量重排
        List<long>? added = null;
        for (var dx = -radius; dx <= radius; dx++)
        for (var dz = -radius; dz <= radius; dz++)
        {
            if (dx * dx + dz * dz > radiusSquared) continue;
            var key = ChunkPos.Pack(centerChunkX + dx, centerChunkZ + dz);
            if (_tracked.Add(key)) (added ??= new List<long>()).Add(key);
        }
        if (added is not null)
        {
            added.Sort((a, b) => DistanceSquared(ChunkPos.Unpack(a)).CompareTo(DistanceSquared(ChunkPos.Unpack(b))));
            foreach (var key in added)
            {
                _pending.Add(key);
                _pendingSet.Add(key);
            }
        }
        //离开视野的遗忘 未发送的仅出队已发送的发 Forget 包对应原版 dropChunk
        foreach (var key in _tracked.Where(key => DistanceSquared(ChunkPos.Unpack(key)) > radiusSquared).ToList())
            DropChunk(key);
        _connection.Send(new ClientboundSetChunkCacheCenterPacket(centerChunkX, centerChunkZ));
    }

    //DropChunk 遗忘单个区块 已发送过的才需要通知客户端
    private void DropChunk(long key)
    {
        _tracked.Remove(key);
        if (_pendingSet.Remove(key))
        {
            _pending.Remove(key);
            return;
        }
        if (_connection.IsConnected)
            _connection.Send(new ClientboundForgetLevelChunkPacket(ChunkPos.Unpack(key)));
    }

    private int DistanceSquared(ChunkPos pos)
    {
        var dx = pos.X - _centerX;
        var dz = pos.Z - _centerZ;
        return dx * dx + dz * dz;
    }

    //Tick 渐进发送本批就绪区块 从游标处按队列序取够一批即停
    //队列入队时已近似距离序 单 tick 扫描量由预算封顶 未就绪的留在原地下轮由游标回看
    public void Tick()
    {
        if (_closed) return;
        //连接已断则终止发送并清空状态 避免每 tick 抛 ObjectDisposedException 刷日志
        if (!_connection.IsConnected)
        {
            Log.Debug($"Chunk sending aborted, connection closed remaining={_pending.Count}");
            ClearQueue();
            _closed = true;
            return;
        }
        if (_pending.Count == 0)
        {
            _cursor = 0;
            return;
        }
        if (_cursor >= _pending.Count) _cursor = 0;
        var budget = Math.Max(ChunksPerTick * 4, 16);
        var batch = new List<(ChunkPos Pos, ChunkAccess Chunk)>(ChunksPerTick);
        var index = _cursor;
        while (index < _pending.Count && budget-- > 0 && batch.Count < ChunksPerTick)
        {
            var key = _pending[index];
            var pos = ChunkPos.Unpack(key);
            if (_isFailed?.Invoke(pos) == true)
            {
                _pending.RemoveAt(index);
                _pendingSet.Remove(key);
                _tracked.Remove(key);
                _failedCount++;
                continue;
            }
            var chunk = _provider(pos);
            if (chunk is null)
            {
                index++;
                continue;
            }
            batch.Add((pos, chunk));
            _pending.RemoveAt(index);
            _pendingSet.Remove(key);
        }
        _cursor = index >= _pending.Count ? 0 : index;
        if (batch.Count == 0) return;
        try
        {
            _connection.Send(new ClientboundChunkBatchStartPacket());
            foreach (var (pos, chunk) in batch)
            {
                //区块序列化是重活且不碰光照引擎 放在光照锁外做 锁内只读光照数据
                //整段进锁会让主线程持锁时间被序列化拉长 生成线程跟着一起等
                var lightSource = _lightSource;
                //方块实体随区块一起下发 客户端进服就能看到已有的方块实体
                var blockEntities = _blockEntityBridge?.Collect(pos);
                var packet = lightSource is null
                    ? ClientboundLevelChunkWithLightPacket.CreatePrepared(
                        pos.X, pos.Z, chunk, () => ClientboundLightUpdatePacketData.Empty, blockEntities)
                    : ClientboundLevelChunkWithLightPacket.CreatePrepared(
                        pos.X, pos.Z, chunk,
                        () => lightSource.WithLightLock(engine => new ClientboundLightUpdatePacketData(pos, engine)),
                        blockEntities);
                _connection.Send(packet);
                _sentCount++;
            }
            _connection.Send(new ClientboundChunkBatchFinishedPacket(batch.Count));
        }
        catch (Exception e)
        {
            _closed = true;
            //连接在发送期间被对端关闭属正常竞态 静默终止不误报
            if (!_connection.IsConnected)
            {
                Log.Debug($"Chunk sending aborted, connection closed remaining={_pending.Count}");
                ClearQueue();
                return;
            }
            Log.Warning($"Chunk send failed remaining={_pending.Count} {e.Message} connAlive={_connection.IsConnected} details={_connection.DisconnectionDetails?.Reason ?? "none"}");
        }
    }

    private void ClearQueue()
    {
        _pending.Clear();
        _pendingSet.Clear();
        _tracked.Clear();
        _cursor = 0;
    }
}
