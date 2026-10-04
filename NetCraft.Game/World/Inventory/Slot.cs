using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Registry;

namespace NetCraft.Game.World.Inventory;

//Slot 菜单槽位对应原版 net.minecraft.world.inventory.Slot
//持有后端容器与容器内下标 菜单按登记顺序分配菜单内下标 Index
//X/Y 是 GUI 坐标 服务端不参与逻辑但客户端渲染与协议无关 先保留占位
public class Slot
{
    public Slot(Container container, int slotIndex, int x, int y)
    {
        Container = container;
        SlotIndex = slotIndex;
        X = x;
        Y = y;
    }

    //Container 后端容器
    public Container Container { get; }

    //SlotIndex 容器内下标
    public int SlotIndex { get; }

    //X/Y 槽位在 GUI 中的相对坐标
    public int X { get; }
    public int Y { get; }

    //Index 菜单内槽位号 由 AbstractContainerMenu.AddSlot 写入
    public int Index { get; internal set; } = -1;

    //GetItem 取该槽位物品
    public virtual ItemStack GetItem() => Container.GetItem(SlotIndex);

    //Set 写该槽位物品并触发变更通知
    public virtual void Set(ItemStack stack)
    {
        Container.SetItem(SlotIndex, stack);
        SetChanged();
    }

    //SetChanged 通知容器内容已变更
    public virtual void SetChanged() => Container.SetChanged();

    //HasItem 该槽位是否有物品
    public virtual bool HasItem() => !GetItem().IsEmpty();

    //Remove 移除指定数量
    public virtual ItemStack Remove(int count) => Container.RemoveItem(SlotIndex, count);

    //SafeTake 从槽位安全取走若干 数量同时受 amount 与 maxAmount 约束 对应原版 safeTake
    public virtual ItemStack SafeTake(int amount, int maxAmount, Player player)
    {
        var stack = GetItem();
        var taken = Math.Min(Math.Min(amount, maxAmount), stack.GetCount());
        if (taken <= 0) return ItemStack.Empty;
        var result = stack.Split(taken);
        Set(stack);
        return result;
    }

    //MayPlace 该槽位是否接受该物品
    public virtual bool MayPlace(ItemStack stack) => Container.CanPlaceItem(SlotIndex, stack);

    //GetMaxStackSize 该槽位允许的最大堆叠数
    public virtual int GetMaxStackSize()
    {
        var stack = GetItem();
        return stack.IsEmpty() ? Item.DEFAULT_MAX_STACK_SIZE : stack.GetItem().GetDefaultMaxStackSize();
    }

    //IsSameInventory 两个槽位是否挂在同一个容器上
    public bool IsSameInventory(Slot other) => ReferenceEquals(Container, other.Container);
}
