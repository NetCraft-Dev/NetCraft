using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//GeodeBlockSettings 紫水晶洞各层用的方块提供者 对应原版 GeodeBlockSettings
public sealed class GeodeBlockSettings
{
    public static readonly Codec<GeodeBlockSettings> Codec =
        RecordCodecBuilder.Of8<GeodeBlockSettings, BlockStateProvider, BlockStateProvider, BlockStateProvider,
            BlockStateProvider, BlockStateProvider, IReadOnlyList<BlockState>, HolderSet<RegBlock>, HolderSet<RegBlock>>(
            BlockStateProvider.Codec.FieldOf("filling_provider")
                .ForGetter<GeodeBlockSettings, BlockStateProvider>(s => s.FillingProvider),
            BlockStateProvider.Codec.FieldOf("inner_layer_provider")
                .ForGetter<GeodeBlockSettings, BlockStateProvider>(s => s.InnerLayerProvider),
            BlockStateProvider.Codec.FieldOf("alternate_inner_layer_provider")
                .ForGetter<GeodeBlockSettings, BlockStateProvider>(s => s.AlternateInnerLayerProvider),
            BlockStateProvider.Codec.FieldOf("middle_layer_provider")
                .ForGetter<GeodeBlockSettings, BlockStateProvider>(s => s.MiddleLayerProvider),
            BlockStateProvider.Codec.FieldOf("outer_layer_provider")
                .ForGetter<GeodeBlockSettings, BlockStateProvider>(s => s.OuterLayerProvider),
            BlockStateCodec.Instance.ListOf().FieldOf("inner_placements")
                .ForGetter<GeodeBlockSettings, IReadOnlyList<BlockState>>(s => s.InnerPlacements),
            HolderSetCodecs.BlockSet.FieldOf("cannot_replace")
                .ForGetter<GeodeBlockSettings, HolderSet<RegBlock>>(s => s.CannotReplace),
            HolderSetCodecs.BlockSet.FieldOf("invalid_blocks")
                .ForGetter<GeodeBlockSettings, HolderSet<RegBlock>>(s => s.InvalidBlocks),
            (filling, inner, alternateInner, middle, outer, innerPlacements, cannotReplace, invalidBlocks) =>
                new GeodeBlockSettings(filling, inner, alternateInner, middle, outer, innerPlacements, cannotReplace,
                    invalidBlocks));

    public BlockStateProvider FillingProvider { get; }
    public BlockStateProvider InnerLayerProvider { get; }
    public BlockStateProvider AlternateInnerLayerProvider { get; }
    public BlockStateProvider MiddleLayerProvider { get; }
    public BlockStateProvider OuterLayerProvider { get; }
    public IReadOnlyList<BlockState> InnerPlacements { get; }
    public HolderSet<RegBlock> CannotReplace { get; }
    public HolderSet<RegBlock> InvalidBlocks { get; }

    public GeodeBlockSettings(BlockStateProvider fillingProvider, BlockStateProvider innerLayerProvider,
        BlockStateProvider alternateInnerLayerProvider, BlockStateProvider middleLayerProvider,
        BlockStateProvider outerLayerProvider, IReadOnlyList<BlockState> innerPlacements,
        HolderSet<RegBlock> cannotReplace, HolderSet<RegBlock> invalidBlocks)
    {
        FillingProvider = fillingProvider;
        InnerLayerProvider = innerLayerProvider;
        AlternateInnerLayerProvider = alternateInnerLayerProvider;
        MiddleLayerProvider = middleLayerProvider;
        OuterLayerProvider = outerLayerProvider;
        InnerPlacements = innerPlacements;
        CannotReplace = cannotReplace;
        InvalidBlocks = invalidBlocks;
    }
}

