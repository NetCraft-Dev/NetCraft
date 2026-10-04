using NetCraft.Game.Server;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//InventoryMenu 玩家背包菜单对应原版 net.minecraft.world.inventory.InventoryMenu
//槽位布局与原版一致: 0 结果 1-4 合成格 5-8 护甲 9-35 主物品栏 36-44 快捷栏 45 副手
//2x2 合成格与工作台的 3x3 共用 AbstractCraftingMenu 的重算与扣料逻辑
public sealed class InventoryMenu : AbstractCraftingMenu
{
    //MenuContainerId 玩家背包菜单固定用容器 id 0
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
        //背包菜单没有对应界面 类型传 null 对应原版 super(null, 0, 2, 2)
        : base(null, containerId, 2, 2)
    {
        OwnerInventory = inventory;
        //0 结果槽 取走时要结算合成格材料
        AddSlot(new ResultSlot(this, ResultSlots, 0, 154, 28));
        //1-4 合成格 2x2
        for (var i = 0; i < 4; i++)
            AddSlot(new Slot(CraftSlots, i, 98 + i % 2 * 18, 18 + i / 2 * 18));
        //5-8 护甲 容器下标 39 头盔 递减到 36 靴子
        for (var i = 0; i < PlayerInventory.ArmorSlots; i++)
            AddSlot(new Slot(inventory, 39 - i, 8, 8 + i * 18));
        //9-35 主物品栏
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i, 8 + i % 9 * 18, 84 + i / 9 * 18));
        //36-44 快捷栏
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, 142));
        //45 副手
        AddSlot(new Slot(inventory, PlayerInventory.OffhandSlot, 77, 62));
    }

    public override bool StillValid(ServerPlayer player) => true;

    //QuickMoveStack 快捷搬运 在原版分区之间搬运: 合成/护甲/副手 -> 背包 背包 <-> 快捷栏
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        if (slotIndex == ResultSlotIndex)
        {
            //搬走了才结算材料 背包塞不下(搬不动)不该白扣 与原版 moveItemStackTo 失败即返回一致
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
