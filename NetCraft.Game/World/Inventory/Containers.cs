using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Inventory;

//ContainerHelper 容器通用操作 对应原版 net.minecraft.world.ContainerHelper
//投掷器的容器搬运与后续漏斗共用这里的插入逻辑
public static class ContainerHelper
{
    //AddItem 把一个物品栈塞进目标容器 返回没放下的剩余
    //先填同类可堆叠的槽再找空槽 与原版 HopperBlockEntity.addItem 的两轮顺序一致
    public static ItemStack AddItem(Container container, ItemStack stack)
    {
        if (stack.IsEmpty()) return stack;
        var remaining = FillExistingSlots(container, stack);
        if (!remaining.IsEmpty()) remaining = FillEmptySlots(container, remaining);
        container.SetChanged();
        return remaining;
    }

    //FillExistingSlots 先往已有的同类槽里叠
    private static ItemStack FillExistingSlots(Container container, ItemStack stack)
    {
        var remaining = stack;
        for (var i = 0; i < container.Size; i++)
        {
            var slot = container.GetItem(i);
            if (slot.IsEmpty() || !slot.IsSameItemAndComponentsAs(remaining)) continue;
            if (!container.CanPlaceItem(i, remaining)) continue;
            var space = slot.GetMaxStackSize() - slot.GetCount();
            if (space <= 0) continue;
            var moved = Math.Min(space, remaining.GetCount());
            container.SetItem(i, slot.CopyWithCount(slot.GetCount() + moved));
            remaining = remaining.CopyWithCount(remaining.GetCount() - moved);
            if (remaining.IsEmpty()) return ItemStack.Empty;
        }
        return remaining;
    }

    //FillEmptySlots 再找空槽整栈放
    private static ItemStack FillEmptySlots(Container container, ItemStack stack)
    {
        var remaining = stack;
        for (var i = 0; i < container.Size; i++)
        {
            if (!container.GetItem(i).IsEmpty()) continue;
            if (!container.CanPlaceItem(i, remaining)) continue;
            var moved = Math.Min(remaining.GetMaxStackSize(), remaining.GetCount());
            container.SetItem(i, remaining.CopyWithCount(moved));
            remaining = remaining.CopyWithCount(remaining.GetCount() - moved);
            if (remaining.IsEmpty()) break;
        }
        return remaining;
    }
}

//Containers 容器与世界交互的工具 对应原版 net.minecraft.world.Containers
public static class Containers
{
    //DropContents 把容器内容物逐槽丢到方块位置 对应原版 dropContents
    //容器方块实体被移除时调它 对应原版 BaseContainerBlockEntity.preRemoveSideEffects
    public static void DropContents(PersistentServerLevel level, BlockPos pos, Container container)
    {
        for (var i = 0; i < container.Size; i++)
        {
            var stack = container.GetItem(i);
            if (stack.IsEmpty()) continue;
            ServerBlockUpdates.SpawnDrop(level, pos, stack);
            container.SetItem(i, ItemStack.Empty);
        }
    }

    //UpdateNeighboursAfterDestroy 方块被移除后通知邻居重新判断 对应原版 updateNeighboursAfterDestroy
    //容器可能是比较器的输入源 拆掉后要让它重算
    public static void UpdateNeighboursAfterDestroy(BlockState state, ServerLevel level, BlockPos pos)
        => level.UpdateNeighborsAt(pos, state.Owner);
}
