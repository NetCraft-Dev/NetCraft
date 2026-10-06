using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//SavedDataType, persistent data type factory, maps to vanilla net.minecraft.world.level.storage.SavedDataType
//T is a concrete SavedData subclass; create deserializes an instance from a CompoundTag
public interface SavedDataType<T> where T : SavedData
{
    //Id, the data type identifier used as the SavedDataStorage index
    string Id { get; }

    //Create deserializes and creates a SavedData instance from a CompoundTag
    T Create(CompoundTag tag, RegistryAccess registryAccess);
}
