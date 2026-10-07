using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Features.Impl;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;

//CountConfiguration count configuration, maps to vanilla CountConfiguration with a single count provider
public sealed class CountConfiguration : FeatureConfiguration
{
    public static readonly Codec<CountConfiguration> Codec =
        new SingleFieldMapCodec<CountConfiguration, IntProvider>(
            IntProviders.Codec.FieldOf("count"),
            count => new CountConfiguration(count),
            config => config.Count);

    public IntProvider Count { get; }

    public CountConfiguration(IntProvider count) => Count = count;
}

//BlockStateConfiguration single state configuration, maps to vanilla BlockStateConfiguration, shared by icebergs and packed ice
public sealed class BlockStateConfiguration : FeatureConfiguration
{
    public static readonly Codec<BlockStateConfiguration> Codec =
        new SingleFieldMapCodec<BlockStateConfiguration, BlockState>(
            BlockStateCodec.Instance.FieldOf("state"),
            state => new BlockStateConfiguration(state),
            config => config.State);

    public BlockState State { get; }

    public BlockStateConfiguration(BlockState state) => State = state;
}

//SpringConfiguration spring configuration, maps to vanilla SpringConfiguration
//In vanilla state is a fluid FluidState; there is no fluid physics here, so it is decoded as a block state and fluid properties such as the falling flag are ignored
public sealed class SpringConfiguration : FeatureConfiguration
{
    public static readonly Codec<SpringConfiguration> Codec =
        RecordCodecBuilder.Of5<SpringConfiguration, BlockState, bool, int, int, HolderSet<RegBlock>>(
            LenientBlockStateCodec.Instance.FieldOf("state")
                .ForGetter<SpringConfiguration, BlockState>(c => c.State),
            Codecs.Bool.OptionalFieldOf("requires_block_below", true)
                .ForGetter<SpringConfiguration, bool>(c => c.RequiresBlockBelow),
            Codecs.Int.OptionalFieldOf("rock_count", 4)
                .ForGetter<SpringConfiguration, int>(c => c.RockCount),
            Codecs.Int.OptionalFieldOf("hole_count", 1)
                .ForGetter<SpringConfiguration, int>(c => c.HoleCount),
            HolderSetCodecs.BlockSet.FieldOf("valid_blocks")
                .ForGetter<SpringConfiguration, HolderSet<RegBlock>>(c => c.ValidBlocks),
            (state, requiresBlockBelow, rockCount, holeCount, validBlocks) =>
                new SpringConfiguration(state, requiresBlockBelow, rockCount, holeCount, validBlocks));

    public BlockState State { get; }
    public bool RequiresBlockBelow { get; }
    public int RockCount { get; }
    public int HoleCount { get; }
    public HolderSet<RegBlock> ValidBlocks { get; }

    public SpringConfiguration(BlockState state, bool requiresBlockBelow, int rockCount, int holeCount,
        HolderSet<RegBlock> validBlocks)
    {
        State = state;
        RequiresBlockBelow = requiresBlockBelow;
        RockCount = rockCount;
        HoleCount = holeCount;
        ValidBlocks = validBlocks;
    }
}

//LenientBlockStateCodec lenient block state codec, the tolerant form of vanilla BlockState.CODEC
//The state in spring configs is originally a fluid state carrying properties like falling that blocks lack; unknown properties are skipped instead of erroring
internal sealed class LenientBlockStateCodec : ScalarCodec<BlockState>
{
    public static readonly LenientBlockStateCodec Instance = new();

    public override DataResult<BlockState> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeState(ops, map));

    private static DataResult<BlockState> DecodeState<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var nameTag = input.Get("Name");
        if (!nameTag.IsPresent) return DataResult<BlockState>.Error(() => "block state is missing Name");
        var idResult = IdentifierCodec.Instance.Parse(ops, nameTag.Get());
        if (!idResult.Result().IsPresent) return DataResult<BlockState>.Error(() => "block state Name is not a valid identifier");
        var id = idResult.GetOrThrow();
        var block = BuiltInRegistries.BLOCK.ContainsKey(id) ? BuiltInRegistries.BLOCK.GetValue(id) : null;
        if (block is null) return DataResult<BlockState>.Error(() => $"unknown block: {id}");
        var state = block.DefaultBlockState;

        var propertiesTag = input.Get("Properties");
        if (!propertiesTag.IsPresent) return DataResult<BlockState>.Success(state);
        var propertiesResult = ops.GetMap(propertiesTag.Get());
        if (!propertiesResult.Result().IsPresent)
            return DataResult<BlockState>.Error(() => "block state Properties must be an object");
        foreach (var (keyTag, valueTag) in propertiesResult.GetOrThrow().Entries())
        {
            var keyResult = ops.GetStringValue(keyTag);
            var valueResult = ops.GetStringValue(valueTag);
            if (!keyResult.Result().IsPresent || !valueResult.Result().IsPresent)
                return DataResult<BlockState>.Error(() => "block state Properties values must be strings");
            var name = keyResult.GetOrThrow();
            var property = FindProperty(state, name);
            if (property is null) continue;
            var parsed = property.GetValueForName(valueResult.GetOrThrow());
            if (parsed is not null) state = state.SetValue(property, parsed);
        }
        return DataResult<BlockState>.Success(state);
    }

    //FindProperty find a property by name in the state's property table
    private static PropertyBase? FindProperty(BlockState state, string name)
    {
        foreach (var property in state.GetProperties())
            if (property.Name == name) return property;
        return null;
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, BlockState value)
        => BlockStateCodec.Instance.EncodeStart(ops, value);
}

