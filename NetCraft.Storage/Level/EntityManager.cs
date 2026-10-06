using NetCraft.Primitives;
using NetCraft.Util;
using NetCraftEntity = NetCraft.Registry.Entity;

namespace NetCraft.Storage;

//EntityManager, entity lifecycle management, maps to vanilla net.minecraft.world.level.entity.PersistentEntitySectionManager
//Entities belong to their chunk and have two states:
//visible (ticks and is network-tracked) / standby (leaves ticking after chunk unload, stays in the index until the next load)
//Being added counts as visible, maps to vanilla addNewEntity entering visibleEntityStorage unconditionally and being corrected later by chunk events
//Add/remove during a tick goes through a deferred queue, avoiding collection changes mid-iteration, maps to active/pending in the vanilla EntityTickList
//After an entity moves across chunks, refresh its chunk ownership and spatial index; otherwise writes go to the wrong file and queries miss it
public sealed class EntityManager
{
    //Spatial index of visible entities, for AABB queries and tracking traversal
    private readonly EntityLookup _visible = new();
    //chunkPacked -> all entities under that chunk (including standby)
    private readonly Dictionary<long, List<NetCraftEntity>> _byChunk = new();
    //entity -> its current chunk, for re-bucketing after a move
    private readonly Dictionary<NetCraftEntity, long> _entityChunk = new(ReferenceEqualityComparer.Instance);
    //entity -> its current section; the spatial index is bucketed by section and must be refreshed together
    private readonly Dictionary<NetCraftEntity, long> _entitySection = new(ReferenceEqualityComparer.Instance);
    //knownUuids, managed entities deduplicated by Uuid, maps to vanilla knownUuids
    private readonly Dictionary<Guid, NetCraftEntity> _knownUuids = new();
    //byEntityId, index from network id to entity; attack/interact packets carry only an entityId and must resolve the target through it
    private readonly Dictionary<int, NetCraftEntity> _byEntityId = new();
    //ticking, entities taking part in ticking this round
    private readonly List<NetCraftEntity> _ticking = new();
    private readonly List<NetCraftEntity> _pendingAdd = new();
    private readonly List<NetCraftEntity> _pendingRemove = new();
    private bool _processing;

    //Count, total managed entities (including standby)
    public int Count => _knownUuids.Count;

    //Visible, the set of visible entities, for network tracking traversal
    public IEnumerable<NetCraftEntity> Visible => _visible.GetAll();

    //VisibleLookup, the visible entity spatial index, for AABB range queries
    public EntityLookup VisibleLookup => _visible;

    //AddEntity takes in an entity, maps to vanilla addNewEntity
    //A duplicate Uuid is rejected outright; adding starts ticking and tracking, and OnChunkUnloaded turns it to standby after chunk unload
    //Entities without an id get one here, maps to level.getNextEntityId in the vanilla constructor; registering immediately is what claims it
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

    //RemoveEntity removes an entity, maps to vanilla removeEntity
    //During a tick it is only enqueued, and actually detached after the tick ends
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

    //OnChunkLoaded: on chunk load completion, standby entities under that chunk become visible and tickable
    public void OnChunkLoaded(ChunkPos pos)
    {
        if (!_byChunk.TryGetValue(pos.Pack(), out var list)) return;
        for (var i = 0; i < list.Count; i++)
            StartTicking(list[i]);
    }

    //OnChunkUnloaded: on chunk unload, entities under that chunk leave ticking and tracking but stay in the index for the next load
    public void OnChunkUnloaded(ChunkPos pos)
    {
        if (!_byChunk.TryGetValue(pos.Pack(), out var list)) return;
        for (var i = 0; i < list.Count; i++)
            StopTicking(list[i]);
    }

