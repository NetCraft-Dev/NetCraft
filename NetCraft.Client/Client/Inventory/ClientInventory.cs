using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Client.Inventory;

//ClientInventory client inventory cache, maps to vanilla LocalPlayer's Inventory and the currently open menu slots
//Menu slots are filled by ClientboundContainerSetContent/SetSlot; the player inventory is filled by ClientboundSetPlayerInventory
public sealed class ClientInventory
{
    //PlayerSlotCount number of player inventory slots, layout matches PlayerInventory
    public const int PlayerSlotCount = PlayerInventory.TotalSize;

    private readonly List<ItemStack> _menuSlots = new();
    private readonly ItemStack[] _playerSlots = new ItemStack[PlayerSlotCount];

    public ClientInventory()
    {
        for (var i = 0; i < _playerSlots.Length; i++) _playerSlots[i] = ItemStack.Empty;
    }

    //ContainerId current menu id; -1 before a content packet is received
    public int ContainerId { get; private set; } = -1;

    //StateId the state id last synced by the server
    public int StateId { get; private set; }

    //Carried cursor item
    public ItemStack Carried { get; private set; } = ItemStack.Empty;

    //MenuSlots read-only view of menu slots, order matches the server's menu slot numbers
    public IReadOnlyList<ItemStack> MenuSlots => _menuSlots;

    //GetPlayerItem gets a player inventory slot; out of range returns an empty stack
    public ItemStack GetPlayerItem(int slot)
        => (uint)slot < PlayerSlotCount ? _playerSlots[slot] : ItemStack.Empty;

    //SetContent applies the full container contents
    public void SetContent(int containerId, int stateId, IReadOnlyList<ItemStack> items, ItemStack carried)
    {
        ContainerId = containerId;
        StateId = stateId;
        _menuSlots.Clear();
        _menuSlots.AddRange(items);
        Carried = carried;
    }

    //SetSlot applies a single-slot change; slot -1 means the cursor item
    public void SetSlot(int slot, ItemStack stack)
    {
        if (slot == AbstractContainerMenu.CarriedSlotIndex)
        {
            Carried = stack;
            return;
        }
        if ((uint)slot >= _menuSlots.Count) return;
        _menuSlots[slot] = stack;
    }

    //SetPlayerSlot applies a single player inventory slot change
    public void SetPlayerSlot(int slot, ItemStack stack)
    {
        if ((uint)slot >= PlayerSlotCount) return;
        _playerSlots[slot] = stack;
    }
}
