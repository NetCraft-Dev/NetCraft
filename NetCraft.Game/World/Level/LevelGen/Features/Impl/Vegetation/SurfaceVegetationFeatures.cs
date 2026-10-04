using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Game.World.Level.LevelGen.Features.Impl;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;

//VegetationSupport 地表植被特征共用的方块查表 标签判定 属性搬运与平面方向表
//按注册名取方块与状态 免得每个特征各自重复一遍查表与空值兜底
internal static class VegetationSupport
{
    //HorizontalPlane 水平方向表 顺序照原版 Direction.Plane.HORIZONTAL 的声明序
    //倒地树取随机朝向 bonus_chest 与 multiface 逐向遍历都吃这个顺序 顺序错了同种子结果就偏
    public static readonly Direction[] HorizontalPlane =
    {
        Direction.North, Direction.East, Direction.South, Direction.West,
    };

    //LeavesTag 树叶标签 对应原版 BlockTags.LEAVES
    public static readonly TagKey<RegBlock> LeavesTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("leaves"));

    //ReplaceableByTreesTag 可被树木覆盖的方块 对应原版 BlockTags.REPLACEABLE_BY_TREES
    public static readonly TagKey<RegBlock> ReplaceableByTreesTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("replaceable_by_trees"));

    //ReplaceableByMushroomsTag 可被蘑菇覆盖的方块 对应原版 BlockTags.REPLACEABLE_BY_MUSHROOMS
    public static readonly TagKey<RegBlock> ReplaceableByMushroomsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("replaceable_by_mushrooms"));

    //SupportsBambooTag 竹子可种植于其上的方块 对应原版 BlockTags.SUPPORTS_BAMBOO
    public static readonly TagKey<RegBlock> SupportsBambooTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("supports_bamboo"));

    //BeneathBambooPodzolReplaceableTag 竹子下方可换成灰化土的方块 对应原版 BlockTags.BENEATH_BAMBOO_PODZOL_REPLACEABLE
    public static readonly TagKey<RegBlock> BeneathBambooPodzolReplaceableTag =
        TagKey<RegBlock>.Create(Registries.BLOCK,
            Identifier.WithDefaultNamespace("beneath_bamboo_podzol_replaceable"));

    //BlockOf 按注册名取方块 未注册退回空气
    public static RegBlock BlockOf(string path)
    {
        var id = Identifier.WithDefaultNamespace(path);
        return BuiltInRegistries.BLOCK.ContainsKey(id)
            ? BuiltInRegistries.BLOCK.GetValue(id) ?? Blocks.AIR
            : Blocks.AIR;
    }

    //StateOf 按注册名取默认状态
    public static BlockState StateOf(string path) => BlockOf(path).DefaultBlockState;

    //IsState 该状态是否由指定注册名的方块构成
    public static bool IsState(BlockState state, string path) => state.Owner.Id.Path == path;

    //IsAir 该位置是否为空
    public static bool IsAir(WorldGenRegion level, BlockPos pos)
        => level.GetBlockState(pos.X, pos.Y, pos.Z).Owner.IsAir;

    //Get 读世界里的方块状态
    public static BlockState Get(WorldGenRegion level, BlockPos pos) => level.GetBlockState(pos.X, pos.Y, pos.Z);

    //Set 写方块状态
    public static void Set(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);

    //InTag 状态是否属于某方块标签 标签未绑定按不属于处理
    public static bool InTag(BlockState state, TagKey<RegBlock> tag) => ProcessorBlockHelper.InTag(state, tag);

    //IsInSet 状态是否属于方块集合 标签走标签判定 直接集合按成员判定
    public static bool IsInSet(BlockState state, HolderSet<RegBlock> set)
    {
        if (state.Id == 0 || !set.IsBound) return false;
        return set.UnwrapKey() is { } tag ? InTag(state, tag) : ProcessorBlockHelper.InSet(state, set);
    }

    //HasProperty 该状态是否有这个属性名
    public static bool HasProperty(BlockState state, string propertyName)
        => state.GetProperties().Any(property => property.Name == propertyName);

    //WithProperty 按属性名与取值名改状态 属性或取值对不上时原样返回
    public static BlockState WithProperty(BlockState state, string propertyName, string valueName)
    {
        foreach (var property in state.GetProperties())
        {
            if (property.Name != propertyName) continue;
            if (property.GetValueForName(valueName) is not { } value) return state;
            return state.SetValue(property, value);
        }
        return state;
    }

    //WithProperty 布尔属性重载
    public static BlockState WithProperty(BlockState state, string propertyName, bool value)
        => WithProperty(state, propertyName, value ? "true" : "false");

    //WithProperty 整型属性重载
    public static BlockState WithProperty(BlockState state, string propertyName, int value)
        => WithProperty(state, propertyName, value.ToString());

    //AxisName 方向所在轴的名字 供按轴属性重设朝向
    public static string AxisName(Direction direction) => direction.AxisValue switch
    {
        Direction.Axis.X => "x",
        Direction.Axis.Y => "y",
        _ => "z",
    };

    //FaceName 方向对应的六向属性名
    public static string FaceName(Direction direction) => direction.Id3D switch
    {
        Direction.DownId => "down",
        Direction.UpId => "up",
        Direction.NorthId => "north",
        Direction.SouthId => "south",
        Direction.WestId => "west",
        _ => "east",
    };

    //From2DDataValue 按水平序号取方向 对应原版 Direction.from2DDataValue 的 south west north east 序
    public static Direction From2DDataValue(int index)
    {
        var i = ((index % 4) + 4) % 4;
        return i switch
        {
            0 => Direction.South,
            1 => Direction.West,
            2 => Direction.North,
            _ => Direction.East,
        };
    }

    //IsFaceSturdy 该状态某面能否作为依附面 对应原版 isFaceSturdy 的整面判定
    public static bool IsFaceSturdy(BlockState state, Direction direction)
        => state.Owner is BlockBehaviour behaviour
            && behaviour.IsFaceSturdy(EmptyBlockGetter.Instance, BlockPos.Zero, state, direction, SupportType.Full);

    //IsOverSolidGround 下方是否为整面实心
    public static bool IsOverSolidGround(WorldGenRegion level, BlockPos pos)
        => IsFaceSturdy(Get(level, pos.Offset(Direction.Down)), Direction.Up);

    //ValidTreePos 该位置能否长树 对应原版 TreeFeature.validTreePos
    public static bool ValidTreePos(WorldGenRegion level, BlockPos pos)
    {
        var state = Get(level, pos);
        return state.Owner.IsAir || InTag(state, ReplaceableByTreesTag);
    }

    //Shuffle 原地洗牌 从后往前逐个与前方随机位置交换 对应原版 Util.shuffle
    public static void Shuffle<T>(IList<T> list, RandomSource random)
    {
        for (var i = list.Count; i > 1; i--)
        {
            var swapTo = random.NextInt(i);
            (list[i - 1], list[swapTo]) = (list[swapTo], list[i - 1]);
        }
    }

    //ShuffledCopy 洗牌后的副本 对应原版 Util.shuffledCopy
    public static List<T> ShuffledCopy<T>(IReadOnlyList<T> source, RandomSource random)
    {
        var list = new List<T>(source);
        Shuffle(list, random);
        return list;
    }

    //MinY 最低可放置 Y 对应原版 getMinY
    public static int MinY(this WorldGenRegion level) => level.MinSectionY * 16;

    //MaxY 最高可放置 Y 的开区间上界 对应原版 getMaxY
    public static int MaxY(this WorldGenRegion level) => (level.MaxSectionY + 1) * 16;

    //SeaLevel 生成器海平面 对应原版 chunkGenerator.getSeaLevel 噪声生成器才带海平面
    public static int SeaLevel(ChunkGenerator generator)
        => generator is NoiseBasedChunkGenerator noise ? noise.Settings.SeaLevel : 63;
}

