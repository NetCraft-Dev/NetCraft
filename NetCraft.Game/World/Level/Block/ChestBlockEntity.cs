using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//ChestBlockEntity 箱子方块实体对应原版 net.minecraft.world.level.block.entity.ChestBlockEntity
//27 格容器 内容随区块落盘 右击由 ChestMenu 打开
public class ChestBlockEntity : BlockEntity, Container, MenuProvider
{
    //ChestSize 箱子槽位数 9x3
    public const int ChestSize = 27;

    //ContainerDistanceSqr 菜单失效距离平方 原版 8 格
    private const double ContainerDistanceSqr = 64.0;

    private readonly SimpleContainer _items = new(ChestSize);

    public ChestBlockEntity(BlockPos pos) : base(BlockEntityTypes.CHEST, pos) { }

    //供木桶等同类容器复用 类型不同行为一致 对应原版 BarrelBlockEntity 继承 ChestBlockEntity
    protected ChestBlockEntity(BlockEntityType type, BlockPos pos) : base(type, pos) { }

    //DisplayName 界面标题 对应原版 getDefaultName
    public virtual Component DisplayName => Component.Translatable("container.chest");

    //CreateMenu 构造箱式菜单 对应原版 createMenu
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

    //StillValid 菜单是否仍然有效 方块还在原位且玩家在 8 格内 对应原版 Container.stillValidBlockEntity
    public bool StillValid(ServerPlayer player)
    {
        if (Level is not ServerLevel level) return false;
        if (level.GetBlockEntity<ChestBlockEntity>(Pos) is not { } current) return false;
        if (!ReferenceEquals(current, this)) return false;
        var center = new Vec3(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
        return player.Position.DistanceToSqr(center) <= ContainerDistanceSqr;
    }

    //SaveAdditional 槽位按 Slot 加物品写出 空槽不写 对应原版 ContainerHelper.saveAllItems
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

    //LoadAdditional 读回槽位 越界槽号与未知物品由 SetItem 与 ReadNbt 各自挡掉
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

    //OnRemoved 被移出世界前把内容物丢在原地 对应原版 BaseContainerBlockEntity.preRemoveSideEffects
    //只有方块被替换才会走到这里 区块卸载不走该回调
    public override void OnRemoved()
    {
        if (Level is PersistentServerLevel level) Containers.DropContents(level, Pos, this);
    }
}

//BarrelBlockEntity 木桶方块实体 行为与箱子一致只是类型与标题不同 对应原版 BarrelBlockEntity
public sealed class BarrelBlockEntity : ChestBlockEntity
{
    public BarrelBlockEntity(BlockPos pos) : base(BlockEntityTypes.BARREL, pos) { }

    public override Component DisplayName => Component.Translatable("container.barrel");
}
