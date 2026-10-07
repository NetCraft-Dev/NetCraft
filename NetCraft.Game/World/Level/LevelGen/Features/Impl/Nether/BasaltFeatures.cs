using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//ColumnFeatureConfiguration basalt column configuration, maps to vanilla ColumnFeatureConfiguration
public sealed class ColumnFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<ColumnFeatureConfiguration> Codec =
        RecordCodecBuilder.Of2<ColumnFeatureConfiguration, IntProvider, IntProvider>(
            IntProviders.Codec.FieldOf("reach").ForGetter<ColumnFeatureConfiguration, IntProvider>(c => c.Reach),
            IntProviders.Codec.FieldOf("height").ForGetter<ColumnFeatureConfiguration, IntProvider>(c => c.Height),
            (reach, height) => new ColumnFeatureConfiguration(reach, height));

    public IntProvider Reach { get; }
    public IntProvider Height { get; }

    public ColumnFeatureConfiguration(IntProvider reach, IntProvider height)
    {
        Reach = reach;
        Height = height;
    }
}

//BasaltColumnsFeature basalt columns feature, maps to vanilla BasaltColumnsFeature
public sealed class BasaltColumnsFeature : Feature<ColumnFeatureConfiguration>
{
    private const string FeatureId = "basalt_columns";

    //CountClustered/CountUnclustered point counts for the clustered and unclustered scales
    private const int ClusteredReach = 5;
    private const int ClusteredCount = 50;
    private const int UnclusteredReach = 8;
    private const int UnclusteredCount = 15;

    public static readonly BasaltColumnsFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BasaltColumnsFeature());

    private BasaltColumnsFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), ColumnFeatureConfiguration.Codec) { }

    //CannotPlaceOn the set of blocks the column base may not sit on, maps to vanilla CANNOT_PLACE_ON
    //Blocks are fetched lazily on first placement; evaluating before the block registry bootstrap would fall back to air for all of them
    private static RegBlock[]? _cannotPlaceOn;

    private static RegBlock[] CannotPlaceOn => _cannotPlaceOn ??= new[]
    {
        NetherSupport.Block("lava"),
        NetherSupport.Block("bedrock"),
        NetherSupport.Block("magma_block"),
        NetherSupport.Block("soul_sand"),
        NetherSupport.Block("nether_bricks"),
        NetherSupport.Block("nether_brick_fence"),
        NetherSupport.Block("nether_brick_stairs"),
        NetherSupport.Block("nether_wart"),
        NetherSupport.Block("chest"),
        NetherSupport.Block("spawner"),
    };

    protected override bool Place(ColumnFeatureConfiguration config, FeaturePlaceContext context)
    {
        var lavaSeaLevel = context.ChunkGenerator is NoiseBasedChunkGenerator generator
            ? generator.Settings.SeaLevel
            : 0;
        var origin = context.Origin;
        var level = context.Level;
        var random = context.Random;
        if (!CanPlaceAt(level, lavaSeaLevel, origin)) return false;
        var columnHeight = config.Height.Sample(random);
        var generateClustered = random.NextFloat() < 0.9f;
        var reach = Math.Min(columnHeight, generateClustered ? ClusteredReach : UnclusteredReach);
        var count = generateClustered ? ClusteredCount : UnclusteredCount;
        var placed = false;
        for (var i = 0; i < count; i++)
        {
            //Vanilla randomBetweenClosed takes one random per axis per point, even when the height span is 1
            var pos = new BlockPos(
                origin.X - reach + random.NextInt(reach * 2 + 1),
                origin.Y + random.NextInt(1),
                origin.Z - reach + random.NextInt(reach * 2 + 1));
            var blocksToPlaceY = columnHeight - NetherSupport.DistManhattan(pos, origin);
            if (blocksToPlaceY >= 0)
                placed |= PlaceColumn(level, lavaSeaLevel, pos, blocksToPlaceY, config.Reach.Sample(random));
        }
        return placed;
    }

    //PlaceColumn lay a cluster of basalt columns near a point, maps to vanilla placeColumn
    private static bool PlaceColumn(WorldGenRegion level, int lavaSeaLevel, BlockPos origin, int columnHeight,
        int reach)
    {
        var basalt = NetherSupport.Block("basalt");
        var placedAny = false;
        foreach (var probe in NetherSupport.BetweenClosed(origin.X - reach, origin.Y, origin.Z - reach,
                     origin.X + reach, origin.Y, origin.Z + reach))
        {
            var stepLimit = NetherSupport.DistManhattan(probe, origin);
            var columnPos = IsAirOrLavaOcean(level, lavaSeaLevel, probe)
                ? FindSurface(level, lavaSeaLevel, probe, stepLimit)
                : FindAir(level, probe, stepLimit);
            if (columnPos is not { } start) continue;
            var cursor = start;
            for (var blocksY = columnHeight - (stepLimit / 2); blocksY >= 0; blocksY--)
            {
                if (IsAirOrLavaOcean(level, lavaSeaLevel, cursor))
                {
                    NetherSupport.SetBlock(level, cursor, basalt.DefaultBlockState);
                    cursor = cursor.Offset(0, 1, 0);
                    placedAny = true;
                }
                else if (NetherSupport.IsBlock(level, cursor, basalt))
                {
                    cursor = cursor.Offset(0, 1, 0);
                }
            }
        }
        return placedAny;
    }

    //FindSurface search down the column for the first height that can hold a pillar, maps to vanilla findSurface
    private static BlockPos? FindSurface(WorldGenRegion level, int lavaSeaLevel, BlockPos cursor, int limit)
    {
        var y = cursor.Y;
        while (y > NetherSupport.MinY(level) + 1 && limit > 0)
        {
            limit--;
            var pos = new BlockPos(cursor.X, y, cursor.Z);
            if (CanPlaceAt(level, lavaSeaLevel, pos)) return pos;
            y--;
        }
        return null;
    }

    //CanPlaceAt the cell is air or lava ocean and the cell below is solid support, maps to vanilla canPlaceAt
    private static bool CanPlaceAt(WorldGenRegion level, int lavaSeaLevel, BlockPos pos)
    {
        if (!IsAirOrLavaOcean(level, lavaSeaLevel, pos)) return false;
        var below = NetherSupport.GetBlockState(level, pos.Offset(0, -1, 0)).Owner;
        return !below.IsAir && !CannotPlaceOn.Contains(below);
    }

    //FindAir search up the column for the first air cell, maps to vanilla findAir
    private static BlockPos? FindAir(WorldGenRegion level, BlockPos cursor, int limit)
    {
        var y = cursor.Y;
        while (y <= NetherSupport.MaxY(level) && limit > 0)
        {
            limit--;
            var pos = new BlockPos(cursor.X, y, cursor.Z);
            var state = NetherSupport.GetBlockState(level, pos);
            if (CannotPlaceOn.Contains(state.Owner)) return null;
            if (state.Owner.IsAir) return pos;
            y++;
        }
        return null;
    }

    //IsAirOrLavaOcean the cell is air or below the lava ocean surface, maps to vanilla isAirOrLavaOcean
    private static bool IsAirOrLavaOcean(WorldGenRegion level, int lavaSeaLevel, BlockPos pos)
    {
        var state = NetherSupport.GetBlockState(level, pos);
        return state.Owner.IsAir
            || (state.Owner == NetherSupport.Block("lava") && pos.Y <= lavaSeaLevel);
    }
}