//SeagrassFeature seagrass feature, maps to vanilla SeagrassFeature
//Picks a random seabed column within eight blocks; if it is water, grows one seagrass or a patch of tall seagrass by chance
public sealed class SeagrassFeature : Feature<ProbabilityFeatureConfiguration>
{
    private const string FeatureId = "seagrass";

    public static readonly SeagrassFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SeagrassFeature());

    private SeagrassFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), ProbabilityFeatureConfiguration.Codec) { }

    protected override bool Place(ProbabilityFeatureConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var level = context.Level;
        var origin = context.Origin;
        var placedAny = false;
        var x = random.NextInt(8) - random.NextInt(8);
        var z = random.NextInt(8) - random.NextInt(8);
        var y = level.GetHeight(Heightmap.Types.OceanFloor, origin.X + x, origin.Z + z);
        var grassPos = new BlockPos(origin.X + x, y, origin.Z + z);
        if (VegetationSupport.IsState(VegetationSupport.Get(level, grassPos), "water"))
        {
            var isTall = random.NextDouble() < config.Probability;
            var state = isTall ? VegetationSupport.StateOf("tall_seagrass") : VegetationSupport.StateOf("seagrass");
            //Vanilla checks canSurvive here; there is no survival check for these blocks, so place directly
            if (isTall)
            {
                var upperState = VegetationSupport.WithProperty(state, "half", "upper");
                var above = grassPos.Offset(Direction.Up);
                if (VegetationSupport.IsState(VegetationSupport.Get(level, above), "water"))
                {
                    VegetationSupport.Set(level, grassPos, state);
                    VegetationSupport.Set(level, above, upperState);
                }
            }
            else
            {
                VegetationSupport.Set(level, grassPos, state);
            }
            placedAny = true;
        }
        return placedAny;
    }
}

//KelpFeature kelp feature, maps to vanilla KelpFeature
//Grows one to eleven segments up from the seabed; at the surface or shore the top segment becomes an age-bearing tip
public sealed class KelpFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "kelp";

    public static readonly KelpFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new KelpFeature());

    private KelpFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var placed = 0;
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        var y = level.GetHeight(Heightmap.Types.OceanFloor, origin.X, origin.Z);
        var kelpPos = new BlockPos(origin.X, y, origin.Z);
        if (VegetationSupport.IsState(VegetationSupport.Get(level, kelpPos), "water"))
        {
            var stateTop = VegetationSupport.StateOf("kelp");
            var state = VegetationSupport.StateOf("kelp_plant");
            var height = 1 + random.NextInt(10);
            var h = 0;
            while (h <= height)
            {
                var here = VegetationSupport.Get(level, kelpPos);
                var above = VegetationSupport.Get(level, kelpPos.Offset(Direction.Up));
                //Vanilla also checks canSurvive; there is no kelp survival check here
                if (VegetationSupport.IsState(here, "water") && VegetationSupport.IsState(above, "water"))
                {
                    if (h == height)
                    {
                        VegetationSupport.Set(level, kelpPos,
                            VegetationSupport.WithProperty(stateTop, "age", random.NextInt(4) + 20));
                        placed++;
                    }
                    else
                    {
                        VegetationSupport.Set(level, kelpPos, state);
                    }
                }
                else if (h > 0)
                {
                    var below = kelpPos.Offset(Direction.Down);
                    var belowBelow = below.Offset(Direction.Down);
                    if (!VegetationSupport.IsState(VegetationSupport.Get(level, belowBelow), "kelp"))
                    {
                        VegetationSupport.Set(level, below,
                            VegetationSupport.WithProperty(stateTop, "age", random.NextInt(4) + 20));
                        placed++;
                    }
                }
                kelpPos = kelpPos.Offset(Direction.Up);
                h++;
            }
        }
        return placed > 0;
    }
}

//SeaPickleFeature sea pickle feature, maps to vanilla SeaPickleFeature
//Randomly finds seabed water cells within eight blocks, placing a cluster of one to four pickles each
public sealed class SeaPickleFeature : Feature<CountConfiguration>
{
    private const string FeatureId = "sea_pickle";

