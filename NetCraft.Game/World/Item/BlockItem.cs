using NetCraft.Registry;

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
}
