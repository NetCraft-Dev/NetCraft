namespace NetCraft.Storage;

using System.Threading.Tasks;
using NetCraft.Primitives;

//Entity persistent storage interface, maps to vanilla net.minecraft.world.level.entity.EntityPersistentStorage
//Loads and stores entities per chunk; T is the entity type and it extends IDisposable, aligning with vanilla AutoCloseable
public interface EntityPersistentStorage<T> : IDisposable
{
    //loadEntities loads the entity collection by chunk pos and returns an async Future
    Task<ChunkEntities<T>> LoadEntities(ChunkPos pos);

    //storeEntities stores the entity collection by chunk
    void StoreEntities(ChunkEntities<T> chunk);

    //flush flushes storage; flushStorage controls whether the backing storage is synced
    Task Flush(bool flushStorage);

    //close closes and releases resources
    void Dispose();
}
