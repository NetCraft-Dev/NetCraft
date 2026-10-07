using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Game.World.Level.LevelGen.Features.Impl;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;

//VegetationSupport shared block lookup, tag checks, property copying and plane direction table for surface vegetation features
//Fetch blocks and states by registry name so every feature does not repeat the lookup and null fallback
internal static class VegetationSupport
{
    //HorizontalPlane horizontal direction table, ordered by the declaration order of vanilla Direction.Plane.HORIZONTAL
    //Fallen tree random facing, bonus_chest and multiface per-direction traversal all depend on this order; a wrong order shifts results for the same seed
    public static readonly Direction[] HorizontalPlane =
    {
        Direction.North, Direction.East, Direction.South, Direction.West,
    };

    //LeavesTag leaf tag, maps to vanilla BlockTags.LEAVES
    public static readonly TagKey<RegBlock> LeavesTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("leaves"));

    //ReplaceableByTreesTag blocks replaceable by trees, maps to vanilla BlockTags.REPLACEABLE_BY_TREES
    public static readonly TagKey<RegBlock> ReplaceableByTreesTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("replaceable_by_trees"));

    //ReplaceableByMushroomsTag blocks replaceable by mushrooms, maps to vanilla BlockTags.REPLACEABLE_BY_MUSHROOMS
    public static readonly TagKey<RegBlock> ReplaceableByMushroomsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("replaceable_by_mushrooms"));

    //SupportsBambooTag blocks bamboo can be planted on, maps to vanilla BlockTags.SUPPORTS_BAMBOO
    public static readonly TagKey<RegBlock> SupportsBambooTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("supports_bamboo"));

    //BeneathBambooPodzolReplaceableTag blocks replaceable with podzol below bamboo, maps to vanilla BlockTags.BENEATH_BAMBOO_PODZOL_REPLACEABLE
    public static readonly TagKey<RegBlock> BeneathBambooPodzolReplaceableTag =
        TagKey<RegBlock>.Create(Registries.BLOCK,
            Identifier.WithDefaultNamespace("beneath_bamboo_podzol_replaceable"));

    //BlockOf fetch a block by registry name; falls back to air when unregistered
    public static RegBlock BlockOf(string path)
    {
        var id = Identifier.WithDefaultNamespace(path);
        return BuiltInRegistries.BLOCK.ContainsKey(id)
            ? BuiltInRegistries.BLOCK.GetValue(id) ?? Blocks.AIR
            : Blocks.AIR;
    }

    //StateOf default state by registry name
    public static BlockState StateOf(string path) => BlockOf(path).DefaultBlockState;

    //IsState whether the state is made of the block with the given registry name
    public static bool IsState(BlockState state, string path) => state.Owner.Id.Path == path;

    //IsAir whether the position is air
    public static bool IsAir(WorldGenRegion level, BlockPos pos)
        => level.GetBlockState(pos.X, pos.Y, pos.Z).Owner.IsAir;

    //Get read the block state from the world
    public static BlockState Get(WorldGenRegion level, BlockPos pos) => level.GetBlockState(pos.X, pos.Y, pos.Z);

    //Set write the block state
    public static void Set(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);

    //InTag whether the state belongs to a block tag; treat an unbound tag as no match
    public static bool InTag(BlockState state, TagKey<RegBlock> tag) => ProcessorBlockHelper.InTag(state, tag);

    //IsInSet whether the state belongs to a block set; tags use tag matching, direct sets use membership
    public static bool IsInSet(BlockState state, HolderSet<RegBlock> set)
    {
        if (state.Id == 0 || !set.IsBound) return false;
        return set.UnwrapKey() is { } tag ? InTag(state, tag) : ProcessorBlockHelper.InSet(state, set);
    }

    //HasProperty whether the state has this property name
    public static bool HasProperty(BlockState state, string propertyName)
        => state.GetProperties().Any(property => property.Name == propertyName);

    //WithProperty change the state by property name and value name; return it unchanged when either does not match
    public static BlockState WithProperty(BlockState state, string propertyName, string valueName)
    {
        foreach (var property in state.GetProperties())
        {
            if (property.Name != propertyName) continue;
            if (property.GetValueForName(valueName) is not { } value) return state;
            return state.SetValue(property, value);
        }
        return state;
    }

    //WithProperty boolean property overload
    public static BlockState WithProperty(BlockState state, string propertyName, bool value)
        => WithProperty(state, propertyName, value ? "true" : "false");

    //WithProperty integer property overload
    public static BlockState WithProperty(BlockState state, string propertyName, int value)
        => WithProperty(state, propertyName, value.ToString());

    //AxisName the name of the direction's axis, used to reset facing by axis property
    public static string AxisName(Direction direction) => direction.AxisValue switch
    {
        Direction.Axis.X => "x",
        Direction.Axis.Y => "y",
        _ => "z",
    };

    //FaceName the six-direction property name for a direction
    public static string FaceName(Direction direction) => direction.Id3D switch
    {
        Direction.DownId => "down",
        Direction.UpId => "up",
        Direction.NorthId => "north",
        Direction.SouthId => "south",
        Direction.WestId => "west",
        _ => "east",
    };

    //From2DDataValue fetch a direction by horizontal index, in the south west north east order of vanilla Direction.from2DDataValue
    public static Direction From2DDataValue(int index)
    {
        var i = ((index % 4) + 4) % 4;
        return i switch
        {
            0 => Direction.South,
            1 => Direction.West,
            2 => Direction.North,
            _ => Direction.East,
        };
    }

    //IsFaceSturdy whether a face of the state can support attachment, matching the full-face check of vanilla isFaceSturdy
    public static bool IsFaceSturdy(BlockState state, Direction direction)
        => state.Owner is BlockBehaviour behaviour
            && behaviour.IsFaceSturdy(EmptyBlockGetter.Instance, BlockPos.Zero, state, direction, SupportType.Full);

    //IsOverSolidGround whether the cell below is a full solid block
    public static bool IsOverSolidGround(WorldGenRegion level, BlockPos pos)
        => IsFaceSturdy(Get(level, pos.Offset(Direction.Down)), Direction.Up);

    //ValidTreePos whether a tree can grow at this position, maps to vanilla TreeFeature.validTreePos
    public static bool ValidTreePos(WorldGenRegion level, BlockPos pos)
    {
        var state = Get(level, pos);
        return state.Owner.IsAir || InTag(state, ReplaceableByTreesTag);
    }

    //Shuffle in-place shuffle: from the end, swap each with a random earlier position, maps to vanilla Util.shuffle
    public static void Shuffle<T>(IList<T> list, RandomSource random)
    {
        for (var i = list.Count; i > 1; i--)
        {
            var swapTo = random.NextInt(i);
            (list[i - 1], list[swapTo]) = (list[swapTo], list[i - 1]);
        }
    }

    //ShuffledCopy shuffled copy, maps to vanilla Util.shuffledCopy
    public static List<T> ShuffledCopy<T>(IReadOnlyList<T> source, RandomSource random)
    {
        var list = new List<T>(source);
        Shuffle(list, random);
        return list;
    }

    //MinY lowest placeable Y, maps to vanilla getMinY
    public static int MinY(this WorldGenRegion level) => level.MinSectionY * 16;

    //MaxY exclusive upper bound of the highest placeable Y, maps to vanilla getMaxY
    public static int MaxY(this WorldGenRegion level) => (level.MaxSectionY + 1) * 16;

    //SeaLevel generator sea level, maps to vanilla chunkGenerator.getSeaLevel; only noise generators have a sea level
    public static int SeaLevel(ChunkGenerator generator)
        => generator is NoiseBasedChunkGenerator noise ? noise.Settings.SeaLevel : 63;
}

