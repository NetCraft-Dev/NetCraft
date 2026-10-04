using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Features.Impl;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;

//ColumnScan 竖直方向上的一段柱体扫描结果 对应原版 Column
//地面与天花板各自可有可无 只有两端都有时才构成一段区间
internal sealed class ColumnScan
{
    public int? Floor { get; }
    public int? Ceiling { get; }

    public ColumnScan(int? floor, int? ceiling)
    {
        Floor = floor;
        Ceiling = ceiling;
    }

    //IsRange 两端都有才算一段完整区间 对应原版 Column.Range
    public bool IsRange => Floor is not null && Ceiling is not null;

    //Height 区间净高度 对应原版 Column.Range.height 只有区间才有
    public int? Height => IsRange ? Ceiling!.Value - Floor!.Value - 1 : null;

    //WithFloor 换掉地面那一端 对应原版 withFloor
    public ColumnScan WithFloor(int floor) => new(floor, Ceiling);
}

//Column 竖直扫描 对应原版 net.minecraft.world.level.levelgen.Column
//从中心向外找第一格不满足内部条件的位置 再看它是否满足边界条件来决定地面或天花板
internal static class Column
{
    //Scan 自该位置上下各扫一次 对应原版 Column.scan
    public static ColumnScan? Scan(WorldGenRegion level, BlockPos pos, int searchRange,
        Func<BlockState, bool> insideColumn, Func<BlockState, bool> validEdge)
    {
        if (!insideColumn(VegetationSupport.Get(level, pos))) return null;
        var ceiling = ScanDirection(level, searchRange, insideColumn, validEdge, pos, Direction.Up);
        var floor = ScanDirection(level, searchRange, insideColumn, validEdge, pos, Direction.Down);
        return new ColumnScan(floor, ceiling);
    }

    //ScanDirection 单向扫描 先沿方向推进到第一个非内部位置 再判它是否算边界 对应原版 scanDirection
    private static int? ScanDirection(WorldGenRegion level, int searchRange, Func<BlockState, bool> insideColumn,
        Func<BlockState, bool> validEdge, BlockPos pos, Direction direction)
    {
        var y = pos.Y;
        for (var i = 1; i < searchRange && insideColumn(VegetationSupport.Get(level, new BlockPos(pos.X, y, pos.Z)));
             i++) y += direction.StepY;
        return validEdge(VegetationSupport.Get(level, new BlockPos(pos.X, y, pos.Z))) ? y : null;
    }
}

