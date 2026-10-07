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

//ColumnScan result of scanning one vertical column, maps to vanilla Column
//Floor and ceiling are each optional; a range exists only when both ends are present
internal sealed class ColumnScan
{
    public int? Floor { get; }
    public int? Ceiling { get; }

    public ColumnScan(int? floor, int? ceiling)
    {
        Floor = floor;
        Ceiling = ceiling;
    }

    //IsRange a complete range requires both ends, maps to vanilla Column.Range
    public bool IsRange => Floor is not null && Ceiling is not null;

    //Height net height of the range, maps to vanilla Column.Range.height; only present for a range
    public int? Height => IsRange ? Ceiling!.Value - Floor!.Value - 1 : null;

    //WithFloor replace the floor end, maps to vanilla withFloor
    public ColumnScan WithFloor(int floor) => new(floor, Ceiling);
}

//Column vertical scan, maps to vanilla net.minecraft.world.level.levelgen.Column
//Searches outward from the center for the first cell that fails the inner condition, then checks whether it satisfies the boundary condition to decide floor or ceiling
internal static class Column
{
    //Scan scan once upward and once downward from the position, maps to vanilla Column.scan
    public static ColumnScan? Scan(WorldGenRegion level, BlockPos pos, int searchRange,
        Func<BlockState, bool> insideColumn, Func<BlockState, bool> validEdge)
    {
        if (!insideColumn(VegetationSupport.Get(level, pos))) return null;
        var ceiling = ScanDirection(level, searchRange, insideColumn, validEdge, pos, Direction.Up);
        var floor = ScanDirection(level, searchRange, insideColumn, validEdge, pos, Direction.Down);
        return new ColumnScan(floor, ceiling);
    }

    //ScanDirection one-way scan: advance along the direction to the first non-inner position, then test whether it counts as a boundary, maps to vanilla scanDirection
    private static int? ScanDirection(WorldGenRegion level, int searchRange, Func<BlockState, bool> insideColumn,
        Func<BlockState, bool> validEdge, BlockPos pos, Direction direction)
    {
        var y = pos.Y;
        for (var i = 1; i < searchRange && insideColumn(VegetationSupport.Get(level, new BlockPos(pos.X, y, pos.Z)));
             i++) y += direction.StepY;
        return validEdge(VegetationSupport.Get(level, new BlockPos(pos.X, y, pos.Z))) ? y : null;
    }
}