//IgnoredJsonValue JSON value parsed then discarded; used as a placeholder for fields like decorators that are not implemented at this stage
public sealed class IgnoredJsonValue
{
    public static readonly IgnoredJsonValue Instance = new();

    private IgnoredJsonValue() { }
}

//IgnoredListCodec list-form ignored codec; only validates it is an array and discards the contents
internal sealed class IgnoredListCodec : ScalarCodec<IReadOnlyList<IgnoredJsonValue>>
{
    public static readonly IgnoredListCodec Instance = new();

    public override DataResult<IReadOnlyList<IgnoredJsonValue>> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetStream(input).Map(stream =>
            (IReadOnlyList<IgnoredJsonValue>)stream.Select(_ => IgnoredJsonValue.Instance).ToList());

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IReadOnlyList<IgnoredJsonValue> value)
        => DataResult<U>.Success(ops.CreateList(Array.Empty<U>()));
}

//FallenTreeConfiguration fallen tree configuration, maps to vanilla FallenTreeConfiguration
//stump_decorators and log_decorators are tree decorators; there is no decorator system at this stage, so the fields are parsed but not executed
public sealed class FallenTreeConfiguration : FeatureConfiguration
{
    public static readonly Codec<FallenTreeConfiguration> Codec =
        RecordCodecBuilder.Of4<FallenTreeConfiguration, BlockStateProvider, IntProvider,
            IReadOnlyList<IgnoredJsonValue>, IReadOnlyList<IgnoredJsonValue>>(
            BlockStateProvider.Codec.FieldOf("trunk_provider")
                .ForGetter<FallenTreeConfiguration, BlockStateProvider>(c => c.TrunkProvider),
            IntProviders.Codec.FieldOf("log_length")
                .ForGetter<FallenTreeConfiguration, IntProvider>(c => c.LogLength),
            IgnoredListCodec.Instance.FieldOf("stump_decorators")
                .ForGetter<FallenTreeConfiguration, IReadOnlyList<IgnoredJsonValue>>(c => c.StumpDecorators),
            IgnoredListCodec.Instance.FieldOf("log_decorators")
                .ForGetter<FallenTreeConfiguration, IReadOnlyList<IgnoredJsonValue>>(c => c.LogDecorators),
            (trunkProvider, logLength, stumpDecorators, logDecorators) =>
                new FallenTreeConfiguration(trunkProvider, logLength, stumpDecorators, logDecorators));