//SpeleothemUtils 钟乳石共用判定与生成 对应原版 SpeleothemUtils
internal static class SpeleothemUtils
{
    //BaseStoneOverworldTag 主世界岩类标签 对应原版 BlockTags.BASE_STONE_OVERWORLD
    public static readonly TagKey<RegBlock> BaseStoneOverworldTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("base_stone_overworld"));

    //IsEmptyOrWater 空气或水 对应原版 isEmptyOrWater
    public static bool IsEmptyOrWater(BlockState state)
        => state.Owner.IsAir || VegetationSupport.IsState(state, "water");

    //IsNeitherEmptyNorWater 既不是空气也不是水 对应原版 isNeitherEmptyNorWater
    public static bool IsNeitherEmptyNorWater(BlockState state) => !IsEmptyOrWater(state);

    //IsEmptyOrWaterOrLava 空气水或岩浆 对应原版 isEmptyOrWaterOrLava
    public static bool IsEmptyOrWaterOrLava(BlockState state)
        => state.Owner.IsAir || VegetationSupport.IsState(state, "water")
            || VegetationSupport.IsState(state, "lava");

    //IsEmptyOrWater 位置版
    public static bool IsEmptyOrWater(WorldGenRegion level, BlockPos pos)
        => IsEmptyOrWater(VegetationSupport.Get(level, pos));

    //IsEmptyOrWaterOrLava 位置版
    public static bool IsEmptyOrWaterOrLava(WorldGenRegion level, BlockPos pos)
        => IsEmptyOrWaterOrLava(VegetationSupport.Get(level, pos));

    //IsBase 该状态是基石方块或可替换方块 对应原版 isBase
    public static bool IsBase(BlockState state, RegBlock baseBlock, HolderSet<RegBlock> replaceableBlocks)
        => state.Owner == baseBlock || VegetationSupport.IsInSet(state, replaceableBlocks);

    //IsBaseOrLava 基石或岩浆 对应原版 isBaseOrLava
    public static bool IsBaseOrLava(BlockState state, RegBlock baseBlock, HolderSet<RegBlock> replaceableBlocks)
        => IsBase(state, baseBlock, replaceableBlocks) || VegetationSupport.IsState(state, "lava");

    //PlaceBaseBlockIfPossible 可替换位置换成基石 对应原版 placeBaseBlockIfPossible
    public static bool PlaceBaseBlockIfPossible(WorldGenRegion level, BlockPos pos, RegBlock baseBlock,
        HolderSet<RegBlock> replaceableBlocks)
    {
        var state = VegetationSupport.Get(level, pos);
        if (!VegetationSupport.IsInSet(state, replaceableBlocks)) return false;
        VegetationSupport.Set(level, pos, baseBlock.DefaultBlockState);
        return true;
    }

    //GetSpeleothemHeight 由半径与距离算石笋高度 对应原版 getSpeleothemHeight
    public static double GetSpeleothemHeight(double xzDistanceFromCenter, double speleothemRadius, double scale,
        double bluntness)
    {
        if (xzDistanceFromCenter < bluntness) xzDistanceFromCenter = bluntness;
        var r = (xzDistanceFromCenter / speleothemRadius) * 0.384d;
        var part1 = 0.75d * Math.Pow(r, 4.0d / 3.0d);
        var part2 = Math.Pow(r, 2.0d / 3.0d);
        var part3 = (1.0d / 3.0d) * Math.Log(r);
        var heightRelativeToMaxRadius = scale * ((part1 - part2) - part3);
        return (Math.Max(heightRelativeToMaxRadius, 0.0d) / 0.384d) * speleothemRadius;
    }

    //IsCircleMostlyEmbeddedInStone 圆周一圈都不在空腔里 对应原版 isCircleMostlyEmbeddedInStone
    public static bool IsCircleMostlyEmbeddedInStone(WorldGenRegion level, BlockPos center, int xzRadius)
    {
        if (IsEmptyOrWaterOrLava(level, center)) return false;
        var angleIncrement = 6.0f / xzRadius;
        for (var angle = 0.0f; angle < 6.2831855f; angle += angleIncrement)
        {
            var dx = (int)(Mth.Cos(angle) * xzRadius);
            var dz = (int)(Mth.Sin(angle) * xzRadius);
            if (IsEmptyOrWaterOrLava(level, center.Offset(dx, 0, dz))) return false;
        }
        return true;
    }

    //GrowSpeleothem 从起点沿尖端方向长一根石笋 对应原版 growSpeleothem
    public static void GrowSpeleothem(WorldGenRegion level, BlockPos startPos, Direction tipDirection, int height,
        bool mergedTip, RegBlock baseBlock, RegBlock pointedBlock, HolderSet<RegBlock> replaceableBlocks)
    {
        if (!IsBase(VegetationSupport.Get(level, startPos.Offset(tipDirection.Opposite)), baseBlock,
                replaceableBlocks)) return;
        var pos = startPos;
        BuildBaseToTipColumn(tipDirection, height, mergedTip, state =>
        {
            var watered = state.Owner == pointedBlock
                ? VegetationSupport.WithProperty(state, "waterlogged",
                    VegetationSupport.IsState(VegetationSupport.Get(level, pos), "water"))
                : state;
            VegetationSupport.Set(level, pos, watered);
            pos = pos.Offset(tipDirection);
        });
    }

    //BuildBaseToTipColumn 按长度从粗到细输出各段 对应原版 buildBaseToTipColumn
    private static void BuildBaseToTipColumn(Direction direction, int totalLength, bool mergedTip,
        Action<BlockState> consumer)
    {
        if (totalLength >= 3)
        {
            consumer(CreatePointedBlock(direction, "base"));
            for (var i = 0; i < totalLength - 3; i++) consumer(CreatePointedBlock(direction, "middle"));
        }
        if (totalLength >= 2) consumer(CreatePointedBlock(direction, "frustum"));
        if (totalLength >= 1) consumer(CreatePointedBlock(direction, mergedTip ? "tip_merge" : "tip"));
    }

    //CreatePointedBlock 造一段朝向与粗细都定好的钟乳石 对应原版 createPointedBlock
    private static BlockState CreatePointedBlock(Direction direction, string thickness)
        => VegetationSupport.WithProperty(
            VegetationSupport.WithProperty(VegetationSupport.StateOf("pointed_dripstone"), "vertical_direction",
                direction == Direction.Up ? "up" : "down"),
            "thickness", thickness);
}

//ClampedNormalFloat 正态再截断的浮点提供者 对应原版 ClampedNormalFloat
//项目的浮点提供者体系没有这个类型 钟乳石簇的湿度字段要用它
public sealed class ClampedNormalFloat : FloatProvider
{
    public float Mean { get; }
    public float Deviation { get; }
    public override float Min { get; }
    public override float Max { get; }

    public ClampedNormalFloat(float mean, float deviation, float min, float max)
    {
        Mean = mean;
        Deviation = deviation;
        Min = min;
        Max = max;
    }

    //Sample 正态采样后截断 对应原版 sample
    public override float Sample(RandomSource random)
        => Mth.Clamp(Mth.Normal(random, Mean, Deviation), Min, Max);
}

//VegetationFloatProviderCodec 浮点提供者编解码 先走通用入口 再补 clamped_normal
//钟乳石簇的 wetness 是 clamped_normal 通用入口认不出 这里兜住它
internal sealed class VegetationFloatProviderCodec : ScalarCodec<FloatProvider>
{
    public static readonly VegetationFloatProviderCodec Instance = new();

