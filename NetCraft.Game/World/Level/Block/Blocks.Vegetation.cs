using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using DoubleBlockHalf = NetCraft.Registry.Enums.DoubleBlockHalf;

namespace NetCraft.Game.World.Level.Block;

//V-8 植被类方块按原版逐个移植形状 生长与放置行为留后续批次
//属性一律取内嵌方块表注入的那份 这里不重复声明 抄漏一个状态数就错
//命名空间段名 Block 与 Registry.Block 类型同名 形状助手要写完全限定名
public static partial class Blocks
{
    public static readonly TallGrassBlock SHORT_GRASS = new("short_grass");
    public static readonly TallGrassBlock FERN = new("fern");
    public static readonly DryVegetationBlock DEAD_BUSH = new("dead_bush");
    public static readonly BushBlock BUSH = new("bush");
    public static readonly ShortDryGrassBlock SHORT_DRY_GRASS = new("short_dry_grass");
    public static readonly TallDryGrassBlock TALL_DRY_GRASS = new("tall_dry_grass");
    public static readonly SeagrassBlock SEAGRASS = new("seagrass");
    public static readonly TallSeagrassBlock TALL_SEAGRASS = new("tall_seagrass");
    public static readonly SugarCaneBlock SUGAR_CANE = new("sugar_cane");
    public static readonly LilyPadBlock LILY_PAD = new("lily_pad");
    public static readonly NetherSproutsBlock NETHER_SPROUTS = new("nether_sprouts");
    public static readonly SporeBlossomBlock SPORE_BLOSSOM = new("spore_blossom");
    public static readonly AzaleaBlock AZALEA = new("azalea");
    public static readonly AzaleaBlock FLOWERING_AZALEA = new("flowering_azalea");
    public static readonly SmallDripleafBlock SMALL_DRIPLEAF = new("small_dripleaf");
    public static readonly HangingRootsBlock HANGING_ROOTS = new("hanging_roots");
    public static readonly CaveVinesBlock CAVE_VINES = new("cave_vines");
    public static readonly CaveVinesBlock CAVE_VINES_PLANT = new("cave_vines_plant");
    public static readonly KelpBlock KELP = new("kelp");
    public static readonly HangingMossBlock PALE_HANGING_MOSS = new("pale_hanging_moss");
    public static readonly SweetBerryBushBlock SWEET_BERRY_BUSH = new("sweet_berry_bush");

    //RegisterVegetation 植被类方块登记进真实方块表 键取注册名
    private static void RegisterVegetation(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks =
        {
            SHORT_GRASS, FERN, DEAD_BUSH, BUSH, SHORT_DRY_GRASS, TALL_DRY_GRASS,
            SEAGRASS, TALL_SEAGRASS, SUGAR_CANE, LILY_PAD, NETHER_SPROUTS, SPORE_BLOSSOM,
            AZALEA, FLOWERING_AZALEA, SMALL_DRIPLEAF, HANGING_ROOTS,
            CAVE_VINES, CAVE_VINES_PLANT, KELP, PALE_HANGING_MOSS, SWEET_BERRY_BUSH,
        };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //NamedBlock 只带注册名的方块基类 同一种形状对应多个注册名时靠它区分
    //植被与装饰类大量如此 十六色地毯与各色珊瑚都是同一个类的不同注册名
    public abstract class NamedBlock : BlockBehaviour
    {
        private readonly string _name;

        protected NamedBlock(string name) => _name = name;

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);
    }

    //VegetationBlock 植被与作物的共同基类 对应原版 VegetationBlock
    //默认要求下方那格属于 supports_vegetation 支撑没了就自删
    //干枯类换 supports_dry_vegetation 作物换 supports_crops 其余按各自的 supports_* 覆写 SupportTag
    //26.2 把"能种在什么上面"从代码分支挪到了标签 这里跟着走同一套
    public abstract class VegetationBlock : NamedBlock
    {
        protected VegetationBlock(string name) : base(name) { }

        //SupportTag 下方那格要属于哪条标签 默认是普通植被那条
        protected virtual NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsVegetation;

        //MayPlaceOn 下方那格能不能托住本方块 对应原版 mayPlaceOn
        protected virtual bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
            => belowState.Owner is BlockBehaviour behaviour && behaviour.IsInTag(SupportTag);

        //CanSurvive 只看下方一格 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            return level.GetBlockState(below) is { } belowState && MayPlaceOn(level, below, belowState);
        }