//BasaltPillarFeature basalt pillar feature, maps to vanilla BasaltPillarFeature
public sealed class BasaltPillarFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "basalt_pillar";

    public static readonly BasaltPillarFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BasaltPillarFeature());

    private BasaltPillarFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var level = context.Level;
        var random = context.Random;
        if (!NetherSupport.IsAir(level, origin)) return false;
        if (NetherSupport.IsAir(level, origin.Offset(0, 1, 0))) return false;
        var basaltState = NetherSupport.Block("basalt").DefaultBlockState;
        var pos = origin;
        var placeNorthHangoff = true;
        var placeSouthHangoff = true;
        var placeWestHangoff = true;
        var placeEastHangoff = true;
        while (NetherSupport.IsAir(level, pos))
        {
            if (NetherSupport.IsOutsideBuildHeight(level, pos.Y)) return true;
            NetherSupport.SetBlock(level, pos, basaltState);
            placeNorthHangoff = placeNorthHangoff && PlaceHangOff(level, random, pos.Offset(Direction.North), basaltState);
            placeSouthHangoff = placeSouthHangoff && PlaceHangOff(level, random, pos.Offset(Direction.South), basaltState);
            placeWestHangoff = placeWestHangoff && PlaceHangOff(level, random, pos.Offset(Direction.West), basaltState);
            placeEastHangoff = placeEastHangoff && PlaceHangOff(level, random, pos.Offset(Direction.East), basaltState);
            pos = pos.Offset(0, -1, 0);
        }
        pos = pos.Offset(0, 1, 0);
        PlaceBaseHangOff(level, random, pos.Offset(Direction.North), basaltState);
        PlaceBaseHangOff(level, random, pos.Offset(Direction.South), basaltState);
        PlaceBaseHangOff(level, random, pos.Offset(Direction.West), basaltState);
        PlaceBaseHangOff(level, random, pos.Offset(Direction.East), basaltState);
        pos = pos.Offset(0, -1, 0);
        for (var dx = -3; dx < 4; dx++)
        {
            for (var dz = -3; dz < 4; dz++)
            {
                var probability = Mth.Abs(dx) * Mth.Abs(dz);
                if (random.NextInt(10) >= 10 - probability) continue;
                var basePos = pos.Offset(dx, 0, dz);
                var maxDrop = 3;
                while (NetherSupport.IsAir(level, basePos.Offset(0, -1, 0)))
                {
                    basePos = basePos.Offset(0, -1, 0);
                    maxDrop--;
                    if (maxDrop <= 0) break;
                }
                if (!NetherSupport.IsAir(level, basePos.Offset(0, -1, 0)))
                    NetherSupport.SetBlock(level, basePos, basaltState);
            }
        }
        return true;
    }

    //PlaceHangOff overhangs on the side of the pillar, maps to vanilla placeHangOff; nine out of ten hang
    private static bool PlaceHangOff(WorldGenRegion level, RandomSource random, BlockPos pos, BlockState state)
    {
        if (random.NextInt(10) == 0) return false;
        NetherSupport.SetBlock(level, pos, state);
        return true;
    }

    //PlaceBaseHangOff overhangs at the pillar base, maps to vanilla placeBaseHangOff
    private static void PlaceBaseHangOff(WorldGenRegion level, RandomSource random, BlockPos pos, BlockState state)
    {
        if (random.NextBoolean()) NetherSupport.SetBlock(level, pos, state);
    }
}