    public override DataResult<FloatProvider> Parse<U>(DynamicOps<U> ops, U input)
    {
        var general = FloatProviders.Codec.Parse(ops, input);
        if (general.Result().IsPresent) return general;
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return general;
        var map = mapResult.GetOrThrow();
        var typeTag = map.Get("type");
        if (!typeTag.IsPresent) return general;
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return general;
        var id = Identifier.TryParse(typeText.GetOrThrow());
        if (id?.Path != "clamped_normal") return general;
        var mean = ReadFloat(ops, map, "mean");
        var deviation = ReadFloat(ops, map, "deviation");
        var min = ReadFloat(ops, map, "min");
        var max = ReadFloat(ops, map, "max");
        if (mean is null || deviation is null || min is null || max is null)
            return DataResult<FloatProvider>.Error(() => "clamped_normal 需要 mean/deviation/min/max");
        if (max < min) return DataResult<FloatProvider>.Error(() => "clamped_normal 的上界不能小于下界");
        return DataResult<FloatProvider>.Success(
            new ClampedNormalFloat(mean.Value, deviation.Value, min.Value, max.Value));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, FloatProvider value)
        => FloatProviders.Codec.EncodeStart(ops, value);

    //ReadFloat 读浮点字段缺失或非数字返回 null
    private static float? ReadFloat<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (float)value.GetOrThrow() : null;
    }
}

//SpeleothemClusterConfiguration 钟乳石簇配置 对应原版 SpeleothemClusterConfiguration
public sealed class SpeleothemClusterConfiguration : FeatureConfiguration
{
    public static readonly Codec<SpeleothemClusterConfiguration> Codec =
        RecordCodecBuilder.Of14<SpeleothemClusterConfiguration, BlockState, BlockState, HolderSet<RegBlock>, int,
            IntProvider, IntProvider, int, int, IntProvider, FloatProvider, FloatProvider, float, int, int>(
            BlockStateCodec.Instance.FieldOf("base_block")
                .ForGetter<SpeleothemClusterConfiguration, BlockState>(c => c.BaseBlock),
            BlockStateCodec.Instance.FieldOf("pointed_block")
                .ForGetter<SpeleothemClusterConfiguration, BlockState>(c => c.PointedBlock),
            HolderSetCodecs.BlockSet.FieldOf("replaceable_blocks")
                .ForGetter<SpeleothemClusterConfiguration, HolderSet<RegBlock>>(c => c.ReplaceableBlocks),
            Codecs.Int.FieldOf("floor_to_ceiling_search_range")
                .ForGetter<SpeleothemClusterConfiguration, int>(c => c.FloorToCeilingSearchRange),
            IntProviders.Codec.FieldOf("height")
                .ForGetter<SpeleothemClusterConfiguration, IntProvider>(c => c.Height),
            IntProviders.Codec.FieldOf("radius")
                .ForGetter<SpeleothemClusterConfiguration, IntProvider>(c => c.Radius),
            Codecs.Int.FieldOf("max_stalagmite_stalactite_height_diff")
                .ForGetter<SpeleothemClusterConfiguration, int>(c => c.MaxStalagmiteStalactiteHeightDiff),
            Codecs.Int.FieldOf("height_deviation")
                .ForGetter<SpeleothemClusterConfiguration, int>(c => c.HeightDeviation),
            IntProviders.Codec.FieldOf("speleothem_block_layer_thickness")
                .ForGetter<SpeleothemClusterConfiguration, IntProvider>(c => c.SpeleothemBlockLayerThickness),
            VegetationFloatProviderCodec.Instance.FieldOf("density")
                .ForGetter<SpeleothemClusterConfiguration, FloatProvider>(c => c.Density),
            VegetationFloatProviderCodec.Instance.FieldOf("wetness")
                .ForGetter<SpeleothemClusterConfiguration, FloatProvider>(c => c.Wetness),
            Codecs.Float.FieldOf("chance_of_speleothem_at_max_distance_from_center")
                .ForGetter<SpeleothemClusterConfiguration, float>(
                    c => c.ChanceOfSpeleothemAtMaxDistanceFromCenter),
            Codecs.Int.FieldOf("max_distance_from_edge_affecting_chance_of_speleothem")
                .ForGetter<SpeleothemClusterConfiguration, int>(
                    c => c.MaxDistanceFromEdgeAffectingChanceOfSpeleothem),
            Codecs.Int.FieldOf("max_distance_from_center_affecting_height_bias")
                .ForGetter<SpeleothemClusterConfiguration, int>(c => c.MaxDistanceFromCenterAffectingHeightBias),
            (baseBlock, pointedBlock, replaceableBlocks, floorToCeilingSearchRange, height, radius,
                maxStalagmiteStalactiteHeightDiff, heightDeviation, speleothemBlockLayerThickness, density, wetness,
                chanceOfSpeleothemAtMaxDistanceFromCenter, maxDistanceFromEdgeAffectingChanceOfSpeleothem,
                maxDistanceFromCenterAffectingHeightBias) => new SpeleothemClusterConfiguration(baseBlock,
                pointedBlock, replaceableBlocks, floorToCeilingSearchRange, height, radius,
                maxStalagmiteStalactiteHeightDiff, heightDeviation, speleothemBlockLayerThickness, density, wetness,
                chanceOfSpeleothemAtMaxDistanceFromCenter, maxDistanceFromEdgeAffectingChanceOfSpeleothem,
                maxDistanceFromCenterAffectingHeightBias));

