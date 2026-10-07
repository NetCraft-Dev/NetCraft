using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//ResultSlot crafting result slot, maps to vanilla net.minecraft.world.inventory.ResultSlot
//The only difference from a normal slot is on take: taking one result consumes the crafting grid ingredients and recomputes
public sealed class ResultSlot : Slot
{
    private readonly AbstractCraftingMenu _menu;

    public ResultSlot(AbstractCraftingMenu menu, Container container, int slotIndex, int x, int y)
        : base(container, slotIndex, x, y) => _menu = menu;

    //Remove consumes ingredients immediately after the result is taken, maps to vanilla ResultSlot.onTake
    public override ItemStack Remove(int count)
    {
        var taken = base.Remove(count);
        if (!taken.IsEmpty()) _menu.OnCraftingTaken();
        return taken;
    }
}
