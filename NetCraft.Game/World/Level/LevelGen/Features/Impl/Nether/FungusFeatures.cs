using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//HugeFungusConfiguration huge fungus configuration, maps to vanilla HugeFungusConfiguration
public sealed class HugeFungusConfiguration : FeatureConfiguration
{
    public static readonly Codec<HugeFungusConfiguration> Codec =
        RecordCodecBuilder.Of6<HugeFungusConfiguration, BlockState, BlockState, BlockState, BlockState,
            BlockPredicate, bool>(
            BlockStateCodec.Instance.FieldOf("valid_base_block")
                .ForGetter<HugeFungusConfiguration, BlockState>(c => c.ValidBaseState),
            BlockStateCodec.Instance.FieldOf("stem_state")
                .ForGetter<HugeFungusConfiguration, BlockState>(c => c.StemState),
            BlockStateCodec.Instance.FieldOf("hat_state")
                .ForGetter<HugeFungusConfiguration, BlockState>(c => c.HatState),
            BlockStateCodec.Instance.FieldOf("decor_state")
                .ForGetter<HugeFungusConfiguration, BlockState>(c => c.DecorState),
            BlockPredicate.Codec.FieldOf("replaceable_blocks")
                .ForGetter<HugeFungusConfiguration, BlockPredicate>(c => c.ReplaceableBlocks),
            Codecs.Bool.OptionalFieldOf("planted", false)
                .ForGetter<HugeFungusConfiguration, bool>(c => c.Planted),
            (validBaseState, stemState, hatState, decorState, replaceableBlocks, planted) =>
                new HugeFungusConfiguration(validBaseState, stemState, hatState, decorState, replaceableBlocks,
                    planted));

    public BlockState ValidBaseState { get; }
    public BlockState StemState { get; }
    public BlockState HatState { get; }
    public BlockState DecorState { get; }
    public BlockPredicate ReplaceableBlocks { get; }
    public bool Planted { get; }

    public HugeFungusConfiguration(BlockState validBaseState, BlockState stemState, BlockState hatState,
        BlockState decorState, BlockPredicate replaceableBlocks, bool planted)
    {
        ValidBaseState = validBaseState;
        StemState = stemState;
        HatState = hatState;
        DecorState = decorState;
        ReplaceableBlocks = replaceableBlocks;
        Planted = planted;
    }
}

//HugeFungusFeature huge fungus feature, maps to vanilla HugeFungusFeature
public sealed class HugeFungusFeature : Feature<HugeFungusConfiguration>
{
    private const string FeatureId = "huge_fungus";

    //HugeProbability chance of growing into a thick huge fungus during natural generation
    private const float HugeProbability = 0.06f;