    public BlockStateProvider TrunkProvider { get; }
    public IntProvider LogLength { get; }
    public IReadOnlyList<IgnoredJsonValue> StumpDecorators { get; }
    public IReadOnlyList<IgnoredJsonValue> LogDecorators { get; }

    public FallenTreeConfiguration(BlockStateProvider trunkProvider, IntProvider logLength,
        IReadOnlyList<IgnoredJsonValue> stumpDecorators, IReadOnlyList<IgnoredJsonValue> logDecorators)
    {
        TrunkProvider = trunkProvider;
        LogLength = logLength;
        StumpDecorators = stumpDecorators;
        LogDecorators = logDecorators;
    }
}

//FallenTreeFeature fallen tree feature, maps to vanilla FallenTreeFeature
//Raises a stump then lays a log along a random horizontal direction; the whole log is abandoned when it cannot fit
public sealed class FallenTreeFeature : Feature<FallenTreeConfiguration>
{
    private const string FeatureId = "fallen_tree";

    public static readonly FallenTreeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new FallenTreeFeature());

    private FallenTreeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), FallenTreeConfiguration.Codec) { }

    protected override bool Place(FallenTreeConfiguration config, FeaturePlaceContext context)
    {
        PlaceFallenTree(config, context.Level, context.Random, context.Origin);
        return true;
    }

    //PlaceFallenTree raise the stump, pick the facing and lay the log, matching the call order of vanilla placeFallenTree
    private static void PlaceFallenTree(FallenTreeConfiguration config, WorldGenRegion level,
        RandomSource random, BlockPos origin)
    {
        PlaceLogBlock(config, level, random, origin, null);
        var direction = VegetationSupport.HorizontalPlane[random.NextInt(4)];
        var logLength = config.LogLength.Sample(random) - 2;
        var logStartPos = origin.Relative(direction, 2 + random.NextInt(2));
        SetGroundHeightForFallenLogStartPos(level, ref logStartPos);
        if (CanPlaceEntireFallenLog(level, logLength, ref logStartPos, direction))
            PlaceFallenLog(config, level, random, logLength, ref logStartPos, direction);
    }

    //SetGroundHeightForFallenLogStartPos lift the start one cell then search down for a placeable footing, maps to the vanilla method of the same name
    private static void SetGroundHeightForFallenLogStartPos(WorldGenRegion level, ref BlockPos logStartPos)
    {
        logStartPos = logStartPos.Offset(Direction.Up);
        for (var i = 0; i < 6 && !MayPlaceOn(level, logStartPos); i++)
            logStartPos = logStartPos.Offset(Direction.Down);
    }

    //MayPlaceOn the position allows trees and the cell below is solid, maps to vanilla mayPlaceOn
    private static bool MayPlaceOn(WorldGenRegion level, BlockPos pos)
        => VegetationSupport.ValidTreePos(level, pos) && VegetationSupport.IsOverSolidGround(level, pos);

    //CanPlaceEntireFallenLog the whole log must fit; abandon when it floats more than two blocks, maps to the vanilla method of the same name
    private static bool CanPlaceEntireFallenLog(WorldGenRegion level, int logLength, ref BlockPos logStartPos,
        Direction direction)
    {
        var gapInGround = 0;
        for (var i = 0; i < logLength; i++)
        {
            if (!VegetationSupport.ValidTreePos(level, logStartPos)) return false;
            if (!VegetationSupport.IsOverSolidGround(level, logStartPos))
            {
                gapInGround++;
                if (gapInGround > 2) return false;
            }
            else gapInGround = 0;
            logStartPos = logStartPos.Offset(direction);
        }
        logStartPos = logStartPos.Relative(direction.Opposite, logLength);
        return true;
    }

    //PlaceFallenLog lay the fallen log cell by cell, resetting sideways logs by axis, maps to vanilla placeFallenLog
    private static void PlaceFallenLog(FallenTreeConfiguration config, WorldGenRegion level,
        RandomSource random, int logLength, ref BlockPos logStartPos, Direction direction)
    {
        for (var i = 0; i < logLength; i++)
        {
            PlaceLogBlock(config, level, random, logStartPos, direction);
            logStartPos = logStartPos.Offset(direction);
        }
    }

    //PlaceLogBlock place one log block; for sideways logs set the axis property to horizontal, maps to vanilla placeLogBlock
    //Vanilla also marks the cell above for post-processing; there is no such mechanism here
    private static BlockPos PlaceLogBlock(FallenTreeConfiguration config, WorldGenRegion level,
        RandomSource random, BlockPos blockPos, Direction? sidewaysDirection)
    {
        var state = config.TrunkProvider.GetState(level, random, blockPos);
        if (sidewaysDirection is { } direction)
            state = VegetationSupport.WithProperty(state, "axis", VegetationSupport.AxisName(direction));
        VegetationSupport.Set(level, blockPos, state);
        return blockPos;
    }
}