    public static readonly SeaPickleFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SeaPickleFeature());

    private SeaPickleFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), CountConfiguration.Codec) { }

    protected override bool Place(CountConfiguration config, FeaturePlaceContext context)
    {
        var placed = 0;
        var random = context.Random;
        var level = context.Level;
        var origin = context.Origin;
        var count = config.Count.Sample(random);
        for (var i = 0; i < count; i++)
        {
            var x = random.NextInt(8) - random.NextInt(8);
            var z = random.NextInt(8) - random.NextInt(8);
            var y = level.GetHeight(Heightmap.Types.OceanFloor, origin.X + x, origin.Z + z);
            var picklePos = new BlockPos(origin.X + x, y, origin.Z + z);
            var pickleState = VegetationSupport.WithProperty(VegetationSupport.StateOf("sea_pickle"),
                "pickles", random.NextInt(4) + 1);
            //Vanilla also checks canSurvive; there is no sea pickle survival check here
            if (VegetationSupport.IsState(VegetationSupport.Get(level, picklePos), "water"))
            {
                VegetationSupport.Set(level, picklePos, pickleState);
                placed++;
            }
        }
        return placed > 0;
    }
}

//BlueIceFeature blue ice feature, maps to vanilla BlueIceFeature
//Grows a patch of blue ice next to packed ice underwater
public sealed class BlueIceFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "blue_ice";

    public static readonly BlueIceFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BlueIceFeature());

    private BlueIceFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var level = context.Level;
        var random = context.Random;
        if (origin.Y > VegetationSupport.SeaLevel(context.ChunkGenerator) - 1) return false;
        var originState = VegetationSupport.Get(level, origin);
        var belowState = VegetationSupport.Get(level, origin.Offset(Direction.Down));
        if (!VegetationSupport.IsState(originState, "water") && !VegetationSupport.IsState(belowState, "water"))
            return false;
        var foundPackedIce = false;
        foreach (var direction in Direction.Values)
        {
            if (direction == Direction.Down) continue;
            if (!VegetationSupport.IsState(VegetationSupport.Get(level, origin.Offset(direction)), "packed_ice"))
                continue;
            foundPackedIce = true;
            break;
        }
        if (!foundPackedIce) return false;
        VegetationSupport.Set(level, origin, VegetationSupport.StateOf("blue_ice"));
        for (var i = 0; i < 200; i++)
        {
            var yOff = random.NextInt(5) - random.NextInt(6);
            var xzDiff = 3;
            if (yOff < 2) xzDiff = 3 + (yOff / 2);
            if (xzDiff < 1) continue;
            var placePos = origin.Offset(random.NextInt(xzDiff) - random.NextInt(xzDiff), yOff,
                random.NextInt(xzDiff) - random.NextInt(xzDiff));
            var placeState = VegetationSupport.Get(level, placePos);
            if (!placeState.Owner.IsAir && !VegetationSupport.IsState(placeState, "water")
                && !VegetationSupport.IsState(placeState, "packed_ice")
                && !VegetationSupport.IsState(placeState, "ice")) continue;
            foreach (var direction in Direction.Values)
            {
                if (!VegetationSupport.IsState(VegetationSupport.Get(level, placePos.Offset(direction)), "blue_ice"))
                    continue;
                VegetationSupport.Set(level, placePos, VegetationSupport.StateOf("blue_ice"));
                break;
            }
        }
        return true;
    }
}

//IcebergFeature iceberg feature, maps to vanilla IcebergFeature
//Builds ice above water from an ellipse or circle profile, adds an inverted cone below, then removes floating and too-thin ice
public sealed class IcebergFeature : Feature<BlockStateConfiguration>
{
    private const string FeatureId = "iceberg";