//IgnoredJsonValue 被解析但丢弃的 JSON 值 装饰器一类本阶段不实现的字段用它占位
public sealed class IgnoredJsonValue
{
    public static readonly IgnoredJsonValue Instance = new();

    private IgnoredJsonValue() { }
}

//IgnoredListCodec 列表形态的忽略编解码 只校验它是数组 内容一律丢弃
internal sealed class IgnoredListCodec : ScalarCodec<IReadOnlyList<IgnoredJsonValue>>
{
    public static readonly IgnoredListCodec Instance = new();

    public override DataResult<IReadOnlyList<IgnoredJsonValue>> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetStream(input).Map(stream =>
            (IReadOnlyList<IgnoredJsonValue>)stream.Select(_ => IgnoredJsonValue.Instance).ToList());

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IReadOnlyList<IgnoredJsonValue> value)
        => DataResult<U>.Success(ops.CreateList(Array.Empty<U>()));
}

//FallenTreeConfiguration 倒地树配置 对应原版 FallenTreeConfiguration
//stump_decorators 与 log_decorators 是树装饰器 本阶段没有装饰器系统 只解析字段不执行
public sealed class FallenTreeConfiguration : FeatureConfiguration
{
    public static readonly Codec<FallenTreeConfiguration> Codec =
        RecordCodecBuilder.Of4<FallenTreeConfiguration, BlockStateProvider, IntProvider,
            IReadOnlyList<IgnoredJsonValue>, IReadOnlyList<IgnoredJsonValue>>(
            BlockStateProvider.Codec.FieldOf("trunk_provider")
                .ForGetter<FallenTreeConfiguration, BlockStateProvider>(c => c.TrunkProvider),
            IntProviders.Codec.FieldOf("log_length")
                .ForGetter<FallenTreeConfiguration, IntProvider>(c => c.LogLength),
            IgnoredListCodec.Instance.FieldOf("stump_decorators")
                .ForGetter<FallenTreeConfiguration, IReadOnlyList<IgnoredJsonValue>>(c => c.StumpDecorators),
            IgnoredListCodec.Instance.FieldOf("log_decorators")
                .ForGetter<FallenTreeConfiguration, IReadOnlyList<IgnoredJsonValue>>(c => c.LogDecorators),
            (trunkProvider, logLength, stumpDecorators, logDecorators) =>
                new FallenTreeConfiguration(trunkProvider, logLength, stumpDecorators, logDecorators));