    public BlockState BaseBlock { get; }
    public BlockState PointedBlock { get; }
    public HolderSet<RegBlock> ReplaceableBlocks { get; }
    public int FloorToCeilingSearchRange { get; }
    public IntProvider Height { get; }
    public IntProvider Radius { get; }
    public int MaxStalagmiteStalactiteHeightDiff { get; }
    public int HeightDeviation { get; }
    public IntProvider SpeleothemBlockLayerThickness { get; }
    public FloatProvider Density { get; }
    public FloatProvider Wetness { get; }
    public float ChanceOfSpeleothemAtMaxDistanceFromCenter { get; }
    public int MaxDistanceFromEdgeAffectingChanceOfSpeleothem { get; }
    public int MaxDistanceFromCenterAffectingHeightBias { get; }

    public SpeleothemClusterConfiguration(BlockState baseBlock, BlockState pointedBlock,
        HolderSet<RegBlock> replaceableBlocks, int floorToCeilingSearchRange, IntProvider height, IntProvider radius,
        int maxStalagmiteStalactiteHeightDiff, int heightDeviation, IntProvider speleothemBlockLayerThickness,
        FloatProvider density, FloatProvider wetness, float chanceOfSpeleothemAtMaxDistanceFromCenter,
        int maxDistanceFromEdgeAffectingChanceOfSpeleothem, int maxDistanceFromCenterAffectingHeightBias)
    {
        BaseBlock = baseBlock;
        PointedBlock = pointedBlock;
        ReplaceableBlocks = replaceableBlocks;
        FloorToCeilingSearchRange = floorToCeilingSearchRange;
        Height = height;
        Radius = radius;
        MaxStalagmiteStalactiteHeightDiff = maxStalagmiteStalactiteHeightDiff;
        HeightDeviation = heightDeviation;
        SpeleothemBlockLayerThickness = speleothemBlockLayerThickness;
        Density = density;
        Wetness = wetness;
        ChanceOfSpeleothemAtMaxDistanceFromCenter = chanceOfSpeleothemAtMaxDistanceFromCenter;
        MaxDistanceFromEdgeAffectingChanceOfSpeleothem = maxDistanceFromEdgeAffectingChanceOfSpeleothem;
        MaxDistanceFromCenterAffectingHeightBias = maxDistanceFromCenterAffectingHeightBias;
    }
}

//SpeleothemClusterFeature 钟乳石簇特征 对应原版 SpeleothemClusterFeature
//按半径内每列扫出天花板与地面 逐列决定要不要水池 要石钟乳 要石笋以及各自长度
//钟乳石簇的随机消耗与循环边界全部照原版 顺序错了同种子长出的洞顶就不同
public sealed class SpeleothemClusterFeature : Feature<SpeleothemClusterConfiguration>
{
    private const string FeatureId = "speleothem_cluster";

