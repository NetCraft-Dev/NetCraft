using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using PosCodec = NetCraft.Game.World.Level.LevelGen.Placement.BlockPosCodec;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//EndIslandFeature end island feature, maps to vanilla EndIslandFeature
public sealed class EndIslandFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "end_island";

    public static readonly EndIslandFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new EndIslandFeature());

    private EndIslandFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var random = context.Random;
        var origin = context.Origin;
        var endStone = NetherSupport.State("end_stone");
        var size = random.NextInt(3) + 4.0f;
        var y = 0;
        while (size > 0.5f)
        {
            for (var x = Mth.Floor(-size); x <= Mth.Ceil(size); x++)
            {
                for (var z = Mth.Floor(-size); z <= Mth.Ceil(size); z++)
                {
                    if ((x * x) + (z * z) <= (size + 1.0f) * (size + 1.0f))
                        NetherSupport.SetBlock(level, origin.Offset(x, y, z), endStone);
                }
            }
            size -= random.NextInt(2) + 0.5f;
            y--;
        }
        return true;
    }
}

//EndSpike end spike, maps to vanilla EndSpikeFeature.EndSpike
public sealed class EndSpike
{
    public static readonly Codec<EndSpike> Codec =
        RecordCodecBuilder.Of5<EndSpike, int, int, int, int, bool>(
            Codecs.Int.OptionalFieldOf("centerX", 0).ForGetter<EndSpike, int>(s => s.CenterX),
            Codecs.Int.OptionalFieldOf("centerZ", 0).ForGetter<EndSpike, int>(s => s.CenterZ),
            Codecs.Int.OptionalFieldOf("radius", 0).ForGetter<EndSpike, int>(s => s.Radius),
            Codecs.Int.OptionalFieldOf("height", 0).ForGetter<EndSpike, int>(s => s.Height),
            Codecs.Bool.OptionalFieldOf("guarded", false).ForGetter<EndSpike, bool>(s => s.Guarded),
            (centerX, centerZ, radius, height, guarded) =>
                new EndSpike(centerX, centerZ, radius, height, guarded));

    public int CenterX { get; }
    public int CenterZ { get; }
    public int Radius { get; }
    public int Height { get; }
    public bool Guarded { get; }

    public EndSpike(int centerX, int centerZ, int radius, int height, bool guarded)
    {
        CenterX = centerX;
        CenterZ = centerZ;
        Radius = radius;
        Height = height;
        Guarded = guarded;
    }

    //IsCenterWithinChunk whether the spike center falls in this chunk, maps to vanilla isCenterWithinChunk
    public bool IsCenterWithinChunk(BlockPos chunkOrigin)
        => (chunkOrigin.X >> 4) == (CenterX >> 4) && (chunkOrigin.Z >> 4) == (CenterZ >> 4);
}

//EndSpikeConfiguration end spike configuration, maps to vanilla EndSpikeConfiguration
public sealed class EndSpikeConfiguration : FeatureConfiguration
{
    public static readonly Codec<EndSpikeConfiguration> Codec =
        RecordCodecBuilder.Of3<EndSpikeConfiguration, bool, IReadOnlyList<EndSpike>, Optional<BlockPos>>(
            Codecs.Bool.OptionalFieldOf("crystal_invulnerable", false)
                .ForGetter<EndSpikeConfiguration, bool>(c => c.CrystalInvulnerable),
            EndSpike.Codec.ListOf().FieldOf("spikes")
                .ForGetter<EndSpikeConfiguration, IReadOnlyList<EndSpike>>(c => c.Spikes),
            PosCodec.Instance.OptionalFieldOf("crystal_beam_target")
                .ForGetter<EndSpikeConfiguration, Optional<BlockPos>>(
                    c => NetherSupport.ToOptional(c.CrystalBeamTarget)),
            (crystalInvulnerable, spikes, crystalBeamTarget) =>
                new EndSpikeConfiguration(crystalInvulnerable, spikes,
                    crystalBeamTarget.IsPresent ? crystalBeamTarget.Get() : null));