    public BlockStateProvider TrunkProvider { get; }
    public IntProvider LogLength { get; }
    public IReadOnlyList<IgnoredJsonValue> StumpDecorators { get; }
    public IReadOnlyList<IgnoredJsonValue> LogDecorators { get; }

    public FallenTreeConfiguration(BlockStateProvider trunkProvider, IntProvider logLength,
        IReadOnlyList<IgnoredJsonValue> stumpDecorators, IReadOnlyList<IgnoredJsonValue> logDecorators)
    {
        TrunkProvider = trunkProvider;
        LogLength = logLength;
        StumpDecorators = stumpDecorators;
        LogDecorators = logDecorators;
    }
}

//FallenTreeFeature 倒地树特征 对应原版 FallenTreeFeature
//先立一截树桩再沿水平随机方向倒一段原木 原木长度超出可放置位置时整段放弃
public sealed class FallenTreeFeature : Feature<FallenTreeConfiguration>
{
    private const string FeatureId = "fallen_tree";

    public static readonly FallenTreeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new FallenTreeFeature());

    private FallenTreeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), FallenTreeConfiguration.Codec) { }

    protected override bool Place(FallenTreeConfiguration config, FeaturePlaceContext context)
    {
        PlaceFallenTree(config, context.Level, context.Random, context.Origin);
        return true;
    }

    //PlaceFallenTree 立桩取向后倒原木 对应原版 placeFallenTree 的调用顺序
    private static void PlaceFallenTree(FallenTreeConfiguration config, WorldGenRegion level,
        RandomSource random, BlockPos origin)
    {
        PlaceLogBlock(config, level, random, origin, null);
        var direction = VegetationSupport.HorizontalPlane[random.NextInt(4)];
        var logLength = config.LogLength.Sample(random) - 2;
        var logStartPos = origin.Relative(direction, 2 + random.NextInt(2));
        SetGroundHeightForFallenLogStartPos(level, ref logStartPos);
        if (CanPlaceEntireFallenLog(level, logLength, ref logStartPos, direction))
            PlaceFallenLog(config, level, random, logLength, ref logStartPos, direction);
    }

    //SetGroundHeightForFallenLogStartPos 起点上抬一格再向下找可放置的落脚面 对应原版同名方法
    private static void SetGroundHeightForFallenLogStartPos(WorldGenRegion level, ref BlockPos logStartPos)
    {
        logStartPos = logStartPos.Offset(Direction.Up);
        for (var i = 0; i < 6 && !MayPlaceOn(level, logStartPos); i++)
            logStartPos = logStartPos.Offset(Direction.Down);
    }

    //MayPlaceOn 位置可长树且下方实心 对应原版 mayPlaceOn
    private static bool MayPlaceOn(WorldGenRegion level, BlockPos pos)
        => VegetationSupport.ValidTreePos(level, pos) && VegetationSupport.IsOverSolidGround(level, pos);

    //CanPlaceEntireFallenLog 整段原木都必须放得下 中途离地超过两格就放弃 对应原版同名方法
    private static bool CanPlaceEntireFallenLog(WorldGenRegion level, int logLength, ref BlockPos logStartPos,
        Direction direction)
    {
        var gapInGround = 0;
        for (var i = 0; i < logLength; i++)
        {
            if (!VegetationSupport.ValidTreePos(level, logStartPos)) return false;
            if (!VegetationSupport.IsOverSolidGround(level, logStartPos))
            {
                gapInGround++;
                if (gapInGround > 2) return false;
            }
            else gapInGround = 0;
            logStartPos = logStartPos.Offset(direction);
        }
        logStartPos = logStartPos.Relative(direction.Opposite, logLength);
        return true;
    }

    //PlaceFallenLog 逐格放倒木 侧向原木按方向轴重设 对应原版 placeFallenLog
    private static void PlaceFallenLog(FallenTreeConfiguration config, WorldGenRegion level,
        RandomSource random, int logLength, ref BlockPos logStartPos, Direction direction)
    {
        for (var i = 0; i < logLength; i++)
        {
            PlaceLogBlock(config, level, random, logStartPos, direction);
            logStartPos = logStartPos.Offset(direction);
        }
    }

    //PlaceLogBlock 放一截原木 侧向时把轴向属性改成水平方向 对应原版 placeLogBlock
    //原版还会标记上方待后处理 本作没有该机制
    private static BlockPos PlaceLogBlock(FallenTreeConfiguration config, WorldGenRegion level,
        RandomSource random, BlockPos blockPos, Direction? sidewaysDirection)
    {
        var state = config.TrunkProvider.GetState(level, random, blockPos);
        if (sidewaysDirection is { } direction)
            state = VegetationSupport.WithProperty(state, "axis", VegetationSupport.AxisName(direction));
        VegetationSupport.Set(level, blockPos, state);
        return blockPos;
    }
}

