using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Logging;
using NetCraft.Game.World.Inventory;
using NetCraft.Util;

namespace NetCraft.Game.World.Inventory;

//AbstractContainerMenu container menu, maps to vanilla net.minecraft.world.inventory.AbstractContainerMenu
//Holds the slot list and state id, handles inbound clicks and syncs the result to the client
//Minimal implementation covers pickup/quick move/swap/throw/creative clone/double-click collect; drag and container data slots not yet done
public abstract class AbstractContainerMenu
{
    //SlotClickedOutside click outside the menu, used to throw the carried item
    public const int SlotClickedOutside = -999;

    //CarriedSlotIndex pseudo slot index for the carried item, maps to the -1 vanilla uses when syncing the cursor
    public const int CarriedSlotIndex = -1;

    private readonly List<Slot> _slots = new();
    private readonly List<ItemStack> _remoteSlots = new();
    //_dataSlots numeric data slots, syncs non-item state such as smelting progress and the stonecutter selection
    //A ContainerData can hold several values (the furnace's four progress values), so entries are keyed by (container, index) rather than the whole container
    private readonly List<DataSlotRef> _dataSlots = new();
    //_remoteDataSlots data slot values the client already knows, packets are sent only when they differ
    private readonly List<int> _remoteDataSlots = new();

    //DataSlotRef data slot reference, points at one index of a ContainerData
    private readonly record struct DataSlotRef(ContainerData Data, int Index);
    private int _stateId;

    protected AbstractContainerMenu(int containerId) : this(null, containerId) { }

    //Kind menu type, sent with open_screen to tell the client which screen to open; the player inventory menu has no screen and is null
    protected AbstractContainerMenu(MenuType? kind, int containerId)
    {
        Kind = kind;
        ContainerId = containerId;
    }

    //ContainerId menu id, the client uses it to find the matching menu
    public int ContainerId { get; }

    //Kind menu type, maps to vanilla AbstractContainerMenu.getType; null for the inventory menu
    public MenuType? Kind { get; }

    //Slots slot list, the order is the slot index the client sees
    public IReadOnlyList<Slot> Slots => _slots;

    //StateId state id, incremented on every sync so the client can drop stale packets
    public int StateId => _stateId;

    //Carried server-authoritative carried item
    public ItemStack Carried { get; private set; } = ItemStack.Empty;

    //RemoteCarried the carried item the client currently believes it has
    public ItemStack RemoteCarried { get; private set; } = ItemStack.Empty;

    //OwnerInventory inventory of the menu's owner, needed for swap/throw/collect; null when the menu has no player
    protected PlayerInventory? OwnerInventory { get; init; }

    //Synchronizer channel for pushing changes, when not injected only local state is updated
    public ContainerSynchronizer? Synchronizer { get; set; }

    //QuickMoveStack quick move, moves the item in the given slot into the target range defined by the menu, returns the stack before the move
    public abstract ItemStack QuickMoveStack(ServerPlayer player, int slotIndex);

    //StillValid whether the menu is still valid for the player
    public abstract bool StillValid(ServerPlayer player);

    //AddSlot registers a slot and assigns its index within the menu
    protected Slot AddSlot(Slot slot)
    {
        slot.Index = _slots.Count;
        _slots.Add(slot);
        _remoteSlots.Add(ItemStack.Empty);
        return slot;
    }

    //GetSlot returns a menu slot
    public Slot GetSlot(int index) => _slots[index];

    //AddDataSlots registers all values of a data container, maps to vanilla addDataSlots
    protected void AddDataSlots(ContainerData data)
    {
        for (var i = 0; i < data.Count; i++)
        {
            _dataSlots.Add(new DataSlotRef(data, i));
            //Treat the initial values as already known by the client, avoids a redundant round right after the menu opens
            _remoteDataSlots.Add(data.Get(i));
        }
    }

    //AddDataSlot registers a numeric data slot, returns the index of its first value
    protected int AddDataSlot(ContainerData data)
    {
        var index = _dataSlots.Count;
        AddDataSlots(data);
        return index;
    }

    //GetDataValue reads the current value of a data slot
    public int GetDataValue(int index)
    {
        var slot = _dataSlots[index];
        return slot.Data.Get(slot.Index);
    }

    //SetDataValue writes the current value of a data slot, values synced from the client go through here
    public void SetDataValue(int index, int value)
    {
        var slot = _dataSlots[index];
        slot.Data.Set(slot.Index, value);
    }

    //ClickMenuButton client button click, used for non-slot interactions such as picking a stonecutter recipe, returns whether it was handled
    //Maps to vanilla AbstractContainerMenu.clickMenuButton
    public virtual bool ClickMenuButton(ServerPlayer player, int buttonId) => false;