    public static readonly IcebergFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new IcebergFeature());

    private IcebergFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), BlockStateConfiguration.Codec) { }

    protected override bool Place(BlockStateConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var level = context.Level;
        var globalOrigin = new BlockPos(origin.X, VegetationSupport.SeaLevel(context.ChunkGenerator), origin.Z);
        var random = context.Random;
        var snowOnTop = random.NextDouble() > 0.7d;
        var mainBlockState = config.State;
        var shapeAngle = random.NextDouble() * 2.0d * Math.PI;
        var shapeEllipseA = 11 - random.NextInt(5);
        var shapeEllipseC = 3 + random.NextInt(3);
        var isEllipse = random.NextDouble() > 0.7d;
        var overWaterHeight = isEllipse ? random.NextInt(6) + 6 : random.NextInt(15) + 3;
        if (!isEllipse && random.NextDouble() > 0.9d) overWaterHeight += random.NextInt(19) + 7;
        var underWaterHeight = Math.Min(overWaterHeight + random.NextInt(11), 18);
        var width = Math.Min(overWaterHeight + random.NextInt(7) - random.NextInt(5), 11);
        var a = isEllipse ? shapeEllipseA : 11;
        for (var xo = -a; xo < a; xo++)
        {
            for (var zo = -a; zo < a; zo++)
            {
                for (var yOff = 0; yOff < overWaterHeight; yOff++)
                {
                    var radius = isEllipse
                        ? HeightDependentRadiusEllipse(yOff, overWaterHeight, width)
                        : HeightDependentRadiusRound(random, yOff, overWaterHeight, width);
                    if (!isEllipse && xo >= radius) continue;
                    GenerateIcebergBlock(level, random, globalOrigin, overWaterHeight, xo, yOff, zo, radius, a,
                        isEllipse, shapeEllipseC, shapeAngle, snowOnTop, mainBlockState);
                }
            }
        }
        Smooth(level, globalOrigin, width, overWaterHeight, isEllipse, shapeEllipseA);
        for (var xo = -a; xo < a; xo++)
        {
            for (var zo = -a; zo < a; zo++)
            {
                for (var yOff = -1; yOff > -underWaterHeight; yOff--)
                {
                    var newA = isEllipse
                        ? Mth.Ceil(a * (1.0f - ((float)Math.Pow(yOff, 2.0d) / (underWaterHeight * 8.0f))))
                        : a;
                    var radius = HeightDependentRadiusSteep(random, -yOff, underWaterHeight, width);
                    if (xo >= radius) continue;
                    GenerateIcebergBlock(level, random, globalOrigin, underWaterHeight, xo, yOff, zo, radius, newA,
                        isEllipse, shapeEllipseC, shapeAngle, snowOnTop, mainBlockState);
                }
            }
        }
        var doCutOut = isEllipse ? random.NextDouble() > 0.1d : random.NextDouble() > 0.7d;
        if (doCutOut)
            GenerateCutOut(random, level, width, overWaterHeight, globalOrigin, isEllipse, shapeEllipseA,
                shapeAngle, shapeEllipseC);
        return true;
    }

    //GenerateCutOut carve out one piece from a corner of the iceberg, maps to vanilla generateCutOut
    private static void GenerateCutOut(RandomSource random, WorldGenRegion level, int width, int height,
        BlockPos globalOrigin, bool isEllipse, int shapeEllipseA, double shapeAngle, int shapeEllipseC)
    {
        var randomSignX = random.NextBoolean() ? -1 : 1;
        var randomSignZ = random.NextBoolean() ? -1 : 1;
        var xOff = random.NextInt(Math.Max((width / 2) - 2, 1));
        if (random.NextBoolean()) xOff = (width / 2) + 1 - random.NextInt(Math.Max(width - (width / 2) - 1, 1));
        var zOff = random.NextInt(Math.Max((width / 2) - 2, 1));
        if (random.NextBoolean()) zOff = (width / 2) + 1 - random.NextInt(Math.Max(width - (width / 2) - 1, 1));
        if (isEllipse)
        {
            var nextInt = random.NextInt(Math.Max(shapeEllipseA - 5, 1));
            zOff = nextInt;
            xOff = nextInt;
        }
        var localOrigin = new BlockPos(randomSignX * xOff, 0, randomSignZ * zOff);
        var angle = isEllipse ? shapeAngle + (Math.PI / 2.0) : random.NextDouble() * 2.0d * Math.PI;
        for (var yOff = 0; yOff < height - 3; yOff++)
        {
            var radius = HeightDependentRadiusRound(random, yOff, height, width);
            Carve(radius, yOff, globalOrigin, level, false, angle, localOrigin, shapeEllipseA, shapeEllipseC);
        }
        for (var yOff = -1; yOff > -height + random.NextInt(5); yOff--)
        {
            var radius = HeightDependentRadiusSteep(random, -yOff, height, width);
            Carve(radius, yOff, globalOrigin, level, true, angle, localOrigin, shapeEllipseA, shapeEllipseC);
        }
    }

    //Carve carve by the ellipse profile, filling water below and air above, maps to vanilla carve
    private static void Carve(int radius, int yOff, BlockPos globalOrigin, WorldGenRegion level, bool underWater,
        double angle, BlockPos localOrigin, int shapeEllipseA, int shapeEllipseC)
    {
        var a = radius + 1 + (shapeEllipseA / 3);
        var c = Math.Min(radius - 3, 3) + (shapeEllipseC / 2) - 1;
        for (var xo = -a; xo < a; xo++)
        {
            for (var zo = -a; zo < a; zo++)
            {
                var signedDist = SignedDistanceEllipse(xo, zo, localOrigin, a, c, angle);
                if (signedDist >= 0.0d) continue;
                var pos = globalOrigin.Offset(xo, yOff, zo);
                var state = VegetationSupport.Get(level, pos);
                if (!IsIcebergState(state) && !VegetationSupport.IsState(state, "snow_block")) continue;
                if (underWater)
                {
                    VegetationSupport.Set(level, pos, VegetationSupport.StateOf("water"));
                }
                else
                {
                    VegetationSupport.Set(level, pos, Blocks.AIR.DefaultBlockState);
                    RemoveFloatingSnowLayer(level, pos);
                }
            }
        }
    }

    //RemoveFloatingSnowLayer also clear floating snow above after carving, maps to vanilla removeFloatingSnowLayer
    private static void RemoveFloatingSnowLayer(WorldGenRegion level, BlockPos pos)
    {
        var above = pos.Offset(Direction.Up);
        if (VegetationSupport.IsState(VegetationSupport.Get(level, above), "snow"))
            VegetationSupport.Set(level, above, Blocks.AIR.DefaultBlockState);
    }

    //GenerateIcebergBlock decide from the profile whether to place ice, maps to vanilla generateIcebergBlock
    private static void GenerateIcebergBlock(WorldGenRegion level, RandomSource random, BlockPos origin,
        int height, int xo, int yOff, int zo, int radius, int a, bool isEllipse, int shapeEllipseC,
        double shapeAngle, bool snowOnTop, BlockState mainBlockState)
    {
        var signedDist = isEllipse
            ? SignedDistanceEllipse(xo, zo, BlockPos.Zero, a, GetEllipseC(yOff, height, shapeEllipseC), shapeAngle)
            : SignedDistanceCircle(xo, zo, BlockPos.Zero, radius, random);
        if (signedDist >= 0.0d) return;
        var pos = origin.Offset(xo, yOff, zo);
        var compareVal = isEllipse ? -0.5d : -6 - random.NextInt(3);
        if (signedDist > compareVal && random.NextDouble() > 0.9d) return;
        SetIcebergBlock(pos, level, random, height - yOff, height, isEllipse, snowOnTop, mainBlockState);
    }

    //SetIcebergBlock replace only air, snow, ice and water; snow blocks above when thick enough, maps to vanilla setIcebergBlock
    private static void SetIcebergBlock(BlockPos pos, WorldGenRegion level, RandomSource random, int hDiff,
        int height, bool isEllipse, bool snowOnTop, BlockState mainBlockState)
    {
        var state = VegetationSupport.Get(level, pos);
        if (!state.Owner.IsAir && !VegetationSupport.IsState(state, "snow_block")
            && !VegetationSupport.IsState(state, "ice") && !VegetationSupport.IsState(state, "water")) return;
        var randomness = !isEllipse || random.NextDouble() > 0.05d;
        var divisor = isEllipse ? 3 : 2;
        if (snowOnTop && !VegetationSupport.IsState(state, "water")
            && hDiff <= random.NextInt(Math.Max(1, height / divisor)) + (height * 0.6d) && randomness)
            VegetationSupport.Set(level, pos, VegetationSupport.StateOf("snow_block"));
        else
            VegetationSupport.Set(level, pos, mainBlockState);
    }

    //GetEllipseC narrow the ellipse minor axis over the top three blocks, maps to vanilla getEllipseC
    private static int GetEllipseC(int yOff, int height, int shapeEllipseC)
    {
        var c = shapeEllipseC;
        if (yOff > 0 && height - yOff <= 3) c -= 4 - (height - yOff);
        return c;
    }

    //SignedDistanceCircle implicit distance of a circular profile, maps to vanilla signedDistanceCircle
    private static double SignedDistanceCircle(int xo, int zo, BlockPos origin, int radius, RandomSource random)
    {
        var off = 10.0f * Mth.Clamp(random.NextFloat(), 0.2f, 0.8f) / radius;
        return off + Math.Pow(xo - origin.X, 2.0d) + Math.Pow(zo - origin.Z, 2.0d) - Math.Pow(radius, 2.0d);
    }

    //SignedDistanceEllipse implicit distance of an elliptical profile, maps to vanilla signedDistanceEllipse
    private static double SignedDistanceEllipse(int xo, int zo, BlockPos origin, int a, int c, double angle)
    {
        return Math.Pow((((xo - origin.X) * Math.Cos(angle)) - ((zo - origin.Z) * Math.Sin(angle))) / a, 2.0d)
            + Math.Pow((((xo - origin.X) * Math.Sin(angle)) + ((zo - origin.Z) * Math.Cos(angle))) / c, 2.0d)
            - 1.0d;
    }

    //HeightDependentRadiusRound radius depends on height for a circular profile, maps to vanilla heightDependentRadiusRound
    private static int HeightDependentRadiusRound(RandomSource random, int yOff, int height, int width)
    {
        var k = 3.5f - random.NextFloat();
        var scale = (1.0f - ((float)Math.Pow(yOff, 2.0d) / (height * k))) * width;
        if (height > 15 + random.NextInt(5))
        {
            var tempYOff = yOff < 3 + random.NextInt(6) ? yOff / 2 : yOff;
            scale = (1.0f - (tempYOff / ((height * k) * 0.4f))) * width;
        }
        return Mth.Ceil(scale / 2.0f);
    }

    //HeightDependentRadiusEllipse radius depends on height for an elliptical profile, maps to vanilla heightDependentRadiusEllipse
    private static int HeightDependentRadiusEllipse(int yOff, int height, int width)
    {
        var scale = (1.0f - ((float)Math.Pow(yOff, 2.0d) / height)) * width;
        return Mth.Ceil(scale / 2.0f);
    }

    //HeightDependentRadiusSteep radius depends on height for the underwater inverted cone, maps to vanilla heightDependentRadiusSteep
    private static int HeightDependentRadiusSteep(RandomSource random, int yOff, int height, int width)
    {
        var k = 1.0f + (random.NextFloat() / 2.0f);
        var scale = (1.0f - (yOff / (height * k))) * width;
        return Mth.Ceil(scale / 2.0f);
    }

    //IsIcebergState whether the state is iceberg material, maps to vanilla isIcebergState
    private static bool IsIcebergState(BlockState state)
        => VegetationSupport.IsState(state, "packed_ice") || VegetationSupport.IsState(state, "snow_block")
            || VegetationSupport.IsState(state, "blue_ice");

    //Smooth remove floating ice and ice too thin on three sides, maps to vanilla smooth
    private static void Smooth(WorldGenRegion level, BlockPos origin, int width, int height, bool isEllipse,
        int shapeEllipseA)
    {
        var a = isEllipse ? shapeEllipseA : width / 2;
        for (var x = -a; x <= a; x++)
        {
            for (var z = -a; z <= a; z++)
            {
                for (var yOff = 0; yOff <= height; yOff++)
                {
                    var pos = origin.Offset(x, yOff, z);
                    var state = VegetationSupport.Get(level, pos);
                    if (!IsIcebergState(state) && !VegetationSupport.IsState(state, "snow")) continue;
                    if (VegetationSupport.Get(level, pos.Offset(Direction.Down)).Owner.IsAir)
                    {
                        VegetationSupport.Set(level, pos, Blocks.AIR.DefaultBlockState);
                        VegetationSupport.Set(level, pos.Offset(Direction.Up), Blocks.AIR.DefaultBlockState);
                    }
                    else if (IsIcebergState(state))
                    {
                        var sides = new[]
                        {
                            VegetationSupport.Get(level, pos.Offset(Direction.West)),
                            VegetationSupport.Get(level, pos.Offset(Direction.East)),
                            VegetationSupport.Get(level, pos.Offset(Direction.North)),
                            VegetationSupport.Get(level, pos.Offset(Direction.South)),
                        };
                        var counter = 0;
                        foreach (var side in sides)
                            if (!IsIcebergState(side)) counter++;
                        if (counter >= 3) VegetationSupport.Set(level, pos, Blocks.AIR.DefaultBlockState);
                    }
                }
            }
        }
    }
}

