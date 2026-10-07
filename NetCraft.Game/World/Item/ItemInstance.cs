using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//ItemInstance read-only view of an item instance, maps to vanilla net.minecraft.world.item.ItemInstance
//Vanilla extends TypedInstance<Item> and DataComponentGetter; here the members in use are listed directly
public interface ItemInstance : DataComponentGetter
{
    public const string FieldId = "id";
    public const string FieldCount = "count";
    public const string FieldComponents = "components";

    int Count { get; }

    Holder<Item> TypeHolder { get; }

    T? Get<T>(DataComponentType<T> type) where T : class;

    //GetMaxStackSize stack limit, reads the MAX_STACK_SIZE component by default
    int GetMaxStackSize() => Get(DataComponents.MAX_STACK_SIZE) is int size ? size : 1;
}