//GeodeLayerSettings 紫水晶洞各层半径参数 对应原版 GeodeLayerSettings
//JSON 里 layers 常写成空对象 四个值全走默认
public sealed class GeodeLayerSettings
{
    public static readonly Codec<GeodeLayerSettings> Codec =
        RecordCodecBuilder.Of4<GeodeLayerSettings, double, double, double, double>(
            Codecs.Double.OptionalFieldOf("filling", 1.7d)
                .ForGetter<GeodeLayerSettings, double>(s => s.Filling),
            Codecs.Double.OptionalFieldOf("inner_layer", 2.2d)
                .ForGetter<GeodeLayerSettings, double>(s => s.InnerLayer),
            Codecs.Double.OptionalFieldOf("middle_layer", 3.2d)
                .ForGetter<GeodeLayerSettings, double>(s => s.MiddleLayer),
            Codecs.Double.OptionalFieldOf("outer_layer", 4.2d)
                .ForGetter<GeodeLayerSettings, double>(s => s.OuterLayer),
            (filling, innerLayer, middleLayer, outerLayer) =>
                new GeodeLayerSettings(filling, innerLayer, middleLayer, outerLayer));

    public double Filling { get; }
    public double InnerLayer { get; }
    public double MiddleLayer { get; }
    public double OuterLayer { get; }

    public GeodeLayerSettings(double filling, double innerLayer, double middleLayer, double outerLayer)
    {
        Filling = filling;
        InnerLayer = innerLayer;
        MiddleLayer = middleLayer;
        OuterLayer = outerLayer;
    }
}

//GeodeCrackSettings 紫水晶洞裂缝参数 对应原版 GeodeCrackSettings
public sealed class GeodeCrackSettings
{
    public static readonly Codec<GeodeCrackSettings> Codec =
        RecordCodecBuilder.Of3<GeodeCrackSettings, double, double, int>(
            Codecs.Double.OptionalFieldOf("generate_crack_chance", 1.0d)
                .ForGetter<GeodeCrackSettings, double>(s => s.GenerateCrackChance),
            Codecs.Double.OptionalFieldOf("base_crack_size", 2.0d)
                .ForGetter<GeodeCrackSettings, double>(s => s.BaseCrackSize),
            Codecs.Int.OptionalFieldOf("crack_point_offset", 2)
                .ForGetter<GeodeCrackSettings, int>(s => s.CrackPointOffset),
            (generateCrackChance, baseCrackSize, crackPointOffset) =>
                new GeodeCrackSettings(generateCrackChance, baseCrackSize, crackPointOffset));

    public double GenerateCrackChance { get; }
    public double BaseCrackSize { get; }
    public int CrackPointOffset { get; }

    public GeodeCrackSettings(double generateCrackChance, double baseCrackSize, int crackPointOffset)
    {
        GenerateCrackChance = generateCrackChance;
        BaseCrackSize = baseCrackSize;
        CrackPointOffset = crackPointOffset;
    }
}

//GeodeConfiguration 紫水晶洞配置 对应原版 GeodeConfiguration
public sealed class GeodeConfiguration : FeatureConfiguration
{
    public static readonly Codec<GeodeConfiguration> Codec =
        RecordCodecBuilder.Of13<GeodeConfiguration, GeodeBlockSettings, GeodeLayerSettings, GeodeCrackSettings, double,
            double, bool, IntProvider, IntProvider, IntProvider, int, int, double, int>(
            GeodeBlockSettings.Codec.FieldOf("blocks")
                .ForGetter<GeodeConfiguration, GeodeBlockSettings>(c => c.GeodeBlockSettings),
            GeodeLayerSettings.Codec.FieldOf("layers")
                .ForGetter<GeodeConfiguration, GeodeLayerSettings>(c => c.GeodeLayerSettings),
            GeodeCrackSettings.Codec.FieldOf("crack")
                .ForGetter<GeodeConfiguration, GeodeCrackSettings>(c => c.GeodeCrackSettings),
            Codecs.Double.OptionalFieldOf("use_potential_placements_chance", 0.35d)
                .ForGetter<GeodeConfiguration, double>(c => c.UsePotentialPlacementsChance),
            Codecs.Double.OptionalFieldOf("use_alternate_layer0_chance", 0.0d)
                .ForGetter<GeodeConfiguration, double>(c => c.UseAlternateLayer0Chance),
            Codecs.Bool.OptionalFieldOf("placements_require_layer0_alternate", true)
                .ForGetter<GeodeConfiguration, bool>(c => c.PlacementsRequireLayer0Alternate),
            IntProviders.Codec.OptionalFieldOf("outer_wall_distance", new UniformInt(4, 5))
                .ForGetter<GeodeConfiguration, IntProvider>(c => c.OuterWallDistance),
            IntProviders.Codec.OptionalFieldOf("distribution_points", new UniformInt(3, 4))
                .ForGetter<GeodeConfiguration, IntProvider>(c => c.DistributionPoints),
            IntProviders.Codec.OptionalFieldOf("point_offset", new UniformInt(1, 2))
                .ForGetter<GeodeConfiguration, IntProvider>(c => c.PointOffset),
            Codecs.Int.OptionalFieldOf("min_gen_offset", -16)
                .ForGetter<GeodeConfiguration, int>(c => c.MinGenOffset),
            Codecs.Int.OptionalFieldOf("max_gen_offset", 16)
                .ForGetter<GeodeConfiguration, int>(c => c.MaxGenOffset),
            Codecs.Double.OptionalFieldOf("noise_multiplier", 0.05d)
                .ForGetter<GeodeConfiguration, double>(c => c.NoiseMultiplier),
            Codecs.Int.FieldOf("invalid_blocks_threshold")
                .ForGetter<GeodeConfiguration, int>(c => c.InvalidBlocksThreshold),
            (blockSettings, layerSettings, crackSettings, usePotentialPlacementsChance, useAlternateLayer0Chance,
                placementsRequireLayer0Alternate, outerWallDistance, distributionPoints, pointOffset, minGenOffset,
                maxGenOffset, noiseMultiplier, invalidBlocksThreshold) => new GeodeConfiguration(blockSettings,
                layerSettings, crackSettings, usePotentialPlacementsChance, useAlternateLayer0Chance,
                placementsRequireLayer0Alternate, outerWallDistance, distributionPoints, pointOffset, minGenOffset,
                maxGenOffset, noiseMultiplier, invalidBlocksThreshold));