    public static readonly SpeleothemClusterFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SpeleothemClusterFeature());

    private SpeleothemClusterFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), SpeleothemClusterConfiguration.Codec) { }

    protected override bool Place(SpeleothemClusterConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        if (!SpeleothemUtils.IsEmptyOrWater(level, origin)) return false;
        var height = config.Height.Sample(random);
        var wetness = config.Wetness.Sample(random);
        var density = config.Density.Sample(random);
        var xRadius = config.Radius.Sample(random);
        var zRadius = config.Radius.Sample(random);
        for (var dx = -xRadius; dx <= xRadius; dx++)
        {
            for (var dz = -zRadius; dz <= zRadius; dz++)
            {
                var chanceOfStalagmiteOrStalactite = GetChanceOfStalagmiteOrStalactite(xRadius, zRadius, dx, dz, config);
                var pos = origin.Offset(dx, 0, dz);
                PlaceColumn(level, random, pos, dx, dz, wetness, chanceOfStalagmiteOrStalactite, height, density,
                    config);
            }
        }
        return true;
    }

    //PlaceColumn 单列生成 对应原版 placeColumn
    private static void PlaceColumn(WorldGenRegion level, RandomSource random, BlockPos pos, int dx, int dz,
        float chanceOfWater, double chanceOfStalagmiteOrStalactite, int clusterHeight, float density,
        SpeleothemClusterConfiguration config)
    {
        var baseColumn = Column.Scan(level, pos, config.FloorToCeilingSearchRange, SpeleothemUtils.IsEmptyOrWater,
            SpeleothemUtils.IsNeitherEmptyNorWater);
        if (baseColumn is null) return;
        var ceiling = baseColumn.Ceiling;
        var baseFloor = baseColumn.Floor;
        if (ceiling is null && baseFloor is null) return;
        var wantPool = random.NextFloat() < chanceOfWater;
        ColumnScan column;
        if (wantPool && baseFloor is { } floorY && CanPlacePool(level, AtY(pos, floorY), config))
        {
            column = baseColumn.WithFloor(floorY - 1);
            VegetationSupport.Set(level, AtY(pos, floorY), VegetationSupport.StateOf("water"));
        }
        else
        {
            column = baseColumn;
        }
        var floor = column.Floor;
        var wantStalactite = random.NextDouble() < chanceOfStalagmiteOrStalactite;
        int stalactiteHeight;
        if (ceiling is { } ceilingY && wantStalactite
            && !VegetationSupport.IsState(VegetationSupport.Get(level, AtY(pos, ceilingY)), "lava"))
        {
            var ceilingThickness = config.SpeleothemBlockLayerThickness.Sample(random);
            ReplaceBlocksWithBaseBlocks(level, AtY(pos, ceilingY), ceilingThickness, Direction.Up, config);
            var maxHeightForThisColumn = floor is { } f ? Math.Min(clusterHeight, ceilingY - f) : clusterHeight;
            stalactiteHeight = GetSpeleothemHeight(random, dx, dz, density, maxHeightForThisColumn, config);
        }
        else
        {
            stalactiteHeight = 0;
        }
        var wantStalagmite = random.NextDouble() < chanceOfStalagmiteOrStalactite;
        int stalagmiteHeight;
        if (floor is { } stalagmiteBase && wantStalagmite
            && !VegetationSupport.IsState(VegetationSupport.Get(level, AtY(pos, stalagmiteBase)), "lava"))
        {
            var floorThickness = config.SpeleothemBlockLayerThickness.Sample(random);
            ReplaceBlocksWithBaseBlocks(level, AtY(pos, stalagmiteBase), floorThickness, Direction.Down, config);
            stalagmiteHeight = ceiling is not null
                ? Math.Max(0, stalactiteHeight + Mth.RandomBetweenInclusive(random,
                    -config.MaxStalagmiteStalactiteHeightDiff, config.MaxStalagmiteStalactiteHeightDiff))
                : GetSpeleothemHeight(random, dx, dz, density, clusterHeight, config);
        }
        else
        {
            stalagmiteHeight = 0;
        }
        int actualStalactiteHeight;
        int actualStalagmiteHeight;
        if (ceiling is { } ceilingTop && floor is { } floorBottom
            && ceilingTop - stalactiteHeight <= floorBottom + stalagmiteHeight)
        {
            var lowestStalactiteBottom = Math.Max(ceilingTop - stalactiteHeight, floorBottom + 1);
            var highestStalagmiteTop = Math.Min(floorBottom + stalagmiteHeight, ceilingTop - 1);
            var actualStalactiteBottom = Mth.RandomBetweenInclusive(random, lowestStalactiteBottom,
                highestStalagmiteTop + 1);
            var actualStalagmiteTop = actualStalactiteBottom - 1;
            actualStalactiteHeight = ceilingTop - actualStalactiteBottom;
            actualStalagmiteHeight = actualStalagmiteTop - floorBottom;
        }
        else
        {
            actualStalactiteHeight = stalactiteHeight;
            actualStalagmiteHeight = stalagmiteHeight;
        }
        var mergeTips = random.NextBoolean() && actualStalactiteHeight > 0 && actualStalagmiteHeight > 0
            && column.Height is { } columnHeight
            && actualStalactiteHeight + actualStalagmiteHeight == columnHeight;
        if (ceiling is { } stalactiteCeiling)
            SpeleothemUtils.GrowSpeleothem(level, AtY(pos, stalactiteCeiling - 1), Direction.Down,
                actualStalactiteHeight, mergeTips, config.BaseBlock.Owner, config.PointedBlock.Owner,
                config.ReplaceableBlocks);
        if (floor is { } stalagmiteFloor)
            SpeleothemUtils.GrowSpeleothem(level, AtY(pos, stalagmiteFloor + 1), Direction.Up, actualStalagmiteHeight,
                mergeTips, config.BaseBlock.Owner, config.PointedBlock.Owner, config.ReplaceableBlocks);
    }

    //GetSpeleothemHeight 按离中心距离偏置后采样长度 对应原版 getSpeleothemHeight
    private static int GetSpeleothemHeight(RandomSource random, int dx, int dz, float density, int maxHeight,
        SpeleothemClusterConfiguration config)
    {
        if (random.NextFloat() > density) return 0;
        var distanceFromCenter = Math.Abs(dx) + Math.Abs(dz);
        var heightMean = (float)Mth.ClampedMap((double)distanceFromCenter, 0.0d,
            config.MaxDistanceFromCenterAffectingHeightBias, maxHeight / 2.0d, 0.0d);
        return (int)Mth.Clamp(Mth.Normal(random, heightMean, config.HeightDeviation), 0.0f, maxHeight);
    }

    //CanPlacePool 该格作为水池边沿要四周与下方都是岩石或水 对应原版 canPlacePool
    private static bool CanPlacePool(WorldGenRegion level, BlockPos pos, SpeleothemClusterConfiguration config)
    {
        var state = VegetationSupport.Get(level, pos);
        if (VegetationSupport.IsState(state, "water") || state.Owner == config.BaseBlock.Owner
            || state.Owner == config.PointedBlock.Owner
            || VegetationSupport.IsState(VegetationSupport.Get(level, pos.Offset(Direction.Up)), "water")) return false;
        foreach (var direction in VegetationSupport.HorizontalPlane)
        {
            if (!CanBeAdjacentToWater(level, pos.Offset(direction))) return false;
        }
        return CanBeAdjacentToWater(level, pos.Offset(Direction.Down));
    }

    //CanBeAdjacentToWater 该格是主世界岩类或水 对应原版 canBeAdjacentToWater
    private static bool CanBeAdjacentToWater(WorldGenRegion level, BlockPos pos)
    {
        var state = VegetationSupport.Get(level, pos);
        return VegetationSupport.InTag(state, SpeleothemUtils.BaseStoneOverworldTag)
            || VegetationSupport.IsState(state, "water");
    }

    //ReplaceBlocksWithBaseBlocks 从起点沿方向连续换成基石 对应原版 replaceBlocksWithBaseBlocks
    private static void ReplaceBlocksWithBaseBlocks(WorldGenRegion level, BlockPos firstPos, int maxCount,
        Direction direction, SpeleothemClusterConfiguration config)
    {
        var pos = firstPos;
        for (var i = 0;
             i < maxCount && SpeleothemUtils.PlaceBaseBlockIfPossible(level, pos, config.BaseBlock.Owner,
                 config.ReplaceableBlocks);
             i++) pos = pos.Offset(direction);
    }

    //GetChanceOfStalagmiteOrStalactite 离边沿越近概率越高 对应原版 getChanceOfStalagmiteOrStalactite
    private static double GetChanceOfStalagmiteOrStalactite(int xRadius, int zRadius, int dx, int dz,
        SpeleothemClusterConfiguration config)
    {
        var xDistanceFromEdge = xRadius - Math.Abs(dx);
        var zDistanceFromEdge = zRadius - Math.Abs(dz);
        var distanceFromEdge = Math.Min(xDistanceFromEdge, zDistanceFromEdge);
        return Mth.ClampedMap((float)distanceFromEdge, 0.0f,
            config.MaxDistanceFromEdgeAffectingChanceOfSpeleothem,
            config.ChanceOfSpeleothemAtMaxDistanceFromCenter, 1.0f);
    }

    //AtY 换掉 Y 坐标
    private static BlockPos AtY(BlockPos pos, int y) => new(pos.X, y, pos.Z);
}