//VinesFeature vines feature, maps to vanilla VinesFeature
//Hangs vines only on air cells toward an attachable neighbor; the top-to-bottom priority follows the vanilla Direction.values order
public sealed class VinesFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "vines";

    public static readonly VinesFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new VinesFeature());

    private VinesFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        if (!VegetationSupport.IsAir(level, origin)) return false;
        foreach (var direction in Direction.Values)
        {
            if (direction == Direction.Down) continue;
            if (!CanAttachTo(level, origin.Offset(direction), direction)) continue;
            VegetationSupport.Set(level, origin,
                VegetationSupport.WithProperty(VegetationSupport.StateOf("vine"),
                    VegetationSupport.FaceName(direction), true));
            return true;
        }
        return false;
    }

    //CanAttachTo whether the neighbor's face toward this cell is fully solid, maps to vanilla MultifaceBlock.canAttachTo
    private static bool CanAttachTo(WorldGenRegion level, BlockPos neighbourPos, Direction direction)
    {
        var state = VegetationSupport.Get(level, neighbourPos);
        if (state.Owner is not BlockBehaviour behaviour) return false;
        var support = behaviour.GetBlockSupportShape(state, EmptyBlockGetter.Instance, neighbourPos);
        if (RegBlock.IsFaceFull(support, direction.Opposite)) return true;
        var collision = behaviour.GetCollisionShape(state, EmptyBlockGetter.Instance, neighbourPos,
            CollisionContext.Empty);
        return RegBlock.IsFaceFull(collision, direction.Opposite);
    }
}

//BambooFeature bamboo feature, maps to vanilla BambooFeature
//Rolls a chance to turn the surrounding ground into podzol, then raises a five to sixteen segment bamboo and adds leaves by height
public sealed class BambooFeature : Feature<ProbabilityFeatureConfiguration>
{
    private const string FeatureId = "bamboo";

