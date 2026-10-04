using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Network.Inventory;

namespace NetCraft.Game.World.Inventory;

//ChestMenu 箱式容器菜单对应原版 net.minecraft.world.inventory.ChestMenu
//槽位布局前 rows*9 格是容器 其后是玩家主背包三行与快捷栏 与原版 addStandardInventorySlots 一致
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
        //玩家背包与箱子网格下沿留 13 像素 快捷栏在背包下方 58 像素
        var inventoryTop = 18 + rows * 18 + 13;
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i,
                8 + i % 9 * 18, inventoryTop + i / 9 * 18));
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, inventoryTop + 58));
    }

    //ThreeRows 三行箱式容器 箱子与木桶共用 对应原版 ChestMenu.threeRows
    public static ChestMenu ThreeRows(int containerId, PlayerInventory inventory, Container container)
        => new(containerId, inventory, container, 3);

    //SixRows 六行箱式容器 双箱用 对应原版 ChestMenu.sixRows
    public static ChestMenu SixRows(int containerId, PlayerInventory inventory, Container container)
        => new(containerId, inventory, container, 6);

    //MenuTypeFor 行数到菜单类型 客户端按类型决定界面高度
    private static MenuType MenuTypeFor(int rows) => rows switch
    {
        1 => MenuTypes.GENERIC_9X1,
        2 => MenuTypes.GENERIC_9X2,
        3 => MenuTypes.GENERIC_9X3,
        4 => MenuTypes.GENERIC_9X4,
        5 => MenuTypes.GENERIC_9X5,
        _ => MenuTypes.GENERIC_9X6,
    };

    //Container 菜单打开的容器 供关闭流程与诊断取回
    public Container Container => _container;

    //RowCount 容器行数
    public int RowCount => _rows;

    //StillValid 容器方块还在且玩家没走远才有效 对应原版 ChestMenu.stillValid
    public override bool StillValid(ServerPlayer player) => _container switch
    {
        ChestBlockEntity chest => chest.StillValid(player),
        CompoundContainer compound => compound.StillValid(player),
        _ => true,
    };

    //QuickMoveStack 容器与背包之间对搬 对应原版 ChestMenu.quickMoveStack
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
