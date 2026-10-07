using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//Container item container contract, maps to vanilla net.minecraft.world.Container
//The minimal set keeps only slot read/write and change notification; open/close and player distance checks are handled by the menu layer
public interface Container
{
    //Size number of container slots
    int Size { get; }

    //GetItem reads the item in a slot, returns ItemStack.Empty for empty slots
    ItemStack GetItem(int slot);

    //SetItem writes the item in a slot
    void SetItem(int slot, ItemStack stack);

    //RemoveItem removes the given count, returns the removed stack
    ItemStack RemoveItem(int slot, int count);

    //RemoveItemNoUpdate removes the whole slot without firing a change notification
    ItemStack RemoveItemNoUpdate(int slot);

    //SetChanged marks the contents as changed; the owner decides how to notify (menu sync)
    void SetChanged();

    //IsEmpty whether all slots are empty
    bool IsEmpty();

    //ClearContent clears all slots
    void ClearContent();

    //CanPlaceItem whether the slot accepts the item
    bool CanPlaceItem(int slot, ItemStack stack);
}
