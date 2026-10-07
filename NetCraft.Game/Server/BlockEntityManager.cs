using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//BlockEntityManager block entity collection, maps to the blockEntities container inside vanilla LevelChunk
//Indexed by BlockPos it provides add/remove/lookup and a per-frame tick; vanilla hangs the container on the chunk, this holds it per level for simplicity
//Chunk deserialization runs on thread pool threads (ServerChunkCache.LoadAsync) and races with reads/writes to this container
//Vanilla does this step on the main thread; this project moved disk reads to the background, so a lock guards the container
public sealed class BlockEntityManager
{
    private readonly Dictionary<long, BlockEntity> _entities = new();

    //Insertion order; vanilla ticks block entities in insertion order and blockEntityTickers is a sequential list
    //The dictionary's own iteration order drifts with insertions/removals, and so does the finishing order of multiple block entities in one tick
    //When a piston retracts, the base and the two cells in front finish in the same tick; the base must finish first to see "the signal is gone"
    //Reversed, the moment the base finishes and falls back to redstone the piston reads it and extends again, showing as a self-excited loop that never stops
    private readonly Dictionary<long, long> _orders = new();
    private long _nextOrder;

    //_entities is a plain dictionary; concurrent writes corrupt the internal array and throw a bogus out-of-range, so both entry and exit hold the lock
    private readonly object _lock = new();

    public int Count
    {
        get
        {
            lock (_lock) return _entities.Count;
        }
    }

    //Entities takes a snapshot of all block entities; returning Values directly would let callers hit concurrent writes while iterating
    public IEnumerable<BlockEntity> Entities
    {
        get
        {
            lock (_lock) return _entities.Values.ToArray();
        }
    }

    //Add registers a block entity and injects the level; a duplicate at the same position overrides
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

    //InChunk takes the block entities in the given chunk; save collection and chunk packet dispatch take them per chunk
    //Take a snapshot first then filter; the lock cannot stay in an iterator across yield
    public IEnumerable<BlockEntity> InChunk(ChunkPos pos)
    {
        List<BlockEntity> snapshot;
        lock (_lock) snapshot = _entities.Values.ToList();
        foreach (var entity in snapshot)
            if (InChunk(entity.Pos, pos)) yield return entity;
    }

    //RemoveInChunk removes all block entities in the given chunk and returns the count
    //Called on chunk unload, maps to vanilla chunk unload carrying away its block entities
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

    //Tick advances all block entities; take a snapshot first so additions/removals during tick do not modify the collection
    //The entity's own tick runs outside the lock; it may modify this container, so the lock only protects the snapshot step
    public void Tick()
    {
        List<BlockEntity> snapshot;
        lock (_lock)
            //Sorted by insertion order, consistent with the iteration order of vanilla blockEntityTickers
            snapshot = _entities
                .OrderBy(pair => _orders.TryGetValue(pair.Key, out var order) ? order : long.MaxValue)
                .Select(pair => pair.Value)
                .ToList();
        foreach (var entity in snapshot) entity.Tick();
    }
}
