using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using PlacementNS = NetCraft.Game.World.Level.LevelGen.Placement;
using StructureNS = NetCraft.Game.World.Level.LevelGen.Structure;
using RegBlock = NetCraft.Registry.Block;
using RegistryConfiguredFeature = NetCraft.Registry.ConfiguredFeature;
using RegistryPlacedFeature = NetCraft.Registry.PlacedFeature;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl;

//OreTargetState 替换目标 对应原版 OreConfiguration.TargetBlockState
//一条「规则测试 + 目标状态」的替换项 ore 与 replace_single_block 共用
public sealed class OreTargetState
{
    public static readonly Codec<OreTargetState> Codec =
        RecordCodecBuilder.Of2<OreTargetState, StructureNS.RuleTest, BlockState>(
            StructureNS.RuleTest.Codec.FieldOf("target")
                .ForGetter<OreTargetState, StructureNS.RuleTest>(t => t.Target),
            BlockStateCodec.Instance.FieldOf("state")
                .ForGetter<OreTargetState, BlockState>(t => t.State),
            (target, state) => new OreTargetState(target, state));

    public StructureNS.RuleTest Target { get; }
    public BlockState State { get; }

    public OreTargetState(StructureNS.RuleTest target, BlockState state)
    {
        Target = target;
        State = state;
    }
}

//OreConfiguration 矿石配置 对应原版 OreConfiguration
public sealed class OreConfiguration : FeatureConfiguration
{
    public static readonly Codec<OreConfiguration> Codec =
        RecordCodecBuilder.Of3<OreConfiguration, IReadOnlyList<OreTargetState>, int, float>(
            OreTargetState.Codec.ListOf().FieldOf("targets")
                .ForGetter<OreConfiguration, IReadOnlyList<OreTargetState>>(c => c.TargetStates),
            Codecs.Int.FieldOf("size").ForGetter<OreConfiguration, int>(c => c.Size),
            Codecs.Float.FieldOf("discard_chance_on_air_exposure")
                .ForGetter<OreConfiguration, float>(c => c.DiscardChanceOnAirExposure),
            (targets, size, discardChance) => new OreConfiguration(targets, size, discardChance));

    public IReadOnlyList<OreTargetState> TargetStates { get; }
    public int Size { get; }
    public float DiscardChanceOnAirExposure { get; }

    public OreConfiguration(IReadOnlyList<OreTargetState> targetStates, int size, float discardChanceOnAirExposure)
    {
        TargetStates = targetStates;
        Size = size;
        DiscardChanceOnAirExposure = discardChanceOnAirExposure;
    }
}

//OreFeature 矿脉特征 对应原版 OreFeature
public sealed class OreFeature : Feature<OreConfiguration>
{
    private const string FeatureId = "ore";

