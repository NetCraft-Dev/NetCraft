using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;

namespace NetCraft.Game.World.Inventory;

//CompoundContainer double-chest composite container, maps to vanilla net.minecraft.world.CompoundContainer
//The first half of the slots falls on first, the rest on second, both share one slot numbering
public sealed class CompoundContainer(Container first, Container second) : Container
{
    //First/Second the two halves, vanilla treats the right half as the first and the left half as the second
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

    //SetChanged both halves must be marked, vanilla saves each side of a double chest independently on change
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

    //StillValid valid while both halves remain and the player is within 8 blocks, maps to vanilla CompoundContainer.stillValid
    public bool StillValid(ServerPlayer player)
        => First is ChestBlockEntity first && Second is ChestBlockEntity second
            && first.StillValid(player) && second.StillValid(player);
}
