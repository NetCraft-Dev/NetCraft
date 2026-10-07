using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//ContainerSynchronizer menu-to-client sync channel, maps to vanilla net.minecraft.world.inventory.ContainerSynchronizer
//The server implementation sends packets directly and the test implementation records calls; the menu depends only on this interface, not on networking
public interface ContainerSynchronizer
{
    //SendSlotChange single slot change
    void SendSlotChange(AbstractContainerMenu menu, int slotIndex, ItemStack stack);

    //SendContentUpdate full content sync (initial sync on join and full refresh)
    void SendContentUpdate(AbstractContainerMenu menu, IReadOnlyList<ItemStack> items, ItemStack carried);

    //SendDataChange container data change (furnace progress / brewing time etc.); the minimal implementation has no producers yet
    void SendDataChange(AbstractContainerMenu menu, int id, int value);
}
