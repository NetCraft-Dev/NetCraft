using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePlacement structure placement abstraction, maps to vanilla net.minecraft.world.level.levelgen.structure.placement.StructurePlacement
//Decides which chunks a structure lands in, plus frequency reduction and exclusion against other structure sets
//Subclasses only implement IsPlacementChunk for the grid check; the base class combines the rest
public abstract class StructurePlacement
{
    //HighlyArbitraryRandomSalt legacy salt, maps to vanilla HIGHLY_ARBITRARY_RANDOM_SALT
    //Only LEGACY_TYPE_2 uses it; any other value would shift structure positions across old saves
    public const int HighlyArbitraryRandomSalt = 10387320;

    //LocateOffset locate offset, maps to vanilla locate_offset
    //Only affects the coordinates reported by /locate; not part of chunk judging
    public Vec3i LocateOffset { get; }

    //ReductionMethod frequency reduction algorithm, maps to vanilla frequency_reduction_method
    public FrequencyReductionMethod ReductionMethod { get; }

    //Frequency hit probability; the default 1.0 means no reduction
    public float Frequency { get; }

    //Salt salt value, part of seed derivation
    public int Salt { get; }

    //Exclusion exclusion zone; a structure in another set nearby prevents this one from generating
    //The property cannot share the name with the nested type ExclusionZone, so a short name is used
    public ExclusionZone? Exclusion { get; }

    protected StructurePlacement(Vec3i locateOffset, FrequencyReductionMethod reductionMethod, float frequency,
        int salt, ExclusionZone? exclusionZone)
    {
        LocateOffset = locateOffset;
        ReductionMethod = reductionMethod;
        Frequency = frequency;
        Salt = salt;
        Exclusion = exclusionZone;
    }

    //IsPlacementChunk base grid check, implemented by subclasses
    protected abstract bool IsPlacementChunk(ChunkGeneratorStructureState state, int sourceX, int sourceZ);

    //IsStructureChunk final check, maps to vanilla isStructureChunk
    //Three-way AND: base grid, frequency reduction, exclusion zone; the order cannot be swapped since frequency reduction consumes randoms
    public bool IsStructureChunk(ChunkGeneratorStructureState state, int sourceX, int sourceZ)
        => IsPlacementChunk(state, sourceX, sourceZ)
            && ApplyAdditionalChunkRestrictions(sourceX, sourceZ, state.LevelSeed)
            && ApplyInteractionsWithOtherStructures(state, sourceX, sourceZ);

    //ApplyAdditionalChunkRestrictions frequency reduction, maps to vanilla applyAdditionalChunkRestrictions
    //When Frequency is 1 the whole thing is skipped without consuming a single random; this affects later judge results
    public bool ApplyAdditionalChunkRestrictions(int sourceX, int sourceZ, long levelSeed)
        => Frequency >= 1.0f || ReductionMethod.ShouldGenerate(levelSeed, Salt, sourceX, sourceZ, Frequency);

    //ApplyInteractionsWithOtherStructures exclusion zone check, maps to vanilla applyInteractionsWithOtherStructures
    public bool ApplyInteractionsWithOtherStructures(ChunkGeneratorStructureState state, int sourceX, int sourceZ)
        => Exclusion is null || !Exclusion.IsPlacementForbidden(state, sourceX, sourceZ);

    //GetLocatePos locate position, maps to vanilla getLocatePos
    public BlockPos GetLocatePos(ChunkPos chunkPos)
        => new(chunkPos.X << 4, LocateOffset.Y, chunkPos.Z << 4);

    //ExclusionZone exclusion zone, maps to vanilla StructurePlacement.ExclusionZone
    //OtherSet is another structure set, ChunkCount is the check radius, 1..16
    public sealed record ExclusionZone(Holder<NetCraft.Registry.StructureSet> OtherSet, int ChunkCount)
    {
        //IsPlacementForbidden forbids generation when a structure of the excluded set hits within range
        //Private members are visible to the containing class, so the outer class can call it directly
        internal bool IsPlacementForbidden(ChunkGeneratorStructureState state, int sourceX, int sourceZ)
            => state.HasStructureChunkInRange(OtherSet, sourceX, sourceZ, ChunkCount);
    }
}

//FrequencyReductionMethod frequency reduction algorithm, maps to vanilla StructurePlacement.FrequencyReductionMethod
//The four algorithms differ in random source and comparison precision; the values must match verbatim or structure distribution diverges from vanilla
public enum FrequencyReductionMethod
{
    Default,
    LegacyType1,
    LegacyType2,
    LegacyType3,
}

//FrequencyReductionMethods reduction algorithm implementation and name mapping
public static class FrequencyReductionMethods
{
    //ShouldGenerate whether the chunk passes frequency reduction, maps to the vanilla reducers
    //All four create a fresh LegacyRandomSource(0) then replay the seed, keeping them independent of call order
    public static bool ShouldGenerate(this FrequencyReductionMethod method, long seed, int salt,
        int sourceX, int sourceZ, float probability)
    {
        var random = new LegacyRandomSource(0L);
        switch (method)
        {
            case FrequencyReductionMethod.LegacyType1:
                //Used by outposts; XORs the chunk coords and hits one slot out of the reciprocal, so the probability means 1 in 1/probability
                var chunkX = sourceX >> 4;
                var chunkZ = sourceZ >> 4;
                random.SetSeed((chunkX ^ (chunkZ << 4)) ^ seed);
                random.NextInt();
                return random.NextInt((int)(1.0f / probability)) == 0;
            case FrequencyReductionMethod.LegacyType2:
                //With the legacy salt; comparison uses float
                WorldgenRandom.SetLargeFeatureWithSalt(random, seed, sourceX, sourceZ,
                    StructurePlacement.HighlyArbitraryRandomSalt);
                return random.NextFloat() < probability;
            case FrequencyReductionMethod.LegacyType3:
                //Derived from the chunk coords; comparison uses double, more precise than float
                WorldgenRandom.SetLargeFeatureSeed(random, seed, sourceX, sourceZ);
                return random.NextDouble() < probability;
            default:
                //With the regular salt; comparison uses float
                WorldgenRandom.SetLargeFeatureWithSalt(random, seed, salt, sourceX, sourceZ);
                return random.NextFloat() < probability;
        }
    }

    //Name returns the JSON name, maps to vanilla getSerializedName
    public static string Name(this FrequencyReductionMethod method) => method switch
    {
        FrequencyReductionMethod.LegacyType1 => "legacy_type_1",
        FrequencyReductionMethod.LegacyType2 => "legacy_type_2",
        FrequencyReductionMethod.LegacyType3 => "legacy_type_3",
        _ => "default",
    };

    //TryParse parses by JSON name, returns null on an invalid name
    public static FrequencyReductionMethod? TryParse(string name) => name switch
    {
        "default" => FrequencyReductionMethod.Default,
        "legacy_type_1" => FrequencyReductionMethod.LegacyType1,
        "legacy_type_2" => FrequencyReductionMethod.LegacyType2,
        "legacy_type_3" => FrequencyReductionMethod.LegacyType3,
        _ => null,
    };
}
