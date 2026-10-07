using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//RandomSpreadType distribution of the random offset inside a grid cell, maps to vanilla RandomSpreadType
//Linear is uniform; triangular biases toward the cell center; villages use linear while strongholds use concentric rings via another path
public enum RandomSpreadType
{
    Linear,
    Triangular,
}

//RandomSpreadTypes distribution implementation and name mapping
public static class RandomSpreadTypes
{
    //Evaluate returns the offset inside the cell, maps to vanilla evaluate
    //Triangular averages two draws; consuming two randoms affects the subsequent spreadZ value
    public static int Evaluate(this RandomSpreadType type, RandomSource random, int limit)
        => type == RandomSpreadType.Triangular
            ? (random.NextInt(limit) + random.NextInt(limit)) / 2
            : random.NextInt(limit);

    //Name returns the JSON name
    public static string Name(this RandomSpreadType type)
        => type == RandomSpreadType.Triangular ? "triangular" : "linear";

    //TryParse parses by JSON name, returns null when invalid
    public static RandomSpreadType? TryParse(string name) => name switch
    {
        "linear" => RandomSpreadType.Linear,
        "triangular" => RandomSpreadType.Triangular,
        _ => null,
    };
}

//RandomSpreadStructurePlacement random spread placement, maps to vanilla RandomSpreadStructurePlacement
//Divides into a grid by spacing; each cell picks a chunk from the salted seed, and a hit is that cell's placement point
public sealed class RandomSpreadStructurePlacement : StructurePlacement
{
    public int Spacing { get; }
    public int Separation { get; }

    //SpreadType offset distribution inside a cell
    public RandomSpreadType SpreadType { get; }

    public RandomSpreadStructurePlacement(Vec3i locateOffset, FrequencyReductionMethod reductionMethod,
        float frequency, int salt, ExclusionZone? exclusionZone, int spacing, int separation,
        RandomSpreadType spreadType = RandomSpreadType.Linear)
        : base(locateOffset, reductionMethod, frequency, salt, exclusionZone)
    {
        Spacing = spacing;
        Separation = separation;
        SpreadType = spreadType;
    }

    //Convenience constructor used when only grid params and salt are needed, maps to the slimmed vanilla constructor
    public RandomSpreadStructurePlacement(int spacing, int separation, int salt,
        RandomSpreadType spreadType = RandomSpreadType.Linear)
        : this(Vec3i.Zero, FrequencyReductionMethod.Default, 1.0f, salt, null, spacing, separation, spreadType) { }

    //GetPotentialStructureChunk computes a cell's placement chunk, maps to vanilla getPotentialStructureChunk
    //Grid coordinates use floorDiv, the offset is derived from the salted seed, and X then Z consume the same random source consecutively
    public ChunkPos GetPotentialStructureChunk(long seed, int sourceX, int sourceZ)
    {
        var gridX = FloorDiv(sourceX, Spacing);
        var gridZ = FloorDiv(sourceZ, Spacing);
        var random = new LegacyRandomSource(0L);
        WorldgenRandom.SetLargeFeatureWithSalt(random, seed, gridX, gridZ, Salt);
        var limit = Spacing - Separation;
        var spreadX = SpreadType.Evaluate(random, limit);
        var spreadZ = SpreadType.Evaluate(random, limit);
        return new ChunkPos(gridX * Spacing + spreadX, gridZ * Spacing + spreadZ);
    }

    //IsPlacementChunk whether the chunk is exactly this cell's placement point, maps to vanilla isPlacementChunk
    protected override bool IsPlacementChunk(ChunkGeneratorStructureState state, int sourceX, int sourceZ)
    {
        var potential = GetPotentialStructureChunk(state.LevelSeed, sourceX, sourceZ);
        return potential.X == sourceX && potential.Z == sourceZ;
    }

    //FloorDiv Java-semantics floor division; wrong negative grid coordinates would shift the whole structure position
    private static int FloorDiv(int a, int b)
    {
        var r = a / b;
        return (a ^ b) < 0 && r * b != a ? r - 1 : r;
    }
}
