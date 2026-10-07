namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePoolBootstrap template pool subsystem type registration, maps to the static registration of vanilla StructurePoolElementType
//Each type registers idempotently through its static Instance; touching them here once ensures registration completes before the registry freezes
public static class StructurePoolBootstrap
{
    public static void RegisterAll()
    {
        _ = SinglePoolElementType.Instance;
        _ = LegacySinglePoolElementType.Instance;
        _ = ListPoolElementType.Instance;
        _ = FeaturePoolElementType.Instance;
        _ = EmptyPoolElementType.Instance;
        //Pool alias types must be in place before jigsaw structure json loads, or the pool_aliases type dispatch cannot find its target
        PoolAliasBindings.RegisterAll();
        //Registers the jigsaw structure type into STRUCTURE_TYPE so worldgen/structure can dispatch by type
        _ = JigsawStructure.JigsawType;
    }
}