    public bool CrystalInvulnerable { get; }
    public IReadOnlyList<EndSpike> Spikes { get; }
    public BlockPos? CrystalBeamTarget { get; }

    public EndSpikeConfiguration(bool crystalInvulnerable, IReadOnlyList<EndSpike> spikes,
        BlockPos? crystalBeamTarget)
    {
        CrystalInvulnerable = crystalInvulnerable;
        Spikes = spikes;
        CrystalBeamTarget = crystalBeamTarget;
    }
}

//EndSpikeFeature end spike feature, maps to vanilla EndSpikeFeature
public sealed class EndSpikeFeature : Feature<EndSpikeConfiguration>
{
    private const string FeatureId = "end_spike";

    //NumberOfSpikes number of spikes around the main island, maps to vanilla NUMBER_OF_SPIKES
    private const int NumberOfSpikes = 10;

    //SpikeDistance distance from a spike to the main island center, maps to vanilla SPIKE_DISTANCE
    private const double SpikeDistance = 42.0d;

    public static readonly EndSpikeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new EndSpikeFeature());

    private EndSpikeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), EndSpikeConfiguration.Codec) { }

    protected override bool Place(EndSpikeConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var random = context.Random;
        var origin = context.Origin;
        var spikes = config.Spikes.Count > 0 ? config.Spikes : GetSpikesForLevel(level.Seed);
        foreach (var spike in spikes)
        {
            if (spike.IsCenterWithinChunk(origin)) PlaceSpike(level, random, spike);
        }
        return true;
    }

    //GetSpikesForLevel derive the layout of the ten spikes from the world seed, maps to vanilla getSpikesForLevel
    //Vanilla caches the result by key; it is recomputed each time here, and the result depends only on the seed so caching does not matter
    public static IReadOnlyList<EndSpike> GetSpikesForLevel(long seed)
    {
        var random = new LegacyRandomSource(seed);
        var key = random.NextLong() & 65535;
        var sizes = new int[NumberOfSpikes];
        for (var i = 0; i < NumberOfSpikes; i++) sizes[i] = i;
        var sizeRandom = new LegacyRandomSource(key);
        for (var i = NumberOfSpikes; i > 1; i--)
        {
            var swapTo = sizeRandom.NextInt(i);
            (sizes[i - 1], sizes[swapTo]) = (sizes[swapTo], sizes[i - 1]);
        }
        var result = new List<EndSpike>(NumberOfSpikes);
        for (var i = 0; i < NumberOfSpikes; i++)
        {
            var x = Mth.Floor(SpikeDistance * Math.Cos(2.0d * (-Math.PI + 0.3141592653589793d * i)));
            var z = Mth.Floor(SpikeDistance * Math.Sin(2.0d * (-Math.PI + 0.3141592653589793d * i)));
            var size = sizes[i];
            var radius = 2 + (size / 3);
            var height = 76 + (size * 3);
            var guarded = size == 1 || size == 2;
            result.Add(new EndSpike(x, z, radius, height, guarded));
        }
        return result;
    }

    //PlaceSpike build one spike, maps to vanilla placeSpike
    private static void PlaceSpike(WorldGenRegion level, RandomSource random, EndSpike spike)
    {
        var obsidian = NetherSupport.State("obsidian");
        var air = Blocks.AIR.DefaultBlockState;
        var radius = spike.Radius;
        foreach (var pos in NetherSupport.BetweenClosed(
                     spike.CenterX - radius, NetherSupport.MinY(level), spike.CenterZ - radius,
                     spike.CenterX + radius, spike.Height + 10, spike.CenterZ + radius))
        {
            var dx = pos.X + 0.5d - spike.CenterX;
            var dy = pos.Y + 0.5d - pos.Y;
            var dz = pos.Z + 0.5d - spike.CenterZ;
            if (dx * dx + dy * dy + dz * dz <= (radius * radius) + 1 && pos.Y < spike.Height)
                NetherSupport.SetBlock(level, pos, obsidian);
            else if (pos.Y > 65)
                NetherSupport.SetBlock(level, pos, air);
        }
        if (spike.Guarded) PlaceGuard(level, spike);
        PlaceCrystal(level, random, spike);
    }

    //PlaceGuard iron bars cage at the top of the spike, maps to the iron bars section of vanilla placeSpike
    private static void PlaceGuard(WorldGenRegion level, EndSpike spike)
    {
        var ironBars = NetherSupport.Block("iron_bars").DefaultBlockState;
        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dz = -2; dz <= 2; dz++)
            {
                for (var dy = 0; dy <= 3; dy++)
                {
                    var isXSide = Mth.Abs(dx) == 2;
                    var isZSide = Mth.Abs(dz) == 2;
                    var top = dy == 3;
                    if (!isXSide && !isZSide && !top) continue;
                    var xEdge = dx == -2 || dx == 2 || top;
                    var zEdge = dz == -2 || dz == 2 || top;
                    var state = ironBars
                        .TrySetValue(BlockStateProperties.NorthConnected, xEdge && dz != -2)
                        .TrySetValue(BlockStateProperties.SouthConnected, xEdge && dz != 2)
                        .TrySetValue(BlockStateProperties.WestConnected, zEdge && dx != -2)
                        .TrySetValue(BlockStateProperties.EastConnected, zEdge && dx != 2);
                    NetherSupport.SetBlock(level,
                        new BlockPos(spike.CenterX + dx, spike.Height + dy, spike.CenterZ + dz), state);
                }
            }
        }
    }

    //PlaceCrystal crystal base at the top of the spike, maps to the end crystal section of vanilla placeSpike
    //Vanilla spawns the end crystal entity here and sets invulnerability and the beam target; there is no entity system here
    //Only that facing random value is kept and bedrock with fire is placed, keeping the random sequence of later spikes in the chunk aligned with vanilla
    private static void PlaceCrystal(WorldGenRegion level, RandomSource random, EndSpike spike)
    {
        _ = random.NextFloat();
        var crystalPos = new BlockPos(spike.CenterX, spike.Height + 1, spike.CenterZ);
        NetherSupport.SetBlock(level, crystalPos.Offset(0, -1, 0), NetherSupport.State("bedrock"));
        //Vanilla uses FireBlock.getState to pick the fire shape from neighbors; the fire default state is used here
        NetherSupport.SetBlock(level, crystalPos, NetherSupport.State("fire"));
    }
}

