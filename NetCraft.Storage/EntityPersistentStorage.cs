namespace NetCraft.Storage;

using System.Threading.Tasks;
using NetCraft.Primitives;

//实体持久化存储接口对应原版net.minecraft.world.level.entity.EntityPersistentStorage
//按chunk加载与存储实体T为实体类型继承IDisposable对齐原版AutoCloseable
public interface EntityPersistentStorage<T> : IDisposable
{
    //loadEntities按chunk位置加载实体集合返回异步Future
    Task<ChunkEntities<T>> LoadEntities(ChunkPos pos);

    //storeEntities按chunk存储实体集合
    void StoreEntities(ChunkEntities<T> chunk);

    //flush刷新存储flushStorage是否同步底层存储
    Task Flush(bool flushStorage);

    //close关闭释放资源
    void Dispose();
}