//SpringFeature spring feature, maps to vanilla SpringFeature
//When the count of stone above, below, and on the four sides matches the config, replace the center with a spring
public sealed class SpringFeature : Feature<SpringConfiguration>
{
    private const string FeatureId = "spring_feature";

    public static readonly SpringFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SpringFeature());

    private SpringFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), SpringConfiguration.Codec) { }

    protected override bool Place(SpringConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        if (!VegetationSupport.IsInSet(VegetationSupport.Get(level, origin.Offset(Direction.Up)), config.ValidBlocks))
            return false;
        if (config.RequiresBlockBelow
            && !VegetationSupport.IsInSet(VegetationSupport.Get(level, origin.Offset(Direction.Down)),
                config.ValidBlocks)) return false;
        var currentState = VegetationSupport.Get(level, origin);
        if (!currentState.Owner.IsAir && !VegetationSupport.IsInSet(currentState, config.ValidBlocks)) return false;

        var rockCount = 0;
        if (VegetationSupport.IsInSet(VegetationSupport.Get(level, origin.Offset(Direction.West)), config.ValidBlocks))
            rockCount++;
        if (VegetationSupport.IsInSet(VegetationSupport.Get(level, origin.Offset(Direction.East)), config.ValidBlocks))
            rockCount++;
        if (VegetationSupport.IsInSet(VegetationSupport.Get(level, origin.Offset(Direction.North)), config.ValidBlocks))
            rockCount++;
        if (VegetationSupport.IsInSet(VegetationSupport.Get(level, origin.Offset(Direction.South)), config.ValidBlocks))
            rockCount++;
        if (VegetationSupport.IsInSet(VegetationSupport.Get(level, origin.Offset(Direction.Down)), config.ValidBlocks))
            rockCount++;

        var holeCount = 0;
        if (VegetationSupport.IsAir(level, origin.Offset(Direction.West))) holeCount++;
        if (VegetationSupport.IsAir(level, origin.Offset(Direction.East))) holeCount++;
        if (VegetationSupport.IsAir(level, origin.Offset(Direction.North))) holeCount++;
        if (VegetationSupport.IsAir(level, origin.Offset(Direction.South))) holeCount++;
        if (VegetationSupport.IsAir(level, origin.Offset(Direction.Down))) holeCount++;

        if (rockCount != config.RockCount || holeCount != config.HoleCount) return false;
        VegetationSupport.Set(level, origin, config.State);
        //Vanilla also schedules a fluid tick for the spring; there is no fluid tick here, so it is omitted
        return true;
    }
}

