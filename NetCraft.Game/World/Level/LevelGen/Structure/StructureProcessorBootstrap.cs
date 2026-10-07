namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureProcessorBootstrap processor subsystem type registration, maps to vanilla StructureProcessorTypes and the three rule-related registries
//Each type registers idempotently through its static field; touching them here once ensures registration completes before the registry freezes
public static class StructureProcessorBootstrap
{
    public static void RegisterAll()
    {
        //Register processor types first; their codecs reference the decode paths of rule tests and block entity modifiers
        StructureProcessorTypes.RegisterAll();
        //Touching any single static field of the rule test, position test and block entity modifier tables completes all three registrations
        _ = RuleTestTypes.AlwaysTrue;
        _ = PosRuleTestTypes.AlwaysTrue;
        _ = RuleBlockEntityModifierTypes.Passthrough;
    }
}
