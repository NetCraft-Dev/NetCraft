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

//PersistentServerLevel, persistent server level, maps to vanilla ServerLevel wired to RegionFileStorage
//Extends SimpleServerLevel and reuses the in-memory dictionary as a cache, implementing LevelHeightAccessor for Parse
//GetChunk delegates to ServerChunkCache for async scheduling on a miss, avoiding a synchronous wait in the main loop
//SaveChunk serializes a ChunkAccess into a CompoundTag and writes it to RegionFileStorage
//Stage 11.47 wires in Tick and the entity collection, aligning with the vanilla ServerLevel.tick scheduling skeleton
//Stage 11.48 wires in ServerChunkCache, replacing the synchronous wait of LoadChunkAsync
public sealed class PersistentServerLevel : SimpleServerLevel, BlockGetter
{
    private readonly SimpleRegionStorage _regionStorage;
    private readonly PalettedContainerFactory _factory;
    private readonly ServerChunkCache _chunkSource;
    //EntityManager, entity lifecycle management with visible and standby states; add/remove during a tick goes through a queue
    private readonly EntityManager _entityManager;
    //_entityStorage, separate entity persistence storage injected by the Game layer; without it entities live only in memory
    private EntityStorage? _entityStorage;
    //_pendingEntityLoads, entities read back in the background, consumed by the main-thread tick
    //Entity reads are heavy IO; leaving them on the main thread would stall the chunk load callback for a whole tick
    private readonly ConcurrentQueue<ChunkEntities<NetCraft.Registry.Entity>> _pendingEntityLoads = new();
    private long _levelTick;

    public int MinSectionY { get; }
    public int SectionsCount { get; }
    public int MaxSectionY => MinSectionY + SectionsCount - 1;

    //Build height is derived from the section range; the base class defaults are only a fallback, the real range must be given here
    public override int MinBuildHeight => MinSectionY * 16;
    public override int MaxBuildHeight => (MaxSectionY + 1) * 16;

    public SimpleRegionStorage RegionStorage => _regionStorage;
    public PalettedContainerFactory Factory => _factory;

    //ChunkSource, the ServerChunkCache chunk source, for external diagnostics and player position updates
    public ServerChunkCache ChunkSource => _chunkSource;

    //SetChunkForced toggles force load, maps to vanilla ServerLevel.setChunkForced
    public bool SetChunkForced(int x, int z, bool add)
        => _chunkSource.UpdateChunkForced(new ChunkPos(x, z), add);

    //GetForceLoadedChunks, currently force-loaded chunks, maps to vanilla ServerLevel.getForceLoadedChunks
    public IReadOnlyCollection<long> GetForceLoadedChunks() => _chunkSource.GetForceLoadedChunks();

    //Entities, a read-only view of the level's visible entities, for external diagnostics
    public IEnumerable<NetCraft.Registry.Entity> Entities => _entityManager.Visible;

    //EntityLookup, the visible entity section index entry point so the business layer can take entities by AABB range
    public EntityLookup EntityLookup => _entityManager.VisibleLookup;

    //EntityManager, the entity manager, for external lookup by Uuid and entity persistence
    public EntityManager EntityManager => _entityManager;

    //EntityStorage, the entity persistence storage; null when not injected
    public EntityStorage? EntityStorage => _entityStorage;

    //EntityDeathCallback, the entity death callback injected by the Game layer to broadcast death effects and remove the entity
    public Action<NetCraft.Registry.Entity>? EntityDeathCallback { get; set; }

    //CollisionShapeProvider, entity shape collision queries injected by the Game layer; without it entities skip collision and only advance position
    //Collision shapes require block behavior, which is a Game-layer thing, so Storage can only take a delegate
    public Func<NetCraft.Registry.Entity, AABB, IReadOnlyList<VoxelShape>>? CollisionShapeProvider { get; set; }