//GlowstoneFeature glowstone feature, maps to vanilla GlowstoneFeature
//Grows hanging only below vanilla base stone: place one block, then search upward for air cells with exactly one glowstone neighbor to fill
public sealed class GlowstoneFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "glowstone_blob";

    public static readonly GlowstoneFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new GlowstoneFeature());

    private GlowstoneFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        if (!VegetationSupport.IsAir(level, origin)) return false;
        var aboveState = VegetationSupport.Get(level, origin.Offset(Direction.Up));
        if (!VegetationSupport.IsState(aboveState, "netherrack") && !VegetationSupport.IsState(aboveState, "basalt")
            && !VegetationSupport.IsState(aboveState, "blackstone")) return false;
        VegetationSupport.Set(level, origin, VegetationSupport.StateOf("glowstone"));
        for (var i = 0; i < 1500; i++)
        {
            var placePos = origin.Offset(random.NextInt(8) - random.NextInt(8), -random.NextInt(12),
                random.NextInt(8) - random.NextInt(8));
            if (!VegetationSupport.Get(level, placePos).Owner.IsAir) continue;
            var neighbours = 0;
            foreach (var direction in Direction.Values)
            {
                if (VegetationSupport.IsState(VegetationSupport.Get(level, placePos.Offset(direction)), "glowstone"))
                    neighbours++;
                if (neighbours > 1) break;
            }
            if (neighbours == 1) VegetationSupport.Set(level, placePos, VegetationSupport.StateOf("glowstone"));
        }
        return true;
    }
}

