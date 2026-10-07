using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//ChunkGeneratorStructureState structure generation state, maps to vanilla net.minecraft.world.level.chunk.ChunkGeneratorStructureState
//Holds the world seed and structure set list for placement judges and exclusion-zone queries
//Keeps only what the judge needs; concentric-ring preset positions will be added when strongholds are wired up
public sealed class ChunkGeneratorStructureState
{
    //LevelSeed world seed; all placement judges derive from it
    public long LevelSeed { get; }

    public ChunkGeneratorStructureState(long levelSeed) => LevelSeed = levelSeed;

    //HasStructureChunkInRange whether the target set has a placement point within range, maps to vanilla hasStructureChunkInRange
    //Asks the target set's placement chunk by chunk; the sole basis for exclusion-zone checks
    public bool HasStructureChunkInRange(Holder<NetCraft.Registry.StructureSet> structureSet,
        int sourceX, int sourceZ, int range)
    {
        if (!structureSet.IsBound() || structureSet.Value is not StructureSet set) return false;
        var placement = set.Placement;
        for (var testX = sourceX - range; testX <= sourceX + range; testX++)
        {
            for (var testZ = sourceZ - range; testZ <= sourceZ + range; testZ++)
            {
                if (placement.IsStructureChunk(this, testX, testZ)) return true;
            }
        }
        return false;
    }
}
