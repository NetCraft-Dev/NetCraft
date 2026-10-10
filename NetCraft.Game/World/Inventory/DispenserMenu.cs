using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Inventory;

namespace NetCraft.Game.World.Inventory;

//DispenserMenu nine-slot menu for dispensers and droppers, maps to vanilla net.minecraft.world.inventory.DispenserMenu
//The vanilla block has only nine slots but shares the generic_3x3 menu type, the client draws a 3x3 screen from that type
public sealed class DispenserMenu : AbstractContainerMenu
{
    //GridSize side length of the nine-slot grid
    private const int GridSize = 3;

    private readonly Container _container;

    private DispenserMenu(int containerId, PlayerInventory inventory, Container container)
        : base(MenuTypes.GENERIC_3X3, containerId)
    {
        OwnerInventory = inventory;
        _container = container;
        //Pixel positions of the nine slots and the player inventory follow the constants in the vanilla DispenserMenu constructor
        for (var row = 0; row < GridSize; row++)
            for (var column = 0; column < GridSize; column++)
                AddSlot(new Slot(container, column + row * GridSize, 62 + column * 18, 17 + row * 18));
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 9; column++)
                AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + column + row * 9, 8 + column * 18, 84 + row * 18));
        for (var column = 0; column < 9; column++)
            AddSlot(new Slot(inventory, column, 8 + column * 18, 142));
    }

    //Create builds a nine-slot menu
    public static DispenserMenu Create(int containerId, PlayerInventory inventory, Container container)
        => new(containerId, inventory, container);

    //StillValid valid while the container block remains and the player is close enough
    public override bool StillValid(ServerPlayer player) => _container switch
    {
        DispenserBlockEntity dispenser => dispenser.StillValid(player),
        _ => true,
    };

    //QuickMoveStack moves items between the nine-slot container and the inventory, maps to vanilla DispenserMenu.quickMoveStack
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        var containerSlots = GridSize * GridSize;
        if (slotIndex < containerSlots)
        {
            if (!MoveItemStackTo(stack, containerSlots, Slots.Count, true)) return ItemStack.Empty;
        }
        else if (!MoveItemStackTo(stack, 0, containerSlots, false))
        {
            return ItemStack.Empty;
        }
        if (stack.IsEmpty()) slot.Set(ItemStack.Empty);
        else slot.SetChanged();
        return moved;
    }
}