//LargeDripstoneConfiguration 大型石笋配置 对应原版 LargeDripstoneConfiguration
public sealed class LargeDripstoneConfiguration : FeatureConfiguration
{
    public static readonly Codec<LargeDripstoneConfiguration> Codec =
        RecordCodecBuilder.Of10<LargeDripstoneConfiguration, HolderSet<RegBlock>, int, IntProvider, FloatProvider,
            float, FloatProvider, FloatProvider, FloatProvider, int, float>(
            HolderSetCodecs.BlockSet.FieldOf("replaceable_blocks")
                .ForGetter<LargeDripstoneConfiguration, HolderSet<RegBlock>>(c => c.ReplaceableBlocks),
            Codecs.Int.OptionalFieldOf("floor_to_ceiling_search_range", 30)
                .ForGetter<LargeDripstoneConfiguration, int>(c => c.FloorToCeilingSearchRange),
            IntProviders.Codec.FieldOf("column_radius")
                .ForGetter<LargeDripstoneConfiguration, IntProvider>(c => c.ColumnRadius),
            FloatProviders.Codec.FieldOf("height_scale")
                .ForGetter<LargeDripstoneConfiguration, FloatProvider>(c => c.HeightScale),
            Codecs.Float.FieldOf("max_column_radius_to_cave_height_ratio")
                .ForGetter<LargeDripstoneConfiguration, float>(c => c.MaxColumnRadiusToCaveHeightRatio),
            FloatProviders.Codec.FieldOf("stalactite_bluntness")
                .ForGetter<LargeDripstoneConfiguration, FloatProvider>(c => c.StalactiteBluntness),
            FloatProviders.Codec.FieldOf("stalagmite_bluntness")
                .ForGetter<LargeDripstoneConfiguration, FloatProvider>(c => c.StalagmiteBluntness),
            FloatProviders.Codec.FieldOf("wind_speed")
                .ForGetter<LargeDripstoneConfiguration, FloatProvider>(c => c.WindSpeed),
            Codecs.Int.FieldOf("min_radius_for_wind")
                .ForGetter<LargeDripstoneConfiguration, int>(c => c.MinRadiusForWind),
            Codecs.Float.FieldOf("min_bluntness_for_wind")
                .ForGetter<LargeDripstoneConfiguration, float>(c => c.MinBluntnessForWind),
            (replaceableBlocks, floorToCeilingSearchRange, columnRadius, heightScale,
                maxColumnRadiusToCaveHeightRatio, stalactiteBluntness, stalagmiteBluntness, windSpeed,
                minRadiusForWind, minBluntnessForWind) => new LargeDripstoneConfiguration(replaceableBlocks,
                floorToCeilingSearchRange, columnRadius, heightScale, maxColumnRadiusToCaveHeightRatio,
                stalactiteBluntness, stalagmiteBluntness, windSpeed, minRadiusForWind, minBluntnessForWind));

    public HolderSet<RegBlock> ReplaceableBlocks { get; }
    public int FloorToCeilingSearchRange { get; }
    public IntProvider ColumnRadius { get; }
    public FloatProvider HeightScale { get; }
    public float MaxColumnRadiusToCaveHeightRatio { get; }
    public FloatProvider StalactiteBluntness { get; }
    public FloatProvider StalagmiteBluntness { get; }
    public FloatProvider WindSpeed { get; }
    public int MinRadiusForWind { get; }
    public float MinBluntnessForWind { get; }

