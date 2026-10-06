using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//V-8 slab blocks ported shape from vanilla: top slab, bottom slab and double slab; placement/merging logic left for a later batch
//Properties always come from the ones injected by the embedded block table; not redeclared here
public static partial class Blocks
{
    //Slabs have nearly seventy registry names, and the shape only varies with the three type states; declaring each is too long, so build them by looping over a name table
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

    //RegisterSlabs registers slab blocks into the real block table
    private static void RegisterSlabs(Dictionary<string, BlockBehaviour> real)
    {
        foreach (var name in SlabNames) real[name] = new SlabBlock(name);
    }

    //SlabBlock slab; bottom slab occupies 0-8 pixels, top slab 8-16 pixels, double slab the whole block
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