    public static readonly OreFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new OreFeature());

    private OreFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), OreConfiguration.Codec) { }

    protected override bool Place(OreConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var origin = context.Origin;
        var level = context.Level;
        var dir = random.NextFloat() * Mth.Pi;
        var spreadXY = config.Size / 8.0f;
        var maxRadius = Mth.Ceil((config.Size / 16.0f * 2.0f + 1.0f) / 2.0f);
        var x0 = (double)origin.X + (Math.Sin(dir) * spreadXY);
        var x1 = (double)origin.X - (Math.Sin(dir) * spreadXY);
        var z0 = (double)origin.Z + (Math.Cos(dir) * spreadXY);
        var z1 = (double)origin.Z - (Math.Cos(dir) * spreadXY);
        var y0 = (double)(origin.Y + random.NextInt(3) - 2);
        var y1 = (double)(origin.Y + random.NextInt(3) - 2);
        var xStart = origin.X - Mth.Ceil(spreadXY) - maxRadius;
        var yStart = origin.Y - 2 - maxRadius;
        var zStart = origin.Z - Mth.Ceil(spreadXY) - maxRadius;
        var sizeXZ = 2 * (Mth.Ceil(spreadXY) + maxRadius);
        var sizeY = 2 * (2 + maxRadius);
        for (var xProbe = xStart; xProbe <= xStart + sizeXZ; xProbe++)
        {
            for (var zProbe = zStart; zProbe <= zStart + sizeXZ; zProbe++)
            {
                if (yStart > level.GetHeight(Heightmap.Types.OceanFloorWg, xProbe, zProbe)) continue;
                return DoPlace(level, random, config, x0, x1, z0, z1, y0, y1,
                    xStart, yStart, zStart, sizeXZ, sizeY);
            }
        }
        return false;
    }

    //DoPlace 按一段线段上的球形相交推算矿脉体 对应原版 doPlace
    private static bool DoPlace(WorldGenRegion level, RandomSource random, OreConfiguration config,
        double x0, double x1, double z0, double z1, double y0, double y1,
        int xStart, int yStart, int zStart, int sizeXZ, int sizeY)
    {
        var placed = 0;
        var tested = new bool[sizeXZ * sizeY * sizeXZ];
        var size = config.Size;
        var data = new double[size * 4];
        for (var i = 0; i < size; i++)
        {
            var step = (float)i / size;
            var xx = Mth.Lerp(step, x0, x1);
            var yy = Mth.Lerp(step, y0, y1);
            var zz = Mth.Lerp(step, z0, z1);
            var ss = random.NextDouble() * size / 16.0d;
            data[i * 4] = xx;
            data[i * 4 + 1] = yy;
            data[i * 4 + 2] = zz;
            data[i * 4 + 3] = ((Mth.Sin(Mth.Pi * step) + 1.0f) * ss + 1.0d) / 2.0d;
        }
        for (var i1 = 0; i1 < size - 1; i1++)
        {
            if (data[i1 * 4 + 3] <= 0.0d) continue;
            for (var i2 = i1 + 1; i2 < size; i2++)
            {
                if (data[i2 * 4 + 3] <= 0.0d) continue;
                var dx = data[i1 * 4] - data[i2 * 4];
                var dy = data[i1 * 4 + 1] - data[i2 * 4 + 1];
                var dz = data[i1 * 4 + 2] - data[i2 * 4 + 2];
                var dr = data[i1 * 4 + 3] - data[i2 * 4 + 3];
                if (dr * dr <= dx * dx + dy * dy + dz * dz) continue;
                if (dr > 0.0d) data[i2 * 4 + 3] = -1.0d;
                else data[i1 * 4 + 3] = -1.0d;
            }
        }
        for (var i = 0; i < size; i++)
        {
            var r = data[i * 4 + 3];
            if (r < 0.0d) continue;
            var xx = data[i * 4];
            var yy = data[i * 4 + 1];
            var zz = data[i * 4 + 2];
            var xMin = Math.Max(Mth.Floor(xx - r), xStart);
            var yMin = Math.Max(Mth.Floor(yy - r), yStart);
            var zMin = Math.Max(Mth.Floor(zz - r), zStart);
            var xMax = Math.Max(Mth.Floor(xx + r), xMin);
            var yMax = Math.Max(Mth.Floor(yy + r), yMin);
            var zMax = Math.Max(Mth.Floor(zz + r), zMin);
            for (var x = xMin; x <= xMax; x++)
            {
                var xd = (x + 0.5d - xx) / r;
                if (xd * xd >= 1.0d) continue;
                for (var y = yMin; y <= yMax; y++)
                {
                    var yd = (y + 0.5d - yy) / r;
                    if (xd * xd + yd * yd >= 1.0d) continue;
                    for (var z = zMin; z <= zMax; z++)
                    {
                        var zd = (z + 0.5d - zz) / r;
                        if (xd * xd + yd * yd + zd * zd >= 1.0d) continue;
                        if (y < level.MinBuildHeight() || y >= level.MaxBuildHeight()) continue;
                        var bit = (x - xStart) + ((y - yStart) * sizeXZ) + ((z - zStart) * sizeXZ * sizeY);
                        if (tested[bit]) continue;
                        tested[bit] = true;
                        if (!level.EnsureCanWrite(x >> 4, z >> 4)) continue;
                        var blockState = level.GetBlockState(x, y, z);
                        foreach (var target in config.TargetStates)
                        {
                            if (!CanPlaceOre(blockState, level, random, config, target, new BlockPos(x, y, z))) continue;
                            level.SetBlockState(x, y, z, target.State);
                            placed++;
                            break;
                        }
                    }
                }
            }
        }
        return placed > 0;
    }

    //CanPlaceOre 判定该位置能否被替换成目标状态 对应原版 canPlaceOre
    public static bool CanPlaceOre(BlockState state, WorldGenRegion level, RandomSource random,
        OreConfiguration config, OreTargetState target, BlockPos pos)
    {
        if (!target.Target.Test(state, random)) return false;
        if (ShouldSkipAirCheck(random, config.DiscardChanceOnAirExposure)) return true;
        return !IsAdjacentToAir(level, pos);
    }

    //ShouldSkipAirCheck 按暴露比例决定是否跳过邻空判定 对应原版 shouldSkipAirCheck
    protected static bool ShouldSkipAirCheck(RandomSource random, float discardChanceOnAirExposure)
    {
        if (discardChanceOnAirExposure <= 0.0f) return true;
        if (discardChanceOnAirExposure >= 1.0f) return false;
        return random.NextFloat() >= discardChanceOnAirExposure;
    }

    //IsAdjacentToAir 六向邻居里有空气即为暴露 对应原版 isAdjacentToAir
    public static bool IsAdjacentToAir(WorldGenRegion level, BlockPos pos)
    {
        foreach (var direction in Direction.Values)
        {
            var state = level.GetBlockState(pos.X + direction.StepX, pos.Y + direction.StepY, pos.Z + direction.StepZ);
            if (state.Owner.IsAir) return true;
        }
        return false;
    }
}

//ScatteredOreFeature 散状矿石特征 对应原版 ScatteredOreFeature
public sealed class ScatteredOreFeature : Feature<OreConfiguration>
{
    private const string FeatureId = "scattered_ore";

    public static readonly ScatteredOreFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new ScatteredOreFeature());

    private ScatteredOreFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), OreConfiguration.Codec) { }

    protected override bool Place(OreConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var random = context.Random;
        var origin = context.Origin;
        var tries = random.NextInt(config.Size + 1);
        for (var i = 0; i < tries; i++)
        {
            var maxDistance = Math.Min(i, 7);
            var target = origin.Offset(
                RandomOffset(random, maxDistance),
                RandomOffset(random, maxDistance),
                RandomOffset(random, maxDistance));
            var blockState = level.GetBlockState(target.X, target.Y, target.Z);
            foreach (var targetState in config.TargetStates)
            {
                if (!OreFeature.CanPlaceOre(blockState, level, random, config, targetState, target)) continue;
                level.SetBlockState(target.X, target.Y, target.Z, targetState.State);
                break;
            }
        }
        return true;
    }

    //RandomOffset 单轴偏移取两次随机数之差 对应原版 getRandomPlacementInOneAxisRelativeToOrigin
    private static int RandomOffset(RandomSource random, int maxDistance)
        => (int)Math.Floor(((random.NextFloat() - random.NextFloat()) * maxDistance) + 0.5f);
}

//LayerConfiguration 填充层配置 对应原版 LayerConfiguration
public sealed class LayerConfiguration : FeatureConfiguration
{
    public static readonly Codec<LayerConfiguration> Codec =
        RecordCodecBuilder.Of2<LayerConfiguration, int, BlockState>(
            Codecs.Int.FieldOf("height").ForGetter<LayerConfiguration, int>(c => c.Height),
            BlockStateCodec.Instance.FieldOf("state").ForGetter<LayerConfiguration, BlockState>(c => c.State),
            (height, state) => new LayerConfiguration(height, state));

    public int Height { get; }
    public BlockState State { get; }

    public LayerConfiguration(int height, BlockState state)
    {
        Height = height;
        State = state;
    }
}