    public GeodeBlockSettings GeodeBlockSettings { get; }
    public GeodeLayerSettings GeodeLayerSettings { get; }
    public GeodeCrackSettings GeodeCrackSettings { get; }
    public double UsePotentialPlacementsChance { get; }
    public double UseAlternateLayer0Chance { get; }
    public bool PlacementsRequireLayer0Alternate { get; }
    public IntProvider OuterWallDistance { get; }
    public IntProvider DistributionPoints { get; }
    public IntProvider PointOffset { get; }
    public int MinGenOffset { get; }
    public int MaxGenOffset { get; }
    public double NoiseMultiplier { get; }
    public int InvalidBlocksThreshold { get; }

    public GeodeConfiguration(GeodeBlockSettings geodeBlockSettings, GeodeLayerSettings geodeLayerSettings,
        GeodeCrackSettings geodeCrackSettings, double usePotentialPlacementsChance, double useAlternateLayer0Chance,
        bool placementsRequireLayer0Alternate, IntProvider outerWallDistance, IntProvider distributionPoints,
        IntProvider pointOffset, int minGenOffset, int maxGenOffset, double noiseMultiplier, int invalidBlocksThreshold)
    {
        GeodeBlockSettings = geodeBlockSettings;
        GeodeLayerSettings = geodeLayerSettings;
        GeodeCrackSettings = geodeCrackSettings;
        UsePotentialPlacementsChance = usePotentialPlacementsChance;
        UseAlternateLayer0Chance = useAlternateLayer0Chance;
        PlacementsRequireLayer0Alternate = placementsRequireLayer0Alternate;
        OuterWallDistance = outerWallDistance;
        DistributionPoints = distributionPoints;
        PointOffset = pointOffset;
        MinGenOffset = minGenOffset;
        MaxGenOffset = maxGenOffset;
        NoiseMultiplier = noiseMultiplier;
        InvalidBlocksThreshold = invalidBlocksThreshold;
    }
}

//GeodeFeature 紫水晶洞特征 对应原版 GeodeFeature
//先按外壁距离撒分布点 再在包围盒内逐格算点集的反距离平方和决定填充层
//噪声偏移与随机消耗顺序全部照原版 顺序错了同种子长出的洞就不同
public sealed class GeodeFeature : Feature<GeodeConfiguration>
{
    private const string FeatureId = "geode";

