using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Inventory;

//ContainerHelper common container operations, maps to vanilla net.minecraft.world.ContainerHelper
//The dispenser's container transfer and future hoppers share the insert logic here
public static class ContainerHelper
{
    //AddItem inserts a stack into the target container, returns the remainder that did not fit
    //Fills matching stackable slots first, then finds empty ones, same two-pass order as vanilla HopperBlockEntity.addItem
    public static ItemStack AddItem(Container container, ItemStack stack)
    {
        if (stack.IsEmpty()) return stack;
        var remaining = FillExistingSlots(container, stack);
        if (!remaining.IsEmpty()) remaining = FillEmptySlots(container, remaining);
        container.SetChanged();
        return remaining;
    }

    //FillExistingSlots stacks into existing matching slots first
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

    //FillEmptySlots then places the whole stack into empty slots
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

//Containers utilities for container-world interaction, maps to vanilla net.minecraft.world.Containers
public static class Containers
{
    //DropContents drops the container contents slot by slot at the block position, maps to vanilla dropContents
    //Called when a container block entity is removed, maps to vanilla BaseContainerBlockEntity.preRemoveSideEffects
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

    //UpdateNeighboursAfterDestroy notifies neighbours to re-evaluate after a block is removed, maps to vanilla updateNeighboursAfterDestroy
    //A container may feed a comparator, so it must recompute after being broken
    public static void UpdateNeighboursAfterDestroy(BlockState state, ServerLevel level, BlockPos pos)
        => level.UpdateNeighborsAt(pos, state.Owner);
}