//EndPlatformFeature end spawn platform feature, maps to vanilla EndPlatformFeature
public sealed class EndPlatformFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "end_platform";

    public static readonly EndPlatformFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new EndPlatformFeature());

    private EndPlatformFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        CreateEndPlatform(context.Level, context.Origin);
        return true;
    }

    //CreateEndPlatform build the end spawn platform, maps to vanilla createEndPlatform with drop resources forced off
    public static void CreateEndPlatform(WorldGenRegion level, BlockPos origin)
    {
        var obsidian = NetherSupport.Block("obsidian");
        var air = Blocks.AIR;
        for (var dz = -2; dz <= 2; dz++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                for (var dy = -1; dy < 3; dy++)
                {
                    var pos = origin.Offset(dx, dy, dz);
                    var block = dy == -1 ? obsidian : air;
                    if (NetherSupport.GetBlockState(level, pos).Owner == block) continue;
                    NetherSupport.SetBlock(level, pos, block.DefaultBlockState);
                }
            }
        }
    }
}

//EndGatewayConfiguration end gateway configuration, maps to vanilla EndGatewayConfiguration
public sealed class EndGatewayConfiguration : FeatureConfiguration
{
    public static readonly Codec<EndGatewayConfiguration> Codec =
        RecordCodecBuilder.Of2<EndGatewayConfiguration, Optional<BlockPos>, bool>(
            PosCodec.Instance.OptionalFieldOf("exit")
                .ForGetter<EndGatewayConfiguration, Optional<BlockPos>>(
                    c => NetherSupport.ToOptional(c.Exit)),
            Codecs.Bool.FieldOf("exact").ForGetter<EndGatewayConfiguration, bool>(c => c.Exact),
            (exit, exact) => new EndGatewayConfiguration(exit.IsPresent ? exit.Get() : null, exact));

