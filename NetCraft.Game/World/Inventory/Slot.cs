using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Registry;

namespace NetCraft.Game.World.Inventory;

//Slot menu slot, maps to vanilla net.minecraft.world.inventory.Slot
//Holds the backing container and a container index, the menu assigns the menu index Index in registration order
//X/Y are GUI coordinates, not used by server logic; unrelated to the protocol and kept as placeholders for now
public class Slot
{
    public Slot(Container container, int slotIndex, int x, int y)
    {
        Container = container;
        SlotIndex = slotIndex;
        X = x;
        Y = y;
    }

    //Container backing container
    public Container Container { get; }

    //SlotIndex container index
    public int SlotIndex { get; }

    //X/Y relative slot coordinates in the GUI
    public int X { get; }
    public int Y { get; }

    //Index slot index within the menu, written by AbstractContainerMenu.AddSlot
    public int Index { get; internal set; } = -1;

    //GetItem returns the item in this slot
    public virtual ItemStack GetItem() => Container.GetItem(SlotIndex);

    //Set writes the item in this slot and fires a change notification
    public virtual void Set(ItemStack stack)
    {
        Container.SetItem(SlotIndex, stack);
        SetChanged();
    }

    //SetChanged notifies the container that its contents changed
    public virtual void SetChanged() => Container.SetChanged();

    //HasItem whether this slot holds an item
    public virtual bool HasItem() => !GetItem().IsEmpty();

    //Remove removes the given count
    public virtual ItemStack Remove(int count) => Container.RemoveItem(SlotIndex, count);

    //SafeTake safely takes items from the slot bounded by both amount and maxAmount, maps to vanilla safeTake
    public virtual ItemStack SafeTake(int amount, int maxAmount, Player player)
    {
        var stack = GetItem();
        var taken = Math.Min(Math.Min(amount, maxAmount), stack.GetCount());
        if (taken <= 0) return ItemStack.Empty;
        var result = stack.Split(taken);
        Set(stack);
        return result;
    }

    //MayPlace whether this slot accepts the item
    public virtual bool MayPlace(ItemStack stack) => Container.CanPlaceItem(SlotIndex, stack);

    //GetMaxStackSize maximum stack size allowed in this slot
    public virtual int GetMaxStackSize()
    {
        var stack = GetItem();
        return stack.IsEmpty() ? Item.DEFAULT_MAX_STACK_SIZE : stack.GetItem().GetDefaultMaxStackSize();
    }

    //IsSameInventory whether two slots share the same container
    public bool IsSameInventory(Slot other) => ReferenceEquals(Container, other.Container);
}