//VinesFeature 藤蔓特征 对应原版 VinesFeature
//只在空气格上朝一个可依附的邻居挂藤蔓 自上而下优先级按原版 Direction.values 顺序
public sealed class VinesFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "vines";

    public static readonly VinesFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new VinesFeature());

    private VinesFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        if (!VegetationSupport.IsAir(level, origin)) return false;
        foreach (var direction in Direction.Values)
        {
            if (direction == Direction.Down) continue;
            if (!CanAttachTo(level, origin.Offset(direction), direction)) continue;
            VegetationSupport.Set(level, origin,
                VegetationSupport.WithProperty(VegetationSupport.StateOf("vine"),
                    VegetationSupport.FaceName(direction), true));
            return true;
        }
        return false;
    }

    //CanAttachTo 邻格朝向本格的那一面是否整面实心 对应原版 MultifaceBlock.canAttachTo
    private static bool CanAttachTo(WorldGenRegion level, BlockPos neighbourPos, Direction direction)
    {
        var state = VegetationSupport.Get(level, neighbourPos);
        if (state.Owner is not BlockBehaviour behaviour) return false;
        var support = behaviour.GetBlockSupportShape(state, EmptyBlockGetter.Instance, neighbourPos);
        if (RegBlock.IsFaceFull(support, direction.Opposite)) return true;
        var collision = behaviour.GetCollisionShape(state, EmptyBlockGetter.Instance, neighbourPos,
            CollisionContext.Empty);
        return RegBlock.IsFaceFull(collision, direction.Opposite);
    }
}

//BambooFeature 竹丛特征 对应原版 BambooFeature
//先掷概率把一圈地表换成灰化土 再立一根五到十六节的竹子并按高度补叶
public sealed class BambooFeature : Feature<ProbabilityFeatureConfiguration>
{
    private const string FeatureId = "bamboo";