    public static readonly BambooFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new BambooFeature());

    private BambooFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), ProbabilityFeatureConfiguration.Codec) { }

    //BambooTrunk bamboo body with age=1, leaves=none and stage=0, maps to vanilla BAMBOO_TRUNK
    private static BlockState BambooTrunk => VegetationSupport.WithProperty(
        VegetationSupport.WithProperty(
            VegetationSupport.WithProperty(VegetationSupport.StateOf("bamboo"), "age", 1), "leaves", "none"),
        "stage", 0);

    //BambooFinalLarge the top segment with the largest leaves and stage=1, maps to vanilla BAMBOO_FINAL_LARGE
    private static BlockState BambooFinalLarge
        => VegetationSupport.WithProperty(VegetationSupport.WithProperty(BambooTrunk, "leaves", "large"), "stage", 1);

    //BambooTopLarge the segment above with large leaves, maps to vanilla BAMBOO_TOP_LARGE
    private static BlockState BambooTopLarge
        => VegetationSupport.WithProperty(BambooTrunk, "leaves", "large");

    //BambooTopSmall the segment above that with small leaves, maps to vanilla BAMBOO_TOP_SMALL
    private static BlockState BambooTopSmall
        => VegetationSupport.WithProperty(BambooTrunk, "leaves", "small");

    protected override bool Place(ProbabilityFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        var placed = 0;
        var bambooPos = origin;
        if (VegetationSupport.IsAir(level, bambooPos))
        {
            if (CanBambooSurvive(level, bambooPos))
            {
                var height = random.NextInt(12) + 5;
                if (random.NextFloat() < config.Probability)
                {
                    var r = random.NextInt(4) + 1;
                    for (var xx = origin.X - r; xx <= origin.X + r; xx++)
                    {
                        for (var zz = origin.Z - r; zz <= origin.Z + r; zz++)
                        {
                            var xd = xx - origin.X;
                            var zd = zz - origin.Z;
                            if (xd * xd + zd * zd > r * r) continue;
                            var podzolPos = new BlockPos(xx,
                                level.GetHeight(Heightmap.Types.WorldSurface, xx, zz) - 1, zz);
                            var podzolState = VegetationSupport.Get(level, podzolPos);
                            if (VegetationSupport.InTag(podzolState, VegetationSupport.BeneathBambooPodzolReplaceableTag))
                                VegetationSupport.Set(level, podzolPos, VegetationSupport.StateOf("podzol"));
                        }
                    }
                }
                for (var i = 0; i < height && VegetationSupport.IsAir(level, bambooPos); i++)
                {
                    VegetationSupport.Set(level, bambooPos, BambooTrunk);
                    bambooPos = bambooPos.Offset(Direction.Up);
                }
                if (bambooPos.Y - origin.Y >= 3)
                {
                    VegetationSupport.Set(level, bambooPos, BambooFinalLarge);
                    bambooPos = bambooPos.Offset(Direction.Down);
                    VegetationSupport.Set(level, bambooPos, BambooTopLarge);
                    bambooPos = bambooPos.Offset(Direction.Down);
                    VegetationSupport.Set(level, bambooPos, BambooTopSmall);
                }
            }
            placed = 1;
        }
        return placed > 0;
    }

    //CanBambooSurvive the cell below must support bamboo, maps to vanilla BambooStalkBlock.canSurvive
    private static bool CanBambooSurvive(WorldGenRegion level, BlockPos pos)
        => VegetationSupport.InTag(VegetationSupport.Get(level, pos.Offset(Direction.Down)),
            VegetationSupport.SupportsBambooTag);
}

//HugeMushroomFeatureConfiguration huge mushroom configuration, maps to vanilla HugeMushroomFeatureConfiguration
public sealed class HugeMushroomFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<HugeMushroomFeatureConfiguration> Codec =
        RecordCodecBuilder.Of4<HugeMushroomFeatureConfiguration, BlockStateProvider, BlockStateProvider, int,
            BlockPredicate>(
            BlockStateProvider.Codec.FieldOf("cap_provider")
                .ForGetter<HugeMushroomFeatureConfiguration, BlockStateProvider>(c => c.CapProvider),
            BlockStateProvider.Codec.FieldOf("stem_provider")
                .ForGetter<HugeMushroomFeatureConfiguration, BlockStateProvider>(c => c.StemProvider),
            Codecs.Int.OptionalFieldOf("foliage_radius", 2)
                .ForGetter<HugeMushroomFeatureConfiguration, int>(c => c.FoliageRadius),
            BlockPredicate.Codec.FieldOf("can_place_on")
                .ForGetter<HugeMushroomFeatureConfiguration, BlockPredicate>(c => c.CanPlaceOn),
            (capProvider, stemProvider, foliageRadius, canPlaceOn) =>
                new HugeMushroomFeatureConfiguration(capProvider, stemProvider, foliageRadius, canPlaceOn));

    public BlockStateProvider CapProvider { get; }
    public BlockStateProvider StemProvider { get; }
    public int FoliageRadius { get; }
    public BlockPredicate CanPlaceOn { get; }

    public HugeMushroomFeatureConfiguration(BlockStateProvider capProvider, BlockStateProvider stemProvider,
        int foliageRadius, BlockPredicate canPlaceOn)
    {
        CapProvider = capProvider;
        StemProvider = stemProvider;
        FoliageRadius = foliageRadius;
        CanPlaceOn = canPlaceOn;
    }
}