    //LevelTick, the accumulated level tick count, used for diagnostics and write scheduling
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
        //The loader delegates to LoadChunkAsync and is scheduled asynchronously by ServerChunkCache, avoiding a circular dependency
        //The async lambda lets Task<LevelChunk?> convert implicitly to Task<ChunkAccess?> since Task is not covariant
        //generator is passed by the Game layer and runs the ChunkStatusProcessor generation chain to create a new chunk on a save miss
        _chunkSource = new ServerChunkCache(async pos => await LoadChunkAsync(pos), viewDistance, generator);
        //Simulation distance decides the range that actually ticks; when view distance is larger, the ring between them is the load-but-do-not-tick weak band
        //Uses the same vanilla clamp range as view distance
        _chunkSource.SimulationDistance = Math.Clamp(simulationDistance, 3, 32);
        //An entity ticks as soon as it is added and goes standby on chunk unload; the chunk load completion callback turns that chunk's standby entities back to tickable
        _entityManager = new EntityManager();
        _chunkSource.ChunkLoaded = OnChunkLoaded;
        _chunkSource.ChunkUnloaded = OnChunkUnloaded;
        _chunkSource.ChunkSaveSink = SaveChunkOnUnload;
    }

    //OnChunkLoaded, wiring after chunk load completion; read entities and register the scheduled tick container
    private void OnChunkLoaded(ChunkPos pos)
    {
        LoadChunkEntities(pos);
        EnsureChunkTicksRegistered(pos);
    }

    //OnChunkUnloaded, wiring after chunk unload; block entities leave memory with the chunk
    //Entities only stop ticking without removing their index; the vanilla semantics are to restore them when the chunk reloads
    //The scheduled tick container and the in-memory copy must be detached too, or reloading the chunk would double-register and get the stale unloaded object
    private void OnChunkUnloaded(ChunkPos pos)
    {
        BlockEntityBridge?.Unload(pos);
        UnregisterChunkTicks(pos);
        RemoveChunk(pos);
    }

    //UnloadChunk explicitly unloads a chunk; the caller must write changes first, maps to the chunk unload of vanilla ChunkMap
    public bool UnloadChunk(ChunkPos pos) => _chunkSource.UnloadChunk(pos);

    //AttachEntityStorage injects entity persistence storage, mounted by the Game layer after the server is built
    public void AttachEntityStorage(EntityStorage storage) => _entityStorage = storage;

    //LoadChunkEntities reads the chunk's entities into management, maps to the chunk load branch of vanilla PersistentEntitySectionManager
    //Triggered by the ServerChunkCache chunk load completion callback, which runs in the main-thread tick
    //Reads go to the background and results are queued for the next tick: EntityManager's indexes are plain dictionaries and only the main thread may modify them
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
                //A read failure must not be silent, or that chunk's entities are permanently missing
                Log.Warning($"Entity read failed {pos}: {e.Message}");
            }
        });
    }

    //DrainPendingEntityLoads consumes background-read entities on the main thread, maps to vanilla PersistentEntitySectionManager.processPendingLoads
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

    //SaveAllEntitiesAsync writes each chunk's entities to disk, maps to entity persistence on chunk unload and shutdown in vanilla
    //Builds ChunkEntities per chunk first then flushes them together; on shutdown force=true ensures completion
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

    //LoadChunkAsync asynchronously loads from RegionFileStorage and deserializes, maps to the vanilla chunk load path
    //A miss returns null; a deserialization failure throws ChunkReadException
    //ServerChunkCache calls this through the loader callback
    public async Task<LevelChunk?> LoadChunkAsync(ChunkPos pos)
    {
        Log.Debug($"LoadChunkAsync entry pos={pos}");
        var optional = await _regionStorage.Read(pos).ConfigureAwait(false);
        if (!optional.IsPresent)
        {
            //Log.Debug($"LoadChunkAsync exit result=null save miss");
            return null;
        }
        var tag = optional.Get();
        var data = SerializableChunkData.Parse(this, _factory, tag);
        if (data is null)
        {
            //Log.Debug($"LoadChunkAsync exit result=null parse failed");
            return null;
        }
        var result = data.Read(this, SimplePoiManager.Empty, null, pos);
        //Log.Debug($"LoadChunkAsync exit result={(result is null ? "null" : result.Pos.ToString())}");
        return result;
    }

    //GetChunk prefers the in-memory cache and delegates to ServerChunkCache async scheduling on a miss
    //require=false does not block the main loop; not ready returns null and the caller retries next tick
    public override ChunkAccess? GetChunk(ChunkPos pos)
    {
        var cached = base.GetChunk(pos);
        if (cached is not null) return cached;
        return _chunkSource.GetChunk(pos.X, pos.Z, ChunkStatus.FULL, false);
    }

    //GetLoadedChunk takes only chunks already in memory without triggering a load
    //Per-tick entity bounding box queries use it so a position bordering an unloaded chunk does not pull up a chunk with no ticket
    public override ChunkAccess? GetLoadedChunk(ChunkPos pos) => _chunkSource.GetLoadedChunk(pos.X, pos.Z);

    //GetChunkSync gets a chunk synchronously; require=true triggers a load, only for tests or scenarios that must be synchronous
    public ChunkAccess? GetChunkSync(ChunkPos pos, bool require = true)
        => _chunkSource.GetChunk(pos.X, pos.Z, ChunkStatus.FULL, require);

    //IsChunkFailed, whether the chunk is confirmed to have failed loading, so the sender drops permanently failed entries
    public bool IsChunkFailed(ChunkPos pos) => _chunkSource.IsChunkFailed(pos.X, pos.Z);

    //WriteBlockState writes only the state and heightmap; light, neighbor and other interactions are triggered in order by ServerLevel.SetBlock
    protected override BlockState? WriteBlockState(BlockPos pos, BlockState state)
    {
        var chunk = GetChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        var section = chunk?.GetSection(pos.Y >> 4);
        var previous = section?.SetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15, state);
        if (previous is not null && chunk is not null)
            chunk.UpdateHeightmaps(pos.X, pos.Y, pos.Z, state);
        return previous;
    }

    //UpdateLight recomputes the position's light after a block state change, sharing a lock with ProcessLight to serialize the engine
    public override void UpdateLight(BlockPos pos) => _chunkSource.UpdateLight(pos);

    //GetLightValue reads the given light layer's value at the position, maps to vanilla Level.getBrightness
    //Reads the published snapshot, so it takes no lock and never waits on a chunk generation thread
    public override int GetLightValue(NetCraft.Registry.LightLayer layer, BlockPos pos)
        => _chunkSource.LightEngine.GetLayerListener(layer).GetLightValue(pos);

    //UpdateLightBatch recomputes light after a batch of block changes; all positions are marked then one propagation round runs
    public void UpdateLightBatch(IReadOnlyList<BlockPos> positions) => _chunkSource.UpdateLightBatch(positions);

    //TickLight advances and dispatches light once at the end of each tick, called by the main loop after block events
    public void TickLight() => _chunkSource.TickLight();

    //IsPositionTicking, whether the chunk is within block-ticking range, maps to vanilla shouldTickBlocksAt
    //Scheduled ticks and block entity updates filter on it: chunks beyond simulation distance stay loaded but do not advance
    //The vanilla chain is shouldTickBlocksAt -> inBlockTickingRange -> simulation level <= 32
    protected override bool IsPositionTicking(long chunkKey) => _chunkSource.InBlockTickingRange(chunkKey);

    //GetBlockState reads a block by world coords, treating unloaded as air, maps to vanilla BlockGetter.getBlockState
    //Attachment face tests need the real world: shapes like moving pistons are provided by block entities and an empty world view would judge them shapeless
    public BlockState GetBlockState(int x, int y, int z)
    {
        var chunk = GetChunk(new ChunkPos(x >> 4, z >> 4));
        return chunk is null ? default : chunk.GetBlockState(x, y, z);
    }

    //LightUpdateSink, the light change dispatch callback injected by the Game layer; takes the chunk pos and the affected section indices for both layers
    public Action<ChunkPos, IReadOnlyList<int>, IReadOnlyList<int>>? LightUpdateSink
    {
        get => _chunkSource.LightUpdateSink;
        set => _chunkSource.LightUpdateSink = value;
    }

    //SaveChunkAsync serializes a ChunkAccess and writes it to RegionFileStorage
    //Uses PersistentServerLevel.Factory to keep the codec consistent with the registered Block/Biome
    public async Task SaveChunkAsync(ChunkAccess chunk)
    {
        Log.Debug($"SaveChunkAsync entry chunk={chunk.Pos}");
        var data = SerializableChunkData.CopyOf(this, chunk, _factory);
        var tag = data.Write();
        //ConfigureAwait(false) is required; the caller may be a UI thread with a synchronization context, e.g. a GUI command box running save-all
        //Without breaking context the continuation would be queued back to the UI thread, which is blocking on this Task, deadlocking in a mutual wait
        await _regionStorage.Write(chunk.Pos, tag).ConfigureAwait(false);
        AddChunk(chunk);
        //Log.Debug($"SaveChunkAsync exit");
    }

    //SaveChunkOnUnload writes before a chunk leaves memory; the main thread only snapshots and serialization plus writing go to background IO threads
    //Unlike SaveChunkAsync it does not put the chunk back in the in-memory copy; an unload is meant to let it leave memory
    //The snapshot must be taken on the main thread; block entities are collected into NBT here because the unload callback clears them right after
    public void SaveChunkOnUnload(ChunkAccess chunk)
    {
        var data = SerializableChunkData.CopyOf(this, chunk, _factory);
        //The supplier form enqueues on the main thread; later reads of the same chunk hit this pending write instead of stale pre-write data
        _ = _regionStorage.Write(data.ChunkPos, data.Write);
    }

    //Synchronize flushes to disk, maps to the vanilla chunk save stage
    public Task SynchronizeAsync(bool flush)
        => _regionStorage.Synchronize(flush);

    //SaveAllChunksAsync writes all loaded chunks in the chunk source, maps to vanilla saveAllChunks
    //After generation chunks live only in ServerChunkCache memory; without actively saving, the disk stays empty
    public async Task SaveAllChunksAsync()
    {
        var snapshots = SnapshotAllChunks();
        await WriteSnapshotsAsync(snapshots).ConfigureAwait(false);
    }

    //SnapshotAllChunks collects snapshots of all loaded chunks on the main thread for background writes
    //CopyOf runs on the main thread, avoiding a data race between background section copies and main-thread chunk writes
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

    //WriteSnapshotsAsync serializes NBT and writes the region in the background without blocking the main thread
    //NBT serialization and disk IO are the bulk of the flush cost, matching the worker side of the vanilla savingExecutor
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

    //GetNextEntityId allocates the next available entity id, maps to vanilla ServerLevel.getNextEntityId
    //The occupancy check is left to the entity manager; loaded entities (including standby after unload) count as taken
    public override int GetNextEntityId()
        => NetCraft.Registry.Entity.NextEntityId(_entityManager.HasEntityWithId);

    //AddEntity adds an entity to the level, maps to vanilla Level.addFreshEntity
    //Also injects block collision queries and the level reference; a duplicate Uuid is rejected
    //When its chunk is not loaded the entity starts standby and joins ticking and tracking once the chunk loads
    public bool AddEntity(NetCraft.Registry.Entity entity)
    {
        AttachEntity(entity);
        return _entityManager.AddEntity(entity);
    }

    //AttachEntity backfills the level reference and collision queries and hooks up the death callback
    //The death callback is hooked up only here, so a death from any source (disk load/summon) is noticed by the server
    private void AttachEntity(NetCraft.Registry.Entity entity)
    {
        entity.Level = this;
        BindCollisionShapes(entity);
        if (EntityDeathCallback is null) return;
        //On re-adding the same entity, detach then attach to avoid the callback running multiple times
        entity.Died -= EntityDeathCallback;
        entity.Died += EntityDeathCallback;
    }

    //BindCollisionShapes binds the shape collision query to the entity, staying null when no delegate is injected
    //The delegate queries per entity because collision shapes depend on the entity: slab orientation and scaffolding fall tolerance both depend on it
    private void BindCollisionShapes(NetCraft.Registry.Entity entity)
    {
        if (CollisionShapeProvider is not { } provider) return;
        entity.CollisionShapes = box => provider(entity, box);
    }

    //RemoveEntity removes a level entity, returns whether it succeeded
    public bool RemoveEntity(NetCraft.Registry.Entity entity)
        => _entityManager.RemoveEntity(entity);

    //Tick, per-frame level scheduling, maps to vanilla ServerLevel.tick
    //1. tick ChunkSource to advance chunk scheduling; finished chunks move into the cache and trigger the entity load callback
    //2. tick the entity manager to advance visible entities and handle cross-chunk moves
    //3. increment levelTick for write scheduling
    //When runsNormally is false the world advance halts while chunk scheduling and entity management keep running, matching vanilla freezing only filtering entities without stopping traversal
    public void Tick(bool runsNormally = true)
    {
        //Log.Debug($"Tick entry levelTick={_levelTick}");
        _chunkSource.Tick();
        //Chunk scheduling just triggered entity reads; completed ones land here so entities added this tick still catch the entity tick
        DrainPendingEntityLoads();
        //Chunks retrieved directly through GetChunk without the load callback get their scheduled tick container registered here
        foreach (var chunk in _chunkSource.LoadedChunks) EnsureChunkTicksRegistered(chunk);
        //Entity advance filters by simulation distance; chunk entities beyond it stay standby in the index
        _entityManager.Tick(!runsNormally, _chunkSource.InEntityTickingRange);
        if (runsNormally) _levelTick++;
        //Log.Debug($"Tick exit levelTick={_levelTick}");
    }

    //EntityBoxes includes the injected player bounding boxes in addition to the entity manager's entities for the entity-inside test
    protected override IEnumerable<AABB> EntityBoxes()
    {
        foreach (var entity in _entityManager.Visible) yield return entity.BoundingBox;
        if (ExtraEntityBoxes is null) yield break;
        foreach (var box in ExtraEntityBoxes()) yield return box;
    }

    //CountLevelEntitiesInBox takes candidates from the spatial index then filters by AABB intersection; players are counted separately by the base
    protected override int CountLevelEntitiesInBox(AABB box)
    {
        var count = 0;
        foreach (var entity in _entityManager.VisibleLookup.GetInRange(box.Min, box.Max))
            if (box.Intersects(entity.BoundingBox)) count++;
        return count;
    }

    //LevelEntitiesInBox uses the same index as CountLevelEntitiesInBox but hands out the entities themselves
    protected override IEnumerable<NetCraft.Registry.Entity> LevelEntitiesInBox(AABB box)
    {
        foreach (var entity in _entityManager.VisibleLookup.GetInRange(box.Min, box.Max))
            if (box.Intersects(entity.BoundingBox)) yield return entity;
    }
}
