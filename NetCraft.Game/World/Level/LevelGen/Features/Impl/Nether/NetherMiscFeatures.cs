using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//DeltaFeatureConfiguration delta configuration, maps to vanilla DeltaFeatureConfiguration
public sealed class DeltaFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<DeltaFeatureConfiguration> Codec =
        RecordCodecBuilder.Of4<DeltaFeatureConfiguration, BlockState, BlockState, IntProvider, IntProvider>(
            BlockStateCodec.Instance.FieldOf("contents")
                .ForGetter<DeltaFeatureConfiguration, BlockState>(c => c.Contents),
            BlockStateCodec.Instance.FieldOf("rim")
                .ForGetter<DeltaFeatureConfiguration, BlockState>(c => c.Rim),
            IntProviders.Codec.FieldOf("size").ForGetter<DeltaFeatureConfiguration, IntProvider>(c => c.Size),
            IntProviders.Codec.FieldOf("rim_size")
                .ForGetter<DeltaFeatureConfiguration, IntProvider>(c => c.RimSize),
            (contents, rim, size, rimSize) => new DeltaFeatureConfiguration(contents, rim, size, rimSize));

    public BlockState Contents { get; }
    public BlockState Rim { get; }
    public IntProvider Size { get; }
    public IntProvider RimSize { get; }

    public DeltaFeatureConfiguration(BlockState contents, BlockState rim, IntProvider size, IntProvider rimSize)
    {
        Contents = contents;
        Rim = rim;
        Size = size;
        RimSize = rimSize;
    }
}

//DeltaFeature delta feature, maps to vanilla DeltaFeature
//Nether lava deltas with obsidian rims
public sealed class DeltaFeature : Feature<DeltaFeatureConfiguration>
{
    private const string FeatureId = "delta_feature";

    //RimSpawnChance chance of spawning a rim, maps to vanilla RIM_SPAWN_CHANCE
    private const double RimSpawnChance = 0.9d;

    public static readonly DeltaFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new DeltaFeature());

    private DeltaFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), DeltaFeatureConfiguration.Codec) { }

    //CannotReplace blocks the delta may not cover, maps to vanilla CANNOT_REPLACE
    private static RegBlock[]? _cannotReplace;

    private static RegBlock[] CannotReplace => _cannotReplace ??= new[]
    {
        NetherSupport.Block("bedrock"),
        NetherSupport.Block("nether_bricks"),
        NetherSupport.Block("nether_brick_fence"),
        NetherSupport.Block("nether_brick_stairs"),
        NetherSupport.Block("nether_wart"),
        NetherSupport.Block("chest"),
        NetherSupport.Block("spawner"),
    };

    protected override bool Place(DeltaFeatureConfiguration config, FeaturePlaceContext context)
    {
        var anyPlaced = false;
        var random = context.Random;
        var level = context.Level;
        var origin = context.Origin;
        var spawnRim = random.NextDouble() < RimSpawnChance;
        var rimX = spawnRim ? config.RimSize.Sample(random) : 0;
        var rimZ = spawnRim ? config.RimSize.Sample(random) : 0;
        var hasRim = spawnRim && rimX != 0 && rimZ != 0;
        var radiusX = config.Size.Sample(random);
        var radiusZ = config.Size.Sample(random);
        var radiusLimit = Math.Max(radiusX, radiusZ);
        foreach (var pos in NetherSupport.WithinManhattan(origin, radiusX, 0, radiusZ))
        {
            if (NetherSupport.DistManhattan(pos, origin) > radiusLimit) break;
            if (!IsClear(level, pos, config)) continue;
            if (hasRim)
            {
                anyPlaced = true;
                NetherSupport.SetBlock(level, pos, config.Rim);
            }
            var posOffset = pos.Offset(rimX, 0, rimZ);
            if (!IsClear(level, posOffset, config)) continue;
            anyPlaced = true;
            NetherSupport.SetBlock(level, posOffset, config.Contents);
        }
        return anyPlaced;
    }

    //IsClear the position is open only above and is not a cannot-replace block, maps to vanilla isClear
    private static bool IsClear(WorldGenRegion level, BlockPos pos, DeltaFeatureConfiguration config)
    {
        var state = NetherSupport.GetBlockState(level, pos);
        if (state.Owner == config.Contents.Owner || CannotReplace.Contains(state.Owner)) return false;
        foreach (var direction in Direction.Values)
        {
            var isAir = NetherSupport.GetBlockState(level, pos.Offset(direction)).Owner.IsAir;
            if (isAir && direction != Direction.Up) return false;
            if (!isAir && direction == Direction.Up) return false;
        }
        return true;
    }
}

//SpikeConfiguration spike configuration, maps to vanilla SpikeConfiguration
//Shared by ice spikes and sulfur spikes, distinguished by block state and two predicates
public sealed class SpikeConfiguration : FeatureConfiguration
{
    public static readonly Codec<SpikeConfiguration> Codec =
        RecordCodecBuilder.Of3<SpikeConfiguration, BlockState, BlockPredicate, BlockPredicate>(
            BlockStateCodec.Instance.FieldOf("state")
                .ForGetter<SpikeConfiguration, BlockState>(c => c.State),
            BlockPredicate.Codec.FieldOf("can_place_on")
                .ForGetter<SpikeConfiguration, BlockPredicate>(c => c.CanPlaceOn),
            BlockPredicate.Codec.FieldOf("can_replace")
                .ForGetter<SpikeConfiguration, BlockPredicate>(c => c.CanReplace),
            (state, canPlaceOn, canReplace) => new SpikeConfiguration(state, canPlaceOn, canReplace));

