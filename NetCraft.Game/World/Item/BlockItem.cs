using NetCraft.Game.World.Items.Component;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Items;

//BlockItem block item, maps to vanilla net.minecraft.world.item.BlockItem
//Holds the associated block; use_item_on places the default state of PlacedBlock
public sealed class BlockItem : Item
{
    private readonly string _name;

    //PlacedBlock the block placed on placement
    public NetCraft.Registry.Block PlacedBlock { get; }

    //WallBlock wall variant placed when clicking a horizontal side, maps to the wall variant of vanilla StandingAndWallBlockItem
    public NetCraft.Registry.Block? WallBlock { get; init; }

    public BlockItem(string name, NetCraft.Registry.Block placedBlock)
    {
        _name = name;
        PlacedBlock = placedBlock;
    }

    public override Identifier Id => Identifier.WithDefaultNamespace(_name);

    //UpdateCustomBlockEntityTag flushes the block entity data carried by the item into the just-placed block entity, maps to the vanilla static method of the same name
    //None of the block entity types registered here need an op restriction, so the vanilla onlyOpCanSetNbt check is skipped
    public static bool UpdateCustomBlockEntityTag(PersistentServerLevel level, BlockPos pos, ItemStack itemStack)
    {
        if (itemStack.Get(DataComponents.BLOCK_ENTITY_DATA)
            is not TypedEntityData<Holder<NetCraft.Registry.BlockEntityType<object>>> customData) return false;
        if (level.GetBlockEntity<BlockEntity>(pos) is not { } blockEntity) return false;
        //The data type on the item must match the placed block's block entity type, otherwise the data must not be applied
        if (!Equals(blockEntity.Type, customData.Type.Value)) return false;
        return customData.LoadInto(blockEntity);
    }
}
