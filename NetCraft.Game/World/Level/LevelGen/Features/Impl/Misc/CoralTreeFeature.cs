using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//CoralFeature 珊瑚特征基类 对应原版 CoralFeature
//先从珊瑚块标签随机取一种材质 再交给子类摆造型 每个珊瑚方块落位时按概率加装饰
public abstract class CoralFeature : Feature<NoneFeatureConfiguration>
{
    //CoralBlocksTag 珊瑚块标签 造型材质由它取 对应原版 BlockTags.CORAL_BLOCKS
    private static readonly TagKey<RegBlock> CoralBlocksTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("coral_blocks"));

    //CoralsTag 珊瑚标签 放在珊瑚上方的那些 对应原版 BlockTags.CORALS
    private static readonly TagKey<RegBlock> CoralsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("corals"));

    //WallCoralsTag 墙珊瑚标签 贴在水里侧面的珊瑚扇 对应原版 BlockTags.WALL_CORALS
    private static readonly TagKey<RegBlock> WallCoralsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("wall_corals"));

    protected CoralFeature(Identifier id, Codec<NoneFeatureConfiguration> codec) : base(id, codec) { }

    //PlaceFeature 子类各自的珊瑚造型 对应原版 placeFeature
    protected abstract bool PlaceFeature(WorldGenRegion level, RandomSource random, BlockPos origin, BlockState state);

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var coral = RandomElementOf(CoralBlocksTag, random);
        if (coral is null) return false;
        return PlaceFeature(context.Level, random, context.Origin, coral.DefaultBlockState);
    }

    //PlaceCoralBlock 水中放一节珊瑚 按概率在顶上加珊瑚或海泡菜 侧向水里再挂珊瑚扇 对应原版 placeCoralBlock
    protected static bool PlaceCoralBlock(WorldGenRegion level, RandomSource random, BlockPos pos, BlockState state)
    {
        var above = pos.Offset(Direction.Up);
        var targetState = VegetationSupport.Get(level, pos);
        if ((!VegetationSupport.IsState(targetState, "water") && !VegetationSupport.InTag(targetState, CoralsTag))
            || !VegetationSupport.IsState(VegetationSupport.Get(level, above), "water")) return false;
        VegetationSupport.Set(level, pos, state);
        if (random.NextFloat() < 0.25f)
        {
            var coral = RandomElementOf(CoralsTag, random);
            if (coral is not null) VegetationSupport.Set(level, above, coral.DefaultBlockState);
        }
        else if (random.NextFloat() < 0.05f)
        {
            VegetationSupport.Set(level, above,
                VegetationSupport.WithProperty(VegetationSupport.StateOf("sea_pickle"), "pickles",
                    random.NextInt(4) + 1));
        }
        foreach (var direction in VegetationSupport.HorizontalPlane)
        {
            if (random.NextFloat() >= 0.2f) continue;
            var sidePos = pos.Offset(direction);
            if (!VegetationSupport.IsState(VegetationSupport.Get(level, sidePos), "water")) continue;
            var wallCoral = RandomElementOf(WallCoralsTag, random);
            if (wallCoral is null) continue;
            var fanState = wallCoral.DefaultBlockState;
            if (VegetationSupport.HasProperty(fanState, "facing"))
                fanState = VegetationSupport.WithProperty(fanState, "facing", VegetationSupport.FaceName(direction));
            VegetationSupport.Set(level, sidePos, fanState);
        }
        return true;
    }

    //RandomElementOf 标签里随机取一个方块 对应原版 Registry.getRandomElementOf
    //标签未绑定或不含方块时按原版不消耗随机数直接返回空
    protected static RegBlock? RandomElementOf(TagKey<RegBlock> tag, RandomSource random)
    {
        var set = BuiltInRegistries.BLOCK.Get(tag);
        if (set is null || set.Size == 0) return null;
        return set.Get(random.NextInt(set.Size)).Value;
    }
}

//CoralTreeFeature 珊瑚树特征 对应原版 CoralTreeFeature
//竖着长一到三节树干 再在高处向两到四个方向展分枝
public sealed class CoralTreeFeature : CoralFeature
{
    private const string FeatureId = "coral_tree";

