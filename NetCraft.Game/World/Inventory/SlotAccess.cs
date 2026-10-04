using NetCraft.Game.Server;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//SlotAccess 一个可读写的槽位引用 对应原版 net.minecraft.world.entity.SlotAccess
//item 命令靠它取放物品 不可访问的位置统一用 Null 表示
public abstract class SlotAccess
{
    //Null 不可访问的槽位 读为空栈写恒失败 对应原版 SlotAccess.NULL
    public static readonly SlotAccess Null = new NullAccess();

    //Get 读当前内容 空槽返回 ItemStack.Empty
    public abstract ItemStack Get();

    //Set 写回内容 返回是否写入成功
    public abstract bool Set(ItemStack stack);

    //ForContainer 容器槽位 越界给空槽 对应原版 forContainer
    public static SlotAccess ForContainer(Container container, int index)
        => (uint)index < (uint)container.Size ? new ContainerAccess(container, index) : Null;

    //ForPlayer 按原版实体槽位号取玩家身上的槽 对应原版 Player.getSlot 与 LivingEntity.getSlot
    //0..40 就是背包本身(0..8 快捷栏 9..35 背包 36..39 护甲 40 副手)
    //98 主手落到当前选中的快捷栏格 101..104 护甲按 脚->腿->胸->头 对应背包 36..39
    //103 同时是 weapon.offhand 与 armor.chest 原版按 EquipmentSlot 声明序先命中 OFFHAND 这里照做
    //499 是 player.cursor 落到当前菜单的光标物品
    //其余号段(末影箱/生物背包/骑马/鞍)本作没有对应容器 一律给空槽
    public static SlotAccess ForPlayer(ServerPlayer player, int slotId)
    {
        var inventory = player.Inventory;
        if (slotId is >= 0 and <= PlayerInventory.TotalSize - 1) return ForContainer(inventory, slotId);
        return slotId switch
        {
            SlotRanges.MainHand => ForContainer(inventory, inventory.SelectedSlot),
            SlotRanges.Feet => ForContainer(inventory, 36),
            SlotRanges.Legs => ForContainer(inventory, 37),
            SlotRanges.OffHand => ForContainer(inventory, 40),
            SlotRanges.Head => ForContainer(inventory, 39),
            499 => player.ContainerMenu is { } menu ? new CarriedAccess(menu) : Null,
            _ => Null,
        };
    }

    //NullAccess 空实现
    private sealed class NullAccess : SlotAccess
    {
        public override ItemStack Get() => ItemStack.Empty;
        public override bool Set(ItemStack stack) => false;
    }

    //ContainerAccess 容器槽位引用
    private sealed class ContainerAccess : SlotAccess
    {
        private readonly Container _container;
        private readonly int _index;

        public ContainerAccess(Container container, int index)
        {
            _container = container;
            _index = index;
        }

        public override ItemStack Get() => _container.GetItem(_index);

        public override bool Set(ItemStack stack)
        {
            _container.SetItem(_index, stack);
            _container.SetChanged();
            return true;
        }
    }

    //CarriedAccess 菜单光标物品引用
    private sealed class CarriedAccess : SlotAccess
    {
        private readonly AbstractContainerMenu _menu;

        public CarriedAccess(AbstractContainerMenu menu) => _menu = menu;

        public override ItemStack Get() => _menu.Carried;

        public override bool Set(ItemStack stack)
        {
            _menu.SetCarried(stack);
            return true;
        }
    }
}