    //IsValidSlotIndex whether the index falls within the slot range
    public bool IsValidSlotIndex(int index) => (uint)index < _slots.Count;

    //FindSlot finds a menu slot by container and container index, returns null when not found
    protected Slot? FindSlot(Container container, int containerSlotIndex)
    {
        foreach (var slot in _slots)
            if (slot.SlotIndex == containerSlotIndex && ReferenceEquals(slot.Container, container))
                return slot;
        return null;
    }

    //SetCarried sets the server-side carried item
    public void SetCarried(ItemStack stack) => Carried = stack ?? ItemStack.Empty;

    //SetRemoteCarried records the carried item reported by the client
    public void SetRemoteCarried(ItemStack stack) => RemoteCarried = stack ?? ItemStack.Empty;

    //IncrementStateId increments the state id
    public void IncrementStateId() => _stateId++;

    //InitializeContents initializes contents on the client, maps to vanilla initializeContents
    public void InitializeContents(int stateId, IReadOnlyList<ItemStack> items, ItemStack carried)
    {
        for (var i = 0; i < _slots.Count && i < items.Count; i++)
            _slots[i].Set(items[i]);
        SetCarried(carried);
        _stateId = stateId;
    }

    //SendAllDataToRemote sends the full current contents (initial sync when the player joins the world)
    //The remote side must store copies, not references: adding to the player inventory reuses the same stack via an in-place SetCount
    //Storing references makes dirty-slot comparison always equal, showing up as "the item was picked up but the client count never updates"
    public void SendAllDataToRemote()
    {
        for (var i = 0; i < _slots.Count; i++) _remoteSlots[i] = _slots[i].GetItem().Copy();
        RemoteCarried = Carried.Copy();
        Synchronizer?.SendContentUpdate(this, _remoteSlots, Carried);
    }

