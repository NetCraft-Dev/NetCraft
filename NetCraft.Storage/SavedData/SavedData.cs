using NetCraft.Nbt;

namespace NetCraft.Storage;

//SavedData 持久化数据抽象基类对应原版 net.minecraft.world.level.storage.SavedData
//子类实现 Save 把自身写入 CompoundTag 并标记 notDirty
//SavedDataStorage.ComputeIfAbsent 用 SavedDataType 工厂创建实例
public abstract class SavedData
{
    //IsDirty 是否有未保存修改
    public bool IsDirty { get; protected set; }

    //Id 数据文件名用于持久化定位
    public abstract string Id { get; }

    //Save 把自身写入 CompoundTag 返回
    public abstract CompoundTag Save(CompoundTag tag);

    //SetDirty 标记有未保存修改
    public virtual void SetDirty() => IsDirty = true;

    //ClearDirty 保存完成后清除 dirty 标记
    public void ClearDirty() => IsDirty = false;
}
