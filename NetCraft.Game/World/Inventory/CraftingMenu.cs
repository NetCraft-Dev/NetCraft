using NetCraft.Game.Server;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//CraftingMenu 工作台菜单对应原版 net.minecraft.world.inventory.CraftingMenu
//槽位布局照原版: 0 结果 1-9 合成格 10-36 主物品栏 37-45 快捷栏 共 46 槽
//工作台没有护甲与副手槽 这两样只在背包菜单里有
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
        //0 结果槽 取走时要结算合成格材料
        AddSlot(new ResultSlot(this, ResultSlots, 0, 124, 35));
        //1-9 合成格 3x3
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                AddSlot(new Slot(CraftSlots, x + y * 3, 30 + x * 18, 17 + y * 18));
        //10-36 主物品栏 37-45 快捷栏 对应原版 addStandardInventorySlots(inventory, 8, 84)
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i, 8 + i % 9 * 18, 84 + i / 9 * 18));
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, 142));
    }

    public override bool StillValid(ServerPlayer player) => true;

    //QuickMoveStack 快捷搬运 结果槽搬进背包 背包物品优先塞合成格 塞不下再在背包两区之间挪
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        if (slotIndex == ResultSlotIndex)
        {
            //搬走了才结算材料 背包塞不下(搬不动)不该白扣 与原版 moveItemStackTo 失败即返回一致
            if (MoveItemStackTo(stack, InvSlotStart, HotbarSlotEnd, true)) OnCraftingTaken();
        }
        else if (slotIndex < CraftSlotEnd)
            MoveItemStackTo(stack, InvSlotStart, HotbarSlotEnd, false);
        else if (slotIndex < HotbarSlotEnd)
        {
            //主区与快捷栏之间互相倒 对应原版 index < 37 与 else 两条分支
            if (!MoveItemStackTo(stack, CraftSlotStart, CraftSlotEnd, false))
                MoveItemStackTo(stack, slotIndex < HotbarSlotStart ? HotbarSlotStart : InvSlotStart,
                    slotIndex < HotbarSlotStart ? HotbarSlotEnd : InvSlotEnd, false);
        }
        slot.SetChanged();
        return moved;
    }
}
