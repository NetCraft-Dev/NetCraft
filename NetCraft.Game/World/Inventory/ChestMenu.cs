using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Network.Inventory;

namespace NetCraft.Game.World.Inventory;

//ChestMenu chest container menu, maps to vanilla net.minecraft.world.inventory.ChestMenu
//Slot layout: the first rows*9 slots are the container, followed by three rows of main inventory and the hotbar, same as vanilla addStandardInventorySlots
public sealed class ChestMenu : AbstractContainerMenu
{
    private readonly Container _container;
    private readonly int _rows;

    private ChestMenu(int containerId, PlayerInventory inventory, Container container, int rows)
        : base(MenuTypeFor(rows), containerId)
    {
        OwnerInventory = inventory;
        _container = container;
        _rows = rows;
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < 9; column++)
                AddSlot(new Slot(container, column + row * 9, 8 + column * 18, 18 + row * 18));
        //13 pixels between the player inventory and the bottom of the chest grid, the hotbar sits 58 pixels below the inventory
        var inventoryTop = 18 + rows * 18 + 13;
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i,
                8 + i % 9 * 18, inventoryTop + i / 9 * 18));
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, inventoryTop + 58));
    }

    //ThreeRows three-row chest container, shared by chests and barrels, maps to vanilla ChestMenu.threeRows
    public static ChestMenu ThreeRows(int containerId, PlayerInventory inventory, Container container)
        => new(containerId, inventory, container, 3);

    //SixRows six-row chest container used by double chests, maps to vanilla ChestMenu.sixRows
    public static ChestMenu SixRows(int containerId, PlayerInventory inventory, Container container)
        => new(containerId, inventory, container, 6);

    //MenuTypeFor maps a row count to a menu type, the client uses the type to size the screen
    private static MenuType MenuTypeFor(int rows) => rows switch
    {
        1 => MenuTypes.GENERIC_9X1,
        2 => MenuTypes.GENERIC_9X2,
        3 => MenuTypes.GENERIC_9X3,
        4 => MenuTypes.GENERIC_9X4,
        5 => MenuTypes.GENERIC_9X5,
        _ => MenuTypes.GENERIC_9X6,
    };

    //Container container this menu opened, retrieved by the close path and diagnostics
    public Container Container => _container;

    //RowCount number of container rows
    public int RowCount => _rows;

    //StillValid valid while the container block remains and the player is close enough, maps to vanilla ChestMenu.stillValid
    public override bool StillValid(ServerPlayer player) => _container switch
    {
        ChestBlockEntity chest => chest.StillValid(player),
        CompoundContainer compound => compound.StillValid(player),
        _ => true,
    };

    //QuickMoveStack moves items between the container and the inventory, maps to vanilla ChestMenu.quickMoveStack
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        var containerSlots = _rows * 9;
        if (slotIndex < containerSlots)
        {
            if (!MoveItemStackTo(stack, containerSlots, Slots.Count, true)) return ItemStack.Empty;
        }
        else if (!MoveItemStackTo(stack, 0, containerSlots, false))
        {
            return ItemStack.Empty;
        }
        if (stack.IsEmpty()) slot.Set(ItemStack.Empty);
        else slot.SetChanged();
        return moved;
    }
}