    public static readonly HugeFungusFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new HugeFungusFeature());

    private HugeFungusFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), HugeFungusConfiguration.Codec) { }

    protected override bool Place(HugeFungusConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        if (!NetherSupport.IsBlock(level, origin.Offset(0, -1, 0), config.ValidBaseState.Owner)) return false;
        var totalHeight = Mth.NextInt(random, 4, 13);
        if (random.NextInt(12) == 0) totalHeight *= 2;
        if (!config.Planted)
        {
            var maxHeight = context.ChunkGenerator.GetGenDepth();
            if (origin.Y + totalHeight + 1 >= maxHeight) return false;
        }
        var isHuge = !config.Planted && random.NextFloat() < HugeProbability;
        NetherSupport.SetBlock(level, origin, Blocks.AIR.DefaultBlockState);
        PlaceStem(level, random, config, origin, totalHeight, isHuge);
        PlaceHat(level, random, config, origin, totalHeight, isHuge);
        return true;
    }

    //IsReplaceable whether the position can be replaced by the fungus, maps to vanilla isReplaceable
    private static bool IsReplaceable(WorldGenRegion level, BlockPos pos, HugeFungusConfiguration config,
        bool checkNonReplaceablePlants)
    {
        if (NetherSupport.CanBeReplaced(NetherSupport.GetBlockState(level, pos))) return true;
        return checkNonReplaceablePlants && config.ReplaceableBlocks.Test(level, pos);
    }

    //PlaceStem raise the fungus stem, maps to vanilla placeStem
    private static void PlaceStem(WorldGenRegion level, RandomSource random, HugeFungusConfiguration config,
        BlockPos surfaceOrigin, int totalHeight, bool isHuge)
    {
        var stem = config.StemState;
        var stemRadius = isHuge ? 1 : 0;
        for (var dx = -stemRadius; dx <= stemRadius; dx++)
        {
            for (var dz = -stemRadius; dz <= stemRadius; dz++)
            {
                var cornerOfHugeStem = isHuge && Mth.Abs(dx) == stemRadius && Mth.Abs(dz) == stemRadius;
                for (var dy = 0; dy < totalHeight; dy++)
                {
                    var pos = surfaceOrigin.Offset(dx, dy, dz);
                    if (!IsReplaceable(level, pos, config, true)) continue;
                    if (config.Planted)
                    {
                        //Vanilla calls destroyBlock on the existing plant first; there is no drop system here, so it is overwritten directly
                        NetherSupport.SetBlock(level, pos, stem);
                    }
                    else if (!cornerOfHugeStem)
                    {
                        NetherSupport.SetBlock(level, pos, stem);
                    }
                    else if (random.NextFloat() < 0.1f)
                    {
                        NetherSupport.SetBlock(level, pos, stem);
                    }
                }
            }
        }
    }

    //PlaceHat grow the fungus cap, maps to vanilla placeHat
    private static void PlaceHat(WorldGenRegion level, RandomSource random, HugeFungusConfiguration config,
        BlockPos surfaceOrigin, int totalHeight, bool isHuge)
    {
        var placeVines = config.HatState.Owner == NetherSupport.Block("nether_wart_block");
        var hatHeight = Math.Min(random.NextInt(1 + (totalHeight / 3)) + 5, totalHeight);
        var hatStartY = totalHeight - hatHeight;
        for (var dy = hatStartY; dy <= totalHeight; dy++)
        {
            var radius = dy < totalHeight - random.NextInt(3) ? 2 : 1;
            if (hatHeight > 8 && dy < hatStartY + 4) radius = 3;
            if (isHuge) radius++;
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    var isEdgeX = dx == -radius || dx == radius;
                    var isEdgeZ = dz == -radius || dz == radius;
                    var inside = !isEdgeX && !isEdgeZ && dy != totalHeight;
                    var corner = isEdgeX && isEdgeZ;
                    var isHatBottom = dy < hatStartY + 3;
                    var pos = surfaceOrigin.Offset(dx, dy, dz);
                    if (!IsReplaceable(level, pos, config, false)) continue;
                    if (isHatBottom)
                    {
                        if (!inside) PlaceHatDropBlock(level, random, pos, config.HatState, placeVines);
                    }
                    else if (inside)
                    {
                        PlaceHatBlock(level, random, config, pos, 0.1f, 0.2f, placeVines ? 0.1f : 0f);
                    }
                    else if (corner)
                    {
                        PlaceHatBlock(level, random, config, pos, 0.01f, 0.7f, placeVines ? 0.083f : 0f);
                    }
                    else
                    {
                        PlaceHatBlock(level, random, config, pos, 5.0E-4f, 0.98f, placeVines ? 0.07f : 0f);
                    }
                }
            }
        }
    }

    //PlaceHatBlock three-stage probability decides whether the cap cell holds decoration or cap, maps to vanilla placeHatBlock
    private static void PlaceHatBlock(WorldGenRegion level, RandomSource random, HugeFungusConfiguration config,
        BlockPos pos, float decorBlockProbability, float hatBlockProbability, float vinesProbability)
    {
        if (random.NextFloat() < decorBlockProbability)
        {
            NetherSupport.SetBlock(level, pos, config.DecorState);
        }
        else if (random.NextFloat() < hatBlockProbability)
        {
            NetherSupport.SetBlock(level, pos, config.HatState);
            if (random.NextFloat() < vinesProbability) TryPlaceWeepingVines(pos, level, random);
        }
    }

    //PlaceHatDropBlock add one block to the cap fringe, maps to vanilla placeHatDropBlock
    private static void PlaceHatDropBlock(WorldGenRegion level, RandomSource random, BlockPos pos,
        BlockState hatState, bool placeVines)
    {
        if (NetherSupport.IsBlock(level, pos.Offset(0, -1, 0), hatState.Owner))
        {
            NetherSupport.SetBlock(level, pos, hatState);
            return;
        }
        if (random.NextFloat() < 0.15d)
        {
            NetherSupport.SetBlock(level, pos, hatState);
            if (placeVines && random.NextInt(11) == 0) TryPlaceWeepingVines(pos, level, random);
        }
    }

    //TryPlaceWeepingVines hang one weeping vine below the cap, maps to vanilla tryPlaceWeepingVines
    private static void TryPlaceWeepingVines(BlockPos hatBlockPos, WorldGenRegion level, RandomSource random)
    {
        var placePos = hatBlockPos.Offset(0, -1, 0);
        if (!NetherSupport.IsAir(level, placePos)) return;
        var goalVineHeight = Mth.NextInt(random, 1, 5);
        if (random.NextInt(7) == 0) goalVineHeight *= 2;
        WeepingVinesFeature.PlaceWeepingVinesColumn(level, random, placePos, goalVineHeight, 23, 25);
    }
}

