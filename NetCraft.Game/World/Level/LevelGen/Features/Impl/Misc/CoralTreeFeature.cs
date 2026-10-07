using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//CoralFeature coral feature base, maps to vanilla CoralFeature
//First picks a material at random from the coral block tag, then hands off to the subclass for the shape; each coral block may add decoration by chance
public abstract class CoralFeature : Feature<NoneFeatureConfiguration>
{
    //CoralBlocksTag the coral block tag supplying shape material, maps to vanilla BlockTags.CORAL_BLOCKS
    private static readonly TagKey<RegBlock> CoralBlocksTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("coral_blocks"));

    //CoralsTag the coral tag for the pieces placed above, maps to vanilla BlockTags.CORALS
    private static readonly TagKey<RegBlock> CoralsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("corals"));

    //WallCoralsTag the wall coral tag for fans attached to underwater sides, maps to vanilla BlockTags.WALL_CORALS
    private static readonly TagKey<RegBlock> WallCoralsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("wall_corals"));

    protected CoralFeature(Identifier id, Codec<NoneFeatureConfiguration> codec) : base(id, codec) { }

    //PlaceFeature each subclass's coral shape, maps to vanilla placeFeature
    protected abstract bool PlaceFeature(WorldGenRegion level, RandomSource random, BlockPos origin, BlockState state);

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var coral = RandomElementOf(CoralBlocksTag, random);
        if (coral is null) return false;
        return PlaceFeature(context.Level, random, context.Origin, coral.DefaultBlockState);
    }

    //PlaceCoralBlock place one coral segment in water; by chance add coral or sea pickle on top, and attach wall fans to adjacent water, maps to vanilla placeCoralBlock
    protected static bool PlaceCoralBlock(WorldGenRegion level, RandomSource random, BlockPos pos, BlockState state)
    {
        var above = pos.Offset(Direction.Up);
        var targetState = VegetationSupport.Get(level, pos);
        if ((!VegetationSupport.IsState(targetState, "water") && !VegetationSupport.InTag(targetState, CoralsTag))
            || !VegetationSupport.IsState(VegetationSupport.Get(level, above), "water")) return false;
        VegetationSupport.Set(level, pos, state);
        if (random.NextFloat() < 0.25f)
        {
            var coral = RandomElementOf(CoralsTag, random);
            if (coral is not null) VegetationSupport.Set(level, above, coral.DefaultBlockState);
        }
        else if (random.NextFloat() < 0.05f)
        {
            VegetationSupport.Set(level, above,
                VegetationSupport.WithProperty(VegetationSupport.StateOf("sea_pickle"), "pickles",
                    random.NextInt(4) + 1));
        }
        foreach (var direction in VegetationSupport.HorizontalPlane)
        {
            if (random.NextFloat() >= 0.2f) continue;
            var sidePos = pos.Offset(direction);
            if (!VegetationSupport.IsState(VegetationSupport.Get(level, sidePos), "water")) continue;
            var wallCoral = RandomElementOf(WallCoralsTag, random);
            if (wallCoral is null) continue;
            var fanState = wallCoral.DefaultBlockState;
            if (VegetationSupport.HasProperty(fanState, "facing"))
                fanState = VegetationSupport.WithProperty(fanState, "facing", VegetationSupport.FaceName(direction));
            VegetationSupport.Set(level, sidePos, fanState);
        }
        return true;
    }

    //RandomElementOf pick a random block from a tag, maps to vanilla Registry.getRandomElementOf
    //When the tag is unbound or empty, return null without consuming random, same as vanilla
    protected static RegBlock? RandomElementOf(TagKey<RegBlock> tag, RandomSource random)
    {
        var set = BuiltInRegistries.BLOCK.Get(tag);
        if (set is null || set.Size == 0) return null;
        return set.Get(random.NextInt(set.Size)).Value;
    }
}

//CoralTreeFeature coral tree feature, maps to vanilla CoralTreeFeature
//Grows one to three trunk segments upward, then branches out in two to four directions at the top
public sealed class CoralTreeFeature : CoralFeature
{
    private const string FeatureId = "coral_tree";