    public static readonly BambooFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BambooFeature());

    private BambooFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), ProbabilityFeatureConfiguration.Codec) { }

    //BambooTrunk 竹身 age=1 leaves=none stage=0 对应原版 BAMBOO_TRUNK
    private static BlockState BambooTrunk => VegetationSupport.WithProperty(
        VegetationSupport.WithProperty(
            VegetationSupport.WithProperty(VegetationSupport.StateOf("bamboo"), "age", 1), "leaves", "none"),
        "stage", 0);

    //BambooFinalLarge 顶端那节叶子最大且 stage=1 对应原版 BAMBOO_FINAL_LARGE
    private static BlockState BambooFinalLarge
        => VegetationSupport.WithProperty(VegetationSupport.WithProperty(BambooTrunk, "leaves", "large"), "stage", 1);

    //BambooTopLarge 上一节大叶 对应原版 BAMBOO_TOP_LARGE
    private static BlockState BambooTopLarge
        => VegetationSupport.WithProperty(BambooTrunk, "leaves", "large");

    //BambooTopSmall 再上一节小叶 对应原版 BAMBOO_TOP_SMALL
    private static BlockState BambooTopSmall
        => VegetationSupport.WithProperty(BambooTrunk, "leaves", "small");

    protected override bool Place(ProbabilityFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        var placed = 0;
        var bambooPos = origin;
        if (VegetationSupport.IsAir(level, bambooPos))
        {
            if (CanBambooSurvive(level, bambooPos))
            {
                var height = random.NextInt(12) + 5;
                if (random.NextFloat() < config.Probability)
                {
                    var r = random.NextInt(4) + 1;
                    for (var xx = origin.X - r; xx <= origin.X + r; xx++)
                    {
                        for (var zz = origin.Z - r; zz <= origin.Z + r; zz++)
                        {
                            var xd = xx - origin.X;
                            var zd = zz - origin.Z;
                            if (xd * xd + zd * zd > r * r) continue;
                            var podzolPos = new BlockPos(xx,
                                level.GetHeight(Heightmap.Types.WorldSurface, xx, zz) - 1, zz);
                            var podzolState = VegetationSupport.Get(level, podzolPos);
                            if (VegetationSupport.InTag(podzolState, VegetationSupport.BeneathBambooPodzolReplaceableTag))
                                VegetationSupport.Set(level, podzolPos, VegetationSupport.StateOf("podzol"));
                        }
                    }
                }
                for (var i = 0; i < height && VegetationSupport.IsAir(level, bambooPos); i++)
                {
                    VegetationSupport.Set(level, bambooPos, BambooTrunk);
                    bambooPos = bambooPos.Offset(Direction.Up);
                }
                if (bambooPos.Y - origin.Y >= 3)
                {
                    VegetationSupport.Set(level, bambooPos, BambooFinalLarge);
                    bambooPos = bambooPos.Offset(Direction.Down);
                    VegetationSupport.Set(level, bambooPos, BambooTopLarge);
                    bambooPos = bambooPos.Offset(Direction.Down);
                    VegetationSupport.Set(level, bambooPos, BambooTopSmall);
                }
            }
            placed = 1;
        }
        return placed > 0;
    }

    //CanBambooSurvive 下方必须是可种竹子的方块 对应原版 BambooStalkBlock.canSurvive
    private static bool CanBambooSurvive(WorldGenRegion level, BlockPos pos)
        => VegetationSupport.InTag(VegetationSupport.Get(level, pos.Offset(Direction.Down)),
            VegetationSupport.SupportsBambooTag);
}

//HugeMushroomFeatureConfiguration 巨型蘑菇配置 对应原版 HugeMushroomFeatureConfiguration
public sealed class HugeMushroomFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<HugeMushroomFeatureConfiguration> Codec =
        RecordCodecBuilder.Of4<HugeMushroomFeatureConfiguration, BlockStateProvider, BlockStateProvider, int,
            BlockPredicate>(
            BlockStateProvider.Codec.FieldOf("cap_provider")
                .ForGetter<HugeMushroomFeatureConfiguration, BlockStateProvider>(c => c.CapProvider),
            BlockStateProvider.Codec.FieldOf("stem_provider")
                .ForGetter<HugeMushroomFeatureConfiguration, BlockStateProvider>(c => c.StemProvider),
            Codecs.Int.OptionalFieldOf("foliage_radius", 2)
                .ForGetter<HugeMushroomFeatureConfiguration, int>(c => c.FoliageRadius),
            BlockPredicate.Codec.FieldOf("can_place_on")
                .ForGetter<HugeMushroomFeatureConfiguration, BlockPredicate>(c => c.CanPlaceOn),
            (capProvider, stemProvider, foliageRadius, canPlaceOn) =>
                new HugeMushroomFeatureConfiguration(capProvider, stemProvider, foliageRadius, canPlaceOn));

    public BlockStateProvider CapProvider { get; }
    public BlockStateProvider StemProvider { get; }
    public int FoliageRadius { get; }
    public BlockPredicate CanPlaceOn { get; }

    public HugeMushroomFeatureConfiguration(BlockStateProvider capProvider, BlockStateProvider stemProvider,
        int foliageRadius, BlockPredicate canPlaceOn)
    {
        CapProvider = capProvider;
        StemProvider = stemProvider;
        FoliageRadius = foliageRadius;
        CanPlaceOn = canPlaceOn;
    }
}

