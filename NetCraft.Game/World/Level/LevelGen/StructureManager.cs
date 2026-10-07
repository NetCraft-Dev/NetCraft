using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.LevelGen;

//StructureManager structure manager adapter, maps to vanilla net.minecraft.world.level.StructureManager
//Phase E upgraded it to a thin wrapper over StructureFeatureManager exposing the StructureManager API
//Internally delegates to StructureFeatureManager for the real structure query logic
public sealed class StructureManager
{
    private readonly StructureFeatureManager _featureManager;

    public StructureManager(StructureFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    //Default default empty StructureManager for scenarios without structures
    public static StructureManager Default => new(new StructureFeatureManager());

    public bool HasStructureReferences(ChunkPos pos)
        => _featureManager.HasStructureReferences(pos);

    public bool HasStructureStartsForChunk(ChunkAccess chunk)
        => _featureManager.HasStructureStartsForChunk(chunk);

    public IReadOnlyCollection<StructureStart> GetStructureStarts(ChunkPos pos)
        => _featureManager.GetStructureStarts(pos);

    public void AddStructureStart(ChunkPos pos, StructureStart start)
        => _featureManager.AddStructureStart(pos, start);

    public void AddStructureReference(ChunkPos pos, StructureReference reference)
        => _featureManager.AddStructureReference(pos, reference);
}
