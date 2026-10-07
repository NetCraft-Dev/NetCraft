using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//PlayerInventory player inventory, maps to vanilla net.minecraft.world.entity.player.Inventory
//Slot layout: 0-8 hotbar, 9-35 main inventory, 36-39 armor (helmet -> boots), 40 offhand, 41 slots total
//This class also serves as the menu's backing container, menu slots map to indices here via the InventoryMenu constants
public sealed class PlayerInventory : Container
{
    //HotbarSlots number of hotbar slots
    public const int HotbarSlots = 9;

    //MainSlots number of main inventory slots
    public const int MainSlots = 27;

    //BackpackSize hotbar plus main inventory, 36 slots total
    public const int BackpackSize = HotbarSlots + MainSlots;

    //ArmorSlots number of armor slots
    public const int ArmorSlots = 4;

    //TotalSize total slot count including armor and offhand
    public const int TotalSize = BackpackSize + ArmorSlots + 1;

    //OffhandSlot container index of the offhand
    public const int OffhandSlot = BackpackSize + ArmorSlots;

    //_items slot contents, empty slots uniformly use ItemStack.Empty
    private readonly ItemStack[] _items = new ItemStack[TotalSize];
    private int _selected;

    public PlayerInventory()
    {
        for (var i = 0; i < _items.Length; i++) _items[i] = ItemStack.Empty;
    }

    public int Size => TotalSize;

    //SelectedSlot currently selected hotbar slot, 0-8, out-of-range values wrap around
    public int SelectedSlot
    {
        get => _selected;
        set => _selected = ((value % HotbarSlots) + HotbarSlots) % HotbarSlots;
    }

    //GetSelectedItem returns the item in the current hotbar slot
    public ItemStack GetSelectedItem() => _items[_selected];

    public ItemStack GetItem(int slot)
        => (uint)slot < TotalSize ? _items[slot] : ItemStack.Empty;

    public void SetItem(int slot, ItemStack stack)
    {
        if ((uint)slot >= TotalSize) return;
        _items[slot] = stack ?? ItemStack.Empty;
        SetChanged();
    }

    public ItemStack RemoveItem(int slot, int count)
    {
        if ((uint)slot >= TotalSize || count <= 0) return ItemStack.Empty;
        var stack = _items[slot];
        if (stack.IsEmpty()) return ItemStack.Empty;
        var taken = stack.GetCount() <= count ? stack : stack.CopyWithCount(count);
        var remain = stack.GetCount() - taken.GetCount();
        _items[slot] = remain <= 0 ? ItemStack.Empty : stack.CopyWithCount(remain);
        SetChanged();
        return taken;
    }

    //RemoveFromSelected takes the item from the current slot, maps to vanilla removeFromSelected
    //all true takes the whole slot, false takes only one
    public ItemStack RemoveFromSelected(bool all)
    {
        var stack = GetSelectedItem();
        return RemoveItem(SelectedSlot, all ? stack.GetMaxStackSize() : 1);
    }