//FillLayerFeature 单层填充特征 对应原版 FillLayerFeature
//超平坦世界用来铺一层方块 只填空气
public sealed class FillLayerFeature : Feature<LayerConfiguration>
{
    private const string FeatureId = "fill_layer";

    public static readonly FillLayerFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new FillLayerFeature());

    private FillLayerFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), LayerConfiguration.Codec) { }

    protected override bool Place(LayerConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var level = context.Level;
        for (var dx = 0; dx < 16; dx++)
        {
            for (var dz = 0; dz < 16; dz++)
            {
                var x = origin.X + dx;
                var z = origin.Z + dz;
                var y = level.MinBuildHeight() + config.Height;
                if (!level.GetBlockState(x, y, z).Owner.IsAir) continue;
                level.SetBlockState(x, y, z, config.State);
            }
        }
        return true;
    }
}

//DiskConfiguration 圆盘配置 对应原版 DiskConfiguration
public sealed class DiskConfiguration : FeatureConfiguration
{
    public static readonly Codec<DiskConfiguration> Codec =
        RecordCodecBuilder.Of4<DiskConfiguration, BlockStateProvider, BlockPredicate, IntProvider, int>(
            BlockStateProvider.Codec.FieldOf("state_provider")
                .ForGetter<DiskConfiguration, BlockStateProvider>(c => c.StateProvider),
            BlockPredicate.Codec.FieldOf("target")
                .ForGetter<DiskConfiguration, BlockPredicate>(c => c.Target),
            IntProviders.Codec.FieldOf("radius").ForGetter<DiskConfiguration, IntProvider>(c => c.Radius),
            Codecs.Int.FieldOf("half_height").ForGetter<DiskConfiguration, int>(c => c.HalfHeight),
            (stateProvider, target, radius, halfHeight) =>
                new DiskConfiguration(stateProvider, target, radius, halfHeight));

    public BlockStateProvider StateProvider { get; }
    public BlockPredicate Target { get; }
    public IntProvider Radius { get; }
    public int HalfHeight { get; }

    public DiskConfiguration(BlockStateProvider stateProvider, BlockPredicate target, IntProvider radius, int halfHeight)
    {
        StateProvider = stateProvider;
        Target = target;
        Radius = radius;
        HalfHeight = halfHeight;
    }
}

//DiskFeature 圆盘特征 对应原版 DiskFeature
public sealed class DiskFeature : Feature<DiskConfiguration>
{
    private const string FeatureId = "disk";

    public static readonly DiskFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new DiskFeature());

    private DiskFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), DiskConfiguration.Codec) { }

    protected override bool Place(DiskConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var level = context.Level;
        var random = context.Random;
        var placedAny = false;
        var top = origin.Y + config.HalfHeight;
        var bottom = origin.Y - config.HalfHeight - 1;
        var r = config.Radius.Sample(random);
        for (var x = origin.X - r; x <= origin.X + r; x++)
        {
            for (var z = origin.Z - r; z <= origin.Z + r; z++)
            {
                var xd = x - origin.X;
                var zd = z - origin.Z;
                if (xd * xd + zd * zd > r * r) continue;
                if (PlaceColumn(config, level, random, top, bottom, x, z)) placedAny = true;
            }
        }
        return placedAny;
    }

    //PlaceColumn 从上往下逐格替换 对应原版 placeColumn
    private static bool PlaceColumn(DiskConfiguration config, WorldGenRegion level, RandomSource random,
        int top, int bottom, int x, int z)
    {
        var placedAny = false;
        for (var y = top; y > bottom; y--)
        {
            var pos = new BlockPos(x, y, z);
            if (!config.Target.Test(level, pos)) continue;
            var state = config.StateProvider.GetOptionalState(level, random, pos);
            if (state is not { } toPlace) continue;
            level.SetBlockState(x, y, z, toPlace);
            placedAny = true;
        }
        return placedAny;
    }
}

//BlockBlobConfiguration 方块团配置 对应原版 BlockBlobConfiguration
public sealed class BlockBlobConfiguration : FeatureConfiguration
{
    public static readonly Codec<BlockBlobConfiguration> Codec =
        RecordCodecBuilder.Of2<BlockBlobConfiguration, BlockState, BlockPredicate>(
            BlockStateCodec.Instance.FieldOf("state")
                .ForGetter<BlockBlobConfiguration, BlockState>(c => c.State),
            BlockPredicate.Codec.FieldOf("can_place_on")
                .ForGetter<BlockBlobConfiguration, BlockPredicate>(c => c.CanPlaceOn),
            (state, canPlaceOn) => new BlockBlobConfiguration(state, canPlaceOn));

    public BlockState State { get; }
    public BlockPredicate CanPlaceOn { get; }

    public BlockBlobConfiguration(BlockState state, BlockPredicate canPlaceOn)
    {
        State = state;
        CanPlaceOn = canPlaceOn;
    }
}

//BlockBlobFeature 方块团特征 对应原版 BlockBlobFeature
public sealed class BlockBlobFeature : Feature<BlockBlobConfiguration>
{
    private const string FeatureId = "block_blob";

    public static readonly BlockBlobFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BlockBlobFeature());

    private BlockBlobFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), BlockBlobConfiguration.Codec) { }

    protected override bool Place(BlockBlobConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var random = context.Random;
        var origin = context.Origin;
        while (origin.Y > level.MinBuildHeight() + 3 && !config.CanPlaceOn.Test(level, origin.Offset(0, -1, 0)))
            origin = origin.Offset(0, -1, 0);
        if (origin.Y <= level.MinBuildHeight() + 3) return false;
        for (var blob = 0; blob < 3; blob++)
        {
            var xr = random.NextInt(2);
            var yr = random.NextInt(2);
            var zr = random.NextInt(2);
            var tr = ((xr + yr + zr) * 0.333f) + 0.5f;
            for (var x = origin.X - xr; x <= origin.X + xr; x++)
            {
                for (var y = origin.Y - yr; y <= origin.Y + yr; y++)
                {
                    for (var z = origin.Z - zr; z <= origin.Z + zr; z++)
                    {
                        var dx = x - origin.X;
                        var dy = y - origin.Y;
                        var dz = z - origin.Z;
                        if (dx * dx + dy * dy + dz * dz > tr * tr) continue;
                        level.SetBlockState(x, y, z, config.State);
                    }
                }
            }
            origin = origin.Offset(-1 + random.NextInt(2), -random.NextInt(2), -1 + random.NextInt(2));
        }
        return true;
    }
}

