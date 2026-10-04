using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//V-8 台阶类方块按原版移植形状 上半砖下半砖与双半砖三种 放置合并逻辑留后续批次
//属性一律取内嵌方块表注入的那份 这里不重复声明
public static partial class Blocks
{
    //台阶有近七十个注册名 形状只随 type 三态变化 逐个声明太长 用注册名表循环建
    private static readonly string[] SlabNames =
    {
        "resin_brick_slab", "prismarine_slab", "prismarine_brick_slab", "dark_prismarine_slab",
        "oak_slab", "spruce_slab", "birch_slab", "jungle_slab", "acacia_slab", "cherry_slab",
        "dark_oak_slab", "pale_oak_slab", "mangrove_slab", "bamboo_slab", "bamboo_mosaic_slab",
        "stone_slab", "smooth_stone_slab", "sandstone_slab", "cut_sandstone_slab", "petrified_oak_slab",
        "cobblestone_slab", "brick_slab", "stone_brick_slab", "mud_brick_slab", "nether_brick_slab",
        "quartz_slab", "red_sandstone_slab", "cut_red_sandstone_slab", "purpur_slab",
        "polished_granite_slab", "smooth_red_sandstone_slab", "mossy_stone_brick_slab",
        "polished_diorite_slab", "mossy_cobblestone_slab", "end_stone_brick_slab",
        "smooth_sandstone_slab", "smooth_quartz_slab", "granite_slab", "andesite_slab",
        "red_nether_brick_slab", "polished_andesite_slab", "diorite_slab",
        "crimson_slab", "warped_slab", "blackstone_slab", "polished_blackstone_brick_slab",
        "polished_blackstone_slab", "tuff_slab", "polished_tuff_slab", "tuff_brick_slab",
        "sulfur_slab", "polished_sulfur_slab", "sulfur_brick_slab", "cinnabar_slab",
        "polished_cinnabar_slab", "cinnabar_brick_slab",
        "cut_copper_slab", "exposed_cut_copper_slab", "weathered_cut_copper_slab", "oxidized_cut_copper_slab",
        "waxed_cut_copper_slab", "waxed_exposed_cut_copper_slab", "waxed_weathered_cut_copper_slab",
        "waxed_oxidized_cut_copper_slab",
        "cobbled_deepslate_slab", "polished_deepslate_slab", "deepslate_tile_slab", "deepslate_brick_slab",
    };

    //RegisterSlabs 台阶类方块登记进真实方块表
    private static void RegisterSlabs(Dictionary<string, BlockBehaviour> real)
    {
        foreach (var name in SlabNames) real[name] = new SlabBlock(name);
    }

    //SlabBlock 台阶 下半砖占 0-8 像素 上半砖占 8-16 像素 双半砖就是整块
    public sealed class SlabBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeBottom = NetCraft.Registry.Block.Column(16.0, 0.0, 8.0);
        private static readonly VoxelShape ShapeTop = NetCraft.Registry.Block.Column(16.0, 8.0, 16.0);

        public SlabBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.SlabTypeProperty) switch
            {
                SlabType.top => ShapeTop,
                SlabType.bottom => ShapeBottom,
                _ => Shapes.Block(),
            };
    }
}
