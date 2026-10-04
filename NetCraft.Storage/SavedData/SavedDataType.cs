using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//SavedDataType 持久化数据类型工厂对应原版 net.minecraft.world.level.storage.SavedDataType
//T 是具体 SavedData 子类 create 从 CompoundTag 反序列化实例
public interface SavedDataType<T> where T : SavedData
{
    //Id 数据类型标识用于 SavedDataStorage 索引
    string Id { get; }

    //Create 从 CompoundTag 反序列化创建 SavedData 实例
    T Create(CompoundTag tag, RegistryAccess registryAccess);
}
