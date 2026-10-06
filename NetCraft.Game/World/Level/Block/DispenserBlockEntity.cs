using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//DispenserBlockEntity dispenser block entity, maps to vanilla net.minecraft.world.level.block.entity.DispenserBlockEntity
//Nine-slot container; dispensing takes one item from a random non-empty slot, contents persist with the chunk, opened by DispenserMenu on right-click
public class DispenserBlockEntity : BlockEntity, Container, MenuProvider
{
    //ContainerSize dispenser slot count, nine
    public const int ContainerSize = 9;

    //ContainerDistanceSqr squared menu invalidation distance, vanilla 8 blocks
    private const double ContainerDistanceSqr = 64.0;

    private readonly SimpleContainer _items = new(ContainerSize);

    public DispenserBlockEntity(BlockPos pos) : base(BlockEntityTypes.DISPENSER, pos) { }

    //Reused by droppers; same behavior, different type and title, maps to vanilla DropperBlockEntity extending DispenserBlockEntity
    protected DispenserBlockEntity(BlockEntityType type, BlockPos pos) : base(type, pos) { }

    //DisplayName screen title, maps to vanilla getDefaultName
    public virtual Component DisplayName => Component.Translatable("container.dispenser");

    //CreateMenu builds the nine-slot menu, maps to vanilla createMenu
    public virtual AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
        => DispenserMenu.Create(containerId, inventory, this);

    public int Size => _items.Size;

    public ItemStack GetItem(int slot) => _items.GetItem(slot);

    public void SetItem(int slot, ItemStack stack) => _items.SetItem(slot, stack);

    public ItemStack RemoveItem(int slot, int count) => _items.RemoveItem(slot, count);

    public ItemStack RemoveItemNoUpdate(int slot) => _items.RemoveItemNoUpdate(slot);

    public void SetChanged() => _items.SetChanged();

    public bool IsEmpty() => _items.IsEmpty();

    public void ClearContent() => _items.ClearContent();

    public bool CanPlaceItem(int slot, ItemStack stack) => true;

    //GetRandomSlot picks a random non-empty slot; each slot's chance of being chosen is inversely proportional to the number of empty slots before it
    //Equivalent to picking uniformly among all non-empty slots, maps to vanilla getRandomSlot
    public int GetRandomSlot(Random random)
    {
        var slot = -1;
        var odds = 1;
        for (var i = 0; i < _items.Size; i++)
        {
            if (_items.GetItem(i).IsEmpty() || random.Next(odds++) != 0) continue;
            slot = i;
        }
        return slot;
    }

    //InsertItem inserts items into its own container, returns the remainder that did not fit, maps to vanilla insertItem
    //Leftovers dropped by a dispense behavior are first tried back into the dispenser
    public ItemStack InsertItem(ItemStack stack) => ContainerHelper.AddItem(this, stack);

    //StillValid valid only when the block is still in place and the player is within 8 blocks, maps to vanilla Container.stillValidBlockEntity
    public bool StillValid(ServerPlayer player)
    {
        if (Level is not ServerLevel level) return false;
        if (level.GetBlockEntity<DispenserBlockEntity>(Pos) is not { } current) return false;
        if (!ReferenceEquals(current, this)) return false;
        var center = new Vec3(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
        return player.Position.DistanceToSqr(center) <= ContainerDistanceSqr;
    }

    public override void SaveAdditional(CompoundTag tag)
    {
        base.SaveAdditional(tag);
        var items = new ListTag();
        for (var i = 0; i < _items.Size; i++)
        {
            var stack = _items.GetItem(i);
            if (stack.IsEmpty()) continue;
            var entry = new CompoundTag();
            entry.PutByte("Slot", (byte)i);
            ItemStack.WriteNbt(entry, "item", stack);
            items.Add(entry);
        }
        tag.Put("Items", items);
    }

    public override void LoadAdditional(CompoundTag tag)
    {
        base.LoadAdditional(tag);
        _items.ClearContent();
        if (tag.GetList("Items") is not { } items) return;
        foreach (var element in items)
        {
            if (element is not CompoundTag entry) continue;
            var slot = entry.GetByteOr("Slot", (byte)ContainerSize);
            if (slot >= _items.Size) continue;
            _items.SetItem(slot, ItemStack.ReadNbt(entry.GetCompound("item")));
        }
    }

    //OnRemoved drops the contents in place before removal, maps to vanilla preRemoveSideEffects
    public override void OnRemoved()
    {
        if (Level is PersistentServerLevel level) Containers.DropContents(level, Pos, this);
    }
}

//DropperBlockEntity dropper block entity, behavior identical to a dispenser with different type and title, maps to vanilla DropperBlockEntity
public sealed class DropperBlockEntity : DispenserBlockEntity
{
    public DropperBlockEntity(BlockPos pos) : base(BlockEntityTypes.DROPPER, pos) { }

    public override Component DisplayName => Component.Translatable("container.dropper");
}
