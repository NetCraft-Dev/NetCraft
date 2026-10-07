using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//WeepingVinesFeature weeping vines feature, maps to vanilla WeepingVinesFeature
//Vine clusters hanging below nylium and nether wart blocks
public sealed class WeepingVinesFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "weeping_vines";

    //VineAge vine head age 0-25, maps to vanilla GrowingPlantHeadBlock.AGE
    private static readonly IntegerProperty VineAge = new("age", 0, 25);

    public static readonly WeepingVinesFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new WeepingVinesFeature());

    private WeepingVinesFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        if (!NetherSupport.IsAir(level, origin)) return false;
        var netherrack = NetherSupport.Block("netherrack");
        var netherWartBlock = NetherSupport.Block("nether_wart_block");
        var above = NetherSupport.GetBlockState(level, origin.Offset(0, 1, 0)).Owner;
        if (above != netherrack && above != netherWartBlock) return false;
        PlaceRoofNetherWart(level, random, origin, netherrack, netherWartBlock);
        PlaceRoofWeepingVines(level, random, origin, netherrack, netherWartBlock);
        return true;
    }

    //PlaceRoofNetherWart spread nether wart blocks across the ceiling, maps to vanilla placeRoofNetherWart
    private static void PlaceRoofNetherWart(WorldGenRegion level, RandomSource random, BlockPos origin,
        RegBlock netherrack, RegBlock netherWartBlock)
    {
        var wartState = netherWartBlock.DefaultBlockState;
        NetherSupport.SetBlock(level, origin, wartState);
        for (var i = 0; i < 200; i++)
        {
            var pos = origin.Offset(
                random.NextInt(6) - random.NextInt(6),
                random.NextInt(2) - random.NextInt(5),
                random.NextInt(6) - random.NextInt(6));
            if (!NetherSupport.IsAir(level, pos)) continue;
            var neighbours = 0;
            foreach (var direction in Direction.Values)
            {
                var neighbour = NetherSupport.GetBlockState(level, pos.Offset(direction)).Owner;
                if (neighbour == netherrack || neighbour == netherWartBlock) neighbours++;
                if (neighbours > 1) break;
            }
            if (neighbours == 1) NetherSupport.SetBlock(level, pos, wartState);
        }
    }

    //PlaceRoofWeepingVines hang weeping vines below the ceiling, maps to vanilla placeRoofWeepingVines
    private static void PlaceRoofWeepingVines(WorldGenRegion level, RandomSource random, BlockPos origin,
        RegBlock netherrack, RegBlock netherWartBlock)
    {
        for (var i = 0; i < 100; i++)
        {
            var pos = origin.Offset(
                random.NextInt(8) - random.NextInt(8),
                random.NextInt(2) - random.NextInt(7),
                random.NextInt(8) - random.NextInt(8));
            if (!NetherSupport.IsAir(level, pos)) continue;
            var above = NetherSupport.GetBlockState(level, pos.Offset(0, 1, 0)).Owner;
            if (above != netherrack && above != netherWartBlock) continue;
            var vineHeight = Mth.NextInt(random, 1, 8);
            if (random.NextInt(6) == 0) vineHeight *= 2;
            if (random.NextInt(5) == 0) vineHeight = 1;
            PlaceWeepingVinesColumn(level, random, pos, vineHeight, 17, 25);
        }
    }

    //PlaceWeepingVinesColumn lay weeping vines downward from the start, maps to vanilla placeWeepingVinesColumn
    //Shared by cap vines and nether ceiling vines, reused by HugeFungusFeature
    internal static void PlaceWeepingVinesColumn(WorldGenRegion level, RandomSource random, BlockPos start,
        int totalHeight, int minAge, int maxAge)
    {
        var plantState = NetherSupport.Block("weeping_vines_plant").DefaultBlockState;
        var headState = NetherSupport.Block("weeping_vines").DefaultBlockState;
        var cursor = start;
        for (var height = 0; height <= totalHeight; height++)
        {
            if (NetherSupport.IsAir(level, cursor))
            {
                if (height == totalHeight || !NetherSupport.IsAir(level, cursor.Offset(0, -1, 0)))
                {
                    NetherSupport.SetBlock(level, cursor,
                        headState.TrySetValue(VineAge, Mth.NextInt(random, minAge, maxAge)));
                    return;
                }
                NetherSupport.SetBlock(level, cursor, plantState);
            }
            cursor = cursor.Offset(0, -1, 0);
        }
    }
}