//NetherForestVegetationConfig nether forest vegetation configuration, maps to vanilla NetherForestVegetationConfig
public sealed class NetherForestVegetationConfig : FeatureConfiguration
{
    public static readonly Codec<NetherForestVegetationConfig> Codec =
        RecordCodecBuilder.Of3<NetherForestVegetationConfig, BlockStateProvider, int, int>(
            BlockStateProvider.Codec.FieldOf("state_provider")
                .ForGetter<NetherForestVegetationConfig, BlockStateProvider>(c => c.StateProvider),
            Codecs.Int.FieldOf("spread_width")
                .ForGetter<NetherForestVegetationConfig, int>(c => c.SpreadWidth),
            Codecs.Int.FieldOf("spread_height")
                .ForGetter<NetherForestVegetationConfig, int>(c => c.SpreadHeight),
            (stateProvider, spreadWidth, spreadHeight) =>
                new NetherForestVegetationConfig(stateProvider, spreadWidth, spreadHeight));

    public BlockStateProvider StateProvider { get; }
    public int SpreadWidth { get; }
    public int SpreadHeight { get; }

    public NetherForestVegetationConfig(BlockStateProvider stateProvider, int spreadWidth, int spreadHeight)
    {
        StateProvider = stateProvider;
        SpreadWidth = spreadWidth;
        SpreadHeight = spreadHeight;
    }
}

//NetherForestVegetationFeature nether forest vegetation feature, maps to vanilla NetherForestVegetationFeature
public sealed class NetherForestVegetationFeature : Feature<NetherForestVegetationConfig>
{
    private const string FeatureId = "nether_forest_vegetation";

    public static readonly NetherForestVegetationFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new NetherForestVegetationFeature());

    private NetherForestVegetationFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NetherForestVegetationConfig.Codec) { }

    protected override bool Place(NetherForestVegetationConfig config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        if (!NetherSupport.MatchesTag(level, origin.Offset(0, -1, 0), NetherSupport.NyliumTag)) return false;
        var y = origin.Y;
        if (y < NetherSupport.MinY(level) + 1 || y + 1 > NetherSupport.MaxY(level)) return false;
        var placed = 0;
        for (var i = 0; i < config.SpreadWidth * config.SpreadWidth; i++)
        {
            var pos = origin.Offset(
                random.NextInt(config.SpreadWidth) - random.NextInt(config.SpreadWidth),
                random.NextInt(config.SpreadHeight) - random.NextInt(config.SpreadHeight),
                random.NextInt(config.SpreadWidth) - random.NextInt(config.SpreadWidth));
            var state = config.StateProvider.GetState(level, random, pos);
            if (!NetherSupport.IsAir(level, pos)) continue;
            if (pos.Y <= NetherSupport.MinY(level)) continue;
            if (!NetherSupport.CanSurvive(level, pos, state)) continue;
            NetherSupport.SetBlock(level, pos, state);
            placed++;
        }
        return placed > 0;
    }
}
