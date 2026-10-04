using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Network.Inventory;

namespace NetCraft.Game.World.Inventory;

//DispenserMenu 发射器与投掷器的九格菜单 对应原版 net.minecraft.world.inventory.DispenserMenu
//原版方块只有九格但共用 generic_3x3 菜单类型 客户端按该类型画出三乘三的界面
public sealed class DispenserMenu : AbstractContainerMenu
{
    //GridSize 九格容器的边长
    private const int GridSize = 3;

    private readonly Container _container;

    private DispenserMenu(int containerId, PlayerInventory inventory, Container container)
        : base(MenuTypes.GENERIC_3X3, containerId)
    {
        OwnerInventory = inventory;
        _container = container;
        //九格槽位与玩家背包的像素位置照原版 DispenserMenu 构造里的常量
        for (var row = 0; row < GridSize; row++)
            for (var column = 0; column < GridSize; column++)
                AddSlot(new Slot(container, column + row * GridSize, 62 + column * 18, 17 + row * 18));
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 9; column++)
                AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + column + row * 9, 8 + column * 18, 84 + row * 18));
        for (var column = 0; column < 9; column++)
            AddSlot(new Slot(inventory, column, 8 + column * 18, 142));
    }

    //Create 构造九格菜单
    public static DispenserMenu Create(int containerId, PlayerInventory inventory, Container container)
        => new(containerId, inventory, container);

    //StillValid 容器方块还在原位且玩家没走远才有效
    public override bool StillValid(ServerPlayer player) => _container switch
    {
        DispenserBlockEntity dispenser => dispenser.StillValid(player),
        _ => true,
    };

    //QuickMoveStack 九格容器与背包之间对搬 对应原版 DispenserMenu.quickMoveStack
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        var containerSlots = GridSize * GridSize;
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