    public ItemStack RemoveItemNoUpdate(int slot)
    {
        if ((uint)slot >= TotalSize) return ItemStack.Empty;
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

    //CanPlaceItem the player inventory accepts any item
    public bool CanPlaceItem(int slot, ItemStack stack) => true;

    //InfiniteMaterials creative infinite materials, maps to vanilla player.hasInfiniteMaterials
    //In creative the item is swallowed instead of reporting a full inventory, synced when the player's game mode changes
    public bool InfiniteMaterials { get; set; }

    //HasRemainingSpaceForItem whether the slot can still take more of the same item, maps to vanilla hasRemainingSpaceForItem
    //All four conditions must hold: the slot is non-empty, same item and components, stackable, and below the stack limit
    private static bool HasRemainingSpaceForItem(ItemStack slotStack, ItemStack newStack)
        => !slotStack.IsEmpty()
            && slotStack.IsSameItemAndComponentsAs(newStack)
            && slotStack.IsStackable()
            && slotStack.GetCount() < slotStack.GetMaxStackSize();

    //GetFreeSlot finds the first empty slot from the start, returns -1 when none, maps to vanilla getFreeSlot
    //Scans all 41 slots, armor and offhand also count as valid positions
    public int GetFreeSlot()
    {
        for (var i = 0; i < _items.Length; i++)
            if (_items[i].IsEmpty()) return i;
        return -1;
    }

    //FindSlotMatchingItem searches the whole inventory for a slot with the same item and components, returns -1 when none, maps to vanilla findSlotMatchingItem
    //Scans all 41 slots, armor and offhand also count as hits
    public int FindSlotMatchingItem(ItemStack stack)
    {
        for (var i = 0; i < _items.Length; i++)
            if (!_items[i].IsEmpty() && _items[i].IsSameItemAndComponentsAs(stack)) return i;
        return -1;
    }

    //GetSuitableHotbarSlot picks a hotbar slot suitable for a new item, maps to vanilla getSuitableHotbarSlot
    //Walks from the current slot to find the first empty one, vanilla then looks for the first unenchanted slot when all are full
    //This project has no enchantment component yet, so every item counts as unenchanted and the second pass always hits the current slot; it therefore reduces to using an empty slot when available and the current slot otherwise
    public int GetSuitableHotbarSlot()
    {
        for (var offset = 0; offset < HotbarSlots; offset++)
        {
            var slot = (_selected + offset) % HotbarSlots;
            if (_items[slot].IsEmpty()) return slot;
        }
        return _selected;
    }

    //AddAndPickItem puts the item in a suitable hotbar slot and selects it, maps to vanilla addAndPickItem
    //When the slot already holds something, it is moved to any free slot first, only displaced when no free slot exists
    public void AddAndPickItem(ItemStack stack)
    {
        SelectedSlot = GetSuitableHotbarSlot();
        var current = _items[_selected];
        if (!current.IsEmpty())
        {
            var free = GetFreeSlot();
            if (free != -1) _items[free] = current;
        }
        _items[_selected] = stack;
        SetChanged();
    }

    //PickSlot swaps an inventory slot with a suitable hotbar slot, maps to vanilla pickSlot
    public void PickSlot(int slot)
    {
        SelectedSlot = GetSuitableHotbarSlot();
        var swapped = _items[_selected];
        _items[_selected] = _items[slot];
        _items[slot] = swapped;
        SetChanged();
    }

    //GetSlotWithRemainingSpace finds a slot that can still stack, order follows vanilla: current slot -> offhand 40 -> whole inventory from the start
    public int GetSlotWithRemainingSpace(ItemStack stack)
    {
        if (HasRemainingSpaceForItem(_items[_selected], stack)) return _selected;
        if (HasRemainingSpaceForItem(_items[OffhandSlot], stack)) return OffhandSlot;
        for (var i = 0; i < _items.Length; i++)
            if (HasRemainingSpaceForItem(_items[i], stack)) return i;
        return -1;
    }

    //AddResource inserts into the given slot, returns the remainder that did not fit, maps to vanilla addResource(int, ItemStack)
    private int AddResource(int slot, ItemStack stack)
    {
        var count = stack.GetCount();
        var slotStack = _items[slot];
        //An empty slot first receives a matching stack with count 0; vanilla also places then grows
        if (slotStack.IsEmpty())
        {
            slotStack = stack.CopyWithCount(0);
            _items[slot] = slotStack;
        }
        var toAdd = Math.Min(count, slotStack.GetMaxStackSize() - slotStack.GetCount());
        if (toAdd == 0) return count;
        slotStack.SetCount(slotStack.GetCount() + toAdd);
        SetChanged();
        return count - toAdd;
    }

    //AddResource picks a slot automatically, maps to vanilla addResource(ItemStack)
    private int AddResource(ItemStack stack)
    {
        var slot = GetSlotWithRemainingSpace(stack);
        if (slot == -1) slot = GetFreeSlot();
        return slot == -1 ? stack.GetCount() : AddResource(slot, stack);
    }

    //Add inserts items into the inventory with vanilla Inventory.add semantics, returns whether at least one was inserted
    //The inserted part is removed from the passed stack, the remainder is read via stack.GetCount(); this is the vanilla contract callers use to decide what to drop
    //Loops until a round inserts nothing; each round fills a full stack, so multiple stacks fill later empty slots in turn
    public bool Add(ItemStack stack) => Add(-1, stack);

    //Add slot-specific version, maps to vanilla add(int, ItemStack); -1 means pick the slot automatically
    //The branch where a whole stack of a damageable item takes its own empty slot is added once the damage component lands; for now everything goes through the merge path
    public bool Add(int slot, ItemStack stack)
    {
        if (stack.IsEmpty()) return false;
        int lastSize;
        do
        {
            lastSize = stack.GetCount();
            stack.SetCount(slot == -1 ? AddResource(stack) : AddResource(slot, stack));
            if (stack.IsEmpty()) break;
        } while (stack.GetCount() < lastSize);
        //When none of the stack fit and the player is in creative, vanilla swallows it without counting it as a failure
        if (stack.GetCount() != lastSize || !InfiniteMaterials) return stack.GetCount() < lastSize;
        stack.SetCount(0);
        return true;
    }

    //PlaceItemBackInInventory puts items back in the inventory, dropping what does not fit at the player's feet, maps to vanilla placeItemBackInInventory
    //Used when a menu closes or a result slot is returned; unlike vanilla it does not send per-slot set_slot packets, the caller syncs uniformly
    public void PlaceItemBackInInventory(ItemStack stack, Func<ItemStack, bool> dropFallback)
    {
        while (!stack.IsEmpty())
        {
            var slot = GetSlotWithRemainingSpace(stack);
            if (slot == -1) slot = GetFreeSlot();
            if (slot == -1)
            {
                dropFallback(stack);
                return;
            }
            var space = stack.GetMaxStackSize() - GetItem(slot).GetCount();
            Add(slot, stack.CopyWithCount(Math.Min(space, stack.GetCount())));
            stack.Shrink(space);
        }
    }

    //SetChanged content change hook, the menu forwards the sync after registering a listener in AddSlot
    public void SetChanged() => Changed?.Invoke(this);

    //Changed content change event, the menu subscribes to it and triggers a sync
    public event Action<Container>? Changed;
}
