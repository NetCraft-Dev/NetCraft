namespace NetCraft.Registry;

//Biomes 内置生物群系键常量对应原版 net.minecraft.world.level.biome.Biomes
//覆盖 data/minecraft/worldgen/biome 下全部 66 个原版群系 顺序与文件名一致
public static class Biomes
{
    public static readonly ResourceKey<Biome> BADLANDS = Create("badlands");
    public static readonly ResourceKey<Biome> BAMBOO_JUNGLE = Create("bamboo_jungle");
    public static readonly ResourceKey<Biome> BASALT_DELTAS = Create("basalt_deltas");
    public static readonly ResourceKey<Biome> BEACH = Create("beach");
    public static readonly ResourceKey<Biome> BIRCH_FOREST = Create("birch_forest");
    public static readonly ResourceKey<Biome> CHERRY_GROVE = Create("cherry_grove");
    public static readonly ResourceKey<Biome> COLD_OCEAN = Create("cold_ocean");
    public static readonly ResourceKey<Biome> CRIMSON_FOREST = Create("crimson_forest");
    public static readonly ResourceKey<Biome> DARK_FOREST = Create("dark_forest");
    public static readonly ResourceKey<Biome> DEEP_COLD_OCEAN = Create("deep_cold_ocean");
    public static readonly ResourceKey<Biome> DEEP_DARK = Create("deep_dark");
    public static readonly ResourceKey<Biome> DEEP_FROZEN_OCEAN = Create("deep_frozen_ocean");
    public static readonly ResourceKey<Biome> DEEP_LUKEWARM_OCEAN = Create("deep_lukewarm_ocean");
    public static readonly ResourceKey<Biome> DEEP_OCEAN = Create("deep_ocean");
    public static readonly ResourceKey<Biome> DESERT = Create("desert");
    public static readonly ResourceKey<Biome> DRIPSTONE_CAVES = Create("dripstone_caves");
    public static readonly ResourceKey<Biome> END_BARRENS = Create("end_barrens");
    public static readonly ResourceKey<Biome> END_HIGHLANDS = Create("end_highlands");
    public static readonly ResourceKey<Biome> END_MIDLANDS = Create("end_midlands");
    public static readonly ResourceKey<Biome> ERODED_BADLANDS = Create("eroded_badlands");
    public static readonly ResourceKey<Biome> FLOWER_FOREST = Create("flower_forest");
    public static readonly ResourceKey<Biome> FOREST = Create("forest");
    public static readonly ResourceKey<Biome> FROZEN_OCEAN = Create("frozen_ocean");
    public static readonly ResourceKey<Biome> FROZEN_PEAKS = Create("frozen_peaks");
    public static readonly ResourceKey<Biome> FROZEN_RIVER = Create("frozen_river");
    public static readonly ResourceKey<Biome> GROVE = Create("grove");
    public static readonly ResourceKey<Biome> ICE_SPIKES = Create("ice_spikes");
    public static readonly ResourceKey<Biome> JAGGED_PEAKS = Create("jagged_peaks");
    public static readonly ResourceKey<Biome> JUNGLE = Create("jungle");
    public static readonly ResourceKey<Biome> LUKEWARM_OCEAN = Create("lukewarm_ocean");
    public static readonly ResourceKey<Biome> LUSH_CAVES = Create("lush_caves");
    public static readonly ResourceKey<Biome> MANGROVE_SWAMP = Create("mangrove_swamp");
    public static readonly ResourceKey<Biome> MEADOW = Create("meadow");
    public static readonly ResourceKey<Biome> MUSHROOM_FIELDS = Create("mushroom_fields");
    public static readonly ResourceKey<Biome> NETHER_WASTES = Create("nether_wastes");
    public static readonly ResourceKey<Biome> OCEAN = Create("ocean");
    public static readonly ResourceKey<Biome> OLD_GROWTH_BIRCH_FOREST = Create("old_growth_birch_forest");
    public static readonly ResourceKey<Biome> OLD_GROWTH_PINE_TAIGA = Create("old_growth_pine_taiga");
    public static readonly ResourceKey<Biome> OLD_GROWTH_SPRUCE_TAIGA = Create("old_growth_spruce_taiga");
    public static readonly ResourceKey<Biome> PALE_GARDEN = Create("pale_garden");
    public static readonly ResourceKey<Biome> PLAINS = Create("plains");
    public static readonly ResourceKey<Biome> RIVER = Create("river");
    public static readonly ResourceKey<Biome> SAVANNA = Create("savanna");
    public static readonly ResourceKey<Biome> SAVANNA_PLATEAU = Create("savanna_plateau");
    public static readonly ResourceKey<Biome> SMALL_END_ISLANDS = Create("small_end_islands");
    public static readonly ResourceKey<Biome> SNOWY_BEACH = Create("snowy_beach");
    public static readonly ResourceKey<Biome> SNOWY_PLAINS = Create("snowy_plains");
    public static readonly ResourceKey<Biome> SNOWY_SLOPES = Create("snowy_slopes");
    public static readonly ResourceKey<Biome> SNOWY_TAIGA = Create("snowy_taiga");
    public static readonly ResourceKey<Biome> SOUL_SAND_VALLEY = Create("soul_sand_valley");
    public static readonly ResourceKey<Biome> SPARSE_JUNGLE = Create("sparse_jungle");
    public static readonly ResourceKey<Biome> STONY_PEAKS = Create("stony_peaks");
    public static readonly ResourceKey<Biome> STONY_SHORE = Create("stony_shore");
    public static readonly ResourceKey<Biome> SULFUR_CAVES = Create("sulfur_caves");
    public static readonly ResourceKey<Biome> SUNFLOWER_PLAINS = Create("sunflower_plains");
    public static readonly ResourceKey<Biome> SWAMP = Create("swamp");
    public static readonly ResourceKey<Biome> TAIGA = Create("taiga");
    public static readonly ResourceKey<Biome> THE_END = Create("the_end");
    public static readonly ResourceKey<Biome> THE_VOID = Create("the_void");
    public static readonly ResourceKey<Biome> WARM_OCEAN = Create("warm_ocean");
    public static readonly ResourceKey<Biome> WARPED_FOREST = Create("warped_forest");
    public static readonly ResourceKey<Biome> WINDSWEPT_FOREST = Create("windswept_forest");
    public static readonly ResourceKey<Biome> WINDSWEPT_GRAVELLY_HILLS = Create("windswept_gravelly_hills");
    public static readonly ResourceKey<Biome> WINDSWEPT_HILLS = Create("windswept_hills");
    public static readonly ResourceKey<Biome> WINDSWEPT_SAVANNA = Create("windswept_savanna");
    public static readonly ResourceKey<Biome> WOODED_BADLANDS = Create("wooded_badlands");

    //Create 在 BIOME 注册表内建键
    private static ResourceKey<Biome> Create(string path)
        => ResourceKey<Biome>.Create(Registries.BIOME, Identifier.WithDefaultNamespace(path));
}
