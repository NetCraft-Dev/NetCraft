using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePlacementRegistry structure set registry, maps to the List<StructureSet> held by vanilla ChunkGenerator
//All structure sets used by the world live here; chunk assembly filters the hit sets through it
public sealed class StructurePlacementRegistry
{
    private readonly List<NetCraft.Registry.StructureSet> _sets = new();

    public long Seed { get; }

    //State placement judge state, holds the world seed for placements to query exclusion zones
    public ChunkGeneratorStructureState State { get; }

    public IReadOnlyList<NetCraft.Registry.StructureSet> Sets => _sets;

    public StructurePlacementRegistry(long seed)
    {
        Seed = seed;
        State = new ChunkGeneratorStructureState(seed);
    }

    //AddSet appends a set
    public void AddSet(NetCraft.Registry.StructureSet set) => _sets.Add(set);

    //GetSetsForChunk returns the sets hitting the chunk, maps to the set filtering in vanilla createStructures
    //Exclusion-zone checks look back at other sets in this registry, so all checks must share the same state
    public List<StructureSet> GetSetsForChunk(ChunkPos pos)
    {
        var result = new List<StructureSet>();
        foreach (var set in _sets)
        {
            if (set is StructureSet gameSet && gameSet.Placement.IsStructureChunk(State, pos.X, pos.Z))
                result.Add(gameSet);
        }
        return result;
    }

    //Empty empty registry, generates no structures
    public static StructurePlacementRegistry Empty { get; } = new(0L);
}