    public BlockPos? Exit { get; }
    public bool Exact { get; }

    public EndGatewayConfiguration(BlockPos? exit, bool exact)
    {
        Exit = exit;
        Exact = exact;
    }
}

//EndGatewayFeature end gateway feature, maps to vanilla EndGatewayFeature
//Only the gateway blocks and geometry are placed; the exit position and exact flag belong to block entity data, and block entities are not created here
public sealed class EndGatewayFeature : Feature<EndGatewayConfiguration>
{
    private const string FeatureId = "end_gateway";

    public static readonly EndGatewayFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new EndGatewayFeature());

    private EndGatewayFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), EndGatewayConfiguration.Codec) { }

    protected override bool Place(EndGatewayConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var level = context.Level;
        var gateway = NetherSupport.State("end_gateway");
        var air = Blocks.AIR.DefaultBlockState;
        var bedrock = NetherSupport.State("bedrock");
        foreach (var pos in NetherSupport.BetweenClosed(origin.X - 1, origin.Y - 2, origin.Z - 1,
                     origin.X + 1, origin.Y + 2, origin.Z + 1))
        {
            var sameX = pos.X == origin.X;
            var sameY = pos.Y == origin.Y;
            var sameZ = pos.Z == origin.Z;
            var end = Math.Abs(pos.Y - origin.Y) == 2;
            if (sameX && sameY && sameZ)
            {
                NetherSupport.SetBlock(level, pos, gateway);
            }
            else if (sameY)
            {
                NetherSupport.SetBlock(level, pos, air);
            }
            else if (end && sameX && sameZ)
            {
                NetherSupport.SetBlock(level, pos, bedrock);
            }
            else if ((!sameX && !sameZ) || end)
            {
                NetherSupport.SetBlock(level, pos, air);
            }
            else
            {
                NetherSupport.SetBlock(level, pos, bedrock);
            }
        }
        return true;
    }
}