//SnowAndFreezeFeature snow and freeze feature, maps to vanilla SnowAndFreezeFeature
//Decides per column from the motion-blocking height; when the biome is cold enough, water surfaces freeze, snow falls and the block below is marked snowy
//Vanilla's light and precipitation-type checks depend on the light and fluid systems, which are absent here, so biome temperature and the precipitation flag approximate them
public sealed class SnowAndFreezeFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "freeze_top_layer";

    public static readonly SnowAndFreezeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SnowAndFreezeFeature());

    private SnowAndFreezeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var generator = context.ChunkGenerator;
        var seaLevel = VegetationSupport.SeaLevel(generator);
        for (var dx = 0; dx < 16; dx++)
        {
            for (var dz = 0; dz < 16; dz++)
            {
                var x = origin.X + dx;
                var z = origin.Z + dz;
                var y = level.GetHeight(Heightmap.Types.MotionBlocking, x, z);
                var topPos = new BlockPos(x, y, z);
                var belowPos = topPos.Offset(Direction.Down);
                var biome = generator.BiomeSource.GetBiome(x, y, z);
                if (ShouldFreeze(biome, level, belowPos, seaLevel))
                    VegetationSupport.Set(level, belowPos, VegetationSupport.StateOf("ice"));
                if (!ShouldSnow(biome, level, topPos, seaLevel)) continue;
                VegetationSupport.Set(level, topPos, VegetationSupport.StateOf("snow"));
                var belowState = VegetationSupport.Get(level, belowPos);
                if (belowState.GetProperties().Any(p => p.Name == "snowy"))
                    VegetationSupport.Set(level, belowPos,
                        VegetationSupport.WithProperty(belowState, "snowy", true));
            }
        }
        return true;
    }

    //ShouldFreeze freeze when cold enough and the cell is water, matching the branch of vanilla shouldFreeze that does not check neighbors
    private static bool ShouldFreeze(Biome biome, WorldGenRegion level, BlockPos pos, int seaLevel)
    {
        if (!biome.Climate.HasPrecipitation) return false;
        if (!ColdEnoughToSnow(biome, pos, seaLevel)) return false;
        if (pos.Y < level.MinY() || pos.Y >= level.MaxY()) return false;
        return VegetationSupport.IsState(VegetationSupport.Get(level, pos), "water");
    }

    //ShouldSnow snow when cold enough and the cell is air or snow, maps to vanilla shouldSnow
    private static bool ShouldSnow(Biome biome, WorldGenRegion level, BlockPos pos, int seaLevel)
    {
        if (!biome.Climate.HasPrecipitation) return false;
        if (!ColdEnoughToSnow(biome, pos, seaLevel)) return false;
        if (pos.Y < level.MinY() || pos.Y >= level.MaxY()) return false;
        var state = VegetationSupport.Get(level, pos);
        return state.Owner.IsAir || VegetationSupport.IsState(state, "snow");
    }

    //ColdEnoughToSnow the height-adjusted temperature is below the rain threshold, maps to vanilla coldEnoughToSnow
    private static bool ColdEnoughToSnow(Biome biome, BlockPos pos, int seaLevel)
        => !BiomeTemperature.WarmEnoughToRain(biome, pos.X, pos.Y, pos.Z, seaLevel);
}

//BonusChestFeature bonus chest feature, maps to vanilla BonusChestFeature
//Searches the chunk in random order for the first spot that can hold a chest, and adds torches where they can stand
//Vanilla attaches the spawn bonus loot table to the chest; loot tables are out of scope at this stage, so only blocks are placed
public sealed class BonusChestFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "bonus_chest";

    public static readonly BonusChestFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BonusChestFeature());

    private BonusChestFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var level = context.Level;
        var chunkX = context.Origin.X >> 4;
        var chunkZ = context.Origin.Z >> 4;
        var xPoses = new List<int>();
        for (var x = chunkX * 16; x <= (chunkX * 16) + 15; x++) xPoses.Add(x);
        var zPoses = new List<int>();
        for (var z = chunkZ * 16; z <= (chunkZ * 16) + 15; z++) zPoses.Add(z);
        VegetationSupport.Shuffle(xPoses, random);
        VegetationSupport.Shuffle(zPoses, random);
        foreach (var x in xPoses)
        {
            foreach (var z in zPoses)
            {
                var chestPos = new BlockPos(x, level.GetHeight(Heightmap.Types.MotionBlockingNoLeaves, x, z), z);
                var chestState = VegetationSupport.Get(level, chestPos);
                if (!chestState.Owner.IsAir && !chestState.GetCollisionShape(EmptyBlockGetter.Instance, chestPos)
                        .IsEmpty) continue;
                VegetationSupport.Set(level, chestPos, VegetationSupport.StateOf("chest"));
                foreach (var direction in VegetationSupport.HorizontalPlane)
                {
                    var torchPos = chestPos.Offset(direction);
                    if (!VegetationSupport.IsFaceSturdy(VegetationSupport.Get(level, torchPos.Offset(Direction.Down)),
                            Direction.Up)) continue;
                    VegetationSupport.Set(level, torchPos, VegetationSupport.StateOf("torch"));
                }
                return true;
            }
        }
        return false;
    }
}