//BlockPileConfiguration 方块堆配置 对应原版 BlockPileConfiguration
public sealed class BlockPileConfiguration : FeatureConfiguration
{
    public static readonly Codec<BlockPileConfiguration> Codec =
        new SingleFieldMapCodec<BlockPileConfiguration, BlockStateProvider>(
            BlockStateProvider.Codec.FieldOf("state_provider"),
            stateProvider => new BlockPileConfiguration(stateProvider),
            config => config.StateProvider);

    public BlockStateProvider StateProvider { get; }

    public BlockPileConfiguration(BlockStateProvider stateProvider) => StateProvider = stateProvider;
}

//BlockPileFeature 方块堆特征 对应原版 BlockPileFeature
public sealed class BlockPileFeature : Feature<BlockPileConfiguration>
{
    private const string FeatureId = "block_pile";

    public static readonly BlockPileFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BlockPileFeature());

    private BlockPileFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), BlockPileConfiguration.Codec) { }

    protected override bool Place(BlockPileConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var level = context.Level;
        var random = context.Random;
        if (origin.Y < level.MinBuildHeight() + 5) return false;
        var xr = 2 + random.NextInt(2);
        var zr = 2 + random.NextInt(2);
        for (var x = origin.X - xr; x <= origin.X + xr; x++)
        {
            for (var y = origin.Y; y <= origin.Y + 1; y++)
            {
                for (var z = origin.Z - zr; z <= origin.Z + zr; z++)
                {
                    var xd = origin.X - x;
                    var zd = origin.Z - z;
                    if (xd * xd + zd * zd <= (random.NextFloat() * 10.0f) - (random.NextFloat() * 6.0f))
                        TryPlaceBlock(level, new BlockPos(x, y, z), random, config);
                    else if (random.NextFloat() < 0.031d)
                        TryPlaceBlock(level, new BlockPos(x, y, z), random, config);
                }
            }
        }
        return true;
    }

    private static void TryPlaceBlock(WorldGenRegion level, BlockPos pos, RandomSource random,
        BlockPileConfiguration config)
    {
        if (!level.GetBlockState(pos.X, pos.Y, pos.Z).Owner.IsAir) return;
        if (!MayPlaceOn(level, pos)) return;
        level.SetBlockState(pos.X, pos.Y, pos.Z, config.StateProvider.GetState(level, random, pos));
    }

    //MayPlaceOn 下方要有整格上表面 对应原版 mayPlaceOn
    //原版下方是土径时再掷一次随机 本作没有土径方块 只保留整格判定
    private static bool MayPlaceOn(WorldGenRegion level, BlockPos pos)
    {
        var below = pos.Offset(0, -1, 0);
        var belowState = level.GetBlockState(below.X, below.Y, below.Z);
        return RegBlock.IsShapeFullBlock(belowState.Owner.GetOcclusionShape(belowState));
    }
}

//BlockColumnConfiguration 方块柱配置 对应原版 BlockColumnConfiguration
public sealed class BlockColumnConfiguration : FeatureConfiguration
{
    public static readonly Codec<BlockColumnConfiguration> Codec =
        RecordCodecBuilder.Of4<BlockColumnConfiguration, IReadOnlyList<BlockColumnConfiguration.Layer>,
            Direction, BlockPredicate, bool>(
            BlockColumnConfiguration.Layer.Codec.ListOf().FieldOf("layers")
                .ForGetter<BlockColumnConfiguration, IReadOnlyList<BlockColumnConfiguration.Layer>>(c => c.Layers),
            DirectionCodec.Instance.FieldOf("direction")
                .ForGetter<BlockColumnConfiguration, Direction>(c => c.Direction),
            BlockPredicate.Codec.FieldOf("allowed_placement")
                .ForGetter<BlockColumnConfiguration, BlockPredicate>(c => c.AllowedPlacement),
            Codecs.Bool.FieldOf("prioritize_tip")
                .ForGetter<BlockColumnConfiguration, bool>(c => c.PrioritizeTip),
            (layers, direction, allowedPlacement, prioritizeTip) =>
                new BlockColumnConfiguration(layers, direction, allowedPlacement, prioritizeTip));

    public IReadOnlyList<Layer> Layers { get; }
    public Direction Direction { get; }
    public BlockPredicate AllowedPlacement { get; }
    public bool PrioritizeTip { get; }

    public BlockColumnConfiguration(IReadOnlyList<Layer> layers, Direction direction,
        BlockPredicate allowedPlacement, bool prioritizeTip)
    {
        Layers = layers;
        Direction = direction;
        AllowedPlacement = allowedPlacement;
        PrioritizeTip = prioritizeTip;
    }

    //Layer 柱体的一段 对应原版 BlockColumnConfiguration.Layer
    public sealed class Layer
    {
        public static readonly Codec<Layer> Codec =
            RecordCodecBuilder.Of2<Layer, IntProvider, BlockStateProvider>(
                IntProviders.NonNegativeCodec.FieldOf("height").ForGetter<Layer, IntProvider>(l => l.Height),
                BlockStateProvider.Codec.FieldOf("provider")
                    .ForGetter<Layer, BlockStateProvider>(l => l.State),
                (height, state) => new Layer(height, state));

        public IntProvider Height { get; }
        public BlockStateProvider State { get; }

        public Layer(IntProvider height, BlockStateProvider state)
        {
            Height = height;
            State = state;
        }
    }
}

//BlockColumnFeature 方块柱特征 对应原版 BlockColumnFeature
public sealed class BlockColumnFeature : Feature<BlockColumnConfiguration>
{
    private const string FeatureId = "block_column";

