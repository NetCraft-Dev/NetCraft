using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//DimensionTypes built-in dimension types, maps to vanilla net.minecraft.world.level.dimension.BuiltinDimensionTypes
//Data-driven loading from data/minecraft/dimension_type/*.json is preferred; this is the fallback when no data pack is present
//Values are copied from the three vanilla json files; verify against the jar before changing them
public static class DimensionTypes
{
    //Overworld has skylight, no ceiling, height -64..320, coordinate scale 1
    public static readonly DimensionType Overworld = Create(
        "overworld", hasFixedTime: false, hasSkyLight: true, hasCeiling: false, hasEnderDragonFight: false,
        coordinateScale: 1.0, minY: -64, height: 384, logicalHeight: 384,
        infiniburn: "infiniburn_overworld", ambientLight: 0f,
        spawnLightMin: 0, spawnLightMax: 7, blockLightLimit: 0, skybox: null, cardinalLight: null);

    //Nether has a ceiling, no skylight, height 0..256, coordinates scaled 1:8
    public static readonly DimensionType Nether = Create(
        "the_nether", hasFixedTime: true, hasSkyLight: false, hasCeiling: true, hasEnderDragonFight: false,
        coordinateScale: 8.0, minY: 0, height: 256, logicalHeight: 128,
        infiniburn: "infiniburn_nether", ambientLight: 0.1f,
        spawnLightMin: 7, spawnLightMax: 7, blockLightLimit: 15, skybox: "none", cardinalLight: "nether");

    //End has skylight, no ceiling, height 0..256 and logical height equal to the total height
    public static readonly DimensionType End = Create(
        "the_end", hasFixedTime: true, hasSkyLight: true, hasCeiling: false, hasEnderDragonFight: true,
        coordinateScale: 1.0, minY: 0, height: 256, logicalHeight: 256,
        infiniburn: "infiniburn_end", ambientLight: 0.25f,
        spawnLightMin: 15, spawnLightMax: 15, blockLightLimit: 0, skybox: "end", cardinalLight: null);

    //All the three built-in dimension types
    public static readonly DimensionType[] All = { Overworld, Nether, End };

    //RegisterBuiltin register the built-in dimension types; skip existing keys so the data-driven values win
    public static void RegisterBuiltin()
    {
        var registry = (WritableRegistry<NetCraft.Registry.DimensionType>)BuiltInRegistries.DIMENSION_TYPE;
        foreach (var type in All)
        {
            if (registry.ContainsKey(type.Id)) continue;
            registry.Register(ResourceKey<NetCraft.Registry.DimensionType>.Create(registry.Key, type.Id), type,
                RegistrationInfo.BuiltIn);
        }
    }

    private static DimensionType Create(string path, bool hasFixedTime, bool hasSkyLight, bool hasCeiling,
        bool hasEnderDragonFight, double coordinateScale, int minY, int height, int logicalHeight,
        string infiniburn, float ambientLight, int spawnLightMin, int spawnLightMax, int blockLightLimit,
        string? skybox, string? cardinalLight)
    {
        var type = new DimensionType(
            hasFixedTime,
            hasSkyLight,
            hasCeiling,
            hasEnderDragonFight,
            coordinateScale,
            minY,
            height,
            logicalHeight,
            Identifier.WithDefaultNamespace(infiniburn),
            ambientLight,
            new MonsterSettings(
                spawnLightMin == spawnLightMax
                    ? ConstantInt.Of(spawnLightMin)
                    : UniformInt.Of(spawnLightMin, spawnLightMax),
                blockLightLimit),
            skybox is null ? null : Identifier.WithDefaultNamespace(skybox),
            cardinalLight is null ? null : Identifier.WithDefaultNamespace(cardinalLight));
        type.SetRegistryId(Identifier.WithDefaultNamespace(path));
        return type;
    }
}
