using NetCraft.Game.Server;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//CraftingMenu crafting table menu, maps to vanilla net.minecraft.world.inventory.CraftingMenu
//Slot layout follows vanilla: 0 result, 1-9 crafting grid, 10-36 main inventory, 37-45 hotbar, 46 slots total
//The crafting table has no armor or offhand slots, those exist only in the inventory menu
public sealed class CraftingMenu : AbstractCraftingMenu
{
    public const int ResultSlotIndex = 0;
    public const int CraftSlotStart = 1;
    public const int CraftSlotEnd = 10;
    public const int InvSlotStart = 10;
    public const int InvSlotEnd = 37;
    public const int HotbarSlotStart = 37;
    public const int HotbarSlotEnd = 46;

    public CraftingMenu(int containerId, PlayerInventory inventory)
        : base(MenuTypes.CRAFTING, containerId, 3, 3)
    {
        OwnerInventory = inventory;
        //0 result slot, taking it consumes ingredients from the crafting grid
        AddSlot(new ResultSlot(this, ResultSlots, 0, 124, 35));
        //1-9 crafting grid, 3x3
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                AddSlot(new Slot(CraftSlots, x + y * 3, 30 + x * 18, 17 + y * 18));
        //10-36 main inventory, 37-45 hotbar, maps to vanilla addStandardInventorySlots(inventory, 8, 84)
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i, 8 + i % 9 * 18, 84 + i / 9 * 18));
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, 142));
    }

    public override bool StillValid(ServerPlayer player) => true;

    //QuickMoveStack quick move: the result slot goes to the inventory, inventory items fill the crafting grid first and otherwise shift between the two inventory ranges
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        if (slotIndex == ResultSlotIndex)
        {
            //Ingredients are only consumed when a move happened; an item that does not fit must not be charged, matching vanilla moveItemStackTo returning early on failure
            if (MoveItemStackTo(stack, InvSlotStart, HotbarSlotEnd, true)) OnCraftingTaken();
        }
        else if (slotIndex < CraftSlotEnd)
            MoveItemStackTo(stack, InvSlotStart, HotbarSlotEnd, false);
        else if (slotIndex < HotbarSlotEnd)
        {
            //Shifts between the main range and the hotbar, matching vanilla's index < 37 and else branches
            if (!MoveItemStackTo(stack, CraftSlotStart, CraftSlotEnd, false))
                MoveItemStackTo(stack, slotIndex < HotbarSlotStart ? HotbarSlotStart : InvSlotStart,
                    slotIndex < HotbarSlotStart ? HotbarSlotEnd : InvSlotEnd, false);
        }
        slot.SetChanged();
        return moved;
    }
}
