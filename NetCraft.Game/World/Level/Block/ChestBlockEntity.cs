using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//ChestBlockEntity chest block entity, maps to vanilla net.minecraft.world.level.block.entity.ChestBlockEntity
//27-slot container; contents persist with the chunk, opened by ChestMenu on right-click
public class ChestBlockEntity : BlockEntity, Container, MenuProvider
{
    //ChestSize chest slot count, 9x3
    public const int ChestSize = 27;

    //ContainerDistanceSqr squared menu invalidation distance, vanilla 8 blocks
    private const double ContainerDistanceSqr = 64.0;

    private readonly SimpleContainer _items = new(ChestSize);

    public ChestBlockEntity(BlockPos pos) : base(BlockEntityTypes.CHEST, pos) { }

    //Reused by barrels and similar containers; same behavior, different type, maps to vanilla BarrelBlockEntity extending ChestBlockEntity
    protected ChestBlockEntity(BlockEntityType type, BlockPos pos) : base(type, pos) { }

    //DisplayName screen title, maps to vanilla getDefaultName
    public virtual Component DisplayName => Component.Translatable("container.chest");

    //CreateMenu builds the chest-style menu, maps to vanilla createMenu
    public virtual AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
        => ChestMenu.ThreeRows(containerId, inventory, this);

    public int Size => _items.Size;

    public ItemStack GetItem(int slot) => _items.GetItem(slot);

    public void SetItem(int slot, ItemStack stack) => _items.SetItem(slot, stack);

    public ItemStack RemoveItem(int slot, int count) => _items.RemoveItem(slot, count);

    public ItemStack RemoveItemNoUpdate(int slot) => _items.RemoveItemNoUpdate(slot);

    public void SetChanged() => _items.SetChanged();

    public bool IsEmpty() => _items.IsEmpty();

    public void ClearContent() => _items.ClearContent();

    public bool CanPlaceItem(int slot, ItemStack stack) => true;

    //StillValid whether the menu is still valid: block still in place and player within 8 blocks, maps to vanilla Container.stillValidBlockEntity
    public bool StillValid(ServerPlayer player)
    {
        if (Level is not ServerLevel level) return false;
        if (level.GetBlockEntity<ChestBlockEntity>(Pos) is not { } current) return false;
        if (!ReferenceEquals(current, this)) return false;
        var center = new Vec3(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
        return player.Position.DistanceToSqr(center) <= ContainerDistanceSqr;
    }

    //SaveAdditional writes slots as Slot plus item, empty slots omitted, maps to vanilla ContainerHelper.saveAllItems
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

    //LoadAdditional reads slots back; out-of-range slot ids and unknown items are filtered by SetItem and ReadNbt respectively
    public override void LoadAdditional(CompoundTag tag)
    {
        base.LoadAdditional(tag);
        _items.ClearContent();
        if (tag.GetList("Items") is not { } items) return;
        foreach (var element in items)
        {
            if (element is not CompoundTag entry) continue;
            var slot = entry.GetByteOr("Slot", (byte)ChestSize);
            if (slot >= _items.Size) continue;
            _items.SetItem(slot, ItemStack.ReadNbt(entry.GetCompound("item")));
        }
    }

    //OnRemoved drops the contents in place before removal, maps to vanilla BaseContainerBlockEntity.preRemoveSideEffects
    //Only reached when the block is replaced; chunk unload does not invoke this callback
    public override void OnRemoved()
    {
        if (Level is PersistentServerLevel level) Containers.DropContents(level, Pos, this);
    }
}

//BarrelBlockEntity barrel block entity, behavior identical to a chest with different type and title, maps to vanilla BarrelBlockEntity
public sealed class BarrelBlockEntity : ChestBlockEntity
{
    public BarrelBlockEntity(BlockPos pos) : base(BlockEntityTypes.BARREL, pos) { }

    public override Component DisplayName => Component.Translatable("container.barrel");
}
