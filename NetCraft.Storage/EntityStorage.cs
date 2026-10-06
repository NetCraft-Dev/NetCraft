using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Thread;

namespace NetCraft.Storage;

//Entity storage, maps to vanilla net.minecraft.world.level.chunk.storage.EntityStorage
//Implements EntityPersistentStorage, reading and writing entity collections per chunk, relying on SimpleRegionStorage
//Entity serialization strategy is injected as delegates, avoiding dependencies on concrete Entity subclasses
public sealed class EntityStorage : EntityPersistentStorage<Entity>
{
    private const string EntitiesTag = "Entities";

    private readonly SimpleRegionStorage _simpleRegionStorage;
    //emptyChunks caches known-empty chunks to avoid repeated IO reads
    //Must be concurrency-safe: chunk entity reads are dispatched to the thread pool by PersistentServerLevel.LoadChunkEntities
    //Several adjacent chunks arrive at once; a plain HashSet would corrupt its internal state under concurrent access
    //Vanilla serializes access through entityDeserializerQueue; this project's read path is not wired to that queue yet
    private readonly ConcurrentDictionary<long, byte> _emptyChunks = new();
    //entityDeserializerQueue, the entity deserialization queue ensuring single-threaded serialization
    private readonly ConsecutiveExecutor _entityDeserializerQueue;
    //registryAccess, the registry access entry point used by TagValueInput lookups
    private readonly RegistryAccess _registryAccess;
    //entityLoader deserializes an Entity from a CompoundTag, maps to vanilla EntityType.loadRecursive
    private readonly Func<CompoundTag, RegistryAccess, Entity> _entityLoader;
    //entitySaver serializes an Entity into a CompoundTag, maps to vanilla Entity.save
    private readonly Func<Entity, CompoundTag> _entitySaver;

    //Constructor taking the SimpleRegionStorage and the main-thread Executor
    //registryAccess is for TagValueInput lookups; entityLoader/entitySaver inject the concrete serialization strategy
    public EntityStorage(
        SimpleRegionStorage simpleRegionStorage,
        IExecutor mainThreadExecutor,
        RegistryAccess registryAccess,
        Func<CompoundTag, RegistryAccess, Entity?> entityLoader,
        Func<Entity, CompoundTag> entitySaver)
    {
        _simpleRegionStorage = simpleRegionStorage;
        _entityDeserializerQueue = new ConsecutiveExecutor(mainThreadExecutor, "entity-deserializer");
        _registryAccess = registryAccess;
        _entityLoader = entityLoader;
        _entitySaver = entitySaver;
    }

    //LoadEntities loads the entity collection by chunk pos, maps to vanilla loadEntities
    //Goes through SimpleRegionStorage.Read + TagValueInput + the injected entityLoader strategy
    public async Task<ChunkEntities<Entity>> LoadEntities(ChunkPos pos)
    {
        var packed = pos.Pack();
        if (_emptyChunks.ContainsKey(packed))
            return new ChunkEntities<Entity>(pos, new List<Entity>());

        var tagOptional = await _simpleRegionStorage.Read(pos);
        if (!tagOptional.IsPresent)
        {
            _emptyChunks[packed] = 0;
            return new ChunkEntities<Entity>(pos, new List<Entity>());
        }

        var chunkTag = tagOptional.Get();
        //Wrap the registry access with TagValueInput, maps to vanilla TagValueInput.create
        //Actual entity parsing uses the injected entityLoader rather than the Codec path, maps to vanilla EntityType.loadRecursive
        var input = TagValueInput.Create(_registryAccess, chunkTag);
        var entitiesList = input.ChildrenList(EntitiesTag);
        var entities = new List<Entity>();
        if (entitiesList is not null)
        {
            //Read the raw ListTag directly and parse each CompoundTag with the injected entityLoader
            var rawList = chunkTag.GetList(EntitiesTag);
            if (rawList is not null)
                foreach (var t in rawList)
                    if (t is CompoundTag entityTag && _entityLoader(entityTag, _registryAccess) is { } entity)
                        entities.Add(entity);
        }
        return new ChunkEntities<Entity>(pos, entities);
    }

    //StoreEntities stores the entity collection by chunk and marks empty chunks in emptyChunks, maps to vanilla storeEntities
    //Goes through the injected entitySaver strategy + SimpleRegionStorage.Write
    public void StoreEntities(ChunkEntities<Entity> chunk)
    {
        var pos = chunk.Pos;
        var packed = pos.Pack();
        if (chunk.IsEmpty())
        {
            _emptyChunks[packed] = 0;
            _simpleRegionStorage.Write(pos, () => null!);
            return;
        }

        var entitiesList = new ListTag();
        foreach (var entity in chunk.GetEntities())
        {
            var entityTag = _entitySaver(entity);
            entitiesList.Add(entityTag);
        }
        var chunkTag = new CompoundTag();
        chunkTag.Put(EntitiesTag, entitiesList);
        _simpleRegionStorage.Write(pos, chunkTag);
        _emptyChunks.TryRemove(packed, out _);
    }

    //Flush syncs the backing storage and runs entityDeserializerQueue.runAll
    public async Task Flush(bool flushStorage)
    {
        await _simpleRegionStorage.Synchronize(flushStorage).ConfigureAwait(false);
        _entityDeserializerQueue.RunAll();
    }

    public void Dispose()
    {
        _simpleRegionStorage.Dispose();
        _entityDeserializerQueue.Close();
    }
}