    public LargeDripstoneConfiguration(HolderSet<RegBlock> replaceableBlocks, int floorToCeilingSearchRange,
        IntProvider columnRadius, FloatProvider heightScale, float maxColumnRadiusToCaveHeightRatio,
        FloatProvider stalactiteBluntness, FloatProvider stalagmiteBluntness, FloatProvider windSpeed,
        int minRadiusForWind, float minBluntnessForWind)
    {
        ReplaceableBlocks = replaceableBlocks;
        FloorToCeilingSearchRange = floorToCeilingSearchRange;
        ColumnRadius = columnRadius;
        HeightScale = heightScale;
        MaxColumnRadiusToCaveHeightRatio = maxColumnRadiusToCaveHeightRatio;
        StalactiteBluntness = stalactiteBluntness;
        StalagmiteBluntness = stalagmiteBluntness;
        WindSpeed = windSpeed;
        MinRadiusForWind = minRadiusForWind;
        MinBluntnessForWind = minBluntnessForWind;
    }
}

//LargeDripstoneFeature 大型石笋特征 对应原版 LargeDripstoneFeature
//扫出一段洞穴区间后按区间高度定半径 上下各造一根巨型石笋并按风偏移错开
public sealed class LargeDripstoneFeature : Feature<LargeDripstoneConfiguration>
{
    private const string FeatureId = "large_dripstone";