        //UpdateShape 支撑没了就自删 对应原版 updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (!CanSurvive(level, pos, state)) return AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }
    }

    //TallGrassBlock 矮草与蕨 原版同一个类挂两个注册名
    public sealed class TallGrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 13.0);

        public TallGrassBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DryVegetationBlock 枯灌木 落脚面换干枯那条标签
    public sealed class DryVegetationBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 13.0);

        public DryVegetationBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsDryVegetation;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BushBlock 灌木 原版这一族植物的形状基类 本作按注册名扁平使用
    public sealed class BushBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 13.0);

        public BushBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //ShortDryGrassBlock 矮枯草 同枯灌木走干枯标签
    public sealed class ShortDryGrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 10.0);

        public ShortDryGrassBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsDryVegetation;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //TallDryGrassBlock 高枯草 同枯灌木走干枯标签
    public sealed class TallDryGrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public TallDryGrassBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsDryVegetation;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SeagrassBlock 海草 落脚面要够坚固且不是岩浆块
    public sealed class SeagrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 12.0);

        public SeagrassBlock(string name) : base(name) { }

        //MayPlaceOn 下方那格朝上的面要够坚固 岩浆块托不住 对应原版 mayPlaceOn
        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
            => belowState.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, belowPos, belowState, Direction.Up)
                && !behaviour.IsInTag(BlockTags.CannotSupportSeagrass);

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //TallSeagrassBlock 高海草 双格 下半还要泡在满水里
    public sealed class TallSeagrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 16.0);

        public TallSeagrassBlock(string name) : base(name) { }

        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
            => belowState.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, belowPos, belowState, Direction.Up)
                && !behaviour.IsInTag(BlockTags.CannotSupportSeagrass);

        //CanSurvive 上半只认自己的下半 下半看落脚面且所在格得是满水 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (state.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.upper)
            {
                var below = pos.Offset(Direction.Down);
                return level.GetBlockState(below) is { } belowState
                    && ReferenceEquals(belowState.Owner, this)
                    && belowState.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.lower;
            }
            var fluid = level.GetBlockState(pos)?.FluidState;
            return base.CanSurvive(level, pos, state) && fluid is { IsWater: true, IsFull: true };
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SugarCaneBlock 甘蔗 下方是同类就能叠 否则要落在沙土上且四邻傍着水或霜冰
    public sealed class SugarCaneBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 16.0);
        private static readonly Direction[] Horizontals =
            { Direction.North, Direction.South, Direction.West, Direction.East };

        public SugarCaneBlock(string name) : base(name) { }

        //CanSurvive 下方同类直接过 落在地里时下方四邻要有水或霜冰 对应原版 canSurvive
        //水那条走原版的 supports_sugar_cane_adjacently 流体标签 内容就是水 这里按水判
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var belowPos = pos.Offset(Direction.Down);
            if (level.GetBlockState(belowPos) is not { } belowState) return false;
            if (ReferenceEquals(belowState.Owner, this)) return true;
            if (belowState.Owner is not BlockBehaviour belowBehaviour
                || !belowBehaviour.IsInTag(BlockTags.SupportsSugarCane)) return false;
            foreach (var direction in Horizontals)
            {
                var neighbourPos = belowPos.Offset(direction);
                if (level.GetBlockState(neighbourPos) is not { } neighbour) continue;
                if (neighbour.FluidState.IsWater) return true;
                if (neighbour.Owner is BlockBehaviour behaviour
                    && behaviour.IsInTag(BlockTags.SupportsSugarCaneAdjacently)) return true;
            }
            return false;
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //LilyPadBlock 睡莲 薄片且浮在水面高度上
    public sealed class LilyPadBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 1.5);

        public LilyPadBlock(string name) : base(name) { }

        //MayPlaceOn 下方是水或 supports_lily_pad 且自己那格不能有流体 对应原版 mayPlaceOn
        //原版流体那条走 supports_lily_pad 标签 内容就是水
        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
        {
            var supported = belowState.FluidState.IsWater
                || (belowState.Owner is BlockBehaviour behaviour
                    && behaviour.IsInTag(BlockTags.SupportsLilyPad));
            if (!supported) return false;
            return level.GetBlockState(belowPos.Offset(Direction.Up))?.FluidState.IsEmpty ?? true;
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //NetherSproutsBlock 下界苗 落脚面走它自己那条标签
    public sealed class NetherSproutsBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 3.0);

        public NetherSproutsBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsNetherSprouts;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SporeBlossomBlock 孢子花 挂在方块下表面 形状贴着格子顶端
    public sealed class SporeBlossomBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 13.0, 16.0);

        public SporeBlossomBlock(string name) : base(name) { }

        //CanSurvive 上方那格朝下的面按中心判定要顶得住 且自己那格不能泡在水里 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var abovePos = pos.Offset(Direction.Up);
            if (level.GetBlockState(abovePos) is not { } aboveState
                || aboveState.Owner is not BlockBehaviour behaviour
                || !behaviour.IsFaceSturdy(EmptyBlockGetter.Instance, abovePos, aboveState,
                    Direction.Down, SupportType.Center))
                return false;
            return level.GetBlockState(pos)?.FluidState.IsWater != true;
        }

        //UpdateShape 上方那格变了就重判 撑不住直接变空气 对应原版 updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Up && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //AzaleaBlock 杜鹃与盛开的杜鹃 上宽下窄两段拼起来 落脚面走杜鹃那条标签
    public sealed class AzaleaBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = Shapes.Or(
            NetCraft.Registry.Block.Column(16.0, 8.0, 16.0),
            NetCraft.Registry.Block.Column(4.0, 0.0, 8.0));

        public AzaleaBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsAzalea;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SmallDripleafBlock 小型垂滴叶 双格
    public sealed class SmallDripleafBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 13.0);

        public SmallDripleafBlock(string name) : base(name) { }

        //MayPlaceOn 下方是 supports_small_dripleaf 就过 否则自己上方那格是水源时按普通植被的落脚面判 对应原版 mayPlaceOn
        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
        {
            if (belowState.Owner is BlockBehaviour behaviour
                && behaviour.IsInTag(BlockTags.SupportsSmallDripleaf)) return true;
            if (level.GetBlockState(belowPos.Offset(Direction.Up))?.FluidState.IsWater != true) return false;
            return base.MayPlaceOn(level, belowPos, belowState);
        }

        //CanSurvive 上半只认自己的下半 下半看落脚面 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (state.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.upper)
            {
                var below = pos.Offset(Direction.Down);
                return level.GetBlockState(below) is { } belowState
                    && ReferenceEquals(belowState.Owner, this)
                    && belowState.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.lower;
            }
            return base.CanSurvive(level, pos, state);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //HangingRootsBlock 垂根 上方那格朝下的面要够坚固
    public sealed class HangingRootsBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 10.0, 16.0);

        public HangingRootsBlock(string name) : base(name) { }

        //CanSurvive 上方那格朝下的面要够坚固 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var abovePos = pos.Offset(Direction.Up);
            return level.GetBlockState(abovePos) is { } aboveState
                && aboveState.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, abovePos, aboveState, Direction.Down);
        }

        //UpdateShape 上方那格变了就重判 撑不住直接变空气 对应原版 updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Up && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //CaveVinesBlock 洞穴藤蔓与藤蔓身 原版两段共用同一形状 从上往下垂
    public sealed class CaveVinesBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public CaveVinesBlock(string name) : base(name) { }

        //CanSurvive 上方是同族藤蔓的任意一截 或者上方那格朝下的面够坚固 对应原版 GrowingPlantBlock.canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var abovePos = pos.Offset(Direction.Up);
            if (level.GetBlockState(abovePos) is not { } aboveState) return false;
            if (aboveState.Owner is CaveVinesBlock) return true;
            return aboveState.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, abovePos, aboveState, Direction.Down);
        }

        //UpdateShape 上方那格变了就重判 撑不住直接变空气 对应原版 GrowingPlantBlock.updateShape
        //原版只排一刻调度刻再删 这里与植被族一致改成即刻自删
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Up && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //KelpBlock 海带 自下往上长
    public sealed class KelpBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 9.0);

        public KelpBlock(string name) : base(name) { }

        //CanSurvive 下方是同族海带 或者下方那格朝上的面够坚固 挡海带的方块一票否决 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var belowPos = pos.Offset(Direction.Down);
            if (level.GetBlockState(belowPos) is not { } belowState) return false;
            if (belowState.Owner is not BlockBehaviour behaviour) return false;
            if (behaviour.IsInTag(BlockTags.CannotSupportKelp)) return false;
            return belowState.Owner is KelpBlock
                || behaviour.IsFaceSturdy(level, belowPos, belowState, Direction.Up);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //HangingMossBlock 垂丝苔藓 末端段比中段底部高两像素
    public sealed class HangingMossBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeBase = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);
        private static readonly VoxelShape ShapeTip = NetCraft.Registry.Block.Column(14.0, 2.0, 16.0);

        public HangingMossBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Tip) ? ShapeTip : ShapeBase;
    }

    //SweetBerryBushBlock 甜浆果丛 幼苗细 长大后高一圈 成熟时是整块
    public sealed class SweetBerryBushBlock : VegetationBlock
    {
        private static readonly VoxelShape ShapeSapling = NetCraft.Registry.Block.Column(10.0, 0.0, 8.0);
        private static readonly VoxelShape ShapeGrowing = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public SweetBerryBushBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Age3) switch
            {
                0 => ShapeSapling,
                3 => Shapes.Block(),
                _ => ShapeGrowing,
            };
    }
}
