using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//SimpleContainer fixed-size simple container, maps to vanilla net.minecraft.world.SimpleContainer
//Used for the crafting grid/result slot and similar cases that need only slot read/write without extra logic
public sealed class SimpleContainer : Container
{
    private readonly ItemStack[] _items;

    public SimpleContainer(int size)
    {
        Size = size;
        _items = new ItemStack[size];
        for (var i = 0; i < size; i++) _items[i] = ItemStack.Empty;
    }

    public int Size { get; }

    public ItemStack GetItem(int slot) => (uint)slot < Size ? _items[slot] : ItemStack.Empty;

    public void SetItem(int slot, ItemStack stack)
    {
        if ((uint)slot >= Size) return;
        _items[slot] = stack ?? ItemStack.Empty;
        SetChanged();
    }

    public ItemStack RemoveItem(int slot, int count)
    {
        if ((uint)slot >= Size || count <= 0) return ItemStack.Empty;
        var stack = _items[slot];
        if (stack.IsEmpty()) return ItemStack.Empty;
        var taken = stack.GetCount() <= count ? stack : stack.CopyWithCount(count);
        var remain = stack.GetCount() - taken.GetCount();
        _items[slot] = remain <= 0 ? ItemStack.Empty : stack.CopyWithCount(remain);
        SetChanged();
        return taken;
    }

    public ItemStack RemoveItemNoUpdate(int slot)
    {
        if ((uint)slot >= Size) return ItemStack.Empty;
        var stack = _items[slot];
        _items[slot] = ItemStack.Empty;
        return stack;
    }

    public bool IsEmpty()
    {
        foreach (var stack in _items)
            if (!stack.IsEmpty()) return false;
        return true;
    }

    public void ClearContent()
    {
        for (var i = 0; i < _items.Length; i++) _items[i] = ItemStack.Empty;
    }

    public bool CanPlaceItem(int slot, ItemStack stack) => true;

    public void SetChanged() => Changed?.Invoke(this);

    //Changed content change event, subscribed by the menu
    public event Action<Container>? Changed;
}