    //Tick advances all visible entities, maps to vanilla EntityTickList.forEach
    //Order: tick entities -> collect self-requested removals -> apply this round's add/remove -> refresh moved entities' ownership and spatial index
    //When frozen is true entities stay put, matching the vanilla isEntityFrozen hit (players are not in this set and are scheduled separately by PlayerList)
    //Traversal and index refresh still run so newly added entities can be tracked and synced; falling out of tracking unseen would really look like "no response"
    //When entityTicking is true the chunk's entities advance, maps to the inEntityTickingRange filter in vanilla ServerLevel.tick
    //Chunk entities beyond simulation distance stay standby in the index; when not supplied, all advance
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
        //Entities requesting their own removal (empty stack/timeout/pickup) join the deferred queue; _ticking is not modified during iteration
        for (var i = 0; i < _ticking.Count; i++)
        {
            var entity = _ticking[i];
            if (entity.IsRemoved && !_pendingRemove.Contains(entity)) _pendingRemove.Add(entity);
        }
        ApplyPending();
        for (var i = 0; i < _ticking.Count; i++)
            UpdatePlacement(_ticking[i]);
    }

    //GetEntitiesInChunk gets all entities under the given chunk (including standby), for entity writes
    public IReadOnlyList<NetCraftEntity> GetEntitiesInChunk(ChunkPos pos)
        => _byChunk.TryGetValue(pos.Pack(), out var list) ? list : Array.Empty<NetCraftEntity>();

    //LoadedChunks, chunk positions currently holding entities, for entity write traversal
    //Previously Select+ToList materialized a list on every call; since writes use it every round, it is now lazy
    public IEnumerable<ChunkPos> LoadedChunks
    {
        get
        {
            foreach (var key in _byChunk.Keys) yield return ChunkPos.Unpack(key);
        }
    }

    //GetByUuid looks up an entity by Uuid, maps to vanilla getEntity(uuid)
    public NetCraftEntity? GetByUuid(Guid uuid)
        => _knownUuids.TryGetValue(uuid, out var entity) ? entity : null;

    //GetByEntityId looks up an entity by network id, maps to vanilla getEntity(int id)
    //Attack and interact packets carry only an entityId; a miss is dropped as an invalid target
    public NetCraftEntity? GetByEntityId(int entityId)
        => _byEntityId.TryGetValue(entityId, out var entity) ? entity : null;

    //HasEntityWithId, whether the network id is taken, maps to vanilla ChunkMap.hasEntityWithId
    //Used for id-allocation checks; an unloaded entity still in the index also counts as taken
    public bool HasEntityWithId(int entityId) => _byEntityId.ContainsKey(entityId);

    //Clear clears all entities and indexes
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

    //StartTicking puts an entity into the visible, tickable set; during a tick it is enqueued and takes effect after the round
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

    //StopTicking removes an entity from the visible, tickable set
    private void StopTicking(NetCraftEntity entity)
    {
        _ticking.Remove(entity);
        _visible.Remove(entity);
        _entitySection.Remove(entity);
        _pendingAdd.Remove(entity);
    }

    //RemoveNow detaches the entity and all its indexes immediately
    private bool RemoveNow(NetCraftEntity entity)
    {
        if (!_knownUuids.Remove(entity.Uuid)) return false;
        _byEntityId.Remove(entity.EntityId);
        if (_entityChunk.TryGetValue(entity, out var chunk)) RemoveFromChunkIndex(entity, chunk);
        StopTicking(entity);
        _pendingRemove.Remove(entity);
        return true;
    }

    //ApplyPending applies add/remove accumulated during the tick
    private void ApplyPending()
    {
        if (_pendingRemove.Count > 0)
        {
            //RemoveNow also cleans that queue; snapshot first and clear to avoid collection changes mid-iteration
            var removals = _pendingRemove.ToArray();
            _pendingRemove.Clear();
            foreach (var entity in removals) RemoveNow(entity);
        }
        for (var i = 0; i < _pendingAdd.Count; i++)
        {
            var entity = _pendingAdd[i];
            //Entities removed during the tick do not enter the visible set
            if (_knownUuids.ContainsKey(entity.Uuid)) StartTicking(entity);
        }
        _pendingAdd.Clear();
    }

    //UpdatePlacement syncs chunk ownership and the spatial index after an entity moves
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