//AbstractHugeMushroomFeature 巨型蘑菇抽象基类 对应原版 AbstractHugeMushroomFeature
//高度四到六格 十二分之一概率翻倍 先铺伞盖再立柄
public abstract class AbstractHugeMushroomFeature : Feature<HugeMushroomFeatureConfiguration>
{
    //MinMushroomHeight 最矮的蘑菇高度
    public const int MinMushroomHeight = 4;

    protected AbstractHugeMushroomFeature(Identifier id, Codec<HugeMushroomFeatureConfiguration> codec)
        : base(id, codec) { }

    //GetTreeRadiusForHeight 某一层的伞盖半径 对应原版 getTreeRadiusForHeight
    protected abstract int GetTreeRadiusForHeight(int trunkHeight, int treeHeight, int leafRadius, int yo);

    //MakeCap 铺伞盖 对应原版 makeCap
    protected abstract void MakeCap(WorldGenRegion level, RandomSource random, BlockPos origin, int treeHeight,
        HugeMushroomFeatureConfiguration config);

    protected override bool Place(HugeMushroomFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        var treeHeight = GetTreeHeight(random);
        if (!IsValidPosition(level, origin, treeHeight, config)) return false;
        MakeCap(level, random, origin, treeHeight, config);
        PlaceTrunk(level, random, origin, config, treeHeight);
        return true;
    }

    //GetTreeHeight 蘑菇高度 对应原版 getTreeHeight 两次取随机不能合并
    protected static int GetTreeHeight(RandomSource random)
    {
        var treeHeight = random.NextInt(3) + 4;
        if (random.NextInt(12) == 0) treeHeight *= 2;
        return treeHeight;
    }

    //PlaceTrunk 立柄 整柄用同一份柄方块状态 对应原版 placeTrunk
    protected void PlaceTrunk(WorldGenRegion level, RandomSource random, BlockPos origin,
        HugeMushroomFeatureConfiguration config, int treeHeight)
    {
        for (var dy = 0; dy < treeHeight; dy++)
        {
            var blockPos = origin.Offset(0, dy, 0);
            PlaceMushroomBlock(level, blockPos, config.StemProvider.GetState(level, random, origin));
        }
    }

    //PlaceMushroomBlock 只在空气或可被蘑菇覆盖的位置落子 对应原版 placeMushroomBlock
    protected static void PlaceMushroomBlock(WorldGenRegion level, BlockPos blockPos, BlockState newState)
    {
        var currentState = VegetationSupport.Get(level, blockPos);
        if (!currentState.Owner.IsAir
            && !VegetationSupport.InTag(currentState, VegetationSupport.ReplaceableByMushroomsTag)) return;
        VegetationSupport.Set(level, blockPos, newState);
    }

    //IsValidPosition 原点上下都要够高且底座可种 伞盖范围内不能有非空气非树叶 对应原版 isValidPosition
    protected bool IsValidPosition(WorldGenRegion level, BlockPos origin, int treeHeight,
        HugeMushroomFeatureConfiguration config)
    {
        var y = origin.Y;
        if (y < level.MinY() + 1 || y + treeHeight + 1 > level.MaxY()
            || !config.CanPlaceOn.Test(level, origin.Offset(0, -1, 0))) return false;
        for (var dy = 0; dy <= treeHeight; dy++)
        {
            var radius = GetTreeRadiusForHeight(-1, -1, config.FoliageRadius, dy);
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    var state = VegetationSupport.Get(level, origin.Offset(dx, dy, dz));
                    if (!state.Owner.IsAir && !VegetationSupport.InTag(state, VegetationSupport.LeavesTag))
                        return false;
                }
            }
        }
        return true;
    }
}

