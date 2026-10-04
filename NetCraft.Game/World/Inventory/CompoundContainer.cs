using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;

namespace NetCraft.Game.World.Inventory;

//CompoundContainer 双箱组合容器对应原版 net.minecraft.world.CompoundContainer
//前一半槽位落在 first 上 其余落在 second 上 两侧共用同一套槽号
public sealed class CompoundContainer(Container first, Container second) : Container
{
    //First/Second 两半容器 原版把右半当第一半 左半当第二半
    public Container First { get; } = first;

    public Container Second { get; } = second;

    public int Size => First.Size + Second.Size;

    public ItemStack GetItem(int slot)
        => slot < First.Size ? First.GetItem(slot) : Second.GetItem(slot - First.Size);

    public void SetItem(int slot, ItemStack stack)
    {
        if (slot < First.Size) First.SetItem(slot, stack);
        else Second.SetItem(slot - First.Size, stack);
    }

    public ItemStack RemoveItem(int slot, int count)
        => slot < First.Size ? First.RemoveItem(slot, count) : Second.RemoveItem(slot - First.Size, count);

    public ItemStack RemoveItemNoUpdate(int slot)
        => slot < First.Size ? First.RemoveItemNoUpdate(slot) : Second.RemoveItemNoUpdate(slot - First.Size);

    //SetChanged 两半都要标记 原版双箱任意一侧变更都要各自落盘
    public void SetChanged()
    {
        First.SetChanged();
        Second.SetChanged();
    }

    public bool IsEmpty() => First.IsEmpty() && Second.IsEmpty();

    public void ClearContent()
    {
        First.ClearContent();
        Second.ClearContent();
    }

    public bool CanPlaceItem(int slot, ItemStack stack)
        => slot < First.Size
            ? First.CanPlaceItem(slot, stack)
            : Second.CanPlaceItem(slot - First.Size, stack);

    //StillValid 两半都在原位且玩家都在 8 格内才有效 对应原版 CompoundContainer.stillValid
    public bool StillValid(ServerPlayer player)
        => First is ChestBlockEntity first && Second is ChestBlockEntity second
            && first.StillValid(player) && second.StillValid(player);
}