    public static readonly BlockColumnFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BlockColumnFeature());

    private BlockColumnFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), BlockColumnConfiguration.Codec) { }

    protected override bool Place(BlockColumnConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var random = context.Random;
        var layerCount = config.Layers.Count;
        var layerHeights = new int[layerCount];
        var totalHeight = 0;
        for (var i = 0; i < layerCount; i++)
        {
            layerHeights[i] = config.Layers[i].Height.Sample(random);
            totalHeight += layerHeights[i];
        }
        if (totalHeight == 0) return false;
        var placePos = context.Origin;
        var nextPos = placePos.Offset(config.Direction);
        var placedHeight = 0;
        while (placedHeight < totalHeight)
        {
            if (!config.AllowedPlacement.Test(level, nextPos))
            {
                Truncate(layerHeights, totalHeight, placedHeight, config.PrioritizeTip);
                break;
            }
            nextPos = nextPos.Offset(config.Direction);
            placedHeight++;
        }
        for (var i = 0; i < layerCount; i++)
        {
            var count = layerHeights[i];
            if (count == 0) continue;
            var layer = config.Layers[i];
            for (var j = 0; j < count; j++)
            {
                level.SetBlockState(placePos.X, placePos.Y, placePos.Z,
                    layer.State.GetState(level, random, placePos));
                placePos = placePos.Offset(config.Direction);
            }
        }
        return true;
    }

    //Truncate 把超出可放置高度的余量按层的顺序削掉 对应原版 truncate
    private static void Truncate(int[] layerHeights, int totalHeight, int newHeight, bool prioritizeTip)
    {
        var amountToRemove = totalHeight - newHeight;
        var direction = prioritizeTip ? 1 : -1;
        var start = prioritizeTip ? 0 : layerHeights.Length - 1;
        var end = prioritizeTip ? layerHeights.Length : -1;
        var index = start;
        while (index != end && amountToRemove > 0)
        {
            var toRemove = Math.Min(layerHeights[index], amountToRemove);
            amountToRemove -= toRemove;
            layerHeights[index] -= toRemove;
            index += direction;
        }
    }
}

//ReplaceBlockConfiguration 单点替换配置 对应原版 ReplaceBlockConfiguration
public sealed class ReplaceBlockConfiguration : FeatureConfiguration
{
    public static readonly Codec<ReplaceBlockConfiguration> Codec =
        new SingleFieldMapCodec<ReplaceBlockConfiguration, IReadOnlyList<OreTargetState>>(
            OreTargetState.Codec.ListOf().FieldOf("targets"),
            targets => new ReplaceBlockConfiguration(targets),
            config => config.TargetStates);

    public IReadOnlyList<OreTargetState> TargetStates { get; }

    public ReplaceBlockConfiguration(IReadOnlyList<OreTargetState> targetStates) => TargetStates = targetStates;
}

//ReplaceSingleBlockFeature 单点替换特征 对应原版 ReplaceBlockFeature 注册名 replace_single_block
public sealed class ReplaceSingleBlockFeature : Feature<ReplaceBlockConfiguration>
{
    private const string FeatureId = "replace_single_block";

    public static readonly ReplaceSingleBlockFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new ReplaceSingleBlockFeature());

    private ReplaceSingleBlockFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), ReplaceBlockConfiguration.Codec) { }

    protected override bool Place(ReplaceBlockConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var state = level.GetBlockState(origin.X, origin.Y, origin.Z);
        foreach (var targetState in config.TargetStates)
        {
            if (!targetState.Target.Test(state, context.Random)) continue;
            level.SetBlockState(origin.X, origin.Y, origin.Z, targetState.State);
            return true;
        }
        return true;
    }
}

//SimpleBlockConfiguration 单方块配置 对应原版 SimpleBlockConfiguration
public sealed class SimpleBlockConfiguration : FeatureConfiguration
{
    public static readonly Codec<SimpleBlockConfiguration> Codec =
        RecordCodecBuilder.Of2<SimpleBlockConfiguration, BlockStateProvider, bool>(
            BlockStateProvider.Codec.FieldOf("to_place")
                .ForGetter<SimpleBlockConfiguration, BlockStateProvider>(c => c.ToPlace),
            Codecs.Bool.OptionalFieldOf("schedule_tick", false)
                .ForGetter<SimpleBlockConfiguration, bool>(c => c.ScheduleTick),
            (toPlace, scheduleTick) => new SimpleBlockConfiguration(toPlace, scheduleTick));

    public BlockStateProvider ToPlace { get; }
    public bool ScheduleTick { get; }

    public SimpleBlockConfiguration(BlockStateProvider toPlace, bool scheduleTick)
    {
        ToPlace = toPlace;
        ScheduleTick = scheduleTick;
    }
}

//SimpleBlockFeature 单方块特征 对应原版 SimpleBlockFeature
public sealed class SimpleBlockFeature : Feature<SimpleBlockConfiguration>
{
    private const string FeatureId = "simple_block";

    public static readonly SimpleBlockFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SimpleBlockFeature());

    private SimpleBlockFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), SimpleBlockConfiguration.Codec) { }

    protected override bool Place(SimpleBlockConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var state = config.ToPlace.GetOptionalState(level, context.Random, origin);
        if (state is not { } toPlace) return false;
        //原版此处还判 canSurvive 与双层植物/苔藓毯的特殊落位 本作这些方块行为未接入 先直接落位
        level.SetBlockState(origin.X, origin.Y, origin.Z, toPlace);
        return true;
    }
}

//CompositeFeatureConfiguration 组合特征配置 对应原版 CompositeFeatureConfiguration
//已放置特征集合为空的校验未做 解析时只解引用不做非空断言
public sealed class CompositeFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<CompositeFeatureConfiguration> Codec =
        new SingleFieldMapCodec<CompositeFeatureConfiguration, HolderSet<RegistryPlacedFeature>>(
            PlacedFeatureSetCodec.Instance.FieldOf("features"),
            features => new CompositeFeatureConfiguration(features),
            config => config.Features);

    public HolderSet<RegistryPlacedFeature> Features { get; }

    public CompositeFeatureConfiguration(HolderSet<RegistryPlacedFeature> features) => Features = features;

    public override IEnumerable<Holder<RegistryConfiguredFeature>> SubFeatures
        => Features.SelectMany(PlacedFeatureHelpers.SubFeatures);
}