//HugeRedMushroomFeature 巨型红蘑菇 对应原版 HugeRedMushroomFeature
//伞盖自下而上三层 边缘格按是否到角决定朝向属性
public sealed class HugeRedMushroomFeature : AbstractHugeMushroomFeature
{
    private const string FeatureId = "huge_red_mushroom";

    public static readonly HugeRedMushroomFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new HugeRedMushroomFeature());

    private HugeRedMushroomFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), HugeMushroomFeatureConfiguration.Codec) { }

    protected override void MakeCap(WorldGenRegion level, RandomSource random, BlockPos origin, int treeHeight,
        HugeMushroomFeatureConfiguration config)
    {
        for (var dy = treeHeight - 3; dy <= treeHeight; dy++)
        {
            var radius = dy < treeHeight ? config.FoliageRadius : config.FoliageRadius - 1;
            var center = config.FoliageRadius - 2;
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    var minX = dx == -radius;
                    var maxX = dx == radius;
                    var minZ = dz == -radius;
                    var maxZ = dz == radius;
                    var xEdge = minX || maxX;
                    var zEdge = minZ || maxZ;
                    if (dy < treeHeight && xEdge == zEdge) continue;
                    var state = config.CapProvider.GetState(level, random, origin);
                    state = VegetationSupport.WithProperty(state, "up", dy >= treeHeight - 1);
                    state = VegetationSupport.WithProperty(state, "west", dx < -center);
                    state = VegetationSupport.WithProperty(state, "east", dx > center);
                    state = VegetationSupport.WithProperty(state, "north", dz < -center);
                    state = VegetationSupport.WithProperty(state, "south", dz > center);
                    PlaceMushroomBlock(level, origin.Offset(dx, dy, dz), state);
                }
            }
        }
    }

    protected override int GetTreeRadiusForHeight(int trunkHeight, int treeHeight, int leafRadius, int yo)
        => (yo < treeHeight && yo >= treeHeight - 3) || yo == treeHeight ? leafRadius : 0;
}

//HugeBrownMushroomFeature 巨型棕蘑菇 对应原版 HugeBrownMushroomFeature
//伞盖只有一层 四角不留块 四向属性表示该边是否继续延伸
public sealed class HugeBrownMushroomFeature : AbstractHugeMushroomFeature
{
    private const string FeatureId = "huge_brown_mushroom";

    public static readonly HugeBrownMushroomFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new HugeBrownMushroomFeature());

    private HugeBrownMushroomFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), HugeMushroomFeatureConfiguration.Codec) { }

    protected override void MakeCap(WorldGenRegion level, RandomSource random, BlockPos origin, int treeHeight,
        HugeMushroomFeatureConfiguration config)
    {
        var radius = config.FoliageRadius;
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dz = -radius; dz <= radius; dz++)
            {
                var minX = dx == -radius;
                var maxX = dx == radius;
                var minZ = dz == -radius;
                var maxZ = dz == radius;
                var xEdge = minX || maxX;
                var zEdge = minZ || maxZ;
                if (xEdge && zEdge) continue;
                var west = minX || (zEdge && dx == 1 - radius);
                var east = maxX || (zEdge && dx == radius - 1);
                var north = minZ || (xEdge && dz == 1 - radius);
                var south = maxZ || (xEdge && dz == radius - 1);
                var state = config.CapProvider.GetState(level, random, origin);
                state = VegetationSupport.WithProperty(state, "west", west);
                state = VegetationSupport.WithProperty(state, "east", east);
                state = VegetationSupport.WithProperty(state, "north", north);
                state = VegetationSupport.WithProperty(state, "south", south);
                PlaceMushroomBlock(level, origin.Offset(dx, treeHeight, dz), state);
            }
        }
    }

    protected override int GetTreeRadiusForHeight(int trunkHeight, int treeHeight, int leafRadius, int yo)
        => yo <= 3 ? 0 : leafRadius;
}

//ProbabilityFeatureConfiguration 概率配置 对应原版 ProbabilityFeatureConfiguration
//只带一个概率字段 海草与竹子共用
public sealed class ProbabilityFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<ProbabilityFeatureConfiguration> Codec =
        new SingleFieldMapCodec<ProbabilityFeatureConfiguration, float>(
            Codecs.Float.FieldOf("probability"),
            probability => new ProbabilityFeatureConfiguration(probability),
            config => config.Probability);

    public float Probability { get; }

    public ProbabilityFeatureConfiguration(float probability) => Probability = probability;
}