//ReplaceSphereConfiguration sphere replacement configuration, maps to vanilla ReplaceSphereConfiguration
//Registered as netherrack_replace_blobs; replaces netherrack with basalt or blackstone blobs
public sealed class ReplaceSphereConfiguration : FeatureConfiguration
{
    public static readonly Codec<ReplaceSphereConfiguration> Codec =
        RecordCodecBuilder.Of3<ReplaceSphereConfiguration, BlockState, BlockState, IntProvider>(
            BlockStateCodec.Instance.FieldOf("target")
                .ForGetter<ReplaceSphereConfiguration, BlockState>(c => c.TargetState),
            BlockStateCodec.Instance.FieldOf("state")
                .ForGetter<ReplaceSphereConfiguration, BlockState>(c => c.ReplaceState),
            IntProviders.Codec.FieldOf("radius").ForGetter<ReplaceSphereConfiguration, IntProvider>(c => c.Radius),
            (targetState, replaceState, radius) =>
                new ReplaceSphereConfiguration(targetState, replaceState, radius));

    public BlockState TargetState { get; }
    public BlockState ReplaceState { get; }
    public IntProvider Radius { get; }

    public ReplaceSphereConfiguration(BlockState targetState, BlockState replaceState, IntProvider radius)
    {
        TargetState = targetState;
        ReplaceState = replaceState;
        Radius = radius;
    }
}

//NetherrackReplaceBlobsFeature netherrack blob replacement feature, maps to vanilla ReplaceBlobsFeature
public sealed class NetherrackReplaceBlobsFeature : Feature<ReplaceSphereConfiguration>
{
    private const string FeatureId = "netherrack_replace_blobs";

    public static readonly NetherrackReplaceBlobsFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new NetherrackReplaceBlobsFeature());

    private NetherrackReplaceBlobsFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), ReplaceSphereConfiguration.Codec) { }

    protected override bool Place(ReplaceSphereConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var random = context.Random;
        var origin = context.Origin;
        var targetBlock = config.TargetState.Owner;
        var clampedY = Mth.Clamp(origin.Y, NetherSupport.MinY(level) + 1, NetherSupport.MaxY(level));
        var centerPos = FindTarget(level, new BlockPos(origin.X, clampedY, origin.Z), targetBlock);
        if (centerPos is not { } center) return false;
        var radiusX = config.Radius.Sample(random);
        var radiusY = config.Radius.Sample(random);
        var radiusZ = config.Radius.Sample(random);
        var maximumRadius = Math.Max(radiusX, Math.Max(radiusY, radiusZ));
        var replacedAny = false;
        foreach (var pos in NetherSupport.WithinManhattan(center, radiusX, radiusY, radiusZ))
        {
            if (NetherSupport.DistManhattan(pos, center) > maximumRadius) break;
            if (NetherSupport.GetBlockState(level, pos).Owner != targetBlock) continue;
            NetherSupport.SetBlock(level, pos, config.ReplaceState);
            replacedAny = true;
        }
        return replacedAny;
    }

    //FindTarget search down the column for the first target block, maps to vanilla findTarget
    private static BlockPos? FindTarget(WorldGenRegion level, BlockPos cursor, RegBlock target)
    {
        var y = cursor.Y;
        while (y > NetherSupport.MinY(level) + 1)
        {
            var pos = new BlockPos(cursor.X, y, cursor.Z);
            if (NetherSupport.IsBlock(level, pos, target)) return pos;
            y--;
        }
        return null;
    }
}