//TwistingVinesConfig twisting vines configuration, maps to vanilla TwistingVinesConfig
public sealed class TwistingVinesConfig : FeatureConfiguration
{
    public static readonly Codec<TwistingVinesConfig> Codec =
        RecordCodecBuilder.Of3<TwistingVinesConfig, int, int, int>(
            Codecs.Int.FieldOf("spread_width").ForGetter<TwistingVinesConfig, int>(c => c.SpreadWidth),
            Codecs.Int.FieldOf("spread_height").ForGetter<TwistingVinesConfig, int>(c => c.SpreadHeight),
            Codecs.Int.FieldOf("max_height").ForGetter<TwistingVinesConfig, int>(c => c.MaxHeight),
            (spreadWidth, spreadHeight, maxHeight) =>
                new TwistingVinesConfig(spreadWidth, spreadHeight, maxHeight));

    public int SpreadWidth { get; }
    public int SpreadHeight { get; }
    public int MaxHeight { get; }

    public TwistingVinesConfig(int spreadWidth, int spreadHeight, int maxHeight)
    {
        SpreadWidth = spreadWidth;
        SpreadHeight = spreadHeight;
        MaxHeight = maxHeight;
    }
}

//TwistingVinesFeature twisting vines feature, maps to vanilla TwistingVinesFeature
//Vine clusters growing up from nylium ground
public sealed class TwistingVinesFeature : Feature<TwistingVinesConfig>
{
    private const string FeatureId = "twisting_vines";

    //VineAge vine head age 0-25, maps to vanilla GrowingPlantHeadBlock.AGE
    private static readonly IntegerProperty VineAge = new("age", 0, 25);

    public static readonly TwistingVinesFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new TwistingVinesFeature());

    private TwistingVinesFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), TwistingVinesConfig.Codec) { }

    protected override bool Place(TwistingVinesConfig config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        if (IsInvalidPlacementLocation(level, origin)) return false;
        var random = context.Random;
        var spreadWidth = config.SpreadWidth;
        var spreadHeight = config.SpreadHeight;
        var maxHeight = config.MaxHeight;
        for (var i = 0; i < spreadWidth * spreadWidth; i++)
        {
            var pos = origin.Offset(
                Mth.NextInt(random, -spreadWidth, spreadWidth),
                Mth.NextInt(random, -spreadHeight, spreadHeight),
                Mth.NextInt(random, -spreadWidth, spreadWidth));
            if (!FindFirstAirBlockAboveGround(level, ref pos)) continue;
            if (IsInvalidPlacementLocation(level, pos)) continue;
            var vineHeight = Mth.NextInt(random, 1, maxHeight);
            if (random.NextInt(6) == 0) vineHeight *= 2;
            if (random.NextInt(5) == 0) vineHeight = 1;
            PlaceWeepingVinesColumn(level, random, pos, vineHeight, 17, 25);
        }
        return true;
    }

    //FindFirstAirBlockAboveGround search down to the ground then back up to the first air cell above it, maps to vanilla findFirstAirBlockAboveGround
    private static bool FindFirstAirBlockAboveGround(WorldGenRegion level, ref BlockPos pos)
    {
        while (true)
        {
            pos = pos.Offset(0, -1, 0);
            if (NetherSupport.IsOutsideBuildHeight(level, pos.Y)) return false;
            if (!NetherSupport.IsAir(level, pos)) break;
        }
        pos = pos.Offset(0, 1, 0);
        return true;
    }

    //IsInvalidPlacementLocation the cell below must be nylium or nether wart and the current cell must be air, maps to vanilla isInvalidPlacementLocation
    private static bool IsInvalidPlacementLocation(WorldGenRegion level, BlockPos pos)
    {
        if (!NetherSupport.IsAir(level, pos)) return true;
        var below = NetherSupport.GetBlockState(level, pos.Offset(0, -1, 0)).Owner;
        return below != NetherSupport.Block("netherrack")
            && below != NetherSupport.Block("warped_nylium")
            && below != NetherSupport.Block("warped_wart_block");
    }

    //PlaceWeepingVinesColumn lay twisting vines upward from the start, maps to vanilla placeWeepingVinesColumn
    private static void PlaceWeepingVinesColumn(WorldGenRegion level, RandomSource random, BlockPos start,
        int totalHeight, int minAge, int maxAge)
    {
        var plantState = NetherSupport.Block("twisting_vines_plant").DefaultBlockState;
        var headState = NetherSupport.Block("twisting_vines").DefaultBlockState;
        var cursor = start;
        for (var height = 1; height <= totalHeight; height++)
        {
            if (NetherSupport.IsAir(level, cursor))
            {
                if (height == totalHeight || !NetherSupport.IsAir(level, cursor.Offset(0, 1, 0)))
                {
                    NetherSupport.SetBlock(level, cursor,
                        headState.TrySetValue(VineAge, Mth.NextInt(random, minAge, maxAge)));
                    return;
                }
                NetherSupport.SetBlock(level, cursor, plantState);
            }
            cursor = cursor.Offset(0, 1, 0);
        }
    }
}
