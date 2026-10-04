namespace NetCraft.Game.World.Level.LevelGen.Placement;

//PlacementModifierBootstrap 放置修饰器注册入口
//注册表要先于数据加载填好 否则 placed_feature 的 type 字段整批解不出来
public static class PlacementModifierBootstrap
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;
        //方块谓词类型是放置修饰器的内嵌依赖 block_predicate_filter 会带一整个谓词 缺一个修饰器就整条解不出来
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