//ChorusPlantFeature chorus plant feature, maps to vanilla ChorusPlantFeature
public sealed class ChorusPlantFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "chorus_plant";

    //MaxHorizontalSpread max horizontal spread of the plant from the start, matching the 8 passed to vanilla generatePlant
    private const int MaxHorizontalSpread = 8;

    //FlowerAge chorus flower age 0-5, maps to vanilla ChorusFlowerBlock.AGE
    private static readonly IntegerProperty FlowerAge = new("age", 0, 5);

    //UpConnected/DownConnected the up and down connection flags of the plant, maps to vanilla PipeBlock UP/DOWN
    private static readonly BooleanProperty UpConnected = new("up");
    private static readonly BooleanProperty DownConnected = new("down");

    //HorizontalDirections the value order of the four horizontal directions, maps to vanilla Direction.Plane.HORIZONTAL
    private static readonly Direction[] HorizontalDirections =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    public static readonly ChorusPlantFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new ChorusPlantFeature());

    private ChorusPlantFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        if (!NetherSupport.IsAir(level, origin)) return false;
        if (!NetherSupport.MatchesTag(level, origin.Offset(0, -1, 0), NetherSupport.SupportsChorusPlantTag))
            return false;
        GeneratePlant(level, origin, random, MaxHorizontalSpread);
        return true;
    }

    //GeneratePlant grow one chorus plant from the start, maps to vanilla ChorusFlowerBlock.generatePlant
    private static void GeneratePlant(WorldGenRegion level, BlockPos target, RandomSource random,
        int maxHorizontalSpread)
    {
        var plant = NetherSupport.Block("chorus_plant");
        NetherSupport.SetBlock(level, target, WithConnections(level, target, plant.DefaultBlockState));
        GrowTreeRecursive(level, target, random, target, maxHorizontalSpread, 0);
    }

    //GrowTreeRecursive recursively grow the plant and its branches, maps to vanilla growTreeRecursive
    private static void GrowTreeRecursive(WorldGenRegion level, BlockPos current, RandomSource random,
        BlockPos startPos, int maxHorizontalSpread, int depth)
    {
        var plant = NetherSupport.Block("chorus_plant");
        var height = random.NextInt(4) + 1;
        if (depth == 0) height++;
        for (var i = 0; i < height; i++)
        {
            var target = current.Offset(0, i + 1, 0);
            if (!AllNeighborsEmpty(level, target, null)) return;
            NetherSupport.SetBlock(level, target, WithConnections(level, target, plant.DefaultBlockState));
            var below = target.Offset(0, -1, 0);
            NetherSupport.SetBlock(level, below, WithConnections(level, below, plant.DefaultBlockState));
        }
        var placedStem = false;
        if (depth < 4)
        {
            var stems = random.NextInt(4);
            if (depth == 0) stems++;
            for (var i = 0; i < stems; i++)
            {
                var direction = HorizontalDirections[random.NextInt(4)];
                var branch = current.Offset(0, height, 0).Offset(direction);
                if (Math.Abs(branch.X - startPos.X) < maxHorizontalSpread
                    && Math.Abs(branch.Z - startPos.Z) < maxHorizontalSpread
                    && NetherSupport.IsAir(level, branch)
                    && NetherSupport.IsAir(level, branch.Offset(0, -1, 0))
                    && AllNeighborsEmpty(level, branch, direction.Opposite))
                {
                    placedStem = true;
                    NetherSupport.SetBlock(level, branch,
                        WithConnections(level, branch, plant.DefaultBlockState));
                    var opposite = branch.Offset(direction.Opposite);
                    NetherSupport.SetBlock(level, opposite,
                        WithConnections(level, opposite, plant.DefaultBlockState));
                    GrowTreeRecursive(level, branch, random, startPos, maxHorizontalSpread, depth + 1);
                }
            }
        }
        if (placedStem) return;
        var flowerState = NetherSupport.Block("chorus_flower").DefaultBlockState;
        NetherSupport.SetBlock(level, current.Offset(0, height, 0),
            flowerState.TrySetValue(FlowerAge, 5));
    }

    //AllNeighborsEmpty whether all four horizontal neighbors are empty, maps to vanilla allNeighborsEmpty
    private static bool AllNeighborsEmpty(WorldGenRegion level, BlockPos pos, Direction? ignore)
    {
        foreach (var direction in HorizontalDirections)
        {
            if (ignore is { } skip && direction == skip) continue;
            if (!NetherSupport.IsAir(level, pos.Offset(direction))) return false;
        }
        return true;
    }

    //WithConnections compute the plant's connection state from the six neighbors, maps to vanilla ChorusPlantBlock.getStateWithConnections
    private static BlockState WithConnections(WorldGenRegion level, BlockPos pos, BlockState defaultState)
    {
        var plant = defaultState.Owner;
        var flower = NetherSupport.Block("chorus_flower");
        var down = NetherSupport.GetBlockState(level, pos.Offset(0, -1, 0));
        var up = NetherSupport.GetBlockState(level, pos.Offset(0, 1, 0));
        var north = NetherSupport.GetBlockState(level, pos.Offset(Direction.North));
        var east = NetherSupport.GetBlockState(level, pos.Offset(Direction.East));
        var south = NetherSupport.GetBlockState(level, pos.Offset(Direction.South));
        var west = NetherSupport.GetBlockState(level, pos.Offset(Direction.West));
        return defaultState
            .TrySetValue(DownConnected, down.Owner == plant || down.Owner == flower
                || NetherSupport.StateMatchesTag(down, NetherSupport.SupportsChorusPlantTag))
            .TrySetValue(UpConnected, up.Owner == plant || up.Owner == flower)
            .TrySetValue(BlockStateProperties.NorthConnected, north.Owner == plant || north.Owner == flower)
            .TrySetValue(BlockStateProperties.EastConnected, east.Owner == plant || east.Owner == flower)
            .TrySetValue(BlockStateProperties.SouthConnected, south.Owner == plant || south.Owner == flower)
            .TrySetValue(BlockStateProperties.WestConnected, west.Owner == plant || west.Owner == flower);
    }
}
