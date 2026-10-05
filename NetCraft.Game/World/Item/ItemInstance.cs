using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//ItemInstance 物品实例的只读视图 对应原版 net.minecraft.world.item.ItemInstance
//原版继承 TypedInstance<Item> 与 DataComponentGetter 这里把用到的成员直接列出来
public interface ItemInstance : DataComponentGetter
{
    public const string FieldId = "id";
    public const string FieldCount = "count";
    public const string FieldComponents = "components";

    int Count { get; }

    Holder<Item> TypeHolder { get; }

    T? Get<T>(DataComponentType<T> type) where T : class;

    //GetMaxStackSize 堆叠上限 缺省读 MAX_STACK_SIZE 组件
    int GetMaxStackSize() => Get(DataComponents.MAX_STACK_SIZE) is int size ? size : 1;
}
