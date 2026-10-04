using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//LightEngine 光照引擎基类对应原版 net.minecraft.world.level.lighting.LightEngine
//传播机制内嵌在本类 由 checkNode/propagateIncrease/propagateDecrease 三个抽象方法落到具体层
//每个待处理项按顺序入队两个 long(节点 + 打包的层级与方向) 分别构成 decrease/increase 双 FIFO
public abstract class LightEngine<TSelf, TStorage> : LayerLightEventListener
    where TSelf : DataLayerStorageMap<TSelf>
    where TStorage : LayerLightSectionStorage<TSelf>
{
    public const int MaxLevel = 15;
    protected const int MinOpacity = 1;

    private const int MinQueueSize = 512;
    //ChunkCacheMask 区块缓存的坐标掩码 缓存 8x8 个区块
    //光照 BFS 在相邻区块之间来回跳 原版那两格 LRU 在地形一穿插时几乎每步都落空
    //这里改成按坐标掩码直接映射 一个槽只认一个坐标 点到的就是相邻区块 也省掉 LRU 的搬运
    private const int ChunkCacheMask = 7;
    private const int ChunkCacheSize = ChunkCacheMask + 1;

    protected readonly LightChunkGetter ChunkSource;

    //storage 除子类外 LevelLightEngine 也要访问 故放开到同程序集
    protected internal readonly TStorage Storage;

    protected static readonly long PullLightInEntry = QueueEntry.DecreaseAllDirections(1);
    protected static readonly Direction[] PropagationDirections = Direction.Values;

    private readonly HashSet<long> _blockNodesToCheck = new(MinQueueSize);
    private readonly Queue<long> _decreaseQueue = new();
    private readonly Queue<long> _increaseQueue = new();
    private readonly long[] _lastChunkPos = new long[ChunkCacheSize * ChunkCacheSize];
    private readonly LightChunk?[] _lastChunk = new LightChunk?[ChunkCacheSize * ChunkCacheSize];

    //checkNode 重新评估单个方块节点的光照来源
    protected abstract void CheckNode(long blockNode);

    //propagateIncrease 向六邻接推高光照
    protected abstract void PropagateIncrease(long fromNode, long increaseData, int fromLevel);

    //propagateDecrease 向六邻接回落光照
    protected abstract void PropagateDecrease(long fromNode, long decreaseData);

    protected LightEngine(LightChunkGetter chunkSource, TStorage storage)
    {
        ChunkSource = chunkSource;
        Storage = storage;
        ClearChunkCache();
    }

    //hasDifferentLightProperties 方块变化是否影响光照
    //原版还比较 useShapeForLightOcclusion 形状体系未实现故不比较
    public static bool HasDifferentLightProperties(BlockState oldState, BlockState newState)
    {
        if (newState == oldState) return false;
        return newState.GetLightDampening() != oldState.GetLightDampening()
               || newState.GetLightEmission() != oldState.GetLightEmission();
    }

    //IsEmptyShape 该状态是否按空形状参与遮挡比较 对应原版 isEmptyShape
    //只有既遮挡光线又声明按形状遮挡的方块才不按空形状处理
    protected static bool IsEmptyShape(BlockState? state)
        => state is not { } value || !value.Owner.CanOcclude || !value.Owner.UseShapeForLightOcclusion;

    //GetFaceOcclusionShape 某面上用于遮挡比较的形状 对应原版 getOcclusionShape
    private static VoxelShape GetFaceOcclusionShape(BlockState? state, Direction direction)
    {
        if (state is not { } value || IsEmptyShape(value)) return Shapes.Empty();
        return value.Owner.GetOcclusionShape(value).GetFaceShape(direction);
    }

    //ShapeOccludes 两个相接面是否合起来遮住该方向 对应原版 shapeOccludes
    //遮住时该方向不再传播 这是不完整方块之间不漏光的关键
    protected static bool ShapeOccludes(BlockState? fromState, BlockState? toState, Direction direction)
        => Shapes.FaceShapeOccludes(GetFaceOcclusionShape(fromState, direction),
            GetFaceOcclusionShape(toState, direction.Opposite));

    //getState 按坐标取方块状态 区块未加载返回 null
    protected BlockState? GetState(int x, int y, int z)
    {
        var chunkX = SectionPos.BlockToSectionCoord(x);
        var chunkZ = SectionPos.BlockToSectionCoord(z);
        var chunk = GetChunk(chunkX, chunkZ);
        return chunk?.GetBlockState(x, y, z);
    }

    //getOpacity 取方块减光值 未加载位置按完全不透明处理 对应原版取基岩默认状态
    protected int GetOpacity(BlockState? state)
        => state is null ? MaxLevel : Math.Max(MinOpacity, state.Value.GetLightDampening());

    //getChunk 带缓存的区块查询 光照传播大量重复访问同一区块
    //槽位由坐标低位掩码定 相邻区块各占各的槽 不会互相踢
    protected LightChunk? GetChunk(int chunkX, int chunkZ)
    {
        var index = (chunkX & ChunkCacheMask) * ChunkCacheSize + (chunkZ & ChunkCacheMask);
        var pos = ChunkPos.Pack(chunkX, chunkZ);
        if (pos == _lastChunkPos[index]) return _lastChunk[index];

        var chunk = ChunkSource.GetChunkForLighting(chunkX, chunkZ);
        _lastChunkPos[index] = pos;
        _lastChunk[index] = chunk;
        return chunk;
    }

    private void ClearChunkCache()
    {
        Array.Fill(_lastChunkPos, ChunkPos.InvalidChunkPos);
        Array.Fill(_lastChunk, null);
    }

    public void CheckBlock(BlockPos pos) => _blockNodesToCheck.Add(pos.AsLong());

    public void QueueSectionData(long sectionNode, DataLayer? data) => Storage.QueueSectionData(sectionNode, data);

    public void RetainData(ChunkPos pos, bool retain)
        => Storage.RetainData(SectionPos.GetZeroNode(pos.X, pos.Z), retain);

    public void UpdateSectionStatus(SectionPos pos, bool sectionEmpty)
        => Storage.UpdateSectionStatus(pos.AsLong(), sectionEmpty);

    public virtual void SetLightEnabled(ChunkPos pos, bool enable)
        => Storage.SetLightEnabled(SectionPos.GetZeroNode(pos.X, pos.Z), enable);

    //propagateLightSources 传播该区块内的全部光源
    public abstract void PropagateLightSources(ChunkPos pos);

    //RunLightUpdates 无预算推进 把光照队列彻底跑空对应接口默认语义
    public int RunLightUpdates() => RunLightUpdates(0);

    //RunLightUpdates 推进光照更新队列
    //budget 大于 0 时本轮最多处理这么多条 剩下的留到下一轮
    //原版光照每次 tick 只跑有限任务 这里同样分批 免得单次持锁从几毫秒变成几百毫秒
    public int RunLightUpdates(int budget)
    {
        foreach (var node in _blockNodesToCheck) CheckNode(node);
        _blockNodesToCheck.Clear();

        var count = PropagateDecreases(budget);
        var remaining = budget <= 0 ? 0 : Math.Max(0, budget - count);
        count += PropagateIncreases(remaining);

        //队列彻底跑空才收尾 中间批次提前收尾会把待移除的区段数据提前丢掉
        if (_decreaseQueue.Count == 0 && _increaseQueue.Count == 0)
        {
            ClearChunkCache();
            Storage.MarkNewInconsistencies();
            Storage.SwapSectionMap();
        }
        return count;
    }

    private int PropagateIncreases(int budget)
    {
        var count = 0;
        while (_increaseQueue.Count > 0 && (budget <= 0 || count < budget))
        {
            var fromNode = _increaseQueue.Dequeue();
            var increaseData = _increaseQueue.Dequeue();
            var fromLevel = Storage.GetStoredLevel(fromNode);
            var fromTargetLevel = QueueEntry.GetFromLevel(increaseData);
            if (QueueEntry.IsIncreaseFromEmission(increaseData) && fromLevel < fromTargetLevel)
            {
                Storage.SetStoredLevel(fromNode, fromTargetLevel);
                fromLevel = fromTargetLevel;
            }
            if (fromLevel == fromTargetLevel) PropagateIncrease(fromNode, increaseData, fromLevel);
            count++;
        }
        return count;
    }

    private int PropagateDecreases(int budget)
    {
        var count = 0;
        while (_decreaseQueue.Count > 0 && (budget <= 0 || count < budget))
        {
            var fromNode = _decreaseQueue.Dequeue();
            var decreaseData = _decreaseQueue.Dequeue();
            PropagateDecrease(fromNode, decreaseData);
            count++;
        }
        return count;
    }

    protected void EnqueueDecrease(long fromNode, long decreaseData)
    {
        _decreaseQueue.Enqueue(fromNode);
        _decreaseQueue.Enqueue(decreaseData);
    }

    protected void EnqueueIncrease(long fromNode, long increaseData)
    {
        _increaseQueue.Enqueue(fromNode);
        _increaseQueue.Enqueue(increaseData);
    }

    public bool HasLightWork()
        => Storage.HasInconsistencies || _blockNodesToCheck.Count > 0
           || _decreaseQueue.Count > 0 || _increaseQueue.Count > 0;

    public DataLayer? GetDataLayerData(SectionPos pos) => Storage.GetDataLayerData(pos.AsLong());

    public int GetLightValue(BlockPos pos) => Storage.GetLightValue(pos.AsLong());

    public string GetDebugData(long sectionNode) => GetDebugSectionType(sectionNode).ToString();

    public SectionType GetDebugSectionType(long sectionNode) => Storage.GetDebugSectionType(sectionNode);

    //QueueEntry 队列打包格式对应原版 QueueEntry
    //低 4 位是来源层级 第 4..9 位是六方向掩码 第 10/11 位分别是空形状与发光来源标志
    public static class QueueEntry
    {
        private const int FromLevelBits = 4;
        private const int DirectionBits = 6;
        private const long LevelMask = 15;
        private const long DirectionsMask = 1008;
        private const long FlagFromEmptyShape = 1024;
        private const long FlagIncreaseFromEmission = 2048;

        public static long DecreaseSkipOneDirection(int oldFromLevel, Direction skipDirection)
            => WithLevel(WithoutDirection(DirectionsMask, skipDirection), oldFromLevel);

        public static long DecreaseAllDirections(int oldFromLevel) => WithLevel(DirectionsMask, oldFromLevel);

        public static long IncreaseLightFromEmission(int newFromLevel, bool fromEmptyShape)
        {
            var increaseData = DirectionsMask | FlagIncreaseFromEmission;
            if (fromEmptyShape) increaseData |= FlagFromEmptyShape;
            return WithLevel(increaseData, newFromLevel);
        }

        public static long IncreaseSkipOneDirection(int newFromLevel, bool fromEmptyShape, Direction skipDirection)
        {
            var increaseData = WithoutDirection(DirectionsMask, skipDirection);
            if (fromEmptyShape) increaseData |= FlagFromEmptyShape;
            return WithLevel(increaseData, newFromLevel);
        }

        public static long IncreaseOnlyOneDirection(int newFromLevel, bool fromEmptyShape, Direction direction)
        {
            long increaseData = 0;
            if (fromEmptyShape) increaseData |= FlagFromEmptyShape;
            return WithLevel(WithDirection(increaseData, direction), newFromLevel);
        }

        public static long IncreaseSkySourceInDirections(bool down, bool north, bool south, bool west, bool east)
        {
            var increaseData = WithLevel(0L, MaxLevel);
            if (down) increaseData = WithDirection(increaseData, Direction.Down);
            if (north) increaseData = WithDirection(increaseData, Direction.North);
            if (south) increaseData = WithDirection(increaseData, Direction.South);
            if (west) increaseData = WithDirection(increaseData, Direction.West);
            if (east) increaseData = WithDirection(increaseData, Direction.East);
            return increaseData;
        }

        public static int GetFromLevel(long entry) => (int)(entry & LevelMask);

        public static bool IsFromEmptyShape(long entry) => (entry & FlagFromEmptyShape) != 0;

        public static bool IsIncreaseFromEmission(long entry) => (entry & FlagIncreaseFromEmission) != 0;

        public static bool ShouldPropagateInDirection(long entry, Direction direction)
            => (entry & (1L << (direction.Id3D + FromLevelBits))) != 0;

        private static long WithLevel(long entry, int level) => (entry & -16) | (level & LevelMask);

        private static long WithDirection(long entry, Direction direction)
            => entry | (1L << (direction.Id3D + FromLevelBits));

        private static long WithoutDirection(long entry, Direction direction)
            => entry & ((1L << (direction.Id3D + FromLevelBits)) ^ -1);
    }
}