//UnderwaterMagmaConfiguration underwater magma configuration, maps to vanilla UnderwaterMagmaConfiguration
public sealed class UnderwaterMagmaConfiguration : FeatureConfiguration
{
    public static readonly Codec<UnderwaterMagmaConfiguration> Codec =
        RecordCodecBuilder.Of3<UnderwaterMagmaConfiguration, int, int, float>(
            Codecs.Int.FieldOf("floor_search_range")
                .ForGetter<UnderwaterMagmaConfiguration, int>(c => c.FloorSearchRange),
            Codecs.Int.FieldOf("placement_radius_around_floor")
                .ForGetter<UnderwaterMagmaConfiguration, int>(c => c.PlacementRadiusAroundFloor),
            Codecs.Float.FieldOf("placement_probability_per_valid_position")
                .ForGetter<UnderwaterMagmaConfiguration, float>(c => c.PlacementProbabilityPerValidPosition),
            (floorSearchRange, placementRadius, probability) =>
                new UnderwaterMagmaConfiguration(floorSearchRange, placementRadius, probability));

    public int FloorSearchRange { get; }
    public int PlacementRadiusAroundFloor { get; }
    public float PlacementProbabilityPerValidPosition { get; }

    public UnderwaterMagmaConfiguration(int floorSearchRange, int placementRadiusAroundFloor,
        float placementProbabilityPerValidPosition)
    {
        FloorSearchRange = floorSearchRange;
        PlacementRadiusAroundFloor = placementRadiusAroundFloor;
        PlacementProbabilityPerValidPosition = placementProbabilityPerValidPosition;
    }
}

//UnderwaterMagmaFeature underwater magma feature, maps to vanilla UnderwaterMagmaFeature
//Scans the water column for its floor, then by chance turns fully enclosed cells around it into magma blocks
public sealed class UnderwaterMagmaFeature : Feature<UnderwaterMagmaConfiguration>
{
    private const string FeatureId = "underwater_magma";

    public static readonly UnderwaterMagmaFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new UnderwaterMagmaFeature());

    private UnderwaterMagmaFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), UnderwaterMagmaConfiguration.Codec) { }

    protected override bool Place(UnderwaterMagmaConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        var floorY = GetFloorY(level, origin, config);
        if (floorY is null) return false;
        var floorPos = new BlockPos(origin.X, floorY.Value, origin.Z);
        var radius = config.PlacementRadiusAroundFloor;
        var placed = 0;
        for (var x = floorPos.X - radius; x <= floorPos.X + radius; x++)
        {
            for (var y = floorPos.Y - radius; y <= floorPos.Y + radius; y++)
            {
                for (var z = floorPos.Z - radius; z <= floorPos.Z + radius; z++)
                {
                    if (random.NextFloat() >= config.PlacementProbabilityPerValidPosition) continue;
                    var pos = new BlockPos(x, y, z);
                    if (!IsValidPlacement(level, pos)) continue;
                    VegetationSupport.Set(level, pos, VegetationSupport.StateOf("magma_block"));
                    placed++;
                }
            }
        }
        return placed > 0;
    }

    //GetFloorY the Y of the water column floor, maps to vanilla getFloorY
    private static int? GetFloorY(WorldGenRegion level, BlockPos origin, UnderwaterMagmaConfiguration config)
    {
        var column = Column.Scan(level, origin, config.FloorSearchRange,
            state => VegetationSupport.IsState(state, "water"),
            state => !VegetationSupport.IsState(state, "water"));
        return column?.Floor;
    }

    //IsValidPlacement place only when all six faces are enclosed, maps to vanilla isValidPlacement
    private static bool IsValidPlacement(WorldGenRegion level, BlockPos pos)
    {
        var state = VegetationSupport.Get(level, pos);
        if (state.Owner.IsAir || VegetationSupport.IsState(state, "water")) return false;
        if (IsVisibleFromOutside(level, pos.Offset(Direction.Down), Direction.Up)) return false;
        foreach (var direction in VegetationSupport.HorizontalPlane)
            if (IsVisibleFromOutside(level, pos.Offset(direction), direction.Opposite)) return false;
        return true;
    }

    //IsVisibleFromOutside a face not fully covered counts as exposed, maps to vanilla isVisibleFromOutside
    private static bool IsVisibleFromOutside(WorldGenRegion level, BlockPos pos, Direction coveredDirection)
    {
        var state = VegetationSupport.Get(level, pos);
        var faceOcclusionShape = state.Owner.GetOcclusionShape(state).GetFaceShape(coveredDirection);
        return faceOcclusionShape.IsEmpty || !RegBlock.IsShapeFullBlock(faceOcclusionShape);
    }
}