//SpeleothemUtils shared checks and generation for speleothems, maps to vanilla SpeleothemUtils
internal static class SpeleothemUtils
{
    //BaseStoneOverworldTag overworld base stone tag, maps to vanilla BlockTags.BASE_STONE_OVERWORLD
    public static readonly TagKey<RegBlock> BaseStoneOverworldTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("base_stone_overworld"));

    //IsEmptyOrWater air or water, maps to vanilla isEmptyOrWater
    public static bool IsEmptyOrWater(BlockState state)
        => state.Owner.IsAir || VegetationSupport.IsState(state, "water");

    //IsNeitherEmptyNorWater neither air nor water, maps to vanilla isNeitherEmptyNorWater
    public static bool IsNeitherEmptyNorWater(BlockState state) => !IsEmptyOrWater(state);

    //IsEmptyOrWaterOrLava air, water or lava, maps to vanilla isEmptyOrWaterOrLava
    public static bool IsEmptyOrWaterOrLava(BlockState state)
        => state.Owner.IsAir || VegetationSupport.IsState(state, "water")
            || VegetationSupport.IsState(state, "lava");

    //IsEmptyOrWater position-based overload
    public static bool IsEmptyOrWater(WorldGenRegion level, BlockPos pos)
        => IsEmptyOrWater(VegetationSupport.Get(level, pos));

    //IsEmptyOrWaterOrLava position-based overload
    public static bool IsEmptyOrWaterOrLava(WorldGenRegion level, BlockPos pos)
        => IsEmptyOrWaterOrLava(VegetationSupport.Get(level, pos));

    //IsBase whether the state is base stone or replaceable, maps to vanilla isBase
    public static bool IsBase(BlockState state, RegBlock baseBlock, HolderSet<RegBlock> replaceableBlocks)
        => state.Owner == baseBlock || VegetationSupport.IsInSet(state, replaceableBlocks);

    //IsBaseOrLava base stone or lava, maps to vanilla isBaseOrLava
    public static bool IsBaseOrLava(BlockState state, RegBlock baseBlock, HolderSet<RegBlock> replaceableBlocks)
        => IsBase(state, baseBlock, replaceableBlocks) || VegetationSupport.IsState(state, "lava");

    //PlaceBaseBlockIfPossible replace with base stone when possible, maps to vanilla placeBaseBlockIfPossible
    public static bool PlaceBaseBlockIfPossible(WorldGenRegion level, BlockPos pos, RegBlock baseBlock,
        HolderSet<RegBlock> replaceableBlocks)
    {
        var state = VegetationSupport.Get(level, pos);
        if (!VegetationSupport.IsInSet(state, replaceableBlocks)) return false;
        VegetationSupport.Set(level, pos, baseBlock.DefaultBlockState);
        return true;
    }

    //GetSpeleothemHeight compute speleothem height from radius and distance, maps to vanilla getSpeleothemHeight
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

    //IsCircleMostlyEmbeddedInStone whether the whole circle is outside cavities, maps to vanilla isCircleMostlyEmbeddedInStone
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

    //GrowSpeleothem grow one speleothem from the start along the tip direction, maps to vanilla growSpeleothem
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

    //BuildBaseToTipColumn emit segments from thick to thin along the length, maps to vanilla buildBaseToTipColumn
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

    //CreatePointedBlock build one speleothem segment with a fixed orientation and thickness, maps to vanilla createPointedBlock
    private static BlockState CreatePointedBlock(Direction direction, string thickness)
        => VegetationSupport.WithProperty(
            VegetationSupport.WithProperty(VegetationSupport.StateOf("pointed_dripstone"), "vertical_direction",
                direction == Direction.Up ? "up" : "down"),
            "thickness", thickness);
}

//ClampedNormalFloat clamped normal float provider, maps to vanilla ClampedNormalFloat
//The project's float provider system lacks this type; the speleothem cluster wetness field needs it
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

    //Sample clamp after normal sampling, maps to vanilla sample
    public override float Sample(RandomSource random)
        => Mth.Clamp(Mth.Normal(random, Mean, Deviation), Min, Max);
}

//VegetationFloatProviderCodec float provider codec: try the generic entry first, then handle clamped_normal
//The speleothem cluster wetness is clamped_normal, which the generic entry cannot recognize, so catch it here
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
            return DataResult<FloatProvider>.Error(() => "clamped_normal requires mean/deviation/min/max");
        if (max < min) return DataResult<FloatProvider>.Error(() => "clamped_normal max must not be less than min");
        return DataResult<FloatProvider>.Success(
            new ClampedNormalFloat(mean.Value, deviation.Value, min.Value, max.Value));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, FloatProvider value)
        => FloatProviders.Codec.EncodeStart(ops, value);

    //ReadFloat read a float field; returns null when missing or not a number
    private static float? ReadFloat<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (float)value.GetOrThrow() : null;
    }
}

//SpeleothemClusterConfiguration speleothem cluster configuration, maps to vanilla SpeleothemClusterConfiguration
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