    public static readonly GeodeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new GeodeFeature());

    private GeodeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), GeodeConfiguration.Codec) { }

    protected override bool Place(GeodeConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var origin = context.Origin;
        var level = context.Level;
        var minGenOffset = config.MinGenOffset;
        var maxGenOffset = config.MaxGenOffset;
        var points = new List<(BlockPos Pos, int Offset)>();
        var numPoints = config.DistributionPoints.Sample(random);
        var noise = NormalNoise.Create(new LegacyRandomSource(level.Seed), -4, 1.0d);
        var crackPoints = new List<BlockPos>();
        var crackSizeAdjustment = (double)numPoints / config.OuterWallDistance.MaxInclusive;
        var layerSettings = config.GeodeLayerSettings;
        var blockSettings = config.GeodeBlockSettings;
        var crackSettings = config.GeodeCrackSettings;
        var innerAir = 1.0d / Math.Sqrt(layerSettings.Filling);
        var innermostBlockLayer = 1.0d / Math.Sqrt(layerSettings.InnerLayer + crackSizeAdjustment);
        var innerCrust = 1.0d / Math.Sqrt(layerSettings.MiddleLayer + crackSizeAdjustment);
        var outerCrust = 1.0d / Math.Sqrt(layerSettings.OuterLayer + crackSizeAdjustment);
        var crackSize = 1.0d / Math.Sqrt(crackSettings.BaseCrackSize + (random.NextDouble() / 2.0d)
            + (numPoints > 3 ? crackSizeAdjustment : 0.0d));
        var shouldGenerateCrack = (double)random.NextFloat() < crackSettings.GenerateCrackChance;
        var numInvalidPoints = 0;
        for (var i = 0; i < numPoints; i++)
        {
            var x = config.OuterWallDistance.Sample(random);
            var y = config.OuterWallDistance.Sample(random);
            var z = config.OuterWallDistance.Sample(random);
            var pos = origin.Offset(x, y, z);
            var state = level.GetBlockState(pos.X, pos.Y, pos.Z);
            if (state.Owner.IsAir || VegetationSupport.IsInSet(state, blockSettings.InvalidBlocks))
            {
                numInvalidPoints++;
                if (numInvalidPoints > config.InvalidBlocksThreshold) return false;
            }
            points.Add((pos, config.PointOffset.Sample(random)));
        }
        if (shouldGenerateCrack)
        {
            var offsetIndex = random.NextInt(4);
            var crackOffset = numPoints * 2 + 1;
            if (offsetIndex == 0)
            {
                crackPoints.Add(origin.Offset(crackOffset, 7, 0));
                crackPoints.Add(origin.Offset(crackOffset, 5, 0));
                crackPoints.Add(origin.Offset(crackOffset, 1, 0));
            }
            else if (offsetIndex == 1)
            {
                crackPoints.Add(origin.Offset(0, 7, crackOffset));
                crackPoints.Add(origin.Offset(0, 5, crackOffset));
                crackPoints.Add(origin.Offset(0, 1, crackOffset));
            }
            else if (offsetIndex == 2)
            {
                crackPoints.Add(origin.Offset(crackOffset, 7, crackOffset));
                crackPoints.Add(origin.Offset(crackOffset, 5, crackOffset));
                crackPoints.Add(origin.Offset(crackOffset, 1, crackOffset));
            }
            else
            {
                crackPoints.Add(origin.Offset(0, 7, 0));
                crackPoints.Add(origin.Offset(0, 5, 0));
                crackPoints.Add(origin.Offset(0, 1, 0));
            }
        }
        var potentialCrystalPlacements = new List<BlockPos>();
        var cannotReplace = blockSettings.CannotReplace;
        foreach (var pointInside in BetweenClosed(
            origin.X + minGenOffset, origin.Y + minGenOffset, origin.Z + minGenOffset,
            origin.X + maxGenOffset, origin.Y + maxGenOffset, origin.Z + maxGenOffset))
        {
            var noiseOffset = noise.GetValue(pointInside.X, pointInside.Y, pointInside.Z) * config.NoiseMultiplier;
            var distSumShell = 0.0d;
            foreach (var point in points)
            {
                distSumShell += Mth.InvSqrt(pointInside.AsVec3i().DistSqr(point.Pos.AsVec3i()) + point.Offset)
                    + noiseOffset;
            }
            var distSumCrack = 0.0d;
            foreach (var crackPoint in crackPoints)
            {
                distSumCrack += Mth.InvSqrt(pointInside.AsVec3i().DistSqr(crackPoint.AsVec3i())
                    + crackSettings.CrackPointOffset) + noiseOffset;
            }
            if (distSumShell < outerCrust) continue;
            if (shouldGenerateCrack && distSumCrack >= crackSize && distSumShell < innerAir)
            {
                //裂缝里掏空气 原版此处对相邻流体安排一次刻 本作世界生成阶段没有刻队列 略过
                SafeSetBlock(level, pointInside, VegetationSupport.StateOf("air"), cannotReplace);
            }
            else if (distSumShell >= innerAir)
            {
                SafeSetBlock(level, pointInside,
                    blockSettings.FillingProvider.GetState(level, random, pointInside), cannotReplace);
            }
            else if (distSumShell >= innermostBlockLayer)
            {
                var useAlternateLayer = (double)random.NextFloat() < config.UseAlternateLayer0Chance;
                if (useAlternateLayer)
                {
                    SafeSetBlock(level, pointInside,
                        blockSettings.AlternateInnerLayerProvider.GetState(level, random, pointInside), cannotReplace);
                }
                else
                {
                    SafeSetBlock(level, pointInside,
                        blockSettings.InnerLayerProvider.GetState(level, random, pointInside), cannotReplace);
                }
                if (!config.PlacementsRequireLayer0Alternate || useAlternateLayer)
                {
                    if (random.NextFloat() < config.UsePotentialPlacementsChance)
                        potentialCrystalPlacements.Add(pointInside);
                }
            }
            else if (distSumShell >= innerCrust)
            {
                SafeSetBlock(level, pointInside,
                    blockSettings.MiddleLayerProvider.GetState(level, random, pointInside), cannotReplace);
            }
            else if (distSumShell >= outerCrust)
            {
                SafeSetBlock(level, pointInside,
                    blockSettings.OuterLayerProvider.GetState(level, random, pointInside), cannotReplace);
            }
        }
        var innerPlacements = blockSettings.InnerPlacements;
        if (innerPlacements.Count == 0) return true;
        foreach (var crystalPos in potentialCrystalPlacements)
        {
            var blockState = innerPlacements[random.NextInt(innerPlacements.Count)];
            foreach (var direction in Direction.Values)
            {
                if (VegetationSupport.HasProperty(blockState, "facing"))
                    blockState = VegetationSupport.WithProperty(blockState, "facing",
                        VegetationSupport.FaceName(direction));
                var placePos = crystalPos.Offset(direction);
                var placeState = level.GetBlockState(placePos.X, placePos.Y, placePos.Z);
                if (VegetationSupport.HasProperty(blockState, "waterlogged"))
                    blockState = VegetationSupport.WithProperty(blockState, "waterlogged", IsWaterSource(placeState));
                if (!CanClusterGrowAtState(placeState)) continue;
                SafeSetBlock(level, placePos, blockState, cannotReplace);
                break;
            }
        }
        return true;
    }

    //SafeSetBlock 目标格不在禁改集合里才写入 对应原版 safeSetBlock
    private static void SafeSetBlock(WorldGenRegion level, BlockPos pos, BlockState state,
        HolderSet<RegBlock> cannotReplace)
    {
        if (VegetationSupport.IsInSet(level.GetBlockState(pos.X, pos.Y, pos.Z), cannotReplace)) return;
        level.SetBlockState(pos.X, pos.Y, pos.Z, state);
    }

    //CanClusterGrowAtState 该状态能让晶芽长起来 对应原版 BuddingAmethystBlock.canClusterGrowAtState
    private static bool CanClusterGrowAtState(BlockState state)
        => state.Owner.IsAir || state.Owner == VegetationSupport.BlockOf("water");

    //IsWaterSource 该状态是不是水源 本作流体状态只保留有无 水方块在世界生成里恒为源
    private static bool IsWaterSource(BlockState state) => state.Owner == VegetationSupport.BlockOf("water");

    //BetweenClosed 闭区间长方体的遍历顺序 对应原版 BlockPos.betweenClosed
    //z 最外层 y 居中 x 最内 遍历顺序决定随机消耗顺序不能改
    private static IEnumerable<BlockPos> BetweenClosed(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        for (var z = minZ; z <= maxZ; z++)
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
            yield return new BlockPos(x, y, z);
    }
}