//AbstractHugeMushroomFeature huge mushroom abstract base, maps to vanilla AbstractHugeMushroomFeature
//Height four to six, doubled with one-in-twelve chance; lays the cap first then raises the stem
public abstract class AbstractHugeMushroomFeature : Feature<HugeMushroomFeatureConfiguration>
{
    //MinMushroomHeight minimum mushroom height
    public const int MinMushroomHeight = 4;

    protected AbstractHugeMushroomFeature(Identifier id, Codec<HugeMushroomFeatureConfiguration> codec)
        : base(id, codec) { }

    //GetTreeRadiusForHeight cap radius at a given layer, maps to vanilla getTreeRadiusForHeight
    protected abstract int GetTreeRadiusForHeight(int trunkHeight, int treeHeight, int leafRadius, int yo);

    //MakeCap lay the cap, maps to vanilla makeCap
    protected abstract void MakeCap(WorldGenRegion level, RandomSource random, BlockPos origin, int treeHeight,
        HugeMushroomFeatureConfiguration config);

    protected override bool Place(HugeMushroomFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        var treeHeight = GetTreeHeight(random);
        if (!IsValidPosition(level, origin, treeHeight, config)) return false;
        MakeCap(level, random, origin, treeHeight, config);
        PlaceTrunk(level, random, origin, config, treeHeight);
        return true;
    }

    //GetTreeHeight mushroom height, maps to vanilla getTreeHeight; the two random draws must not be merged
    protected static int GetTreeHeight(RandomSource random)
    {
        var treeHeight = random.NextInt(3) + 4;
        if (random.NextInt(12) == 0) treeHeight *= 2;
        return treeHeight;
    }

    //PlaceTrunk raise the stem using the same stem block state throughout, maps to vanilla placeTrunk
    protected void PlaceTrunk(WorldGenRegion level, RandomSource random, BlockPos origin,
        HugeMushroomFeatureConfiguration config, int treeHeight)
    {
        for (var dy = 0; dy < treeHeight; dy++)
        {
            var blockPos = origin.Offset(0, dy, 0);
            PlaceMushroomBlock(level, blockPos, config.StemProvider.GetState(level, random, origin));
        }
    }

    //PlaceMushroomBlock place only on air or cells replaceable by mushrooms, maps to vanilla placeMushroomBlock
    protected static void PlaceMushroomBlock(WorldGenRegion level, BlockPos blockPos, BlockState newState)
    {
        var currentState = VegetationSupport.Get(level, blockPos);
        if (!currentState.Owner.IsAir
            && !VegetationSupport.InTag(currentState, VegetationSupport.ReplaceableByMushroomsTag)) return;
        VegetationSupport.Set(level, blockPos, newState);
    }

    //IsValidPosition requires enough height above and below the origin, a plantable base and no non-air non-leaf blocks within the cap area, maps to vanilla isValidPosition
    protected bool IsValidPosition(WorldGenRegion level, BlockPos origin, int treeHeight,
        HugeMushroomFeatureConfiguration config)
    {
        var y = origin.Y;
        if (y < level.MinY() + 1 || y + treeHeight + 1 > level.MaxY()
            || !config.CanPlaceOn.Test(level, origin.Offset(0, -1, 0))) return false;
        for (var dy = 0; dy <= treeHeight; dy++)
        {
            var radius = GetTreeRadiusForHeight(-1, -1, config.FoliageRadius, dy);
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    var state = VegetationSupport.Get(level, origin.Offset(dx, dy, dz));
                    if (!state.Owner.IsAir && !VegetationSupport.InTag(state, VegetationSupport.LeavesTag))
                        return false;
                }
            }
        }
        return true;
    }
}

