namespace NetCraft.Game.World.Level.LevelGen.Placement;

//PlacementModifierBootstrap placement modifier registration entry
//The registry must be populated before data loading, otherwise the type field of placed_feature cannot be decoded at all
public static class PlacementModifierBootstrap
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;
        //Block predicate types are an embedded dependency of placement modifiers; block_predicate_filter carries a whole predicate, and one missing modifier breaks the entire decode
        NetCraft.Game.World.Level.LevelGen.BlockPredicates.BlockPredicateType.RegisterAll();
        _ = BlockPredicateFilterType.Instance;
        _ = RarityFilterType.Instance;
        _ = SurfaceRelativeThresholdFilterType.Instance;
        _ = SurfaceWaterDepthFilterType.Instance;
        _ = BiomeFilterType.Instance;
        _ = CountPlacementType.Instance;
        _ = NoiseBasedCountPlacementType.Instance;
        _ = NoiseThresholdCountPlacementType.Instance;
        _ = CountOnEveryLayerPlacementType.Instance;
        _ = EnvironmentScanPlacementType.Instance;
        _ = HeightmapPlacementType.Instance;
        _ = HeightRangePlacementType.Instance;
        _ = InSquarePlacementType.Instance;
        _ = RandomOffsetPlacementType.Instance;
        _ = FixedPlacementType.Instance;
    }
}
