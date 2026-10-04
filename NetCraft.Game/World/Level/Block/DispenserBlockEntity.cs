using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//DispenserBlockEntity 发射器方块实体 对应原版 net.minecraft.world.level.block.entity.DispenserBlockEntity
//九格容器 发射时从随机一个非空槽取一件 内容物随区块落盘 右击由 DispenserMenu 打开
public class DispenserBlockEntity : BlockEntity, Container, MenuProvider
{
    //ContainerSize 发射器槽位数 九格
    public const int ContainerSize = 9;

    //ContainerDistanceSqr 菜单失效距离平方 原版 8 格
    private const double ContainerDistanceSqr = 64.0;

    private readonly SimpleContainer _items = new(ContainerSize);

    public DispenserBlockEntity(BlockPos pos) : base(BlockEntityTypes.DISPENSER, pos) { }

    //供投掷器复用 类型与标题不同行为一致 对应原版 DropperBlockEntity 继承 DispenserBlockEntity
    protected DispenserBlockEntity(BlockEntityType type, BlockPos pos) : base(type, pos) { }

    //DisplayName 界面标题 对应原版 getDefaultName
    public virtual Component DisplayName => Component.Translatable("container.dispenser");

    //CreateMenu 构造九格菜单 对应原版 createMenu
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

    //GetRandomSlot 随机挑一个非空槽 每个槽被选中的机会与它前面的空槽数成反比
    //等价于在所有非空槽里等概率取一个 对应原版 getRandomSlot
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

    //InsertItem 往自己容器里塞物品 返回没塞下的剩余 对应原版 insertItem
    //发射行为丢出的剩余物会先试着放回发射器
    public ItemStack InsertItem(ItemStack stack) => ContainerHelper.AddItem(this, stack);

    //StillValid 方块还在原位且玩家在 8 格内才有效 对应原版 Container.stillValidBlockEntity
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

    //OnRemoved 被移出世界前把内容物丢在原地 对应原版 preRemoveSideEffects
    public override void OnRemoved()
    {
        if (Level is PersistentServerLevel level) Containers.DropContents(level, Pos, this);
    }
}

//DropperBlockEntity 投掷器方块实体 行为与发射器一致只是类型与标题不同 对应原版 DropperBlockEntity
public sealed class DropperBlockEntity : DispenserBlockEntity
{
    public DropperBlockEntity(BlockPos pos) : base(BlockEntityTypes.DROPPER, pos) { }

    public override Component DisplayName => Component.Translatable("container.dropper");
}
