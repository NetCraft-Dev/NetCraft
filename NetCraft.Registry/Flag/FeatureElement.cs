namespace NetCraft.Registry.Flag;

//Element with feature flags, maps to vanilla FeatureElement
//Blocks, items and entity types implement it to declare which flags they need
public interface FeatureElement
{
    //Registries filtered by feature flags
    //Vanilla uses a Set with wildcards; C# has no covariant wildcards so object is stored, compared by registry name
    static readonly object[] FILTERED_REGISTRIES =
    [
        Registries.ITEM,
        Registries.BLOCK,
        Registries.ENTITY_TYPE,
        Registries.GAME_RULE,
        Registries.MENU,
        Registries.POTION,
        Registries.MOB_EFFECT,
    ];

    FeatureFlagSet RequiredFeatures();

    //Enabled only when all required flags are on
    bool IsEnabled(FeatureFlagSet enabledFeatures) => RequiredFeatures().IsSubsetOf(enabledFeatures);
}
