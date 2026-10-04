using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//V-8 作物类方块按原版逐个移植形状 生长与骨粉行为留后续批次
//属性一律取内嵌方块表注入的那份 这里不重复声明
//命名空间段名 Block 与 Registry.Block 类型同名 形状助手要写完全限定名
public static partial class Blocks
{
    public static readonly WheatBlock WHEAT = new("wheat");
    public static readonly CarrotBlock CARROTS = new("carrots");
    public static readonly PotatoBlock POTATOES = new("potatoes");
    public static readonly BeetrootBlock BEETROOTS = new("beetroots");
    public static readonly TorchflowerCropBlock TORCHFLOWER_CROP = new("torchflower_crop");
    public static readonly NetherWartBlock NETHER_WART = new("nether_wart");

    //RegisterCrops 作物类方块登记进真实方块表
    private static void RegisterCrops(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { WHEAT, CARROTS, POTATOES, BEETROOTS, TORCHFLOWER_CROP, NETHER_WART };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //CropBlock 作物基类 形状按成熟度逐级长高 各作物给各自的形状表与年龄属性
    //落脚面走 supports_crops 标签 支撑没了自己消失 对应原版 CropBlock 继承 VegetationBlock
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

    //WheatBlock 小麦 每升一级长高两像素
    public sealed class WheatBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(7, age => NetCraft.Registry.Block.Column(16.0, 0.0, 2 + (age * 2)));

        public WheatBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age7;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //CarrotBlock 胡萝卜 每升一级只长一像素 与小麦不同
    public sealed class CarrotBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(7, age => NetCraft.Registry.Block.Column(16.0, 0.0, 2 + age));

        public CarrotBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age7;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //PotatoBlock 马铃薯 与胡萝卜同高
    public sealed class PotatoBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(7, age => NetCraft.Registry.Block.Column(16.0, 0.0, 2 + age));

        public PotatoBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age7;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //BeetrootBlock 甜菜根 只有四级 每级长高两像素
    public sealed class BeetrootBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(3, age => NetCraft.Registry.Block.Column(16.0, 0.0, 2 + (age * 2)));

        public BeetrootBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age3;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //TorchflowerCropBlock 火把花作物 只有两级 成熟后长成六像素宽的柱
    public sealed class TorchflowerCropBlock : CropBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(1, age => NetCraft.Registry.Block.Column(6.0, 0.0, 6 + (age * 4)));

        public TorchflowerCropBlock(string name) : base(name) { }

        protected override IntegerProperty AgeProperty => BlockStateProperties.Age1;

        protected override VoxelShape[] Shapes => ShapeTable;
    }

    //NetherWartBlock 下界疣 原版继承植被类不是作物类 形状自成一档 落脚面走它自己那条标签
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
