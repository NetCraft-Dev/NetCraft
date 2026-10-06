using NetCraft.Nbt;

namespace NetCraft.Storage;

//SavedData, persistent data abstract base class, maps to vanilla net.minecraft.world.level.storage.SavedData
//Subclasses implement Save to write themselves into a CompoundTag and clear dirty
//SavedDataStorage.ComputeIfAbsent creates an instance using the SavedDataType factory
public abstract class SavedData
{
    //IsDirty, whether there are unsaved changes
    public bool IsDirty { get; protected set; }

    //Id, the data file name used for persisted lookup
    public abstract string Id { get; }

    //Save writes itself into a CompoundTag and returns it
    public abstract CompoundTag Save(CompoundTag tag);

    //SetDirty marks unsaved changes
    public virtual void SetDirty() => IsDirty = true;

    //ClearDirty clears the dirty flag after a save completes
    public void ClearDirty() => IsDirty = false;
}
