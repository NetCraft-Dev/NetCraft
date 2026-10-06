using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//V-8 crop blocks ported shape by shape from vanilla; growth and bone meal behavior left for a later batch
//Properties always come from the ones injected by the embedded block table; not redeclared here
//The namespace segment Block clashes with the Registry.Block type, so shape helpers must be fully qualified
public static partial class Blocks
{
    public static readonly WheatBlock WHEAT = new("wheat");
    public static readonly CarrotBlock CARROTS = new("carrots");
    public static readonly PotatoBlock POTATOES = new("potatoes");
    public static readonly BeetrootBlock BEETROOTS = new("beetroots");
    public static readonly TorchflowerCropBlock TORCHFLOWER_CROP = new("torchflower_crop");
    public static readonly NetherWartBlock NETHER_WART = new("nether_wart");

    //RegisterCrops registers crop blocks into the real block table
    private static void RegisterCrops(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { WHEAT, CARROTS, POTATOES, BEETROOTS, TORCHFLOWER_CROP, NETHER_WART };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //CropBlock crop base class; shapes grow taller with each maturity level, each crop supplies its own shape table and age property
    //The support face uses the supports_crops tag and the block disappears when support is lost, maps to vanilla CropBlock extending VegetationBlock
    public abstract class CropBlock : VegetationBlock
    {
        protected CropBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsCrops;

        protected abstract IntegerProperty AgeProperty { get; }

        protected abstract VoxelShape[] Shapes { get; }

        public int Age(BlockState state) => state.GetValue(AgeProperty);

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shapes[Age(state)];
    }

    //WheatBlock wheat; grows two pixels taller per level
    public sealed class WheatBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(7, age => NetCraft.Registry.Block.Column(16.0, 0.0, 2 + (age * 2)));

        public WheatBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age7;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //CarrotBlock carrots; grows only one pixel per level, unlike wheat
    public sealed class CarrotBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(7, age => NetCraft.Registry.Block.Column(16.0, 0.0, 2 + age));

        public CarrotBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age7;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //PotatoBlock potatoes; same height as carrots
    public sealed class PotatoBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(7, age => NetCraft.Registry.Block.Column(16.0, 0.0, 2 + age));

        public PotatoBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age7;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //BeetrootBlock beetroots; only four levels, two pixels taller per level
    public sealed class BeetrootBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(3, age => NetCraft.Registry.Block.Column(16.0, 0.0, 2 + (age * 2)));

        public BeetrootBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age3;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //TorchflowerCropBlock torchflower crop; only two levels, maturing into a six-pixel-wide column
    public sealed class TorchflowerCropBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(1, age => NetCraft.Registry.Block.Column(6.0, 0.0, 6 + (age * 4)));

        public TorchflowerCropBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age1;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //NetherWartBlock nether wart; vanilla extends the vegetation class rather than the crop class, has its own shape tier, and uses its own support tag
    public sealed class NetherWartBlock : VegetationBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(3, age => NetCraft.Registry.Block.Column(16.0, 0.0, 5 + (age * 3)));

        public NetherWartBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsNetherWart;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => ShapeTable[state.GetValue(BlockStateProperties.Age3)];
    }
}