    public BlockState State { get; }
    public BlockPredicate CanPlaceOn { get; }
    public BlockPredicate CanReplace { get; }

    public SpikeConfiguration(BlockState state, BlockPredicate canPlaceOn, BlockPredicate canReplace)
    {
        State = state;
        CanPlaceOn = canPlaceOn;
        CanReplace = canReplace;
    }
}

//SpikeFeature spike feature, maps to vanilla SpikeFeature
//Grows an upward-pointing cone from the top down and drags a column below it
public sealed class SpikeFeature : Feature<SpikeConfiguration>
{
    private const string FeatureId = "spike";

    public static readonly SpikeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SpikeFeature());

    private SpikeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), SpikeConfiguration.Codec) { }

    protected override bool Place(SpikeConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var random = context.Random;
        var level = context.Level;
        while (NetherSupport.IsAir(level, origin) && origin.Y > NetherSupport.MinY(level) + 2)
            origin = origin.Offset(0, -1, 0);
        if (!config.CanPlaceOn.Test(level, origin)) return false;
        var topOrigin = origin.Offset(0, random.NextInt(4), 0);
        var height = random.NextInt(4) + 7;
        var width = (height / 4) + random.NextInt(2);
        if (width > 1 && random.NextInt(60) == 0) topOrigin = topOrigin.Offset(0, 10 + random.NextInt(30), 0);
        for (var yOff = 0; yOff < height; yOff++)
        {
            var scale = (1.0f - (yOff / (float)height)) * width;
            var newWidth = Mth.Ceil(scale);
            for (var xo = -newWidth; xo <= newWidth; xo++)
            {
                var dx = Mth.Abs(xo) - 0.25f;
                for (var zo = -newWidth; zo <= newWidth; zo++)
                {
                    var dz = Mth.Abs(zo) - 0.25f;
                    if (!((xo == 0 && zo == 0) || (dx * dx) + (dz * dz) <= scale * scale)) continue;
                    if ((xo == -newWidth || xo == newWidth || zo == -newWidth || zo == newWidth)
                        && random.NextFloat() > 0.75f) continue;
                    var above = topOrigin.Offset(xo, yOff, zo);
                    if (NetherSupport.IsAir(level, above) || config.CanReplace.Test(level, above))
                        NetherSupport.SetBlock(level, above, config.State);
                    if (yOff == 0 || newWidth <= 1) continue;
                    var below = topOrigin.Offset(xo, -yOff, zo);
                    if (NetherSupport.IsAir(level, below) || config.CanReplace.Test(level, below))
                        NetherSupport.SetBlock(level, below, config.State);
                }
            }
        }
        var pillarWidth = Mth.Clamp(width - 1, 0, 1);
        for (var xo = -pillarWidth; xo <= pillarWidth; xo++)
        {
            for (var zo = -pillarWidth; zo <= pillarWidth; zo++)
            {
                var cursor = topOrigin.Offset(xo, -1, zo);
                var runLength = 50;
                if (Mth.Abs(xo) == 1 && Mth.Abs(zo) == 1) runLength = random.NextInt(5);
                while (cursor.Y > 50)
                {
                    var state = NetherSupport.GetBlockState(level, cursor);
                    if (!state.Owner.IsAir && !config.CanReplace.Test(level, cursor) && state != config.State)
                        continue;
                    NetherSupport.SetBlock(level, cursor, config.State);
                    cursor = cursor.Offset(0, -1, 0);
                    runLength--;
                    if (runLength > 0) continue;
                    cursor = cursor.Offset(0, -(random.NextInt(5) + 1), 0);
                    runLength = random.NextInt(5);
                }
            }
        }
        return true;
    }
}

//VoidStartPlatformFeature void start platform feature, maps to vanilla VoidStartPlatformFeature
//Builds a rounded square stone platform at the main island center for superflat and void worlds
public sealed class VoidStartPlatformFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "void_start_platform";

    //PlatformOffset coordinate of the platform center relative to the world origin, maps to vanilla PLATFORM_OFFSET
    private static readonly BlockPos PlatformOffset = new(8, 3, 8);

    //PlatformRadius square radius of the platform, maps to vanilla PLATFORM_RADIUS
    private const int PlatformRadius = 16;

    public static readonly VoidStartPlatformFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new VoidStartPlatformFeature());

    private VoidStartPlatformFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var currentChunk = new ChunkPos(origin.X >> 4, origin.Z >> 4);
        //The platform covers only the 3x3 chunks around the origin chunk, matching the checkerboard distance check against vanilla PLATFORM_ORIGIN_CHUNK
        if (CheckerboardDistance(currentChunk.X, currentChunk.Z, 0, 0) > 1) return true;
        var platformOrigin = new BlockPos(PlatformOffset.X, origin.Y + PlatformOffset.Y, PlatformOffset.Z);
        var cobblestone = NetherSupport.State("cobblestone");
        var stone = NetherSupport.State("stone");
        for (var z = currentChunk.MinBlockZ; z <= currentChunk.MaxBlockZ; z++)
        {
            for (var x = currentChunk.MinBlockX; x <= currentChunk.MaxBlockX; x++)
            {
                if (CheckerboardDistance(platformOrigin.X, platformOrigin.Z, x, z) > PlatformRadius) continue;
                var pos = new BlockPos(x, platformOrigin.Y, z);
                NetherSupport.SetBlock(level, pos, pos == platformOrigin ? cobblestone : stone);
            }
        }
        return true;
    }

    //CheckerboardDistance checkerboard distance between two points, maps to vanilla checkerboardDistance
    private static int CheckerboardDistance(int xa, int za, int xb, int zb)
        => Math.Max(Math.Abs(xa - xb), Math.Abs(za - zb));
}