//SequenceFeature 顺序特征 对应原版 SequenceFeature
public sealed class SequenceFeature : Feature<CompositeFeatureConfiguration>
{
    private const string FeatureId = "sequence";

    public static readonly SequenceFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SequenceFeature());

    private SequenceFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), CompositeFeatureConfiguration.Codec) { }

    protected override bool Place(CompositeFeatureConfiguration config, FeaturePlaceContext context)
    {
        foreach (var holder in config.Features)
        {
            if (!PlacedFeatureHelpers.Place(holder, context.Level, context.ChunkGenerator, context.Random,
                    context.Origin))
                return false;
        }
        return true;
    }
}

//SimpleRandomSelectorFeature 等概率随机选择特征 对应原版 SimpleRandomSelectorFeature
public sealed class SimpleRandomSelectorFeature : Feature<CompositeFeatureConfiguration>
{
    private const string FeatureId = "simple_random_selector";

    public static readonly SimpleRandomSelectorFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SimpleRandomSelectorFeature());

    private SimpleRandomSelectorFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), CompositeFeatureConfiguration.Codec) { }

    protected override bool Place(CompositeFeatureConfiguration config, FeaturePlaceContext context)
    {
        var index = context.Random.NextInt(config.Features.Size);
        var holder = config.Features.Get(index);
        return PlacedFeatureHelpers.Place(holder, context.Level, context.ChunkGenerator, context.Random,
            context.Origin);
    }
}

//WeightedRandomFeatureConfiguration 权重随机选择配置 对应原版 WeightedRandomFeatureConfiguration
public sealed class WeightedRandomFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<WeightedRandomFeatureConfiguration> Codec =
        new SingleFieldMapCodec<WeightedRandomFeatureConfiguration,
            WeightedList<Holder<RegistryPlacedFeature>>>(
            new WeightedListCodec<Holder<RegistryPlacedFeature>>(PlacedFeatureInlineRefCodec.Instance)
                .FieldOf("features"),
            features => new WeightedRandomFeatureConfiguration(features),
            config => config.Features);

    public WeightedList<Holder<RegistryPlacedFeature>> Features { get; }

    public WeightedRandomFeatureConfiguration(WeightedList<Holder<RegistryPlacedFeature>> features)
        => Features = features;

    public override IEnumerable<Holder<RegistryConfiguredFeature>> SubFeatures
        => Features.Unwrap().SelectMany(entry => PlacedFeatureHelpers.SubFeatures(entry.Value));
}

//WeightedRandomSelectorFeature 权重随机选择特征 对应原版 WeightedRandomSelectorFeature
public sealed class WeightedRandomSelectorFeature : Feature<WeightedRandomFeatureConfiguration>
{
    private const string FeatureId = "weighted_random_selector";

    public static readonly WeightedRandomSelectorFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new WeightedRandomSelectorFeature());

    private WeightedRandomSelectorFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), WeightedRandomFeatureConfiguration.Codec) { }

    protected override bool Place(WeightedRandomFeatureConfiguration config, FeaturePlaceContext context)
    {
        var picked = config.Features.GetRandom(context.Random);
        if (!picked.IsPresent) return false;
        return PlacedFeatureHelpers.Place(picked.GetOrThrow(), context.Level, context.ChunkGenerator,
            context.Random, context.Origin);
    }
}

//RandomBooleanFeatureConfiguration 布尔随机选择配置 对应原版 RandomBooleanFeatureConfiguration
public sealed class RandomBooleanFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<RandomBooleanFeatureConfiguration> Codec =
        RecordCodecBuilder.Of2<RandomBooleanFeatureConfiguration, Holder<RegistryPlacedFeature>,
            Holder<RegistryPlacedFeature>>(
            PlacedFeatureInlineRefCodec.Instance.FieldOf("feature_true")
                .ForGetter<RandomBooleanFeatureConfiguration, Holder<RegistryPlacedFeature>>(c => c.FeatureTrue),
            PlacedFeatureInlineRefCodec.Instance.FieldOf("feature_false")
                .ForGetter<RandomBooleanFeatureConfiguration, Holder<RegistryPlacedFeature>>(c => c.FeatureFalse),
            (featureTrue, featureFalse) => new RandomBooleanFeatureConfiguration(featureTrue, featureFalse));

    public Holder<RegistryPlacedFeature> FeatureTrue { get; }
    public Holder<RegistryPlacedFeature> FeatureFalse { get; }

    public RandomBooleanFeatureConfiguration(Holder<RegistryPlacedFeature> featureTrue,
        Holder<RegistryPlacedFeature> featureFalse)
    {
        FeatureTrue = featureTrue;
        FeatureFalse = featureFalse;
    }

    public override IEnumerable<Holder<RegistryConfiguredFeature>> SubFeatures
        => PlacedFeatureHelpers.SubFeatures(FeatureTrue).Concat(PlacedFeatureHelpers.SubFeatures(FeatureFalse));
}

//RandomBooleanSelectorFeature 布尔随机选择特征 对应原版 RandomBooleanSelectorFeature
public sealed class RandomBooleanSelectorFeature : Feature<RandomBooleanFeatureConfiguration>
{
    private const string FeatureId = "random_boolean_selector";

    public static readonly RandomBooleanSelectorFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new RandomBooleanSelectorFeature());

    private RandomBooleanSelectorFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), RandomBooleanFeatureConfiguration.Codec) { }

    protected override bool Place(RandomBooleanFeatureConfiguration config, FeaturePlaceContext context)
    {
        var picked = context.Random.NextBoolean() ? config.FeatureTrue : config.FeatureFalse;
        return PlacedFeatureHelpers.Place(picked, context.Level, context.ChunkGenerator, context.Random,
            context.Origin);
    }
}