    public static readonly CoralTreeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new CoralTreeFeature());

    private CoralTreeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool PlaceFeature(WorldGenRegion level, RandomSource random, BlockPos origin, BlockState state)
    {
        var pos = origin;
        var trunkHeight = random.NextInt(3) + 1;
        for (var i = 0; i < trunkHeight; i++)
        {
            if (!PlaceCoralBlock(level, random, pos, state)) return true;
            pos = pos.Offset(Direction.Up);
        }
        var trunkTopPos = pos;
        var branchCount = random.NextInt(3) + 2;
        var directions = VegetationSupport.ShuffledCopy(VegetationSupport.HorizontalPlane, random);
        for (var branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            var branchDirection = directions[branchIndex];
            pos = trunkTopPos.Offset(branchDirection);
            var branchHeight = random.NextInt(5) + 2;
            var segmentLength = 0;
            for (var j = 0; j < branchHeight && PlaceCoralBlock(level, random, pos, state); j++)
            {
                segmentLength++;
                pos = pos.Offset(Direction.Up);
                //The branch steps out one block at the start, then may step out again every two blocks by chance, matching vanilla's segment length control
                if (j == 0 || (segmentLength >= 2 && random.NextFloat() < 0.25f))
                {
                    pos = pos.Offset(branchDirection);
                    segmentLength = 0;
                }
            }
        }
        return true;
    }
}

//CoralClawFeature coral claw feature, maps to vanilla CoralClawFeature
//Picks one claw direction, then shuffles and picks two or three from the same or adjacent directions; each branch reaches sideways then turns back to climb toward the claw direction
public sealed class CoralClawFeature : CoralFeature
{
    private const string FeatureId = "coral_claw";

    public static readonly CoralClawFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new CoralClawFeature());

    private CoralClawFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool PlaceFeature(WorldGenRegion level, RandomSource random, BlockPos origin, BlockState state)
    {
        if (!PlaceCoralBlock(level, random, origin, state)) return false;
        var clawDirection = VegetationSupport.HorizontalPlane[random.NextInt(4)];
        var branchCount = random.NextInt(2) + 2;
        var possibleDirections = VegetationSupport.ShuffledCopy(
            new[] { clawDirection, clawDirection.ClockWise, clawDirection.CounterClockWise }, random);
        for (var branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            var branchDirection = possibleDirections[branchIndex];
            var pos = origin;
            var sidewayLength = random.NextInt(2) + 1;
            pos = pos.Offset(branchDirection);
            Direction segmentDirection;
            int inwayLength;
            if (branchDirection == clawDirection)
            {
                segmentDirection = clawDirection;
                inwayLength = random.NextInt(3) + 2;
            }
            else
            {
                pos = pos.Offset(Direction.Up);
                var segmentDirections = new[] { branchDirection, Direction.Up };
                segmentDirection = segmentDirections[random.NextInt(2)];
                inwayLength = random.NextInt(3) + 3;
            }
            for (var i = 0; i < sidewayLength && PlaceCoralBlock(level, random, pos, state); i++)
                pos = pos.Offset(segmentDirection);
            pos = pos.Offset(segmentDirection.Opposite);
            pos = pos.Offset(Direction.Up);
            for (var i = 0; i < inwayLength; i++)
            {
                pos = pos.Offset(clawDirection);
                if (!PlaceCoralBlock(level, random, pos, state)) break;
                if (random.NextFloat() < 0.25f) pos = pos.Offset(Direction.Up);
            }
        }
        return true;
    }
}

//CoralMushroomFeature coral mushroom feature, maps to vanilla CoralMushroomFeature
//Iterates a 3..6 block box; only samples the shell where exactly two coordinates are interior, first carving by chance then placing coral
public sealed class CoralMushroomFeature : CoralFeature
{
    private const string FeatureId = "coral_mushroom";

    public static readonly CoralMushroomFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new CoralMushroomFeature());

    private CoralMushroomFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool PlaceFeature(WorldGenRegion level, RandomSource random, BlockPos origin, BlockState state)
    {
        var height = random.NextInt(3) + 3;
        var width = random.NextInt(3) + 3;
        var length = random.NextInt(3) + 3;
        var sinkValue = random.NextInt(3) + 1;
        for (var x = 0; x <= width; x++)
        {
            for (var y = 0; y <= height; y++)
            {
                for (var z = 0; z <= length; z++)
                {
                    if (!IsFullShape(width, height, length, x, y, z)) continue;
                    if (random.NextFloat() < 0.1f) continue;
                    var pos = origin.Offset(x, y, z).Relative(Direction.Down, sinkValue);
                    PlaceCoralBlock(level, random, pos, state);
                }
            }
        }
        return true;
    }

    //IsFullShape whether the point lies on the mushroom shell; the condition chain matches vanilla's four checks with no simplification
    private static bool IsFullShape(int width, int height, int length, int x, int y, int z)
        => (x != 0 && x != width || y != 0 && y != height)
            && (z != 0 && z != length || y != 0 && y != height)
            && (x != 0 && x != width || z != 0 && z != length)
            && (x == 0 || x == width || y == 0 || y == height || z == 0 || z == length);
}