    //BroadcastChanges syncs only slots and the cursor that changed, called after a click and every tick
    //The remote side keeps copies, same reason as SendAllDataToRemote, otherwise slots mutated in place compare equal and are never sent
    public void BroadcastChanges()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var current = _slots[i].GetItem();
            if (SameStack(current, _remoteSlots[i])) continue;
            _remoteSlots[i] = current.Copy();
            Synchronizer?.SendSlotChange(this, i, current);
        }
        if (!SameStack(Carried, RemoteCarried))
        {
            RemoteCarried = Carried.Copy();
            Synchronizer?.SendSlotChange(this, CarriedSlotIndex, Carried);
        }
        //Data slots are also sent only when changed, matches the dataSlots pass in vanilla broadcastChanges
        for (var i = 0; i < _dataSlots.Count; i++)
        {
            var value = GetDataValue(i);
            if (value == _remoteDataSlots[i]) continue;
            _remoteDataSlots[i] = value;
            Synchronizer?.SendDataChange(this, i, value);
        }
        IncrementStateId();
    }

    //Clicked handles an inbound click, maps to vanilla AbstractContainerMenu.clicked
    public void Clicked(int slotIndex, int buttonNum, ContainerInput input, ServerPlayer player)
    {
        if (!IsValidSlotIndex(slotIndex) && slotIndex != SlotClickedOutside)
        {
            //An out-of-range slot usually means the client and server slot layouts differ; dropping it silently makes pressing Q in the UI look like it does nothing
            Log.Info($"[Container] Slot index out of range, dropped slot={slotIndex} menu slots={_slots.Count} input={input}");
            return;
        }
        var slot = IsValidSlotIndex(slotIndex) ? _slots[slotIndex] : null;
        switch (input)
        {
            case ContainerInput.Pickup:
                HandlePickup(slot, buttonNum);
                break;
            case ContainerInput.QuickMove:
                if (slot is not null) QuickMoveStack(player, slot.Index);
                break;
            case ContainerInput.Swap:
                HandleSwap(slot, buttonNum);
                break;
            case ContainerInput.Clone:
                HandleClone(slot, player);
                break;
            case ContainerInput.Throw:
                HandleThrow(slot, buttonNum, player);
                break;
            case ContainerInput.PickupAll:
                HandlePickupAll(slot);
                break;
            default:
                Log.Debug($"Container click type not implemented yet input={input} slot={slotIndex}");
                break;
        }
        BroadcastChanges();
    }

    //HandlePickup left click picks up/places, right click takes half/places one, clicking outside the menu throws
    private void HandlePickup(Slot? slot, int buttonNum)
    {
        if (buttonNum != 0 && buttonNum != 1) return;
        if (slot is null)
        {
            if (Carried.IsEmpty()) return;
            var dropCount = buttonNum == 0 ? Carried.GetCount() : 1;
            SetCarried(Keep(Carried, Carried.GetCount() - dropCount));
            return;
        }
        var slotStack = slot.GetItem();
        var rightClick = buttonNum == 1;
        if (slotStack.IsEmpty())
        {
            if (Carried.IsEmpty() || !slot.MayPlace(Carried)) return;
            var placeCount = rightClick ? 1 : Math.Min(Carried.GetCount(), slot.GetMaxStackSize());
            slot.Set(Carried.CopyWithCount(placeCount));
            SetCarried(Keep(Carried, Carried.GetCount() - placeCount));
            return;
        }
        if (Carried.IsEmpty())
        {
            //Picking up with an empty hand, right click takes only half
            var takeCount = rightClick ? (slotStack.GetCount() + 1) / 2 : slotStack.GetCount();
            SetCarried(slot.Remove(takeCount));
            slot.SetChanged();
            return;
        }
        if (!slot.MayPlace(Carried)) return;
        //Same-item checks all go through the single ItemStack implementation
        if (Carried.IsSameItemAndComponentsAs(slotStack))
        {
            var max = slot.GetMaxStackSize();
            if (slotStack.GetCount() >= max) return;
            var moveCount = rightClick ? 1 : Math.Min(max - slotStack.GetCount(), Carried.GetCount());
            slot.Set(slotStack.CopyWithCount(slotStack.GetCount() + moveCount));
            SetCarried(Keep(Carried, Carried.GetCount() - moveCount));
            return;
        }
        //Different items, left click swaps (right click on different items does nothing, matches vanilla)
        if (rightClick) return;
        slot.Set(Carried);
        SetCarried(slotStack);
    }

    //HandleSwap number keys or F swap with the hotbar/offhand slot, does nothing when the target slot is the clicked slot itself
    private void HandleSwap(Slot? slot, int buttonNum)
    {
        if (slot is null || OwnerInventory is null) return;
        if (buttonNum is < 0 or > PlayerInventory.HotbarSlots) return;
        var target = FindSlot(OwnerInventory, buttonNum);
        if (target is null || ReferenceEquals(target, slot)) return;
        var temp = slot.GetItem();
        slot.Set(target.GetItem());
        target.Set(temp);
    }

    //HandleClone creative middle-click clone, takes a full stack when the cursor is empty and tops it up when it holds an item
    private void HandleClone(Slot? slot, ServerPlayer player)
    {
        if (slot is null || player.GameType != GameType.Creative) return;
        var slotStack = slot.GetItem();
        if (Carried.IsEmpty())
        {
            if (slotStack.IsEmpty()) return;
            SetCarried(slotStack.CopyWithCount(slotStack.GetItem().GetDefaultMaxStackSize()));
            return;
        }
        SetCarried(Carried.CopyWithCount(Carried.GetItem().GetDefaultMaxStackSize()));
    }

    //HandleThrow throw, left click throws one and right click throws the whole stack, maps to the THROW branch of vanilla AbstractContainerMenu
    //Does nothing when the cursor holds an item, the vanilla branch requires getCarried().isEmpty()
    //Items taken from a slot go through the player drop path and become item entities; removing from the slot without dropping makes pressing Q in a container UI pointless
    private void HandleThrow(Slot? slot, int buttonNum, ServerPlayer player)
    {
        if (slot is null || !slot.HasItem() || !Carried.IsEmpty())
        {
            //Log when the guard fails, so it is obvious which condition blocked it when the client presses Q but the server does nothing
            Log.Info($"[Container] Throw skipped slot={(slot is null ? "none" : slot.Index.ToString())} has item={slot?.HasItem()} carried not empty={!Carried.IsEmpty()}");
            return;
        }
        var count = buttonNum == 0 ? 1 : slot.GetItem().GetCount();
        var dropped = slot.Remove(count);
        slot.SetChanged();
        var entity = player.Drop(dropped, randomly: false, thrownFromHand: true);
        Log.Info($"[Container] Dropped slot={slot.Index} count={dropped.GetCount()} entity={(entity is null ? "not spawned" : entity.EntityId.ToString())}");
    }

    //HandlePickupAll double-click collect, merges same-item stacks from the inventory into the clicked slot
    private void HandlePickupAll(Slot? slot)
    {
        if (slot is null || OwnerInventory is null) return;
        var target = slot.GetItem();
        if (target.IsEmpty()) return;
        for (var i = 0; i < PlayerInventory.BackpackSize; i++)
        {
            var current = slot.GetItem();
            var max = slot.GetMaxStackSize();
            if (current.GetCount() >= max) break;
            var source = FindSlot(OwnerInventory, i);
            if (source is null || ReferenceEquals(source, slot) || !source.HasItem()) continue;
            //Same-item checks all go through the single ItemStack implementation, do not write another item-only version
            if (!current.IsSameItemAndComponentsAs(source.GetItem())) continue;
            var moveCount = Math.Min(max - current.GetCount(), source.GetItem().GetCount());
            slot.Set(current.CopyWithCount(current.GetCount() + moveCount));
            source.Remove(moveCount);
            source.SetChanged();
        }
    }

    //MoveItemStackTo merges or places the given stack within a slot range, returns whether a move happened
    //Merges into matching slots first, then finds an empty one, same order as vanilla; reverse scans from the end of the range
    protected bool MoveItemStackTo(ItemStack stack, int startIndex, int endIndex, bool reverse)
    {
        var changed = false;
        var index = reverse ? endIndex - 1 : startIndex;
        var step = reverse ? -1 : 1;
        while (index >= startIndex && index < endIndex)
        {
            var slot = _slots[index];
            var slotStack = slot.GetItem();
            //Same-item checks use the core isSameItemSameComponents, comparing only the item would merge stacks with different components
            if (!slotStack.IsEmpty() && stack.IsSameItemAndComponentsAs(slotStack))
            {
                var max = slot.GetMaxStackSize();
                var total = stack.GetCount() + slotStack.GetCount();
                if (total <= max)
                {
                    slot.Set(slotStack.CopyWithCount(total));
                    stack.SetCount(0);
                    slot.SetChanged();
                    changed = true;
                }
                else if (slotStack.GetCount() < max)
                {
                    var moveCount = max - slotStack.GetCount();
                    slot.Set(slotStack.CopyWithCount(max));
                    stack.SetCount(stack.GetCount() - moveCount);
                    slot.SetChanged();
                    changed = true;
                }
            }
            index += step;
        }
        if (!stack.IsEmpty())
        {
            index = reverse ? endIndex - 1 : startIndex;
            while (index >= startIndex && index < endIndex)
            {
                var slot = _slots[index];
                if (slot.GetItem().IsEmpty() && slot.MayPlace(stack))
                {
                    var max = slot.GetMaxStackSize();
                    var placeCount = Math.Min(stack.GetCount(), max);
                    slot.Set(stack.CopyWithCount(placeCount));
                    stack.SetCount(stack.GetCount() - placeCount);
                    slot.SetChanged();
                    changed = true;
                    break;
                }
                index += step;
            }
        }
        return changed;
    }

    //Keep keeps remain items from the stack, returns a new stack or an empty one
    private static ItemStack Keep(ItemStack stack, int remain)
        => remain <= 0 ? ItemStack.Empty : stack.CopyWithCount(remain);

    //SameStack whether the stack contents are identical, used for dirty-slot comparison
    //Same-item checks use the core same-item same-components comparison, here only the count is compared in addition
    //The reference short-circuit only hits the empty-stack singleton (ItemStack.Copy returns Empty for empty stacks); non-empty stacks are always distinct objects
    private static bool SameStack(ItemStack a, ItemStack b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.IsEmpty() || b.IsEmpty()) return a.IsEmpty() && b.IsEmpty();
        return a.GetCount() == b.GetCount() && a.IsSameItemAndComponentsAs(b);
    }

    //MaxContainerStackSize container-side stack limit, matches the default of vanilla Container.getMaxStackSize
    private const int MaxContainerStackSize = 64;

    //GetRedstoneSignalFromBlockEntity comparator signal of a block entity, returns 0 when it is not a container, maps to the vanilla method of the same name
    public static int GetRedstoneSignalFromBlockEntity(object? blockEntity)
        => blockEntity is Container container ? GetRedstoneSignalFromContainer(container) : 0;

    //GetRedstoneSignalFromContainer computes the comparator output from container fullness, maps to the vanilla method of the same name
    //Empty returns 0 and full returns 15, in between it averages the fill ratio of each slot
    public static int GetRedstoneSignalFromContainer(Container? container)
    {
        if (container is null || container.Size <= 0) return 0;
        var nonEmptySlots = 0;
        var fill = 0f;
        for (var i = 0; i < container.Size; i++)
        {
            var stack = container.GetItem(i);
            if (stack.IsEmpty()) continue;
            fill += (float)stack.GetCount() / Math.Min(MaxContainerStackSize, stack.GetMaxStackSize());
            nonEmptySlots++;
        }
        return Mth.Floor(fill / container.Size * 14f) + (nonEmptySlots > 0 ? 1 : 0);
    }
}
