using NetCraft.Game.Server;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//InventoryMenu player inventory menu, maps to vanilla net.minecraft.world.inventory.InventoryMenu
//Slot layout matches vanilla: 0 result, 1-4 crafting grid, 5-8 armor, 9-35 main inventory, 36-44 hotbar, 45 offhand
//The 2x2 crafting grid shares AbstractCraftingMenu's recompute and ingredient consumption logic with the crafting table's 3x3
public sealed class InventoryMenu : AbstractCraftingMenu
{
    //MenuContainerId the player inventory menu always uses container id 0
    public const int MenuContainerId = 0;

    public const int ResultSlotIndex = 0;
    public const int CraftSlotStart = 1;
    public const int CraftSlotEnd = 5;
    public const int ArmorSlotStart = 5;
    public const int ArmorSlotEnd = 9;
    public const int InvSlotStart = 9;
    public const int InvSlotEnd = 36;
    public const int UseRowSlotStart = 36;
    public const int UseRowSlotEnd = 45;
    public const int ShieldSlotIndex = 45;

    public InventoryMenu(PlayerInventory inventory, int containerId = MenuContainerId)
        //The inventory menu has no screen, the type is null, maps to vanilla super(null, 0, 2, 2)
        : base(null, containerId, 2, 2)
    {
        OwnerInventory = inventory;
        //0 result slot, taking it consumes ingredients from the crafting grid
        AddSlot(new ResultSlot(this, ResultSlots, 0, 154, 28));
        //1-4 crafting grid, 2x2
        for (var i = 0; i < 4; i++)
            AddSlot(new Slot(CraftSlots, i, 98 + i % 2 * 18, 18 + i / 2 * 18));
        //5-8 armor, container indices 39 helmet down to 36 boots
        for (var i = 0; i < PlayerInventory.ArmorSlots; i++)
            AddSlot(new Slot(inventory, 39 - i, 8, 8 + i * 18));
        //9-35 main inventory
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i, 8 + i % 9 * 18, 84 + i / 9 * 18));
        //36-44 hotbar
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, 142));
        //45 offhand
        AddSlot(new Slot(inventory, PlayerInventory.OffhandSlot, 77, 62));
    }

    public override bool StillValid(ServerPlayer player) => true;

    //QuickMoveStack quick move between the vanilla sections: crafting/armor/offhand -> inventory, inventory <-> hotbar
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        if (slotIndex == ResultSlotIndex)
        {
            //Ingredients are only consumed when a move happened; an item that does not fit must not be charged, matching vanilla moveItemStackTo returning early on failure
            if (MoveItemStackTo(stack, InvSlotStart, UseRowSlotEnd, true)) OnCraftingTaken();
        }
        else if (slotIndex is >= CraftSlotStart and < ArmorSlotStart)
            MoveItemStackTo(stack, InvSlotStart, UseRowSlotEnd, false);
        else if (slotIndex is >= ArmorSlotStart and < InvSlotStart)
            MoveItemStackTo(stack, InvSlotStart, UseRowSlotEnd, false);
        else if (slotIndex is >= InvSlotStart and < InvSlotEnd)
            MoveItemStackTo(stack, UseRowSlotStart, UseRowSlotEnd, false);
        else if (slotIndex is >= UseRowSlotStart and < UseRowSlotEnd)
            MoveItemStackTo(stack, InvSlotStart, InvSlotEnd, false);
        else
            MoveItemStackTo(stack, InvSlotStart, UseRowSlotEnd, false);
        slot.SetChanged();
        return moved;
    }
}
