using NetCraft.Game.World.Items.Component;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Items;

//BlockItem 方块物品对应原版 net.minecraft.world.item.BlockItem
//持有关联方块 use_item_on 放置时取 PlacedBlock 默认状态落位
public sealed class BlockItem : Item
{
    private readonly string _name;

    //PlacedBlock 放置时落位的方块
    public NetCraft.Registry.Block PlacedBlock { get; }

    //WallBlock 贴墙变体 点在水平侧面时落它 对应原版 StandingAndWallBlockItem 的墙变体
    public NetCraft.Registry.Block? WallBlock { get; init; }

    public BlockItem(string name, NetCraft.Registry.Block placedBlock)
    {
        _name = name;
        PlacedBlock = placedBlock;
    }

    public override Identifier Id => Identifier.WithDefaultNamespace(_name);

    //UpdateCustomBlockEntityTag 把物品携带的方块实体数据刷进刚放下的方块实体 对应原版同名静态方法
    //本作注册的方块实体类型都不需要 op 限定 原版那层 onlyOpCanSetNbt 检查略去
    public static bool UpdateCustomBlockEntityTag(PersistentServerLevel level, BlockPos pos, ItemStack itemStack)
    {
        if (itemStack.Get(DataComponents.BLOCK_ENTITY_DATA)
            is not TypedEntityData<Holder<NetCraft.Registry.BlockEntityType<object>>> customData) return false;
        if (level.GetBlockEntity<BlockEntity>(pos) is not { } blockEntity) return false;
        //物品上的数据类型必须与落位方块的方块实体类型一致 否则数据不该往上盖
        if (!Equals(blockEntity.Type, customData.Type.Value)) return false;
        return customData.LoadInto(blockEntity);
    }
}
