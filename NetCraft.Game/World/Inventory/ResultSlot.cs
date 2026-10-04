using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//ResultSlot 合成结果槽对应原版 net.minecraft.world.inventory.ResultSlot
//与普通槽的差别只在取走: 取走一份成品要扣掉合成格里的材料再重算结果
public sealed class ResultSlot : Slot
{
    private readonly AbstractCraftingMenu _menu;

    public ResultSlot(AbstractCraftingMenu menu, Container container, int slotIndex, int x, int y)
        : base(container, slotIndex, x, y) => _menu = menu;

    //Remove 取走成品后立刻结算材料 对应原版 ResultSlot.onTake
    public override ItemStack Remove(int count)
    {
        var taken = base.Remove(count);
        if (!taken.IsEmpty()) _menu.OnCraftingTaken();
        return taken;
    }
}