    public static readonly CoralTreeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new CoralTreeFeature());

    private CoralTreeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool PlaceFeature(WorldGenRegion level, RandomSource random, BlockPos origin, BlockState state)
    {
        var pos = origin;
        var trunkHeight = random.NextInt(3) + 1;
        for (var i = 0; i < trunkHeight; i++)
        {
            if (!PlaceCoralBlock(level, random, pos, state)) return true;
            pos = pos.Offset(Direction.Up);
        }
        var trunkTopPos = pos;
        var branchCount = random.NextInt(3) + 2;
        var directions = VegetationSupport.ShuffledCopy(VegetationSupport.HorizontalPlane, random);
        for (var branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            var branchDirection = directions[branchIndex];
            pos = trunkTopPos.Offset(branchDirection);
            var branchHeight = random.NextInt(5) + 2;
            var segmentLength = 0;
            for (var j = 0; j < branchHeight && PlaceCoralBlock(level, random, pos, state); j++)
            {
                segmentLength++;
                pos = pos.Offset(Direction.Up);
                //分枝起步就往外挪一格 之后每走两格按概率再外挪 对应原版的分段长度控制
                if (j == 0 || (segmentLength >= 2 && random.NextFloat() < 0.25f))
                {
                    pos = pos.Offset(branchDirection);
                    segmentLength = 0;
                }
            }
        }
        return true;
    }
}

//CoralClawFeature 珊瑚爪特征 对应原版 CoralClawFeature
//先定一根爪子的朝向 再从三个同向或邻向里洗牌取两到三个 每枝先横伸再折回朝爪子方向爬
public sealed class CoralClawFeature : CoralFeature
{
    private const string FeatureId = "coral_claw";

    public static readonly CoralClawFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new CoralClawFeature());

    private CoralClawFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool PlaceFeature(WorldGenRegion level, RandomSource random, BlockPos origin, BlockState state)
    {
        if (!PlaceCoralBlock(level, random, origin, state)) return false;
        var clawDirection = VegetationSupport.HorizontalPlane[random.NextInt(4)];
        var branchCount = random.NextInt(2) + 2;
        var possibleDirections = VegetationSupport.ShuffledCopy(
            new[] { clawDirection, clawDirection.ClockWise, clawDirection.CounterClockWise }, random);
        for (var branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            var branchDirection = possibleDirections[branchIndex];
            var pos = origin;
            var sidewayLength = random.NextInt(2) + 1;
            pos = pos.Offset(branchDirection);
            Direction segmentDirection;
            int inwayLength;
            if (branchDirection == clawDirection)
            {
                segmentDirection = clawDirection;
                inwayLength = random.NextInt(3) + 2;
            }
            else
            {
                pos = pos.Offset(Direction.Up);
                var segmentDirections = new[] { branchDirection, Direction.Up };
                segmentDirection = segmentDirections[random.NextInt(2)];
                inwayLength = random.NextInt(3) + 3;
            }
            for (var i = 0; i < sidewayLength && PlaceCoralBlock(level, random, pos, state); i++)
                pos = pos.Offset(segmentDirection);
            pos = pos.Offset(segmentDirection.Opposite);
            pos = pos.Offset(Direction.Up);
            for (var i = 0; i < inwayLength; i++)
            {
                pos = pos.Offset(clawDirection);
                if (!PlaceCoralBlock(level, random, pos, state)) break;
                if (random.NextFloat() < 0.25f) pos = pos.Offset(Direction.Up);
            }
        }
        return true;
    }
}

//CoralMushroomFeature 珊瑚蘑菇特征 对应原版 CoralMushroomFeature
//在三到六格的长宽高里遍历 只给恰好两个坐标落在内部的壳层采样 先按概率掏空再落珊瑚
public sealed class CoralMushroomFeature : CoralFeature
{
    private const string FeatureId = "coral_mushroom";

    public static readonly CoralMushroomFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new CoralMushroomFeature());

    private CoralMushroomFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool PlaceFeature(WorldGenRegion level, RandomSource random, BlockPos origin, BlockState state)
    {
        var height = random.NextInt(3) + 3;
        var width = random.NextInt(3) + 3;
        var length = random.NextInt(3) + 3;
        var sinkValue = random.NextInt(3) + 1;
        for (var x = 0; x <= width; x++)
        {
            for (var y = 0; y <= height; y++)
            {
                for (var z = 0; z <= length; z++)
                {
                    if (!IsFullShape(width, height, length, x, y, z)) continue;
                    if (random.NextFloat() < 0.1f) continue;
                    var pos = origin.Offset(x, y, z).Relative(Direction.Down, sinkValue);
                    PlaceCoralBlock(level, random, pos, state);
                }
            }
        }
        return true;
    }

    //IsFullShape 该点是否落在蘑菇壳层 条件串照原版的四个判定不带任何化简
    private static bool IsFullShape(int width, int height, int length, int x, int y, int z)
        => (x != 0 && x != width || y != 0 && y != height)
            && (z != 0 && z != length || y != 0 && y != height)
            && (x != 0 && x != width || z != 0 && z != length)
            && (x == 0 || x == width || y == 0 || y == height || z == 0 || z == length);
}