//SpeleothemClusterFeature speleothem cluster feature, maps to vanilla SpeleothemClusterFeature
//Scans the ceiling and floor of each column within the radius, then per column decides whether to place a pool, stalactites or stalagmites and their lengths
//Random consumption and loop bounds follow vanilla exactly; a wrong order yields different cave ceilings for the same seed
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

    //PlaceColumn generate one column, maps to vanilla placeColumn
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

    //GetSpeleothemHeight sample the length after biasing by distance from the center, maps to vanilla getSpeleothemHeight
    private static int GetSpeleothemHeight(RandomSource random, int dx, int dz, float density, int maxHeight,
        SpeleothemClusterConfiguration config)
    {
        if (random.NextFloat() > density) return 0;
        var distanceFromCenter = Math.Abs(dx) + Math.Abs(dz);
        var heightMean = (float)Mth.ClampedMap((double)distanceFromCenter, 0.0d,
            config.MaxDistanceFromCenterAffectingHeightBias, maxHeight / 2.0d, 0.0d);
        return (int)Mth.Clamp(Mth.Normal(random, heightMean, config.HeightDeviation), 0.0f, maxHeight);
    }

    //CanPlacePool a pool edge cell requires rock or water all around and below, maps to vanilla canPlacePool
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

    //CanBeAdjacentToWater the cell is overworld base stone or water, maps to vanilla canBeAdjacentToWater
    private static bool CanBeAdjacentToWater(WorldGenRegion level, BlockPos pos)
    {
        var state = VegetationSupport.Get(level, pos);
        return VegetationSupport.InTag(state, SpeleothemUtils.BaseStoneOverworldTag)
            || VegetationSupport.IsState(state, "water");
    }

    //ReplaceBlocksWithBaseBlocks replace with base stone continuously from the start along the direction, maps to vanilla replaceBlocksWithBaseBlocks
    private static void ReplaceBlocksWithBaseBlocks(WorldGenRegion level, BlockPos firstPos, int maxCount,
        Direction direction, SpeleothemClusterConfiguration config)
    {
        var pos = firstPos;
        for (var i = 0;
             i < maxCount && SpeleothemUtils.PlaceBaseBlockIfPossible(level, pos, config.BaseBlock.Owner,
                 config.ReplaceableBlocks);
             i++) pos = pos.Offset(direction);
    }

    //GetChanceOfStalagmiteOrStalactite closer to the edge means higher chance, maps to vanilla getChanceOfStalagmiteOrStalactite
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

    //AtY replace the Y coordinate
    private static BlockPos AtY(BlockPos pos, int y) => new(pos.X, y, pos.Z);
}

//LargeDripstoneConfiguration large dripstone configuration, maps to vanilla LargeDripstoneConfiguration
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

//LargeDripstoneFeature large dripstone feature, maps to vanilla LargeDripstoneFeature
//Scans a cave range, picks a radius from its height, then grows one giant dripstone from each end offset by the wind
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

    //LargeDripstone one giant dripstone, maps to vanilla LargeDripstone
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

        //GetHeight height on the central axis, maps to vanilla getHeight
        private int GetHeight() => GetHeightAtRadius(0.0f);

        //MoveBackUntilBaseIsInsideStoneAndShrinkRadiusIfNecessary move back until the base is inside stone, halving the radius if needed, maps to the vanilla method of the same name
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

        //GetHeightAtRadius height at the given horizontal distance, maps to vanilla getHeightAtRadius
        private int GetHeightAtRadius(float checkRadius)
            => (int)SpeleothemUtils.GetSpeleothemHeight(checkRadius, _radius, _scale, _bluntness);

        //PlaceBlocks place the dripstone body column by column, maps to vanilla placeBlocks
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

        //IsSuitableForWind takes the wind offset only when the radius and bluntness are large enough, maps to vanilla isSuitableForWind
        public bool IsSuitableForWind(LargeDripstoneConfiguration config)
            => _radius >= config.MinRadiusForWind && _bluntness >= config.MinBluntnessForWind;
    }

    //WindOffsetter wind offset that shifts linearly with height, maps to vanilla WindOffsetter
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

        //Offset the farther from the origin the larger the shift, maps to vanilla offset
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

//VegetationBootstrap surface vegetation feature registration entry
//Touching each static Instance triggers static registration
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
