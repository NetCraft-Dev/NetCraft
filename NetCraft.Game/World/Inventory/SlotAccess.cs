using NetCraft.Game.Server;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//SlotAccess a readable/writable slot reference, maps to vanilla net.minecraft.world.entity.SlotAccess
//The item command uses it to get and put items, inaccessible positions are uniformly represented by Null
public abstract class SlotAccess
{
    //Null inaccessible slot, reads as empty and writes always fail, maps to vanilla SlotAccess.NULL
    public static readonly SlotAccess Null = new NullAccess();

    //Get reads the current contents, returns ItemStack.Empty for an empty slot
    public abstract ItemStack Get();

    //Set writes contents back, returns whether the write succeeded
    public abstract bool Set(ItemStack stack);

    //ForContainer container slot, out of range gives an empty slot, maps to vanilla forContainer
    public static SlotAccess ForContainer(Container container, int index)
        => (uint)index < (uint)container.Size ? new ContainerAccess(container, index) : Null;

    //ForPlayer resolves a player slot by vanilla entity slot index, maps to vanilla Player.getSlot and LivingEntity.getSlot
    //0..40 is the inventory itself (0..8 hotbar, 9..35 inventory, 36..39 armor, 40 offhand)
    //98 main hand maps to the current hotbar slot, 101..104 armor goes feet->legs->chest->head mapping to inventory 36..39
    //103 is both weapon.offhand and armor.chest; vanilla hits OFFHAND first per EquipmentSlot declaration order, same here
    //499 is player.cursor, resolving to the current menu's carried item
    //Remaining ranges (ender chest / mob inventory / riding / saddle) have no container here and always give an empty slot
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

    //NullAccess empty implementation
    private sealed class NullAccess : SlotAccess
    {
        public override ItemStack Get() => ItemStack.Empty;
        public override bool Set(ItemStack stack) => false;
    }

    //ContainerAccess container slot reference
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

    //CarriedAccess menu carried item reference
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
