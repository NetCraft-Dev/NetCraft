using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//LightEngine, light engine base class, maps to vanilla net.minecraft.world.level.lighting.LightEngine
//The propagation mechanism is embedded here, with three abstract methods checkNode/propagateIncrease/propagateDecrease landing it on the concrete layer
//Each pending entry enqueues two longs in order (the node + the packed level and direction), forming the decrease/increase FIFOs
public abstract class LightEngine<TSelf, TStorage> : LayerLightEventListener
    where TSelf : DataLayerStorageMap<TSelf>
    where TStorage : LayerLightSectionStorage<TSelf>
{
    public const int MaxLevel = 15;
    protected const int MinOpacity = 1;

    private const int MinQueueSize = 512;
    //ChunkCacheMask, the coordinate mask of the chunk cache, caching 8x8 chunks
    //Light BFS bounces between adjacent chunks; vanilla's two-entry LRU misses on almost every step once terrain interleaves
    //Here it maps directly by coordinate mask, one slot per coordinate, which is exactly the adjacent chunk and also drops the LRU shuffling
    private const int ChunkCacheMask = 7;
    private const int ChunkCacheSize = ChunkCacheMask + 1;

    protected readonly LightChunkGetter ChunkSource;

    //storage is also accessed by LevelLightEngine besides subclasses, so it is opened to the same assembly
    protected internal readonly TStorage Storage;

    protected static readonly long PullLightInEntry = QueueEntry.DecreaseAllDirections(1);
    protected static readonly Direction[] PropagationDirections = Direction.Values;

    private readonly HashSet<long> _blockNodesToCheck = new(MinQueueSize);
    private readonly Queue<long> _decreaseQueue = new();
    private readonly Queue<long> _increaseQueue = new();
    private readonly long[] _lastChunkPos = new long[ChunkCacheSize * ChunkCacheSize];
    private readonly LightChunk?[] _lastChunk = new LightChunk?[ChunkCacheSize * ChunkCacheSize];

    //checkNode re-evaluates the light source of a single block node
    protected abstract void CheckNode(long blockNode);

    //propagateIncrease raises light across the six neighbors
    protected abstract void PropagateIncrease(long fromNode, long increaseData, int fromLevel);

    //propagateDecrease lowers light across the six neighbors
    protected abstract void PropagateDecrease(long fromNode, long decreaseData);

    protected LightEngine(LightChunkGetter chunkSource, TStorage storage)
    {
        ChunkSource = chunkSource;
        Storage = storage;
        ClearChunkCache();
    }

    //hasDifferentLightProperties, whether a block change affects lighting
    //Vanilla also compares useShapeForLightOcclusion; not compared since the shape system is not implemented
    public static bool HasDifferentLightProperties(BlockState oldState, BlockState newState)
    {
        if (newState == oldState) return false;
        return newState.GetLightDampening() != oldState.GetLightDampening()
               || newState.GetLightEmission() != oldState.GetLightEmission();
    }

    //IsEmptyShape, whether the state takes part in occlusion comparison as an empty shape, maps to vanilla isEmptyShape
    //Only blocks that both occlude light and declare shape-based occlusion are not treated as an empty shape
    protected static bool IsEmptyShape(BlockState? state)
        => state is not { } value || !value.Owner.CanOcclude || !value.Owner.UseShapeForLightOcclusion;

    //GetFaceOcclusionShape, the shape used for occlusion comparison on a face, maps to vanilla getOcclusionShape
    private static VoxelShape GetFaceOcclusionShape(BlockState? state, Direction direction)
    {
        if (state is not { } value || IsEmptyShape(value)) return Shapes.Empty();
        return value.Owner.GetOcclusionShape(value).GetFaceShape(direction);
    }

    //ShapeOccludes, whether two touching faces together occlude that direction, maps to vanilla shapeOccludes
    //When occluded, propagation stops in that direction; this is key to no light leaking between non-full blocks
    protected static bool ShapeOccludes(BlockState? fromState, BlockState? toState, Direction direction)
        => Shapes.FaceShapeOccludes(GetFaceOcclusionShape(fromState, direction),
            GetFaceOcclusionShape(toState, direction.Opposite));

    //getState takes the block state by coords; returns null when the chunk is not loaded
    protected BlockState? GetState(int x, int y, int z)
    {
        var chunkX = SectionPos.BlockToSectionCoord(x);
        var chunkZ = SectionPos.BlockToSectionCoord(z);
        var chunk = GetChunk(chunkX, chunkZ);
        return chunk?.GetBlockState(x, y, z);
    }

    //getOpacity takes the block's light dampening; an unloaded position counts as fully opaque, matching vanilla using the bedrock default state
    protected int GetOpacity(BlockState? state)
        => state is null ? MaxLevel : Math.Max(MinOpacity, state.Value.GetLightDampening());

    //getChunk, cached chunk lookup; light propagation revisits the same chunks heavily
    //The slot is fixed by the coordinate low-bit mask, so adjacent chunks take their own slots and do not evict each other
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

    //propagateLightSources propagates all light sources in the chunk
    public abstract void PropagateLightSources(ChunkPos pos);

    //RunLightUpdates advances without a budget, draining the light queue fully, matching the default interface semantics
    public int RunLightUpdates() => RunLightUpdates(0);

    //RunLightUpdates advances the light update queue
    //When budget is above 0, at most that many are processed this round, the rest wait for the next
    //Vanilla lighting runs only a limited amount per tick; this batches the same way so a single lock hold does not go from milliseconds to hundreds of milliseconds
    public int RunLightUpdates(int budget)
    {
        foreach (var node in _blockNodesToCheck) CheckNode(node);
        _blockNodesToCheck.Clear();

        var count = PropagateDecreases(budget);
        var remaining = budget <= 0 ? 0 : Math.Max(0, budget - count);
        count += PropagateIncreases(remaining);

        //Only finish once the queue is fully drained; finishing mid-batch would drop pending-removal section data early
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

    //QueueEntry, the queue packing format, maps to vanilla QueueEntry
    //The low 4 bits are the source level, bits 4..9 are the six-direction mask, and bits 10/11 are the empty-shape and emission-source flags
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