//WeightedPlacedFeature 带权重的已放置特征 对应原版 WeightedPlacedFeature
//random_selector 逐项比 chance 不是按权重归一化抽签
public sealed class WeightedPlacedFeature
{
    public static readonly Codec<WeightedPlacedFeature> Codec =
        RecordCodecBuilder.Of2<WeightedPlacedFeature, Holder<RegistryPlacedFeature>, float>(
            PlacedFeatureInlineRefCodec.Instance.FieldOf("feature")
                .ForGetter<WeightedPlacedFeature, Holder<RegistryPlacedFeature>>(entry => entry.Feature),
            Codecs.Float.FieldOf("chance").ForGetter<WeightedPlacedFeature, float>(entry => entry.Chance),
            (feature, chance) => new WeightedPlacedFeature(feature, chance));

    public Holder<RegistryPlacedFeature> Feature { get; }
    public float Chance { get; }

    public WeightedPlacedFeature(Holder<RegistryPlacedFeature> feature, float chance)
    {
        Feature = feature;
        Chance = chance;
    }
}

//RandomFeatureConfiguration 随机选择配置 对应原版 RandomFeatureConfiguration
public sealed class RandomFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<RandomFeatureConfiguration> Codec =
        RecordCodecBuilder.Of2<RandomFeatureConfiguration, Holder<RegistryPlacedFeature>,
            IReadOnlyList<WeightedPlacedFeature>>(
            PlacedFeatureInlineRefCodec.Instance.FieldOf("default")
                .ForGetter<RandomFeatureConfiguration, Holder<RegistryPlacedFeature>>(c => c.DefaultFeature),
            WeightedPlacedFeature.Codec.ListOf().FieldOf("features")
                .ForGetter<RandomFeatureConfiguration, IReadOnlyList<WeightedPlacedFeature>>(c => c.Features),
            (defaultFeature, features) => new RandomFeatureConfiguration(defaultFeature, features));

    public Holder<RegistryPlacedFeature> DefaultFeature { get; }
    public IReadOnlyList<WeightedPlacedFeature> Features { get; }

    public RandomFeatureConfiguration(Holder<RegistryPlacedFeature> defaultFeature,
        IReadOnlyList<WeightedPlacedFeature> features)
    {
        DefaultFeature = defaultFeature;
        Features = features;
    }

    public override IEnumerable<Holder<RegistryConfiguredFeature>> SubFeatures
        => PlacedFeatureHelpers.SubFeatures(DefaultFeature)
            .Concat(Features.SelectMany(entry => PlacedFeatureHelpers.SubFeatures(entry.Feature)));
}

//RandomSelectorFeature 随机选择特征 对应原版 RandomSelectorFeature
//按顺序逐项掷 chance 先命中的先放 全都没命中才放 default
public sealed class RandomSelectorFeature : Feature<RandomFeatureConfiguration>
{
    private const string FeatureId = "random_selector";

    public static readonly RandomSelectorFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new RandomSelectorFeature());

    private RandomSelectorFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), RandomFeatureConfiguration.Codec) { }

    protected override bool Place(RandomFeatureConfiguration config, FeaturePlaceContext context)
    {
        foreach (var entry in config.Features)
        {
            if (context.Random.NextFloat() < entry.Chance)
                return PlacedFeatureHelpers.Place(entry.Feature, context.Level, context.ChunkGenerator,
                    context.Random, context.Origin);
        }
        return PlacedFeatureHelpers.Place(config.DefaultFeature, context.Level, context.ChunkGenerator,
            context.Random, context.Origin);
    }
}

//PlacedFeatureInlineRefCodec 已放置特征引用编解码 注册名与内联定义都接受
//对应原版 PlacedFeature.CODEC 的 allowInline 形态
//random_selector 的 default 是内联对象 features 里的 feature 是注册名 同一个 codec 要同时吃两种
internal sealed class PlacedFeatureInlineRefCodec : ScalarCodec<Holder<RegistryPlacedFeature>>
{
    public static readonly PlacedFeatureInlineRefCodec Instance = new();

    public override DataResult<Holder<RegistryPlacedFeature>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent) return PlacedFeatureRefCodec.Instance.Parse(ops, input);
        var inline = PlacementNS.PlacedFeature.Codec.Parse(ops, input);
        if (inline.Result().IsPresent)
            return DataResult<Holder<RegistryPlacedFeature>>.Success(
                Holder<RegistryPlacedFeature>.Direct(inline.GetOrThrow()));
        var reason = inline.MapOrElse(_ => string.Empty, error => error);
        return DataResult<Holder<RegistryPlacedFeature>>.Error(
            () => $"已放置特征既不是注册名也不是内联定义: {reason}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryPlacedFeature> value)
        => value.UnwrapKey() is null && value.Value is PlacementNS.PlacedFeature inline
            ? PlacementNS.PlacedFeature.Codec.EncodeStart(ops, inline)
            : PlacedFeatureRefCodec.Instance.EncodeStart(ops, value);
}

//PlacedFeatureSetCodec 已放置特征集合编解码 元素可以是注册名 标签引用 或内联定义
//对应原版 RegistryCodecs.homogeneousList 的 allowInline 形态
//组合类特征的 features 字段里内联对象很常见 只吃字符串会把整条配置挡掉
internal sealed class PlacedFeatureSetCodec : ScalarCodec<HolderSet<RegistryPlacedFeature>>
{
    public static readonly PlacedFeatureSetCodec Instance = new();