//HugeRedMushroomFeature huge red mushroom, maps to vanilla HugeRedMushroomFeature
//The cap is three layers from bottom to top; edge cells get their facing property based on whether they reach a corner
public sealed class HugeRedMushroomFeature : AbstractHugeMushroomFeature
{
    private const string FeatureId = "huge_red_mushroom";

    public static readonly HugeRedMushroomFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new HugeRedMushroomFeature());

    private HugeRedMushroomFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), HugeMushroomFeatureConfiguration.Codec) { }

    protected override void MakeCap(WorldGenRegion level, RandomSource random, BlockPos origin, int treeHeight,
        HugeMushroomFeatureConfiguration config)
    {
        for (var dy = treeHeight - 3; dy <= treeHeight; dy++)
        {
            var radius = dy < treeHeight ? config.FoliageRadius : config.FoliageRadius - 1;
            var center = config.FoliageRadius - 2;
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    var minX = dx == -radius;
                    var maxX = dx == radius;
                    var minZ = dz == -radius;
                    var maxZ = dz == radius;
                    var xEdge = minX || maxX;
                    var zEdge = minZ || maxZ;
                    if (dy < treeHeight && xEdge == zEdge) continue;
                    var state = config.CapProvider.GetState(level, random, origin);
                    state = VegetationSupport.WithProperty(state, "up", dy >= treeHeight - 1);
                    state = VegetationSupport.WithProperty(state, "west", dx < -center);
                    state = VegetationSupport.WithProperty(state, "east", dx > center);
                    state = VegetationSupport.WithProperty(state, "north", dz < -center);
                    state = VegetationSupport.WithProperty(state, "south", dz > center);
                    PlaceMushroomBlock(level, origin.Offset(dx, dy, dz), state);
                }
            }
        }
    }

    protected override int GetTreeRadiusForHeight(int trunkHeight, int treeHeight, int leafRadius, int yo)
        => (yo < treeHeight && yo >= treeHeight - 3) || yo == treeHeight ? leafRadius : 0;
}

//HugeBrownMushroomFeature huge brown mushroom, maps to vanilla HugeBrownMushroomFeature
//The cap is a single layer with no corner blocks; the four direction properties indicate whether that side continues
public sealed class HugeBrownMushroomFeature : AbstractHugeMushroomFeature
{
    private const string FeatureId = "huge_brown_mushroom";

    public static readonly HugeBrownMushroomFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new HugeBrownMushroomFeature());

    private HugeBrownMushroomFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), HugeMushroomFeatureConfiguration.Codec) { }

    protected override void MakeCap(WorldGenRegion level, RandomSource random, BlockPos origin, int treeHeight,
        HugeMushroomFeatureConfiguration config)
    {
        var radius = config.FoliageRadius;
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dz = -radius; dz <= radius; dz++)
            {
                var minX = dx == -radius;
                var maxX = dx == radius;
                var minZ = dz == -radius;
                var maxZ = dz == radius;
                var xEdge = minX || maxX;
                var zEdge = minZ || maxZ;
                if (xEdge && zEdge) continue;
                var west = minX || (zEdge && dx == 1 - radius);
                var east = maxX || (zEdge && dx == radius - 1);
                var north = minZ || (xEdge && dz == 1 - radius);
                var south = maxZ || (xEdge && dz == radius - 1);
                var state = config.CapProvider.GetState(level, random, origin);
                state = VegetationSupport.WithProperty(state, "west", west);
                state = VegetationSupport.WithProperty(state, "east", east);
                state = VegetationSupport.WithProperty(state, "north", north);
                state = VegetationSupport.WithProperty(state, "south", south);
                PlaceMushroomBlock(level, origin.Offset(dx, treeHeight, dz), state);
            }
        }
    }

    protected override int GetTreeRadiusForHeight(int trunkHeight, int treeHeight, int leafRadius, int yo)
        => yo <= 3 ? 0 : leafRadius;
}

//ProbabilityFeatureConfiguration probability configuration, maps to vanilla ProbabilityFeatureConfiguration
//Carries only a probability field, shared by seagrass and bamboo
public sealed class ProbabilityFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<ProbabilityFeatureConfiguration> Codec =
        new SingleFieldMapCodec<ProbabilityFeatureConfiguration, float>(
            Codecs.Float.FieldOf("probability"),
            probability => new ProbabilityFeatureConfiguration(probability),
            config => config.Probability);

    public float Probability { get; }

    public ProbabilityFeatureConfiguration(float probability) => Probability = probability;
}