    public static readonly LargeDripstoneFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new LargeDripstoneFeature());

    private LargeDripstoneFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), LargeDripstoneConfiguration.Codec) { }

    protected override bool Place(LargeDripstoneConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        if (!SpeleothemUtils.IsEmptyOrWater(level, origin)) return false;
        var dripstoneBlock = VegetationSupport.BlockOf("dripstone_block");
        var column = Column.Scan(level, origin, config.FloorToCeilingSearchRange, SpeleothemUtils.IsEmptyOrWater,
            state => SpeleothemUtils.IsBaseOrLava(state, dripstoneBlock, config.ReplaceableBlocks));
        if (column is null || !column.IsRange) return false;
        var rangeHeight = column.Height!.Value;
        if (rangeHeight < 4) return false;
        var maxColumnRadiusBasedOnColumnHeight = (int)(rangeHeight * config.MaxColumnRadiusToCaveHeightRatio);
        var maxColumnRadius = Mth.Clamp(maxColumnRadiusBasedOnColumnHeight, config.ColumnRadius.MinInclusive,
            config.ColumnRadius.MaxInclusive);
        var radius = Mth.RandomBetweenInclusive(random, config.ColumnRadius.MinInclusive, maxColumnRadius);
        var stalactite = LargeDripstone.Create(new BlockPos(origin.X, column.Ceiling!.Value - 1, origin.Z), false,
            radius, config.StalactiteBluntness.Sample(random), config.HeightScale.Sample(random));
        var stalagmite = LargeDripstone.Create(new BlockPos(origin.X, column.Floor!.Value + 1, origin.Z), true, radius,
            config.StalagmiteBluntness.Sample(random), config.HeightScale.Sample(random));
        var wind = stalactite.IsSuitableForWind(config) && stalagmite.IsSuitableForWind(config)
            ? new WindOffsetter(origin.Y, random, config.WindSpeed, 16 - radius)
            : WindOffsetter.NoWind();
        var stalactiteBaseEmbeddedInStone = stalactite.MoveBackUntilBaseIsInsideStoneAndShrinkRadiusIfNecessary(
            level, wind);
        var stalagmiteBaseEmbeddedInStone = stalagmite.MoveBackUntilBaseIsInsideStoneAndShrinkRadiusIfNecessary(
            level, wind);
        if (stalactiteBaseEmbeddedInStone) stalactite.PlaceBlocks(level, random, wind);
        if (stalagmiteBaseEmbeddedInStone) stalagmite.PlaceBlocks(level, random, wind);
        return true;
    }

    //LargeDripstone 一根巨型石笋 对应原版 LargeDripstone
    private sealed class LargeDripstone
    {
        private BlockPos _root;
        private readonly bool _pointingUp;
        private int _radius;
        private readonly double _bluntness;
        private readonly double _scale;

        private LargeDripstone(BlockPos root, bool pointingUp, int radius, double bluntness, double scale)
        {
            _root = root;
            _pointingUp = pointingUp;
            _radius = radius;
            _bluntness = bluntness;
            _scale = scale;
        }

        public static LargeDripstone Create(BlockPos root, bool pointingUp, int radius, double bluntness,
            double scale) => new(root, pointingUp, radius, bluntness, scale);

        //GetHeight 中轴上的高度 对应原版 getHeight
        private int GetHeight() => GetHeightAtRadius(0.0f);

        //MoveBackUntilBaseIsInsideStoneAndShrinkRadiusIfNecessary 退回基部嵌入岩石 不行就减半半径 对应原版同名方法
        public bool MoveBackUntilBaseIsInsideStoneAndShrinkRadiusIfNecessary(WorldGenRegion level, WindOffsetter wind)
        {
            while (_radius > 1)
            {
                var newRoot = _root;
                var maxTries = Math.Min(10, GetHeight());
                for (var i = 0; i < maxTries; i++)
                {
                    if (VegetationSupport.IsState(VegetationSupport.Get(level, newRoot), "lava")) return false;
                    if (SpeleothemUtils.IsCircleMostlyEmbeddedInStone(level, wind.Offset(newRoot), _radius))
                    {
                        _root = newRoot;
                        return true;
                    }
                    newRoot = newRoot.Offset(_pointingUp ? Direction.Down : Direction.Up);
                }
                _radius /= 2;
            }
            return false;
        }

        //GetHeightAtRadius 指定水平距离处的高度 对应原版 getHeightAtRadius
        private int GetHeightAtRadius(float checkRadius)
            => (int)SpeleothemUtils.GetSpeleothemHeight(checkRadius, _radius, _scale, _bluntness);

        //PlaceBlocks 逐列放石笋体 对应原版 placeBlocks
        public void PlaceBlocks(WorldGenRegion level, RandomSource random, WindOffsetter wind)
        {
            for (var dx = -_radius; dx <= _radius; dx++)
            {
                for (var dz = -_radius; dz <= _radius; dz++)
                {
                    var currentRadius = Mth.Sqrt((dx * dx) + (dz * dz));
                    if (currentRadius > _radius) continue;
                    var height = GetHeightAtRadius(currentRadius);
                    if (height <= 0) continue;
                    if (random.NextFloat() < 0.2d) height = (int)(height * Mth.RandomBetween(random, 0.8f, 1.0f));
                    var pos = _root.Offset(dx, 0, dz);
                    var hasBeenOutOfStone = false;
                    var maxY = _pointingUp
                        ? level.GetHeight(Heightmap.Types.WorldSurfaceWg, pos.X, pos.Z)
                        : int.MaxValue;
                    for (var i = 0; i < height && pos.Y < maxY; i++)
                    {
                        var windAdjustedPos = wind.Offset(pos);
                        if (SpeleothemUtils.IsEmptyOrWaterOrLava(level, windAdjustedPos))
                        {
                            hasBeenOutOfStone = true;
                            VegetationSupport.Set(level, windAdjustedPos, VegetationSupport.StateOf("dripstone_block"));
                        }
                        else if (hasBeenOutOfStone
                                 && VegetationSupport.InTag(VegetationSupport.Get(level, windAdjustedPos),
                                     SpeleothemUtils.BaseStoneOverworldTag))
                        {
                            break;
                        }
                        pos = pos.Offset(_pointingUp ? Direction.Up : Direction.Down);
                    }
                }
            }
        }

        //IsSuitableForWind 半径与钝度都够大才吃风偏移 对应原版 isSuitableForWind
        public bool IsSuitableForWind(LargeDripstoneConfiguration config)
            => _radius >= config.MinRadiusForWind && _bluntness >= config.MinBluntnessForWind;
    }

    //WindOffsetter 按高度线性错位的风偏移 对应原版 WindOffsetter
    private sealed class WindOffsetter
    {
        private readonly int _originY;
        private readonly double _windX;
        private readonly double _windZ;
        private readonly int _maxOffset;
        private readonly bool _hasWind;

        private WindOffsetter()
        {
            _hasWind = false;
        }

        public WindOffsetter(int originY, RandomSource random, FloatProvider windSpeedRange, int maxOffset)
        {
            _originY = originY;
            _maxOffset = maxOffset;
            _hasWind = true;
            var speed = windSpeedRange.Sample(random);
            var direction = Mth.RandomBetween(random, 0.0f, Mth.Pi);
            _windX = Mth.Cos(direction) * speed;
            _windZ = Mth.Sin(direction) * speed;
        }

        public static WindOffsetter NoWind() => new();

        //Offset 离原点越远错位越大 对应原版 offset
        public BlockPos Offset(BlockPos pos)
        {
            if (!_hasWind) return pos;
            var dy = _originY - pos.Y;
            var dx = Mth.Clamp(Mth.Floor(_windX * dy), -_maxOffset, _maxOffset);
            var dz = Mth.Clamp(Mth.Floor(_windZ * dy), -_maxOffset, _maxOffset);
            return pos.Offset(dx, 0, dz);
        }
    }
}

//VegetationBootstrap 地表植被类特征注册入口
//触碰各静态 Instance 使静态注册生效
public static class VegetationBootstrap
{
    public static void RegisterAll()
    {
        _ = FallenTreeFeature.Instance;
        _ = VinesFeature.Instance;
        _ = BambooFeature.Instance;
        _ = HugeRedMushroomFeature.Instance;
        _ = HugeBrownMushroomFeature.Instance;

        _ = SeagrassFeature.Instance;
        _ = KelpFeature.Instance;
        _ = SeaPickleFeature.Instance;
        _ = SpringFeature.Instance;
        _ = GlowstoneFeature.Instance;
        _ = SnowAndFreezeFeature.Instance;
        _ = BlueIceFeature.Instance;
        _ = IcebergFeature.Instance;
        _ = BonusChestFeature.Instance;
        _ = UnderwaterMagmaFeature.Instance;

        _ = VegetationPatchFeature.Instance;
        _ = WaterloggedVegetationPatchFeature.WaterloggedInstance;
        _ = MultifaceGrowthFeature.Instance;
        _ = RootSystemFeature.Instance;

        _ = SpeleothemClusterFeature.Instance;
        _ = LargeDripstoneFeature.Instance;
    }
}