    public override DataResult<HolderSet<RegistryPlacedFeature>> Parse<U>(DynamicOps<U> ops, U input)
    {
        //单个字符串交给注册表集合 codec 它自己会分辨 #标签 与普通注册名
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent) return HolderSetCodecs.PlacedFeatureSet.Parse(ops, input);

        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent)
            return DataResult<HolderSet<RegistryPlacedFeature>>.Error(() => "已放置特征集合必须是字符串或数组");
        var holders = new List<Holder<RegistryPlacedFeature>>();
        foreach (var element in stream.GetOrThrow())
        {
            var parsed = PlacedFeatureInlineRefCodec.Instance.Parse(ops, element);
            if (!parsed.Result().IsPresent)
                return DataResult<HolderSet<RegistryPlacedFeature>>.Error(
                    () => "已放置特征集合里有一个元素解析失败: "
                        + parsed.MapOrElse(_ => string.Empty, error => error));
            holders.Add(parsed.GetOrThrow());
        }
        return DataResult<HolderSet<RegistryPlacedFeature>>.Success(
            new DirectHolderSet<RegistryPlacedFeature>(holders));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, HolderSet<RegistryPlacedFeature> value)
        => HolderSetCodecs.PlacedFeatureSet.EncodeStart(ops, value);
}

//PlacedFeatureHelpers 组合类特征共用的已放置特征放置与子特征展开
internal static class PlacedFeatureHelpers
{
    //Place 放置一个已放置特征引用 引用未绑定或不是本作实现时按未放置处理
    public static bool Place(Holder<RegistryPlacedFeature> holder, WorldGenRegion level,
        ChunkGenerator generator, RandomSource random, BlockPos origin)
        => holder.IsBound()
            && holder.Value is PlacementNS.PlacedFeature placed
            && placed.Place(level, generator, random, origin);

    //SubFeatures 展开一个已放置特征引用的全部配置化特征 含配置内嵌的子特征
    public static IEnumerable<Holder<RegistryConfiguredFeature>> SubFeatures(Holder<RegistryPlacedFeature> holder)
        => holder.IsBound() && holder.Value is PlacementNS.PlacedFeature placed
            ? placed.GetFeatures()
            : Enumerable.Empty<Holder<RegistryConfiguredFeature>>();
}

//PlacedFeatureRefCodec 已放置特征引用编解码 对应原版 PlacedFeature.CODEC 的字符串形态
//元素必须已在 PLACED_FEATURE 注册表里 未注册直接报错交给加载器重试
internal sealed class PlacedFeatureRefCodec : ScalarCodec<Holder<RegistryPlacedFeature>>
{
    public static readonly PlacedFeatureRefCodec Instance = new();

    public override DataResult<Holder<RegistryPlacedFeature>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<Holder<RegistryPlacedFeature>>.Error(() => "已放置特征引用必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null)
            return DataResult<Holder<RegistryPlacedFeature>>.Error(() => $"非法的标识符: {text.GetOrThrow()}");
        var holder = BuiltInRegistries.PLACED_FEATURE.Get(id.Value);
        return holder is null
            ? DataResult<Holder<RegistryPlacedFeature>>.Error(() => $"PLACED_FEATURE 里还没有 {id}")
            : DataResult<Holder<RegistryPlacedFeature>>.Success(holder);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryPlacedFeature> value)
    {
        var key = value.UnwrapKey();
        return key is null
            ? DataResult<U>.Error(() => "直接持有者没有注册名 无法编码")
            : DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));
    }
}

//DirectionCodec 六向方向编解码 对应原版 Direction.CODEC
//JSON 形态是 down/up/north/south/west/east 小写名
internal sealed class DirectionCodec : ScalarCodec<Direction>
{
    public static readonly DirectionCodec Instance = new();

    public override DataResult<Direction> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<Direction>.Error(() => "方向必须是字符串");
        return text.GetOrThrow() switch
        {
            "down" => DataResult<Direction>.Success(Direction.Down),
            "up" => DataResult<Direction>.Success(Direction.Up),
            "north" => DataResult<Direction>.Success(Direction.North),
            "south" => DataResult<Direction>.Success(Direction.South),
            "west" => DataResult<Direction>.Success(Direction.West),
            "east" => DataResult<Direction>.Success(Direction.East),
            var other => DataResult<Direction>.Error(() => $"未知的方向: {other}")
        };
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Direction value)
        => DataResult<U>.Success(ops.CreateString(value.Id3D switch
        {
            Direction.DownId => "down",
            Direction.UpId => "up",
            Direction.NorthId => "north",
            Direction.SouthId => "south",
            Direction.WestId => "west",
            _ => "east",
        }));
}

//RequiredFeatures 首批必备特征注册入口
//触碰各静态 Instance 与提供者类型使静态注册生效
public static class RequiredFeatures
{
    public static void RegisterAll()
    {
        _ = BlockStateProviderTypes.Simple;
        _ = BlockStateProviderTypes.Weighted;
        _ = BlockStateProviderTypes.Rotated;
        _ = BlockStateProviderTypes.RandomizedInt;
        _ = BlockStateProviderTypes.RuleBased;

        _ = OreFeature.Instance;
        _ = ScatteredOreFeature.Instance;
        _ = FillLayerFeature.Instance;
        _ = DiskFeature.Instance;
        _ = BlockBlobFeature.Instance;
        _ = BlockPileFeature.Instance;
        _ = BlockColumnFeature.Instance;
        _ = ReplaceSingleBlockFeature.Instance;
        _ = SimpleBlockFeature.Instance;
        _ = SequenceFeature.Instance;
        _ = WeightedRandomSelectorFeature.Instance;
        _ = SimpleRandomSelectorFeature.Instance;
        _ = RandomBooleanSelectorFeature.Instance;
        _ = RandomSelectorFeature.Instance;
    }
}

//WorldGenRegionHeights 关卡高度区间便捷访问
//LevelHeightAccessor 的默认接口成员不能经具体类型取用 这里按区段换算补一层
internal static class WorldGenRegionHeights
{
    //MinBuildHeight 最低可放置 Y 对应原版 getMinBuildHeight
    public static int MinBuildHeight(this WorldGenRegion level) => level.MinSectionY * 16;

    //MaxBuildHeight 最高可放置 Y 的开区间上界 对应原版 getMaxBuildHeight
    public static int MaxBuildHeight(this WorldGenRegion level) => (level.MaxSectionY + 1) * 16;
}
